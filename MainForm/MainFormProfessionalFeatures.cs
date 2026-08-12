using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using AppiumBuilder.Core;
using AppiumBuilder.UI;
using AppiumBuilder.Utils;

namespace AppiumBuilder
{
    public partial class MainForm
    {
        // Home dashboard meta labels
        private Label lblHomeConnMeta = null!, lblHomeModelMeta = null!, lblHomeOsMeta = null!;
        private Label lblStatTotalTrend = null!, lblStatPassTrend = null!, lblStatFailTrend = null!, lblStatRateTrend = null!;

        // Log / media professional controls
        private ComboBox cmbLogLevelFilter = null!;
        private TextBox txtLogSearch = null!;
        private RoundedButton btnLogPause = null!;
        private Label lblLogCaptureMetric = null!, lblLogRecordingMetric = null!, lblLogSavePathMetric = null!;
        private bool isLogPaused;
        private DateTime logSessionStartedAt = DateTime.MinValue;
        private DateTime logRecordingStartedAt = DateTime.MinValue;
        private readonly ConcurrentQueue<string> logArchive = new();
        private int archivedLogLineCount;
        private const int MaxArchivedLogLines = 50000;

        // Appium bot enhancements
        private TextBox txtScenarioSearch = null!, txtRunDelay = null!;
        private ComboBox cmbBotLogFilter = null!, cmbFailureBehavior = null!;
        private RoundedButton btnBotLogClear = null!;
        private ModernToggleSwitch toggleStepScreenshot = null!, toggleRunVideo = null!;
        private readonly List<(string Text, Color Color)> botLiveArchive = new();
        private Process? botRecordingProcess;
        private string? botRecordingRemotePath;
        private string? botRecordingLocalPath;

        // Appium Server controls
        private Label lblAppiumServerState = null!, lblAppiumServerEndpoint = null!;
        private RoundedPanel dotAppiumServer = null!;
        private RoundedButton btnAppiumServerToggle = null!, btnAppiumTerminal = null!;
        private bool appiumServerRefreshInProgress;

        private bool homeMetaRefreshInProgress;
        private DateTime homeMetaLastRefresh = DateTime.MinValue;

