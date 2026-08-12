using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AppiumBuilder.Core
{
    public sealed class BotRunOptions
    {
        public int InterStepDelayMs { get; set; } = 500;
        public bool CaptureStepScreenshots { get; set; } = true;
    }

    public static class BotEngine
    {
        private static readonly object ProcessGate = new();
        private static readonly object CrashLogGate = new();
        private static Process? currentProcess;
        private static int userStoppedProcessId;

        public static bool IsRunning
        {
            get
            {
                lock (ProcessGate)
                {
                    if (currentProcess == null) return false;
                    try { return !currentProcess.HasExited; }
                    catch { return false; }
                }
            }
        }

        private static (string loc, string tgt, string val) ParseStep(string row)
        {
            int locOpen = row.IndexOf('[', 1);
            if (locOpen < 0) return ("", row, "");
            int locClose = row.IndexOf(']', locOpen);
            if (locClose < 0) return ("", row, "");

            string loc = row.Substring(locOpen + 1, locClose - locOpen - 1).Trim();
            string rest = row.Substring(locClose + 1).Trim();
            string tgt = rest;
            string val = "";

            int barIdx = rest.IndexOf('|');
            if (barIdx >= 0)
            {
                tgt = rest.Substring(0, barIdx).Trim();
                string afterBar = rest.Substring(barIdx + 1).Trim();
                int colonIdx = afterBar.IndexOf(':');
                val = colonIdx >= 0 ? afterBar.Substring(colonIdx + 1).Trim() : afterBar;
            }
            return (loc, tgt, val);
        }

        private static string GetAfterColon(string row)
        {
            int idx = row.IndexOf(':');
            return idx >= 0 ? row.Substring(idx + 1).Trim() : "";
        }

        private static string ByType(string loc) => loc switch
        {
            "ID" => "ID",
            "XPath" => "XPATH",
            "Accessibility ID" => "ACCESSIBILITY_ID",
            _ => throw new InvalidOperationException($"지원하지 않는 locator 형식입니다: {loc}")
        };

        private static string PyRepr(string value)
        {
            StringBuilder sb = new StringBuilder(value.Length + 2);
            sb.Append('\'');
            foreach (char ch in value)
            {
                sb.Append(ch switch
                {
                    '\\' => "\\\\",
                    '\'' => "\\'",
                    '\r' => "\\r",
                    '\n' => "\\n",
                    '\t' => "\\t",
                    _ => ch.ToString()
                });
            }
            sb.Append('\'');
            return sb.ToString();
        }

        private static string XPathLiteral(string value)
        {
            if (!value.Contains('\'')) return $"'{value}'";
            if (!value.Contains('"')) return $"\"{value}\"";

            string[] parts = value.Split('\'');
            return "concat(" + string.Join(", \"'\", ", parts.Select(p => $"'{p}'")) + ")";
        }

        private static string SanitizeScenarioName(string scenarioName)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            string safe = new string(scenarioName
                .Select(ch => invalid.Contains(ch) || ch == Path.DirectorySeparatorChar || ch == Path.AltDirectorySeparatorChar ? '_' : ch)
                .ToArray())
                .Trim();
            while (safe.Contains("..", StringComparison.Ordinal)) safe = safe.Replace("..", "_", StringComparison.Ordinal);
            return string.IsNullOrWhiteSpace(safe) ? "UnnamedScenario" : safe;
        }

        private static List<string> SnapshotRows(ListBox listBox)
        {
            return Enumerable.Range(0, listBox.Items.Count)
                .Select(i => listBox.Items[i]?.ToString()?.Trim() ?? "")
                .Where(row => row.Length > 0)
                .ToList();
        }

        public static bool ValidateScenario(ListBox listBox, string loopText, out int loopCount, out string validationMessage)
        {
            var errors = new List<string>();
            List<string> rows = SnapshotRows(listBox);

            if (!int.TryParse(loopText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out loopCount) || loopCount < 1 || loopCount > 1000)
            {
                errors.Add("반복 횟수는 1~1000 사이의 정수여야 합니다.");
            }

            if (rows.Count == 0)
            {
                errors.Add("실행할 유효 스텝이 없습니다.");
            }

            bool otpAvailable = false;
            for (int i = 0; i < rows.Count; i++)
            {
                string row = rows[i];
                string? error = ValidateStep(row);
                if (error != null) errors.Add($"{i + 1}번 스텝: {error}\n  {row}");

                if (row.StartsWith("[Input]", StringComparison.Ordinal))
                {
                    var (_, _, value) = ParseStep(row);
                    if (value == "{OTP}" && !otpAvailable)
                        errors.Add($"{i + 1}번 스텝: OTP 추출 전에 {{OTP}} 입력을 사용할 수 없습니다.\n  {row}");
                }
                if (row.StartsWith("[OTP]", StringComparison.Ordinal)) otpAvailable = true;
            }

            validationMessage = string.Join("\n\n", errors);
            return errors.Count == 0;
        }

        private static string? ValidateStep(string row)
        {
            if (row.Equals("Step", StringComparison.OrdinalIgnoreCase))
                return "CSV 헤더가 스텝으로 들어왔습니다.";

            Match match;
            if (row.StartsWith("[Sleep]", StringComparison.Ordinal))
            {
                match = Regex.Match(row, @"^\[Sleep\]\s+([0-9]+(?:\.[0-9]+)?)\s*(?:초)?$");
                if (!match.Success || !double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds))
                    return "대기 형식은 [Sleep] 1 초 형태여야 합니다.";
                if (seconds < 0 || seconds > 3600) return "대기 시간은 0~3600초 범위여야 합니다.";
                return null;
            }

            if (row.StartsWith("[Tap]", StringComparison.Ordinal))
            {
                match = Regex.Match(row, @"^\[Tap\]\s*(-?\d+)\s*,\s*(-?\d+)\s*$");
                return match.Success ? null : "좌표 클릭 형식은 [Tap] X, Y 형태여야 합니다.";
            }

            if (row.StartsWith("[Swipe]", StringComparison.Ordinal))
            {
                match = Regex.Match(row, @"^\[Swipe\]\s*시작:\s*(-?\d+)\s*,\s*(-?\d+)\s*->\s*도착:\s*(-?\d+)\s*,\s*(-?\d+)\s*$");
                return match.Success ? null : "스크롤 형식은 [Swipe] 시작:X,Y -> 도착:X,Y 형태여야 합니다.";
            }

            if (row.StartsWith("[Key]", StringComparison.Ordinal))
            {
                match = Regex.Match(row, @"^\[Key\]\s*코드:\s*(\d+)\s*$");
                return match.Success ? null : "기기 키 형식은 [Key] 코드: 숫자 형태여야 합니다.";
            }

            if (row.StartsWith("[Click]", StringComparison.Ordinal) ||
                row.StartsWith("[Input]", StringComparison.Ordinal) ||
                row.StartsWith("[OTP]", StringComparison.Ordinal) ||
                row.StartsWith("[Assert]", StringComparison.Ordinal))
            {
                var (loc, target, value) = ParseStep(row);
                if (loc != "ID" && loc != "XPath" && loc != "Accessibility ID") return "Locator는 ID, XPath, Accessibility ID 중 하나여야 합니다.";
                if (string.IsNullOrWhiteSpace(target)) return "대상 locator가 비어 있습니다.";
                if (row.StartsWith("[Assert]", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(value)) return "요소 검증의 기대값이 비어 있습니다.";
                return null;
            }

            if (row.StartsWith("[SecurePad]", StringComparison.Ordinal))
                return string.IsNullOrWhiteSpace(GetAfterColon(row)) ? "보안키패드 입력값이 비어 있습니다." : null;

            if (row.StartsWith("[PhysicalKey]", StringComparison.Ordinal))
            {
                string value = GetAfterColon(row);
                return value.Length > 0 && value.All(char.IsDigit) ? null : "물리키패드 값은 숫자만 입력해야 합니다.";
            }

            if (row.StartsWith("[Notification]", StringComparison.Ordinal))
                return string.IsNullOrWhiteSpace(GetAfterColon(row)) ? "알림창에서 누를 문구가 비어 있습니다." : null;

            if (row.StartsWith("[ScreenAssert]", StringComparison.Ordinal)) return null;

            if (row.StartsWith("[RunPython]", StringComparison.Ordinal))
            {
                string path = row.Substring("[RunPython]".Length).Trim();
                if (string.IsNullOrWhiteSpace(path)) return "Python 파일 경로가 비어 있습니다.";
                if (!File.Exists(path)) return "Python 파일을 찾을 수 없습니다.";
                if (!path.EndsWith(".py", StringComparison.OrdinalIgnoreCase)) return "실행 파일은 .py 확장자여야 합니다.";
                return null;
            }

            return "지원하지 않는 스텝 형식입니다.";
        }

        public static bool StopCurrentRun(string sysPath, out string message)
        {
            Process? process;
            lock (ProcessGate) process = currentProcess;

            if (process == null)
            {
                message = "현재 실행 중인 봇이 없습니다.";
                return false;
            }

            try
            {
                if (!process.HasExited)
                {
                    Interlocked.Exchange(ref userStoppedProcessId, process.Id);
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }

                Directory.CreateDirectory(sysPath);
                File.AppendAllText(Path.Combine(sysPath, "bot_status.txt"), "[ System ] 사용자에 의해 실행이 중지되었습니다.\n", new UTF8Encoding(false));
                message = "실행 중인 Python/Appium 테스트 프로세스를 종료했습니다.";
                return true;
            }
            catch (Exception ex)
            {
                message = "프로세스 종료 실패: " + ex.Message;
                return false;
            }
            finally
            {
                lock (ProcessGate)
                {
                    if (ReferenceEquals(currentProcess, process)) currentProcess = null;
                }
            }
        }

        public static void GenerateAndRun(ListBox listBox, string loopText, Label? statusLabel, string scenarioName, string sysPath, string testSetPath, BotRunOptions? runOptions = null)
        {
            runOptions ??= new BotRunOptions();
            runOptions.InterStepDelayMs = Math.Clamp(runOptions.InterStepDelayMs, 0, 60000);
            if (!ValidateScenario(listBox, loopText, out int loopCount, out string validationMessage))
                throw new InvalidOperationException("시나리오 검증에 실패했습니다.\n\n" + validationMessage);

            lock (ProcessGate)
            {
                if (currentProcess != null)
                {
                    bool existingRunIsActive;
                    try { existingRunIsActive = !currentProcess.HasExited; }
                    catch { existingRunIsActive = false; }

                    if (existingRunIsActive)
                        throw new InvalidOperationException("이미 봇이 실행 중입니다. 먼저 정지한 뒤 다시 실행해주세요.");

                    try { currentProcess.Dispose(); } catch { }
                    currentProcess = null;
                }
            }

            Directory.CreateDirectory(sysPath);
            Directory.CreateDirectory(testSetPath);

            string statusFile = Path.Combine(sysPath, "bot_status.txt");
            string errorFile = Path.Combine(sysPath, "bot_error.log");
            string crashLog = Path.Combine(sysPath, "python_crash.log");
            string stepResultsFile = Path.Combine(sysPath, "step_results.jsonl");
            string scriptPath = Path.Combine(sysPath, "appium_macro.py");
            string selectedSerial = AdbEngine.SelectedSerial ?? string.Empty;

            File.WriteAllText(statusFile, "[ System ] 봇 부팅 중...\n", new UTF8Encoding(false));
            if (File.Exists(errorFile)) File.Delete(errorFile);
            if (File.Exists(crashLog)) File.Delete(crashLog);
            if (File.Exists(stepResultsFile)) File.Delete(stepResultsFile);

            List<string> rows = SnapshotRows(listBox);
            string safeScenarioName = SanitizeScenarioName(scenarioName);
            string interStepDelaySeconds = (runOptions.InterStepDelayMs / 1000D).ToString(CultureInfo.InvariantCulture);
            string captureScreenshots = runOptions.CaptureStepScreenshots ? "True" : "False";
            bool usesOtp = rows.Any(row => row.StartsWith("[OTP]", StringComparison.Ordinal));
            var lines = new List<string>
            {
                "import os, sys, time, traceback, urllib.request, re, json, shutil, subprocess",
                "from datetime import datetime",
                $"STATUS_FILE = {PyRepr(statusFile)}",
                $"ERROR_FILE = {PyRepr(errorFile)}",
                $"STEP_RESULTS_FILE = {PyRepr(stepResultsFile)}",
                $"TEST_SET_PATH = {PyRepr(testSetPath)}",
                $"INTER_STEP_DELAY = {interStepDelaySeconds}",
                $"CAPTURE_STEP_SCREENSHOTS = {captureScreenshots}",
                "CURRENT_STEP = None",
                "CURRENT_STEP_ARTIFACT = None",
                "CURRENT_STEP_MATCH_RATE = None",
                "",
                "def set_status(msg):",
                "    try:",
                "        with open(STATUS_FILE, 'a', encoding='utf-8') as f:",
                "            f.write(str(msg) + '\\n')",
                "            f.flush()",
                "    except Exception:",
                "        pass",
                "",
                "def write_error(msg):",
                "    try:",
                "        with open(ERROR_FILE, 'w', encoding='utf-8-sig') as f:",
                "            f.write(str(msg))",
                "    except Exception:",
                "        pass",
                "",
                "def start_step(step_idx, loop_idx, raw):",
                "    global CURRENT_STEP, CURRENT_STEP_ARTIFACT, CURRENT_STEP_MATCH_RATE",
                "    CURRENT_STEP_ARTIFACT = None",
                "    CURRENT_STEP_MATCH_RATE = None",
                "    CURRENT_STEP = {'index': step_idx, 'loop': loop_idx, 'raw': raw, 'startedAt': datetime.now().isoformat(), 'startedMonotonic': time.monotonic()}",
                "",
                "def set_step_visual(verdict):",
                "    global CURRENT_STEP_ARTIFACT, CURRENT_STEP_MATCH_RATE",
                "    if verdict:",
                "        CURRENT_STEP_ARTIFACT = verdict.get('run_dir')",
                "        CURRENT_STEP_MATCH_RATE = verdict.get('match_rate')",
                "",
                "def finish_step(status, message=''):",
                "    global CURRENT_STEP, CURRENT_STEP_ARTIFACT, CURRENT_STEP_MATCH_RATE",
                "    if not CURRENT_STEP:",
                "        return",
                "    now = datetime.now()",
                "    record = {",
                "        'index': CURRENT_STEP['index'], 'loop': CURRENT_STEP['loop'], 'raw': CURRENT_STEP['raw'],",
                "        'status': status, 'startedAt': CURRENT_STEP['startedAt'], 'timestamp': now.isoformat(),",
                "        'durationMs': max(0, int((time.monotonic() - CURRENT_STEP['startedMonotonic']) * 1000)),",
                "        'message': str(message) if message else None, 'artifactFolder': CURRENT_STEP_ARTIFACT, 'matchRate': CURRENT_STEP_MATCH_RATE",
                "    }",
                "    try:",
                "        with open(STEP_RESULTS_FILE, 'a', encoding='utf-8') as f:",
                "            f.write(json.dumps(record, ensure_ascii=False) + '\\n')",
                "            f.flush()",
                "    except Exception:",
                "        pass",
                "    CURRENT_STEP = None",
                "    CURRENT_STEP_ARTIFACT = None",
                "    CURRENT_STEP_MATCH_RATE = None",
                "",
                "def capture_step_screenshot(driver, scenario_name, step_idx, loop_idx):",
                "    global CURRENT_STEP_ARTIFACT",
                "    try:",
                "        stamp = datetime.now().strftime('%Y%m%d_%H%M%S_%f')",
                "        run_dir = os.path.join(TEST_SET_PATH, scenario_name, 'runs', f'{stamp}_step_{step_idx:03d}_loop_{loop_idx:03d}')",
                "        os.makedirs(run_dir, exist_ok=True)",
                "        screen_path = os.path.join(run_dir, 'screen.png')",
                "        if driver and driver.get_screenshot_as_file(screen_path):",
                "            CURRENT_STEP_ARTIFACT = run_dir",
                "            try:",
                "                with open(os.path.join(run_dir, 'ui_tree.xml'), 'w', encoding='utf-8') as f:",
                "                    f.write(driver.page_source)",
                "            except Exception:",
                "                pass",
                "            return run_dir",
                "    except Exception:",
                "        pass",
                "    return None",
                "",
                "def load_visual_config(base_path, step_name):",
                "    config = {'threshold': 95.0, 'masks': []}",
                "    path = os.path.join(base_path, 'visual_assert.json')",
                "    try:",
                "        if os.path.exists(path):",
                "            with open(path, 'r', encoding='utf-8-sig') as f:",
                "                raw = json.load(f)",
                "            config['threshold'] = float(raw.get('defaultThreshold', 95.0))",
                "            step_cfg = (raw.get('steps') or {}).get(step_name.replace('step_', '').lstrip('0') or '0') or (raw.get('steps') or {}).get(step_name) or {}",
                "            if step_cfg.get('threshold') is not None:",
                "                config['threshold'] = float(step_cfg.get('threshold'))",
                "            config['masks'] = step_cfg.get('masks') or []",
                "    except Exception:",
                "        pass",
                "    config['threshold'] = max(0.0, min(100.0, config['threshold']))",
                "    return config",
                "",
                "def do_visual_assert(driver, scenario_name, step_idx):",
                "    try:",
                "        import cv2",
                "    except ImportError:",
                "        return {'status': 'ERROR', 'match_rate': 0.0, 'message': 'opencv-python 패키지가 설치되지 않았습니다.'}",
                "",
                "    base_path = os.path.join(TEST_SET_PATH, scenario_name)",
                "    base_dir = os.path.join(base_path, 'baseline')",
                "    step_name = f'step_{step_idx:03d}'",
                "    visual_config = load_visual_config(base_path, step_name)",
                "    threshold_required = visual_config['threshold']",
                "    run_name = datetime.now().strftime('%Y%m%d_%H%M%S_%f') + '_' + step_name",
                "    run_dir = os.path.join(base_path, 'runs', run_name)",
                "    os.makedirs(run_dir, exist_ok=True)",
                "    run_img = os.path.join(run_dir, 'screen.png')",
                "    if not driver.get_screenshot_as_file(run_img):",
                "        return {'status': 'ERROR', 'match_rate': 0.0, 'message': '현재 화면 저장에 실패했습니다.'}",
                "    page_source = driver.page_source",
                "    with open(os.path.join(run_dir, 'ui_tree.xml'), 'w', encoding='utf-8') as f:",
                "        f.write(page_source)",
                "    meta = {'step': step_idx, 'timestamp': datetime.now().isoformat(), 'threshold': threshold_required, 'masks': visual_config['masks']}",
                "    with open(os.path.join(run_dir, 'meta.json'), 'w', encoding='utf-8') as f:",
                "        json.dump(meta, f, ensure_ascii=False, indent=4)",
                "",
                "    os.makedirs(base_dir, exist_ok=True)",
                "    base_img = os.path.join(base_dir, step_name + '.png')",
                "    base_tree = os.path.join(base_dir, step_name + '.xml')",
                "    base_meta = os.path.join(base_dir, step_name + '.json')",
                "    if not os.path.exists(base_img):",
                "        shutil.copy(run_img, base_img)",
                "        with open(base_tree, 'w', encoding='utf-8') as f:",
                "            f.write(page_source)",
                "        with open(base_meta, 'w', encoding='utf-8') as f:",
                "            json.dump(meta, f, ensure_ascii=False, indent=4)",
                "        verdict = {'status': 'BASELINE_CREATED', 'match_rate': 0.0, 'message': f'{step_name} 기준 이미지를 생성했습니다. 다시 실행해 검증하세요.'}",
                "    else:",
                "        img_b = cv2.imread(base_img)",
                "        img_r = cv2.imread(run_img)",
                "        if img_b is None or img_r is None:",
                "            verdict = {'status': 'ERROR', 'match_rate': 0.0, 'message': '기준 또는 실행 이미지를 읽지 못했습니다.'}",
                "        else:",
                "            if img_b.shape != img_r.shape:",
                "                img_r = cv2.resize(img_r, (img_b.shape[1], img_b.shape[0]))",
                "            diff = cv2.absdiff(img_b, img_r)",
                "            h, w = diff.shape[:2]",
                "            for mask in visual_config['masks']:",
                "                try:",
                "                    mx = float(mask.get('x', 0)); my = float(mask.get('y', 0)); mw = float(mask.get('width', 0)); mh = float(mask.get('height', 0))",
                "                    if max(mx, my, mw, mh) <= 1.0:",
                "                        x1, y1, x2, y2 = int(mx*w), int(my*h), int((mx+mw)*w), int((my+mh)*h)",
                "                    else:",
                "                        x1, y1, x2, y2 = int(mx), int(my), int(mx+mw), int(my+mh)",
                "                    x1=max(0,min(w,x1)); x2=max(0,min(w,x2)); y1=max(0,min(h,y1)); y2=max(0,min(h,y2))",
                "                    if x2>x1 and y2>y1: diff[y1:y2, x1:x2] = 0",
                "                except Exception:",
                "                    pass",
                "            gray = cv2.cvtColor(diff, cv2.COLOR_BGR2GRAY)",
                "            _, thresh = cv2.threshold(gray, 30, 255, cv2.THRESH_BINARY)",
                "            nz = cv2.countNonZero(thresh)",
                "            tot = thresh.shape[0] * thresh.shape[1]",
                "            rate = ((tot - nz) / float(tot)) * 100.0",
                "            diff_img = img_r.copy()",
                "            contours, _ = cv2.findContours(thresh, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)",
                "            cv2.drawContours(diff_img, contours, -1, (0, 0, 255), 2)",
                "            cv2.imwrite(os.path.join(run_dir, 'diff.png'), diff_img)",
                "            verdict = {'status': 'PASS' if rate >= threshold_required else 'FAIL', 'match_rate': round(rate, 2), 'threshold': threshold_required}",
                "",
                "    verdict['run_dir'] = run_dir",
                "    verdict['threshold'] = verdict.get('threshold', threshold_required)",
                "    with open(os.path.join(run_dir, 'verdict.json'), 'w', encoding='utf-8') as f:",
                "        json.dump(verdict, f, ensure_ascii=False, indent=4)",
                "    return verdict",
                "",
                "from appium import webdriver",
                "from appium.options.common import AppiumOptions",
                "from appium.webdriver.common.appiumby import AppiumBy",
                "from selenium.webdriver.support.ui import WebDriverWait",
                "from selenium.webdriver.support import expected_conditions as EC",
                "",
                "caps = {'platformName': 'Android', 'appium:automationName': 'UiAutomator2', 'appium:newCommandTimeout': 3600, 'appium:noReset': True}",
                $"selected_udid = {PyRepr(selectedSerial)}",
                "if selected_udid: caps['appium:udid'] = selected_udid",
                "options = AppiumOptions()",
                "options.load_capabilities(caps)",
                "driver = None"
            };

            if (usesOtp) lines.Add("otp_value = ''");

            lines.AddRange(new[]
            {
                "try:",
                "    server_url = 'http://127.0.0.1:4723'",
                "    try:",
                "        urllib.request.urlopen('http://127.0.0.1:4723/status', timeout=2)",
                "    except Exception:",
                "        server_url = 'http://127.0.0.1:4723/wd/hub'",
                "    set_status('[ System ] 단말기 연결 중...')",
                "    driver = webdriver.Remote(server_url, options=options)",
                "    wait = WebDriverWait(driver, 10)",
                $"    scenario_name = {PyRepr(safeScenarioName)}",
                $"    for loop_index in range({loopCount}):",
                $"        if {loopCount} > 1:",
                "            set_status(f'[ Loop ] {loop_index + 1}회차 시작')"
            });

            int total = rows.Count;
            for (int index = 0; index < total; index++)
            {
                string row = rows[index];
                string prefix = $"[{index + 1}/{total}]";
                lines.Add($"        start_step({index + 1}, loop_index + 1, {PyRepr(row)})");

                if (row.StartsWith("[Sleep]", StringComparison.Ordinal))
                {
                    Match match = Regex.Match(row, @"^\[Sleep\]\s+([0-9]+(?:\.[0-9]+)?)");
                    double seconds = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                    string secondsLiteral = seconds.ToString(CultureInfo.InvariantCulture);
                    lines.Add($"        set_status('> {prefix} 대기 중... ({secondsLiteral}초)')");
                    lines.Add($"        time.sleep({secondsLiteral})");
                }
                else if (row.StartsWith("[Tap]", StringComparison.Ordinal))
                {
                    Match match = Regex.Match(row, @"^\[Tap\]\s*(-?\d+)\s*,\s*(-?\d+)");
                    int x = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                    int y = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                    lines.Add($"        set_status('> {prefix} 좌표 터치 중: X={x}, Y={y}')");
                    lines.Add($"        driver.execute_script('mobile: clickGesture', {{'x': {x}, 'y': {y}}})");
                }
                else if (row.StartsWith("[Click]", StringComparison.Ordinal))
                {
                    var (loc, target, _) = ParseStep(row);
                    string byType = ByType(loc);
                    lines.Add($"        set_status('> {prefix} 버튼 찾는 중: ' + {PyRepr(target)})");
                    lines.Add("        retry_count = 0");
                    lines.Add("        while retry_count < 10:");
                    lines.Add("            try:");
                    lines.Add($"                driver.find_element(AppiumBy.{byType}, {PyRepr(target)}).click()");
                    lines.Add("                break");
                    lines.Add("            except Exception:");
                    lines.Add("                retry_count += 1");
                    lines.Add("                time.sleep(1)");
                    lines.Add("        if retry_count >= 10:");
                    lines.Add($"            write_error('[클릭 실패] 요소 미노출: ' + {PyRepr(target)})");
                    lines.Add("            raise Exception('Element Not Found')");
                }
                else if (row.StartsWith("[Input]", StringComparison.Ordinal))
                {
                    var (loc, target, value) = ParseStep(row);
                    string byType = ByType(loc);
                    string sendExpression = usesOtp && value == "{OTP}" ? "otp_value" : PyRepr(value);
                    lines.Add($"        set_status('> {prefix} 텍스트 작성 중...')");
                    lines.Add("        try:");
                    lines.Add($"            el = WebDriverWait(driver, 15).until(EC.presence_of_element_located((AppiumBy.{byType}, {PyRepr(target)})))");
                    lines.Add("            try:");
                    lines.Add("                el.clear()");
                    lines.Add("            except Exception:");
                    lines.Add("                pass");
                    lines.Add($"            el.send_keys({sendExpression})");
                    lines.Add("        except Exception:");
                    lines.Add($"            write_error('[입력 실패] 요소 미노출 또는 입력 불가: ' + {PyRepr(target)})");
                    lines.Add("            raise Exception('Input Failed')");
                }
                else if (row.StartsWith("[Swipe]", StringComparison.Ordinal))
                {
                    Match match = Regex.Match(row, @"^\[Swipe\]\s*시작:\s*(-?\d+)\s*,\s*(-?\d+)\s*->\s*도착:\s*(-?\d+)\s*,\s*(-?\d+)");
                    int x1 = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                    int y1 = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                    int x2 = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
                    int y2 = int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture);
                    lines.Add($"        set_status('> {prefix} 화면 스크롤 중...')");
                    lines.Add($"        driver.swipe({x1}, {y1}, {x2}, {y2}, 800)");
                }
                else if (row.StartsWith("[Key]", StringComparison.Ordinal))
                {
                    int code = int.Parse(GetAfterColon(row), CultureInfo.InvariantCulture);
                    lines.Add($"        set_status('> {prefix} 기기 키 입력 중: {code}')");
                    lines.Add($"        driver.press_keycode({code})");
                }
                else if (row.StartsWith("[OTP]", StringComparison.Ordinal))
                {
                    var (loc, target, _) = ParseStep(row);
                    string byType = ByType(loc);
                    lines.Add($"        set_status('> {prefix} OTP 추출 중: ' + {PyRepr(target)})");
                    lines.Add("        try:");
                    lines.Add($"            el_otp = wait.until(EC.presence_of_element_located((AppiumBy.{byType}, {PyRepr(target)})))");
                    lines.Add("            raw_otp = el_otp.text or el_otp.get_attribute('content-desc') or ''");
                    lines.Add("            otp_value = ''.join(re.findall(r'\\d', raw_otp))");
                    lines.Add("            if not otp_value:");
                    lines.Add("                raise Exception('OTP Digits Not Found')");
                    lines.Add($"            set_status(f'> {prefix} OTP 추출됨: {{otp_value}}')");
                    lines.Add("        except Exception:");
                    lines.Add($"            write_error('[OTP 실패] 숫자를 추출하지 못했습니다: ' + {PyRepr(target)})");
                    lines.Add("            raise Exception('OTP Not Found')");
                }
                else if (row.StartsWith("[SecurePad]", StringComparison.Ordinal))
                {
                    string value = GetAfterColon(row);
                    lines.Add($"        set_status('> {prefix} 보안키패드 입력 중...')");
                    lines.Add("        try:");
                    lines.Add($"            driver.execute_script('mobile: type', {{'text': {PyRepr(value)}}})");
                    lines.Add("        except Exception:");
                    lines.Add("            write_error('[보안키패드 실패] 해당 기기/앱에서 접근성 입력이 차단되어 있을 수 있습니다.')");
                    lines.Add("            raise Exception('SecurePad Input Failed')");
                }
                else if (row.StartsWith("[PhysicalKey]", StringComparison.Ordinal))
                {
                    string value = GetAfterColon(row);
                    lines.Add($"        set_status('> {prefix} 물리 키패드 입력 중: {value}')");
                    lines.Add($"        for ch in {PyRepr(value)}:");
                    lines.Add("            driver.press_keycode(7 + int(ch))");
                    lines.Add("            time.sleep(0.2)");
                }
                else if (row.StartsWith("[Notification]", StringComparison.Ordinal))
                {
                    string action = GetAfterColon(row);
                    string xpath = $"//*[contains(@text, {XPathLiteral(action)}) or contains(@content-desc, {XPathLiteral(action)})]";
                    lines.Add($"        set_status('> {prefix} 알림창 처리 중: ' + {PyRepr(action)})");
                    lines.Add("        try:");
                    lines.Add($"            wait.until(EC.element_to_be_clickable((AppiumBy.XPATH, {PyRepr(xpath)}))).click()");
                    lines.Add("        except Exception:");
                    lines.Add($"            write_error('[알림창 실패] 버튼을 찾지 못했습니다: ' + {PyRepr(action)})");
                    lines.Add("            raise Exception('Notification Not Found')");
                }
                else if (row.StartsWith("[Assert]", StringComparison.Ordinal))
                {
                    var (loc, target, value) = ParseStep(row);
                    string byType = ByType(loc);
                    lines.Add($"        set_status('> {prefix} 요소 검증 중...')");
                    lines.Add("        try:");
                    lines.Add($"            el = wait.until(EC.presence_of_element_located((AppiumBy.{byType}, {PyRepr(target)})))");
                    lines.Add($"            expected = {PyRepr(value)}");
                    lines.Add("            actual_text = el.text or el.get_attribute('content-desc') or ''");
                    lines.Add("            if expected not in actual_text:");
                    lines.Add("                write_error(f'[검증 실패] 텍스트 불일치.\\n▶ 기대값:\\n[{expected}]\\n▶ 실제값:\\n[{actual_text}]')");
                    lines.Add("                raise Exception('Assert Fail')");
                    lines.Add($"            set_status('> {prefix} 검증 패스')");
                    lines.Add("        except Exception as ex:");
                    lines.Add($"            if 'Assert Fail' not in str(ex): write_error('[검증 실패] 요소 미노출: ' + {PyRepr(target)})");
                    lines.Add("            raise Exception('Verification Error')");
                }
                else if (row.StartsWith("[ScreenAssert]", StringComparison.Ordinal))
                {
                    string value = GetAfterColon(row);
                    lines.Add($"        set_status('> {prefix} 전체 화면 시각 검증 중...')");
                    lines.Add($"        verdict = do_visual_assert(driver, scenario_name, {index + 1})");
                    lines.Add("        set_step_visual(verdict)");
                    lines.Add("        visual_status = verdict.get('status') if verdict else 'ERROR'");
                    lines.Add("        if visual_status != 'PASS':");
                    lines.Add("            detail = verdict.get('message', '') if verdict else '검증 결과가 없습니다.'");
                    lines.Add("            rate = verdict.get('match_rate', 0.0) if verdict else 0.0");
                    lines.Add("            write_error(f'[시각 검증 중단] 상태={visual_status}, 일치율={rate}%. {detail}')");
                    lines.Add("            raise Exception('Visual Assert Not Passed')");

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        lines.Add($"        expected = {PyRepr(value)}.replace(' ', '')");
                        lines.Add("        is_found = False");
                        lines.Add("        for _ in range(5):");
                        lines.Add("            page_xml = driver.page_source");
                        lines.Add("            all_texts = re.findall(r'(?:text|content-desc)=\"([^\"]+)\"', page_xml)");
                        lines.Add("            full_text = ''.join(all_texts).replace(' ', '')");
                        lines.Add("            if expected in full_text:");
                        lines.Add("                is_found = True");
                        lines.Add("                break");
                        lines.Add("            size = driver.get_window_size()");
                        lines.Add("            sx = int(size['width'] * 0.5)");
                        lines.Add("            sy = int(size['height'] * 0.7)");
                        lines.Add("            ey = int(size['height'] * 0.3)");
                        lines.Add("            driver.swipe(sx, sy, sx, ey, 1200)");
                        lines.Add("            time.sleep(1.5)");
                        lines.Add("        if not is_found:");
                        lines.Add("            write_error(f'[검증 실패] 스크롤을 내려도 문장을 찾을 수 없습니다: {expected}')");
                        lines.Add("            raise Exception('Screen Assert Fail')");
                    }
                    lines.Add($"        set_status('> {prefix} 화면/시각 검증 패스')");
                }
                else if (row.StartsWith("[RunPython]", StringComparison.Ordinal))
                {
                    string pythonPath = row.Substring("[RunPython]".Length).Trim();
                    string fileName = Path.GetFileName(pythonPath);
                    lines.Add($"        set_status('> {prefix} 파이썬 스크립트 실행 준비: ' + {PyRepr(fileName)})");
                    lines.Add("        try:");
                    lines.Add($"            py_path = {PyRepr(pythonPath)}");
                    lines.Add("            command = [sys.executable, '-m', 'pytest', py_path, '-v']");
                    lines.Add("            check = subprocess.run([sys.executable, '-m', 'pytest', '--version'], capture_output=True)");
                    lines.Add("            if check.returncode != 0:");
                    lines.Add("                command = [sys.executable, py_path]");
                    lines.Add("            process = subprocess.Popen(command, cwd=os.path.dirname(py_path) or None, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, encoding='utf-8', errors='replace')");
                    lines.Add("            for output_line in process.stdout:");
                    lines.Add("                output_line = output_line.strip()");
                    lines.Add("                if output_line:");
                    lines.Add("                    set_status(f'> [PY실행중] {output_line[:120]}')");
                    lines.Add("            process.wait()");
                    lines.Add("            if process.returncode != 0:");
                    lines.Add($"                write_error('[스크립트 에러] 테스트 중 실패가 발생했습니다: ' + {PyRepr(fileName)})");
                    lines.Add("                raise Exception('Python Script Failed')");
                    lines.Add($"            set_status('> {prefix} 파이썬 스크립트 성공: ' + {PyRepr(fileName)})");
                    lines.Add("        except Exception as ex:");
                    lines.Add("            if 'Python Script Failed' not in str(ex):");
                    lines.Add("                write_error(f'[실행 오류] {str(ex)}')");
                    lines.Add("            raise Exception('Python Execution Error')");
                }

                lines.Add("        if CAPTURE_STEP_SCREENSHOTS:");
                lines.Add($"            capture_step_screenshot(driver, scenario_name, {index + 1}, loop_index + 1)");
                lines.Add("        finish_step('PASS')");
                lines.Add("        if INTER_STEP_DELAY > 0:");
                lines.Add("            time.sleep(INTER_STEP_DELAY)");
            }

            lines.AddRange(new[]
            {
                "    set_status('[ System ] 모든 시나리오가 성공적으로 끝났습니다!')",
                "except Exception as ex:",
                "    try:",
                "        if CAPTURE_STEP_SCREENSHOTS and driver and CURRENT_STEP:",
                "            capture_step_screenshot(driver, scenario_name if 'scenario_name' in locals() else 'UnknownScenario', CURRENT_STEP.get('index', 0), CURRENT_STEP.get('loop', 0))",
                "    except Exception:",
                "        pass",
                "    finish_step('FAIL', str(ex))",
                "    if not os.path.exists(ERROR_FILE) or os.path.getsize(ERROR_FILE) == 0:",
                "        error_message = str(ex)",
                "        if 'MaxRetryError' in error_message or 'ConnectionRefusedError' in error_message:",
                "            write_error('Appium 서버가 꺼져있거나 연결에 실패했습니다.')",
                "        else:",
                "            write_error(f'시스템 오류 발생: {error_message[:300]}')",
                "    raise",
                "finally:",
                "    if driver is not None:",
                "        try:",
                "            driver.quit()",
                "        except Exception:",
                "            pass"
            });

            File.WriteAllText(scriptPath, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
            ValidateGeneratedPython(scriptPath, sysPath);

            var startInfo = new ProcessStartInfo
            {
                FileName = "python",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = sysPath,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            startInfo.ArgumentList.Add(scriptPath);

            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            Interlocked.Exchange(ref userStoppedProcessId, 0);
            int exitHandled = 0;

            void HandleExit()
            {
                if (Interlocked.Exchange(ref exitHandled, 1) != 0) return;
                try
                {
                    process.WaitForExit();
                    bool wasUserStopped = Interlocked.CompareExchange(ref userStoppedProcessId, 0, process.Id) == process.Id;
                    if (!wasUserStopped && process.ExitCode != 0 && (!File.Exists(errorFile) || new FileInfo(errorFile).Length == 0))
                    {
                        string crashDetails = File.Exists(crashLog) ? File.ReadAllText(crashLog) : "";
                        string message = "Python 실행이 비정상 종료되었습니다.";
                        if (!string.IsNullOrWhiteSpace(crashDetails)) message += "\n\n" + crashDetails.Trim();
                        File.WriteAllText(errorFile, message, new UTF8Encoding(true));
                    }
                }
                catch { }
                finally
                {
                    lock (ProcessGate)
                    {
                        if (ReferenceEquals(currentProcess, process)) currentProcess = null;
                    }
                }
            }

            process.OutputDataReceived += (_, args) => AppendCrashLine(crashLog, args.Data);
            process.ErrorDataReceived += (_, args) => AppendCrashLine(crashLog, args.Data);
            process.Exited += (_, _) => HandleExit();

            lock (ProcessGate) currentProcess = process;
            try
            {
                if (!process.Start()) throw new InvalidOperationException("Python 프로세스를 시작하지 못했습니다.");
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                if (process.HasExited) HandleExit();
                if (statusLabel != null) statusLabel.Text = "봇 실행 중";
            }
            catch
            {
                lock (ProcessGate)
                {
                    if (ReferenceEquals(currentProcess, process)) currentProcess = null;
                }
                process.Dispose();
                throw;
            }
        }

        private static void ValidateGeneratedPython(string scriptPath, string workingDirectory)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "python",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            startInfo.ArgumentList.Add("-m");
            startInfo.ArgumentList.Add("py_compile");
            startInfo.ArgumentList.Add(scriptPath);

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Python 문법 검사 프로세스를 시작하지 못했습니다.");
            Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errorTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(10000))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                throw new TimeoutException("생성된 Python 스크립트 문법 검사가 시간 초과되었습니다.");
            }

            string output = outputTask.GetAwaiter().GetResult();
            string error = errorTask.GetAwaiter().GetResult();
            if (process.ExitCode != 0)
                throw new InvalidOperationException("생성된 Python 스크립트 문법 오류:\n" + (error + output).Trim());
        }

        private static void AppendCrashLine(string crashLog, string? line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            lock (CrashLogGate)
            {
                File.AppendAllText(crashLog, line + Environment.NewLine, new UTF8Encoding(false));
            }
        }
    }
}
