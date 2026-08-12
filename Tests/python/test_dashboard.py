import importlib.util
import json
from datetime import datetime
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parents[2]
MODULE_PATH = PROJECT_ROOT / "Dashboard" / "dashboard_server.py"
spec = importlib.util.spec_from_file_location("dashboard_server", MODULE_PATH)
dashboard = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(dashboard)


def test_summary_excludes_skipped_from_pass_rate(tmp_path: Path):
    now = datetime.now().isoformat()
    history = [
        {"runId": "1", "scenario": "Login", "timestamp": now, "status": "PASS", "pass": True, "durationMs": 1000},
        {"runId": "2", "scenario": "Pay", "timestamp": now, "status": "FAIL", "pass": False, "durationMs": 2000},
        {"runId": "3", "scenario": "Logout", "timestamp": now, "status": "SKIPPED", "pass": False, "durationMs": 0},
    ]
    (tmp_path / "test_history.json").write_text(json.dumps(history), encoding="utf-8")
    summary = dashboard.HistoryStore(tmp_path).summary(7)
    assert summary["total"] == 2
    assert summary["pass"] == 1
    assert summary["fail"] == 1
    assert summary["skipped"] == 1
    assert summary["passRate"] == 50.0


def test_history_falls_back_to_backup(tmp_path: Path):
    (tmp_path / "test_history.json").write_text("{broken", encoding="utf-8")
    backup = [{"runId": "old", "scenario": "Recovered", "status": "PASS", "pass": True}]
    (tmp_path / "test_history.json.bak").write_text(json.dumps(backup), encoding="utf-8")
    records = dashboard.HistoryStore(tmp_path).read()
    assert len(records) == 1
    assert records[0]["scenario"] == "Recovered"


def test_step_records_are_preserved(tmp_path: Path):
    now = datetime.now().isoformat()
    item = {
        "runId": "1", "scenario": "Visual", "timestamp": now, "status": "FAIL", "pass": False,
        "steps": [{"index": 4, "status": "FAIL", "artifactFolder": "C:/logs/run", "matchRate": 87.25}],
    }
    (tmp_path / "test_history.json").write_text(json.dumps([item]), encoding="utf-8")
    record = dashboard.HistoryStore(tmp_path).read()[0]
    assert record["steps"][0]["index"] == 4
    assert record["steps"][0]["matchRate"] == 87.25