        private void RefreshCurrentWorkspace()
        {
            try
            {
                if (pnlTabHome != null && pnlTabHome.Visible)
                {
                    RefreshTestDashboard();
                    lblStatusMsg.Text = "상태: 홈 대시보드를 새로고침했습니다.";
                }
                else if (pnlTabLog != null && pnlTabLog.Visible)
                {
                    RebuildLogConsole();
                    RefreshLogMetaLabels();
                    lblStatusMsg.Text = "상태: 로그/미디어 화면을 새로고침했습니다.";
                }
                else if (pnlTabAuto != null && pnlTabAuto.Visible)
                {
                    RefreshSavedScenariosList();
                    RebuildBotLiveConsole();
                    lblStatusMsg.Text = "상태: Appium 봇 작업 공간을 새로고침했습니다.";
                }
                else
                {
                    lblStatusMsg.Text = "상태: 화면을 새로고침했습니다.";
                }
                lblStatusMsg.ForeColor = Globals.Success;
            }
            catch (Exception ex)
            {
                lblStatusMsg.Text = "상태: 새로고침 실패";
                lblStatusMsg.ForeColor = Globals.Danger;
                MessageBox.Show(this, "새로고침 중 오류가 발생했습니다.\n" + ex.Message, "새로고침 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OpenWorkspaceSettings()
        {
            using var form = new WorkspaceSettingsForm();
            form.OpenEnvironmentDiagnostics += (_, _) =>
            {
                using var diagnostics = new EnvironmentDiagnosticsForm();
                diagnostics.ShowDialog(this);
            };
            form.OpenRetentionSettings += (_, _) =>
            {
                using var retention = new RetentionSettingsForm();
                retention.ShowDialog(this);
            };
            form.OpenVisualBaselines += (_, _) =>
            {
                using var visual = new VisualBaselineManagerForm(TestSetPath, loadedScenarioName);
                visual.ShowDialog(this);
            };
            form.OpenScenarioVersions += (_, _) =>
            {
                using var versions = new ScenarioVersionManagerForm(TestSetPath, CsvPath, loadedScenarioName);
                versions.ShowDialog(this);
            };
            form.ShowDialog(this);
        }

        private void OpenFullHistoryViewer()
        {
            using var viewer = new TestHistoryViewerForm(Globals.LogFolder);
            viewer.ShowDialog(this);
            RefreshTestDashboard();
        }

        private void OpenLogFolder()
        {
            try
            {
                Directory.CreateDirectory(Globals.LogFolder);
                Process.Start(new ProcessStartInfo("explorer.exe", Globals.LogFolder) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "로그 저장 폴더를 열지 못했습니다.\n" + ex.Message, "폴더 열기 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async void RefreshHomeDeviceMeta()
        {
            if (homeMetaRefreshInProgress) return;
            if (DateTime.Now - homeMetaLastRefresh < TimeSpan.FromSeconds(4) && lblHomeConnMeta != null) return;
            homeMetaRefreshInProgress = true;
            try
            {
                var snapshot = await Task.Run(() =>
                {
                    bool connected = AdbEngine.IsDeviceConnected();
                    string serial = AdbEngine.SelectedSerial ?? string.Empty;
                    string api = "-";
                    string patch = "-";
                    if (connected)
                    {
                        string rawApi = AdbEngine.RunCommand("shell getprop ro.build.version.sdk", 3000).Trim();
                        string rawPatch = AdbEngine.RunCommand("shell getprop ro.build.version.security_patch", 3000).Trim();
                        if (!string.IsNullOrWhiteSpace(rawApi) && !rawApi.Contains("ADB ", StringComparison.OrdinalIgnoreCase)) api = rawApi;
                        if (!string.IsNullOrWhiteSpace(rawPatch) && !rawPatch.Contains("ADB ", StringComparison.OrdinalIgnoreCase)) patch = rawPatch;
                    }
                    bool appiumRunning = AppiumServerManager.IsServerRunning();
                    return (Connected: connected, Serial: serial, Api: api, Patch: patch, AppiumRunning: appiumRunning);
                });

                if (IsDisposed || Disposing) return;
                homeMetaLastRefresh = DateTime.Now;
                if (lblHomeConnMeta != null)
                {
                    string serverState = snapshot.AppiumRunning ? "실행 중" : "중지됨";
                    lblHomeConnMeta.Text = snapshot.Connected
                        ? $"Appium 서버 · {serverState} · 127.0.0.1:4723\n마지막 확인 · 방금 전"
                        : $"ADB 연결 대기 · Appium 서버 {serverState}\n마지막 확인 · 방금 전";
                }
                if (lblHomeModelMeta != null)
                    lblHomeModelMeta.Text = snapshot.Connected
                        ? $"디바이스 ID · {(string.IsNullOrWhiteSpace(snapshot.Serial) ? "-" : snapshot.Serial)}\n연결 방식 · {(snapshot.Serial.Contains(':') ? "Wi-Fi" : "USB")}"
                        : "디바이스 ID · -\n연결 방식 · -";
                if (lblHomeOsMeta != null)
                    lblHomeOsMeta.Text = $"API Level · {snapshot.Api}\n보안 패치 · {snapshot.Patch}";
            }
            catch
            {
                // 상태 타이머가 다음 주기에 다시 시도한다.
            }
            finally
            {
                homeMetaRefreshInProgress = false;
            }
        }

        private async Task RefreshAppiumServerUiAsync(bool force = false)
        {
            if (appiumServerRefreshInProgress && !force) return;
            appiumServerRefreshInProgress = true;
            try
            {
                bool running = await AppiumServerManager.IsServerRunningAsync();
                if (IsDisposed || Disposing) return;

                if (lblAppiumServerState != null)
                {
                    lblAppiumServerState.Text = running
                        ? (AppiumServerManager.OwnsRunningServer ? "실행 중 · Appium Builder 관리" : "실행 중 · 외부 서버")
                        : "중지됨";
                    lblAppiumServerState.ForeColor = running ? Globals.Success : Globals.TextMuted;
                }
                if (lblAppiumServerEndpoint != null)
                    lblAppiumServerEndpoint.Text = running ? "127.0.0.1:4723 · /wd/hub 응답 정상" : "127.0.0.1:4723 · 서버 응답 없음";
                if (dotAppiumServer != null)
                {
                    dotAppiumServer.FillColor = running ? Globals.Success : Globals.BorderStrong;
                    dotAppiumServer.Invalidate();
                }
                if (btnAppiumServerToggle != null)
                {
                    btnAppiumServerToggle.Text = running && AppiumServerManager.OwnsRunningServer ? "서버 종료" : running ? "실행 중" : "서버 시작";
                    btnAppiumServerToggle.IconName = running && AppiumServerManager.OwnsRunningServer ? "stop" : "play";
                    btnAppiumServerToggle.Enabled = !running || AppiumServerManager.OwnsRunningServer;
                    btnAppiumServerToggle.FillColor = running && AppiumServerManager.OwnsRunningServer ? Globals.DangerSoft : running ? Globals.SuccessSoft : Globals.Accent;
                    btnAppiumServerToggle.ForeColor = running && AppiumServerManager.OwnsRunningServer ? Globals.Danger : running ? Globals.Success : Color.White;
                    btnAppiumServerToggle.IconColor = btnAppiumServerToggle.ForeColor;
                    btnAppiumServerToggle.BorderColor = running ? (AppiumServerManager.OwnsRunningServer ? Globals.Danger : Globals.Success) : Globals.Accent;
                    btnAppiumServerToggle.BorderThickness = running ? 1 : 0;
                    btnAppiumServerToggle.Invalidate();
                }

                // 홈 연결 카드의 Appium 상태도 같은 주기로 동기화한다.
                if (lblHomeConnMeta != null)
                {
                    string deviceLine = lblHomeConn != null && string.Equals(lblHomeConn.Text, "연결됨", StringComparison.Ordinal)
                        ? "마지막 확인 · 방금 전"
                        : "ADB 연결 대기";
                    lblHomeConnMeta.Text = $"Appium 서버 · {(running ? "실행 중" : "중지됨")} · 127.0.0.1:4723\n{deviceLine}";
                }
            }
            catch
            {
                // 주기 상태 확인은 다음 틱에서 다시 시도한다.
            }
            finally
            {
                appiumServerRefreshInProgress = false;
            }
        }

        private async Task<bool> EnsureAppiumServerReadyAsync(string purpose = "Appium 봇 실행")
        {
            if (await AppiumServerManager.IsServerRunningAsync())
            {
                await RefreshAppiumServerUiAsync(true);
                return true;
            }

            if (MessageBox.Show(
                    this,
                    $"{purpose}을 위해 Appium 서버가 필요합니다.\n\n지금 Appium 서버 터미널을 열고 서버를 시작할까요?",
                    "Appium 서버 필요",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
                return false;

            if (lblAppiumServerState != null)
            {
                lblAppiumServerState.Text = "시작 중...";
                lblAppiumServerState.ForeColor = Globals.Info;
            }

            var result = await AppiumServerManager.StartVisibleAsync();
            await RefreshAppiumServerUiAsync(true);
            if (!result.Success)
            {
                MessageBox.Show(this, result.Message, "Appium 서버 시작 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            lblStatusMsg.Text = "상태: Appium 서버 실행 중 · 127.0.0.1:4723";
            lblStatusMsg.ForeColor = Globals.Success;
            return true;
        }

        private async void ToggleAppiumServer()
        {
            bool running = await AppiumServerManager.IsServerRunningAsync();
            if (running)
            {
                if (!AppiumServerManager.OwnsRunningServer)
                {
                    MessageBox.Show(this,
                        "현재 4723 포트의 Appium 서버는 Appium Builder가 시작한 프로세스가 아닙니다.\n안전을 위해 외부 서버는 여기서 종료하지 않습니다.",
                        "외부 Appium 서버",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                if (BotEngine.IsRunning)
                {
                    MessageBox.Show(this, "봇 실행 중에는 Appium 서버를 종료할 수 없습니다.", "실행 중", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                AppiumServerManager.StopOwnedServer(out string stopMessage);
                lblStatusMsg.Text = "상태: " + stopMessage;
                lblStatusMsg.ForeColor = Globals.TextMuted;
                await Task.Delay(300);
                await RefreshAppiumServerUiAsync(true);
                return;
            }

            var startResult = await AppiumServerManager.StartVisibleAsync();
            lblStatusMsg.Text = "상태: " + startResult.Message;
            lblStatusMsg.ForeColor = startResult.Success ? Globals.Success : Globals.Danger;
            await RefreshAppiumServerUiAsync(true);
            if (!startResult.Success)
                MessageBox.Show(this, startResult.Message, "Appium 서버 시작 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void ShowAppiumTerminal()
        {
            bool ok = AppiumServerManager.ShowTerminal(out string message);
            lblStatusMsg.Text = "상태: " + message;
            lblStatusMsg.ForeColor = ok ? Globals.Info : Globals.Danger;
            if (!ok) MessageBox.Show(this, message, "Appium 터미널", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void PrimeBotAssistant(string prompt, bool runImmediately = false)
        {
            SwitchTab(pnlTabAuto, btnTabAuto);
            if (txtAiPrompt == null) return;
            txtAiPrompt.Text = prompt;
            txtAiPrompt.ForeColor = Globals.TextPrimary;
            txtAiPrompt.Focus();
            txtAiPrompt.SelectionStart = txtAiPrompt.TextLength;
            if (runImmediately && btnAiAnalyze != null && btnAiAnalyze.Enabled) btnAiAnalyze.PerformClick();
        }

        private async void ExploreCurrentUiElements()
        {
            if (!await Task.Run(AdbEngine.IsDeviceConnected))
            {
                MessageBox.Show(this, "요소 탐색을 위해 먼저 Android 기기를 연결해주세요.", "기기 연결 필요");
                return;
            }

            try
            {
                string remote = "/sdcard/appium_builder_ui.xml";
                string local = Path.Combine(SysPath, "element_explorer.xml");
                await AdbEngine.RunCommandAsync($"shell uiautomator dump {remote}", 10000);
                await AdbEngine.RunCommandAsync($"pull {remote} \"{local}\"", 15000);
                _ = AdbEngine.RunCommandAsync($"shell rm {remote}", 5000);
                if (!File.Exists(local)) throw new FileNotFoundException("UI Dump 파일을 가져오지 못했습니다.");

                XDocument doc = XDocument.Load(local);
                var elements = doc.Descendants("node")
                    .Select(node => new
                    {
                        Text = (string?)node.Attribute("text") ?? string.Empty,
                        Desc = (string?)node.Attribute("content-desc") ?? string.Empty,
                        Id = (string?)node.Attribute("resource-id") ?? string.Empty,
                        Clickable = string.Equals((string?)node.Attribute("clickable"), "true", StringComparison.OrdinalIgnoreCase)
                    })
                    .Where(x => x.Clickable || x.Text.Length > 0 || x.Desc.Length > 0)
                    .Take(60)
                    .ToList();

                if (elements.Count == 0)
                {
                    MessageBox.Show(this, "현재 화면에서 노출된 UI 요소를 찾지 못했습니다.", "자동 요소 탐색");
                    return;
                }

                var sb = new StringBuilder();
                int index = 1;
                foreach (var e in elements)
                {
                    string label = e.Text.Length > 0 ? e.Text : e.Desc.Length > 0 ? e.Desc : "(텍스트 없음)";
                    sb.AppendLine($"{index++,2}. {label}");
                    if (e.Id.Length > 0) sb.AppendLine("    ID: " + e.Id);
                }
                string result = sb.ToString();
                if (result.Length > 8000) result = result[..8000];
                MessageBox.Show(this, result, $"자동 요소 탐색 · {elements.Count}개", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "UI 요소 탐색 중 오류가 발생했습니다.\n" + ex.Message, "자동 요소 탐색 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void AnalyzeLatestBotFailure()
        {
            string errorFile = Path.Combine(SysPath, "bot_error.log");
            string error = File.Exists(errorFile) ? ReadFileShared(errorFile).Trim() : string.Empty;
            TestRunRecord? latestFailure = LoadTestHistory().OrderByDescending(GetRecordTime)
                .FirstOrDefault(r => GetRecordStatus(r) is "FAIL" or "STOPPED");
            if (string.IsNullOrWhiteSpace(error) && latestFailure == null)
            {
                MessageBox.Show(this, "분석할 최근 실패 기록이 없습니다.", "오류 원인 분석");
                return;
            }

            string scenario = latestFailure?.scenario ?? loadedScenarioName ?? "현재 시나리오";
            string detail = !string.IsNullOrWhiteSpace(error) ? error : latestFailure?.failMessage ?? "상세 오류 없음";
            if (detail.Length > 2500) detail = detail[..2500];
            PrimeBotAssistant($"'{scenario}' 테스트의 다음 오류 원인을 분석하고 수정할 Appium 단계 또는 로케이터 개선안을 제안해줘. 오류: {detail}", false);
        }

        private void GenerateTestDataPrompt()
        {
            PrimeBotAssistant("현재 화면의 입력 필드에 사용할 안전한 QA 테스트 데이터를 만들고, 그 값을 입력하는 Appium 단계를 추가해줘. 비밀번호나 실제 개인정보는 사용하지 마.", false);
        }

        private void NewScenario()
        {
            if (lstSteps == null) return;
            if (lstSteps.Items.Count > 0 && MessageBox.Show(this, "현재 편집 중인 단계를 비우고 새 시나리오를 만들까요?", "새 시나리오", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            lstSteps.Items.Clear();
            loadedScenarioName = null;
            if (lblFlowTitle != null) lblFlowTitle.Text = "시나리오 플로우 · 새 시나리오";
            ResetToAddMode();
            RefreshSavedScenariosList();
        }

        private void ArchiveLogLine(string line)
        {
            logArchive.Enqueue(line);
            int count = Interlocked.Increment(ref archivedLogLineCount);
            while (count > MaxArchivedLogLines && logArchive.TryDequeue(out _))
                count = Interlocked.Decrement(ref archivedLogLineCount);
        }

        private bool LogLineMatchesCurrentFilter(string line)
        {
            string level = cmbLogLevelFilter?.SelectedItem?.ToString() ?? "전체";
            string search = txtLogSearch?.Text ?? string.Empty;
            if (txtLogSearch != null && string.Equals(search, txtLogSearch.Tag?.ToString(), StringComparison.Ordinal)) search = string.Empty;
            if (level == "전체") level = "ALL";
            return LogLineParser.Matches(line, level, search);
        }

        private Color ResolveLogColor(string line)
        {
            return LogLineParser.GetLevel(line) switch
            {
                "D" => Globals.Info,
                "I" => Globals.Success,
                "W" => Color.FromArgb(217, 119, 6),
                "E" or "F" => Globals.Danger,
                _ => Globals.TextSecondary
            };
        }

        private void AppendStyledLogLine(string line)
        {
            if (txtConsole == null || txtConsole.IsDisposed || !LogLineMatchesCurrentFilter(line)) return;
            txtConsole.SelectionStart = txtConsole.TextLength;
            txtConsole.SelectionLength = 0;
            txtConsole.SelectionColor = ResolveLogColor(line);
            txtConsole.AppendText(line + Environment.NewLine);
            txtConsole.SelectionColor = Globals.TextSecondary;
        }

        private void DrainLogQueueToConsole()
        {
            if (isLogPaused) return;
            int drained = 0;
            const int maxLinesPerTick = 900;
            while (drained < maxLinesPerTick && logQueue.TryDequeue(out string? line))
            {
                Interlocked.Decrement(ref pendingLogLineCount);
                AppendStyledLogLine(line);
                drained++;
            }
            if (drained <= 0) return;
            TrimConsoleBuffer();
            txtConsole.SelectionStart = txtConsole.TextLength;
            txtConsole.ScrollToCaret();
            if (lblLogLineCount != null) lblLogLineCount.Text = $"{archivedLogLineCount:N0}";
            RefreshLogMetaLabels();
        }

        private void RebuildLogConsole()
        {
            if (txtConsole == null || txtConsole.IsDisposed) return;
            txtConsole.SuspendLayout();
            txtConsole.Clear();
            foreach (string line in logArchive.ToArray().Where(LogLineMatchesCurrentFilter).TakeLast(MaxConsoleLines))
                AppendStyledLogLine(line);
            txtConsole.SelectionStart = txtConsole.TextLength;
            txtConsole.ScrollToCaret();
            txtConsole.ResumeLayout();
        }

        private void RefreshLogMetaLabels()
        {
            if (lblLogCaptureMetric != null)
            {
                lblLogCaptureMetric.Text = isLogging && logSessionStartedAt != DateTime.MinValue
                    ? FormatClock(DateTime.Now - logSessionStartedAt)
                    : isLogging ? "활성" : "준비";
                lblLogCaptureMetric.ForeColor = isLogging ? Globals.Success : Globals.TextPrimary;
            }
            if (lblLogRecordingMetric != null)
            {
                lblLogRecordingMetric.Text = isRecording && logRecordingStartedAt != DateTime.MinValue
                    ? FormatClock(DateTime.Now - logRecordingStartedAt)
                    : isRecording ? "녹화 중" : "준비";
                lblLogRecordingMetric.ForeColor = isRecording ? Globals.Success : Globals.TextPrimary;
            }
            if (lblLogSavePathMetric != null)
                lblLogSavePathMetric.Text = Globals.LogFolder;
        }

        private static string FormatClock(TimeSpan span) => $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}";

        private void RebuildBotLiveConsole()
        {
            if (rtbLiveConsole == null || rtbLiveConsole.IsDisposed) return;
            string filter = cmbBotLogFilter?.SelectedItem?.ToString() ?? "전체";
            rtbLiveConsole.Clear();
            foreach ((string text, Color color) in botLiveArchive)
            {
                if (!BotLogMatches(text, filter)) continue;
                rtbLiveConsole.SelectionStart = rtbLiveConsole.TextLength;
                rtbLiveConsole.SelectionColor = color;
                rtbLiveConsole.AppendText(text + Environment.NewLine);
            }
            rtbLiveConsole.SelectionColor = Globals.TextSecondary;
            rtbLiveConsole.ScrollToCaret();
        }

        private static bool BotLogMatches(string text, string filter)
        {
            if (filter == "전체") return true;
            if (filter == "성공") return text.Contains("성공", StringComparison.OrdinalIgnoreCase) || text.Contains("PASS", StringComparison.OrdinalIgnoreCase);
            if (filter == "실패") return text.Contains("실패", StringComparison.OrdinalIgnoreCase) || text.Contains("FAIL", StringComparison.OrdinalIgnoreCase) || text.Contains("오류", StringComparison.OrdinalIgnoreCase);
            if (filter == "정보") return !BotLogMatches(text, "성공") && !BotLogMatches(text, "실패");
            return true;
        }

        private BotRunOptions GetBotRunOptions()
        {
            int delay = 500;
            if (txtRunDelay != null && int.TryParse(txtRunDelay.Text.Trim(), out int parsed)) delay = Math.Clamp(parsed, 0, 60000);
            return new BotRunOptions
            {
                InterStepDelayMs = delay,
                CaptureStepScreenshots = toggleStepScreenshot?.Checked ?? true
            };
        }

        private bool ContinueBatchAfterFailure => cmbFailureBehavior != null && cmbFailureBehavior.SelectedIndex == 1;

        private async Task StartBotRunRecordingAsync()
        {
            if (!(toggleRunVideo?.Checked ?? false) || botRecordingProcess != null) return;
            if (isRecording || isSoloRecording)
            {
                AppendLiveLog("[VIDEO] 다른 화면 녹화가 이미 실행 중이라 봇 실행 영상 녹화를 시작하지 않았습니다.", Globals.Warning);
                return;
            }
            if (!await Task.Run(AdbEngine.IsDeviceConnected)) return;
            string name = $"BotRun_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
            botRecordingRemotePath = "/sdcard/" + name;
            botRecordingLocalPath = Path.Combine(Globals.LogFolder, name);
            try
            {
                botRecordingProcess = AdbEngine.StartAdbProcess($"shell screenrecord --bit-rate 8000000 {botRecordingRemotePath}", hidden: true);
                AppendLiveLog("[VIDEO] 실행 영상 녹화를 시작했습니다.", Globals.Info);
            }
            catch (Exception ex)
            {
                botRecordingProcess = null;
                AppendLiveLog("[VIDEO] 녹화 시작 실패: " + ex.Message, Globals.Warning);
            }
        }

        private async Task StopBotRunRecordingAsync()
        {
            Process? process = botRecordingProcess;
            string? remote = botRecordingRemotePath;
            string? local = botRecordingLocalPath;
            botRecordingProcess = null;
            botRecordingRemotePath = null;
            botRecordingLocalPath = null;
            if (process == null || string.IsNullOrWhiteSpace(remote) || string.IsNullOrWhiteSpace(local)) return;
            try
            {
                await Task.Run(() => AdbEngine.RunCommand("shell pkill -INT screenrecord", 5000));
                try { if (!process.HasExited) process.WaitForExit(4000); } catch { }
                await Task.Delay(350);
                await AdbEngine.RunCommandAsync($"pull {remote} \"{local}\"", 30000);
                _ = AdbEngine.RunCommandAsync($"shell rm {remote}", 5000);
                if (File.Exists(local)) AppendLiveLog("[VIDEO] 실행 영상 저장 완료: " + Path.GetFileName(local), Globals.Success);
            }
            catch (Exception ex)
            {
                AppendLiveLog("[VIDEO] 실행 영상 저장 실패: " + ex.Message, Globals.Warning);
            }
            finally
            {
                AdbEngine.TryKill(process);
            }
        }
    }
}
