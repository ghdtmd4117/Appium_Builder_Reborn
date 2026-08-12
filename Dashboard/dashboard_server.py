#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import mimetypes
import csv
import io
from xml.sax.saxutils import escape as xml_escape, quoteattr as xml_quoteattr
import os
import threading
import webbrowser
from collections import Counter, defaultdict
from datetime import datetime, timedelta
from functools import partial
from http import HTTPStatus
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, urlparse
from typing import Dict, List, Optional

ROOT = Path(__file__).resolve().parent
WEB_ROOT = ROOT / "web"


def parse_datetime(value: object) -> Optional[datetime]:
    if not isinstance(value, str) or not value.strip():
        return None
    raw = value.strip().replace("Z", "+00:00")
    try:
        parsed = datetime.fromisoformat(raw)
        if parsed.tzinfo is not None:
            return parsed.astimezone().replace(tzinfo=None)
        return parsed
    except ValueError:
        return None



def csv_safe(value: object) -> str:
    text = str(value or "")
    if text.lstrip().startswith(("=", "+", "-", "@")):
        return "'" + text
    return text

def record_status(record: Dict) -> str:
    status = str(record.get("status") or "").strip().upper()
    if status in {"PASS", "FAIL", "STOPPED", "SKIPPED"}:
        return status
    if bool(record.get("pass")):
        return "PASS"
    if str(record.get("failMessage") or "").strip() == "사용자 중지":
        return "STOPPED"
    return "FAIL"


def normalize_record(record: Dict) -> Dict:
    completed = parse_datetime(record.get("timestamp"))
    started = parse_datetime(record.get("startedAt"))
    duration_ms = int(record.get("durationMs") or 0)
    if duration_ms <= 0 and completed and started:
        duration_ms = max(0, int((completed - started).total_seconds() * 1000))

    normalized = {
        "runId": str(record.get("runId") or ""),
        "scenario": str(record.get("scenario") or "이름 없는 시나리오"),
        "startedAt": started.isoformat() if started else "",
        "timestamp": completed.isoformat() if completed else "",
        "totalSteps": int(record.get("totalSteps") or 0),
        "pass": bool(record.get("pass")),
        "status": record_status(record),
        "durationMs": duration_ms,
        "batchId": str(record.get("batchId") or ""),
        "deviceSerial": str(record.get("deviceSerial") or ""),
        "deviceModel": str(record.get("deviceModel") or ""),
        "osVersion": str(record.get("osVersion") or ""),
        "failMessage": str(record.get("failMessage") or ""),
        "steps": [item for item in (record.get("steps") or []) if isinstance(item, dict)],
    }
    return normalized


class HistoryStore:
    def __init__(self, log_folder: Path) -> None:
        self.log_folder = log_folder
        self.history_file = log_folder / "test_history.json"
        self._lock = threading.Lock()

    def read(self) -> List[Dict]:
        with self._lock:
            try:
                candidates = [self.history_file, Path(str(self.history_file) + ".bak")]
                data = None
                for candidate in candidates:
                    try:
                        if candidate.exists():
                            candidate_data = json.loads(candidate.read_text(encoding="utf-8-sig"))
                            if isinstance(candidate_data, list):
                                data = candidate_data
                                break
                    except (OSError, json.JSONDecodeError, ValueError, TypeError):
                        continue
                if data is None:
                    return []
                records = [normalize_record(item) for item in data if isinstance(item, dict)]
                records.sort(key=lambda item: item.get("timestamp") or item.get("startedAt") or "", reverse=True)
                return records
            except (OSError, json.JSONDecodeError, ValueError, TypeError):
                return []

    def summary(self, days: int) -> Dict:
        records = self.read()
        now = datetime.now()
        start = now - timedelta(days=max(1, days) - 1)
        start = start.replace(hour=0, minute=0, second=0, microsecond=0)
        filtered = [item for item in records if (parse_datetime(item.get("timestamp")) or datetime.min) >= start]

        executed = [item for item in filtered if item["status"] != "SKIPPED"]
        total = len(executed)
        pass_count = sum(1 for item in executed if item["status"] == "PASS")
        fail_count = sum(1 for item in executed if item["status"] == "FAIL")
        stopped_count = sum(1 for item in executed if item["status"] == "STOPPED")
        skipped_count = sum(1 for item in filtered if item["status"] == "SKIPPED")
        durations = [item["durationMs"] for item in executed if item["durationMs"] > 0]
        average_duration = int(sum(durations) / len(durations)) if durations else 0
        pass_rate = round(pass_count * 100 / total, 1) if total else 0

        daily: List[Dict] = []
        for offset in range(max(1, days)):
            day = start + timedelta(days=offset)
            day_records = [
                item for item in filtered
                if (parse_datetime(item.get("timestamp")) or datetime.min).date() == day.date()
            ]
            day_pass = sum(1 for item in day_records if item["status"] == "PASS")
            day_fail = sum(1 for item in day_records if item["status"] == "FAIL")
            day_stopped = sum(1 for item in day_records if item["status"] == "STOPPED")
            day_skipped = sum(1 for item in day_records if item["status"] == "SKIPPED")
            day_executed = [item for item in day_records if item["status"] != "SKIPPED"]
            day_total = len(day_executed)
            daily.append({
                "date": day.strftime("%Y-%m-%d"),
                "label": day.strftime("%m/%d"),
                "total": day_total,
                "pass": day_pass,
                "fail": day_fail,
                "stopped": day_stopped,
                "skipped": day_skipped,
                "passRate": round(day_pass * 100 / day_total, 1) if day_total else 0,
            })

        failures = [item for item in filtered if item["status"] in {"FAIL", "STOPPED"}]
        scenario_failures = Counter(item["scenario"] for item in failures)
        top_failures = [
            {"scenario": scenario, "count": count}
            for scenario, count in scenario_failures.most_common(5)
        ]

        scenario_stats: Dict[str, Dict] = defaultdict(lambda: {"total": 0, "pass": 0, "fail": 0, "stopped": 0, "skipped": 0, "duration": []})
        for item in filtered:
            bucket = scenario_stats[item["scenario"]]
            if item["status"] != "SKIPPED":
                bucket["total"] += 1
            bucket[item["status"].lower()] += 1
            if item["durationMs"] > 0:
                bucket["duration"].append(item["durationMs"])

        scenario_quality = []
        for scenario, bucket in scenario_stats.items():
            total_runs = bucket["total"]
            avg_duration = int(sum(bucket["duration"]) / len(bucket["duration"])) if bucket["duration"] else 0
            scenario_quality.append({
                "scenario": scenario,
                "total": total_runs,
                "pass": bucket["pass"],
                "fail": bucket["fail"],
                "stopped": bucket["stopped"],
                "skipped": bucket["skipped"],
                "passRate": round(bucket["pass"] * 100 / total_runs, 1) if total_runs else 0,
                "averageDurationMs": avg_duration,
            })
        scenario_quality.sort(key=lambda item: (item["passRate"], -item["total"], item["scenario"]))

        return {
            "periodDays": days,
            "generatedAt": now.isoformat(),
            "logFolder": str(self.log_folder),
            "total": total,
            "pass": pass_count,
            "fail": fail_count,
            "stopped": stopped_count,
            "skipped": skipped_count,
            "passRate": pass_rate,
            "averageDurationMs": average_duration,
            "daily": daily,
            "topFailures": top_failures,
            "scenarioQuality": scenario_quality,
        }


class DashboardHandler(SimpleHTTPRequestHandler):
    server_version = "AppiumBuilderDashboard/1.0"

    def __init__(self, *args, store: HistoryStore, **kwargs) -> None:
        self.store = store
        super().__init__(*args, **kwargs)

    def log_message(self, format: str, *args: object) -> None:
        return

    def _json(self, payload: object, status: int = HTTPStatus.OK) -> None:
        body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(body)

    def _download(self, body: bytes, content_type: str, filename: str) -> None:
        self.send_response(HTTPStatus.OK)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Disposition", f'attachment; filename="{filename}"')
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(body)

    def _export(self, format_name: str, days: int) -> None:
        records = self.store.read()
        cutoff = datetime.now() - timedelta(days=max(1, days) - 1)
        cutoff = cutoff.replace(hour=0, minute=0, second=0, microsecond=0)
        records = [r for r in records if (parse_datetime(r.get("timestamp")) or datetime.min) >= cutoff]
        stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        if format_name == "csv":
            buf = io.StringIO()
            writer = csv.writer(buf)
            writer.writerow(["status", "scenario", "deviceSerial", "deviceModel", "osVersion", "totalSteps", "durationMs", "timestamp", "failMessage"])
            for r in records:
                writer.writerow([csv_safe(r["status"]), csv_safe(r["scenario"]), csv_safe(r.get("deviceSerial", "")), csv_safe(r["deviceModel"]), csv_safe(r["osVersion"]), r["totalSteps"], r["durationMs"], csv_safe(r["timestamp"]), csv_safe(r["failMessage"])])
            self._download(buf.getvalue().encode("utf-8-sig"), "text/csv; charset=utf-8", f"appium_report_{stamp}.csv")
            return
        if format_name == "junit":
            failures = sum(1 for r in records if r["status"] in {"FAIL", "STOPPED"})
            skipped = sum(1 for r in records if r["status"] == "SKIPPED")
            total_seconds = sum(max(0, r["durationMs"]) for r in records) / 1000.0
            cases = []
            for r in records:
                name_attr = xml_quoteattr(r["scenario"])
                time_sec = max(0, r["durationMs"]) / 1000.0
                inner = ""
                if r["status"] in {"FAIL", "STOPPED"}:
                    inner = f'<failure message={xml_quoteattr(r["status"])}>{xml_escape(r["failMessage"] or "")}</failure>'
                elif r["status"] == "SKIPPED":
                    inner = f'<skipped message={xml_quoteattr(r["failMessage"] or "미실행")} />'
                cases.append(f'<testcase classname="AppiumBuilder" name={name_attr} time="{time_sec:.3f}">{inner}</testcase>')
            xml = f'<?xml version="1.0" encoding="UTF-8"?><testsuite name="AppiumBuilder" tests="{len(records)}" failures="{failures}" skipped="{skipped}" time="{total_seconds:.3f}">' + "".join(cases) + "</testsuite>"
            self._download(xml.encode("utf-8"), "application/xml; charset=utf-8", f"appium_junit_{stamp}.xml")
            return
        if format_name == "html":
            rows = ""
            for r in records:
                rows += "<tr><td>{}</td><td>{}</td><td>{}</td><td>{}</td><td>{:.1f}s</td><td>{}</td><td>{}</td></tr>".format(xml_escape(r["status"]), xml_escape(r["scenario"]), xml_escape(r["deviceModel"]), r["totalSteps"], r["durationMs"] / 1000.0, xml_escape(r["timestamp"]), xml_escape(r["failMessage"]))
            html = ("<!doctype html><html lang='ko'><meta charset='utf-8'><title>Appium Builder Report</title>"
                    "<style>body{font-family:Segoe UI,Malgun Gothic,sans-serif;background:#f5f7fc;color:#0f172a;padding:24px}table{width:100%;border-collapse:collapse}th,td{border-bottom:1px solid #e2e8f0;padding:9px;text-align:left;font-size:12px}th{color:#64748b}</style>"
                    f"<h1>Appium Builder QA Report</h1><p>최근 {days}일 · {len(records)} runs</p><table><thead><tr><th>Status</th><th>Scenario</th><th>Device</th><th>Steps</th><th>Duration</th><th>Time</th><th>Error</th></tr></thead><tbody>{rows}</tbody></table></html>")
            self._download(html.encode("utf-8"), "text/html; charset=utf-8", f"appium_report_{stamp}.html")
            return
        self.send_error(HTTPStatus.BAD_REQUEST)

    def _artifact(self, requested: str) -> None:
        try:
            target = Path(requested).expanduser().resolve()
            root = self.store.log_folder.resolve()
            if os.path.commonpath([str(target), str(root)]) != str(root) or not target.is_file():
                self.send_error(HTTPStatus.NOT_FOUND)
                return
            body = target.read_bytes()
            content_type = mimetypes.guess_type(str(target))[0] or "application/octet-stream"
            self.send_response(HTTPStatus.OK)
            self.send_header("Content-Type", content_type)
            self.send_header("Content-Length", str(len(body)))
            self.send_header("Cache-Control", "no-store")
            self.end_headers()
            self.wfile.write(body)
        except (OSError, ValueError):
            self.send_error(HTTPStatus.NOT_FOUND)

    def do_GET(self) -> None:
        parsed = urlparse(self.path)
        query = parse_qs(parsed.query)

        if parsed.path == "/api/health":
            self._json({"ok": True, "historyFile": str(self.store.history_file)})
            return

        if parsed.path == "/api/export":
            fmt = query.get("format", ["csv"])[0].lower()
            try:
                days = max(1, min(365, int(query.get("days", ["30"])[0])))
            except ValueError:
                days = 30
            self._export(fmt, days)
            return

        if parsed.path == "/api/artifact":
            requested = query.get("path", [""])[0]
            self._artifact(requested)
            return

        if parsed.path == "/api/history":
            records = self.store.read()
            try:
                limit = max(1, min(500, int(query.get("limit", ["200"])[0])))
            except ValueError:
                limit = 200
            self._json(records[:limit])
            return

        if parsed.path == "/api/summary":
            try:
                days = max(1, min(90, int(query.get("days", ["7"])[0])))
            except ValueError:
                days = 7
            self._json(self.store.summary(days))
            return

        if parsed.path == "/":
            self.path = "/index.html"
        else:
            self.path = parsed.path
        super().do_GET()


def default_log_folder() -> Path:
    configured = os.environ.get("ADB_LOG_FOLDER", "").strip()
    if configured:
        return Path(configured).expanduser()
    return Path.home() / "Desktop" / "ADB_Logs"


def main() -> None:
    parser = argparse.ArgumentParser(description="Appium Builder Reborn QA dashboard")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8765)
    parser.add_argument("--log-folder", type=Path, default=default_log_folder())
    parser.add_argument("--open", action="store_true")
    args = parser.parse_args()

    args.log_folder.mkdir(parents=True, exist_ok=True)
    store = HistoryStore(args.log_folder.resolve())
    handler = partial(DashboardHandler, directory=str(WEB_ROOT), store=store)
    server = ThreadingHTTPServer((args.host, args.port), handler)
    url = f"http://{args.host}:{args.port}"
    print(f"Appium Builder QA dashboard: {url}")
    print(f"History: {store.history_file}")
    if args.open:
        threading.Timer(0.7, lambda: webbrowser.open(url)).start()
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
