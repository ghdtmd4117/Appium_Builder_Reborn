using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using AppiumBuilder.Utils;
using AppiumBuilder.Core;
using AppiumBuilder.UI;
using Timer = System.Windows.Forms.Timer;

namespace AppiumBuilder
{
    // MainForm은 여러 파일로 나뉘어 있습니다 (partial class):
    //   MainForm.cs             - 필드/생성자/탭전환/테스트 이력/공용 유틸 (이 파일)
    //   MainFormDesign.cs       - 버튼/카드/입력창 등 디자인 헬퍼
    //   MainFormShell.cs        - 연결 화면 + 사이드바
    //   MainFormHomeTab.cs      - 홈 탭 (상태바 + 테스트 현황 + 빠른 실행)
    //   MainFormLogTab.cs       - 로그/미디어 탭
    //   MainFormUtilTab.cs      - 유틸리티 탭
    //   MainFormAutoTab.cs      - Appium 봇 탭 UI 구성
    //   MainFormAutoTabLogic.cs - Appium 봇 탭 파싱/변환 로직
    //   MainFormGemini.cs       - Gemini API 연동
    public partial class MainForm : Form
    {
        private Panel pnlConnect = null!, pnlMain = null!, pnlSidebar = null!, pnlContent = null!;
        private Panel pnlTabHome = null!, pnlTabLog = null!, pnlTabUtil = null!, pnlTabAuto = null!;
        private Label lblSideModel = null!, lblStatusMsg = null!;
        private RoundedPanel navIndicator = null!;
        private TextBox txtIp = null!, txtPort = null!;
        private RoundedButton btnTabHome = null!, btnTabLog = null!, btnTabUtil = null!, btnTabAuto = null!, btnIpConn = null!;
        private RichTextBox txtConsole = null!;
        private Timer statusTimer = null!, consoleTimer = null!, botStatusTimer = null!, appiumServerTimer = null!;
        private ConcurrentQueue<string> logQueue = new ConcurrentQueue<string>();
        private int pendingLogLineCount = 0;
        private const int MaxPendingLogLines = 50000;
        private const int MaxConsoleLines = 20000;
        private Process? logProcess, recProcess, soloRecProcess, dashboardProcess;
        private bool isLogging = false, isRecording = false, isSoloRecording = false, statusCheckInProgress = false, loggingStartedForRecording = false;
        private string lastDeviceModel = "-", lastDeviceOs = "-";
        private string? currentCombinedRecordingName, currentSoloRecordingName;

        // Appium 봇 UI 컨트롤
        private TextBox txtAiPrompt = null!, txtTarget = null!, txtValue = null!, txtX = null!, txtY = null!, txtLoop = null!;
        private ComboBox cmbAction = null!, cmbLocator = null!;
        private ListBox lstSteps = null!;
        private Panel pnlSavedScenarios = null!;
        private RoundedButton btnAddStep = null!, btnEditStep = null!, btnCancelEdit = null!, btnDelStep = null!, btnAiAnalyze = null!;
        private Label lblBotStatusMessage = null!, lblFlowTitle = null!;
        private int editingIndex = -1;
        private string? loadedScenarioName = null;
        private Action? RelayoutBuilderRef;
        private static readonly HttpClient geminiHttp = new HttpClient();

        // 테스트 이력 기록용
        private bool historyLogged = true;
        private string currentRunScenario = "수동 시나리오";
        private int currentRunSteps = 0;
        private DateTime currentRunStartedAt = DateTime.MinValue;
        private string? currentBatchId;
        private readonly Queue<QueuedScenarioRun> batchRunQueue = new();
        private bool batchRunActive = false;

        private sealed class QueuedScenarioRun
        {
            public string Name { get; init; } = string.Empty;
            public string SourcePath { get; init; } = string.Empty;
            public List<string> Steps { get; init; } = new();
        }

        // 홈 대시보드 갱신용 (MainFormHomeTab.cs에서 채움)
        private Label lblStatTotal = null!, lblStatPass = null!, lblStatFail = null!, lblStatRate = null!;
        private Panel pnlRecentRuns = null!;
        private Label lblHomeConn = null!, lblHomeModel = null!, lblHomeOs = null!;
        private RoundedPanel dotHomeConn = null!;

        // 로그/미디어 상태 표시용
        private Label lblLogDevice = null!, lblLogCollectionState = null!, lblLogRecordingState = null!, lblLogLineCount = null!, lblLogSavedFile = null!;

        public MainForm()
        {
            Globals.InitFolders();
            DeviceSelectionStore.Restore();
            SetupBaseUI();
            SetupHomeTab();
            SetupLogTab();
            SetupUtilTab();
            SetupAutoTab();
            BindEvents();
            RefreshSavedScenariosList();
            SwitchTab(pnlTabHome, btnTabHome);
        }

        // ===== 사이드바 안의 이름 없는 컨트롤(점, 글씨)을 찾아내어 동기화하는 스마트 로직 =====
        private void UpdateSidebarUI(bool isConnected)
        {
            if (pnlSidebar == null) return;

            void UpdateControls(Control parent)
            {
                foreach (Control c in parent.Controls)
                {
                    // 1. 상태 텍스트 변경 (아래쪽 세부 모델명 lblSideModel은 제외)
                    if (c is Label lbl && lbl != lblSideModel)
                    {
                        if (lbl.Text.Contains("기기 확인 중") || lbl.Text.Contains("기기 연결됨"))
                        {
                            lbl.Text = isConnected ? "기기 연결됨" : "기기 확인 중...";

                            // 연결 상태는 텍스트와 상태색을 함께 사용한다.
                            lbl.ForeColor = isConnected ? Globals.Success : Globals.TextFaint;
                        }
                    }
                    // 2. 상태 점(Dot) 색상 변경 
                    else if (c is RoundedPanel rp && rp != navIndicator && rp.Width <= 20)
                    {
                        rp.FillColor = isConnected ? Globals.Success : Globals.Danger;
                        rp.Invalidate();
                    }

                    // 자식 컨트롤이 더 있다면 끝까지 파고들며 탐색
                    if (c.HasChildren) UpdateControls(c);
                }
            }
            UpdateControls(pnlSidebar);
        }

        private void BindEvents()
        {
            btnTabHome.Click += (s, e) => { SwitchTab(pnlTabHome, btnTabHome); };
            btnTabLog.Click += (s, e) => { SwitchTab(pnlTabLog, btnTabLog); };
            btnTabUtil.Click += (s, e) => { SwitchTab(pnlTabUtil, btnTabUtil); };
            btnTabAuto.Click += (s, e) => { SwitchTab(pnlTabAuto, btnTabAuto); };

            statusTimer = new Timer { Interval = 2000 };
            statusTimer.Tick += async (s, e) => {
                if (statusCheckInProgress) return;
                statusCheckInProgress = true;
                try
                {
                    var deviceInfo = await System.Threading.Tasks.Task.Run(() =>
                    {
                        bool connected = AdbEngine.IsDeviceConnected();
                        if (!connected) return (Connected: false, Model: "-", Os: "-");

                        string rawModel = AdbEngine.RunCommand("shell getprop ro.product.model", 5000).Trim();
                        string rawOs = AdbEngine.RunCommand("shell getprop ro.build.version.release", 5000).Trim();
                        string model = string.IsNullOrWhiteSpace(rawModel) || rawModel.Contains("error", StringComparison.OrdinalIgnoreCase) ? "Android Device" : rawModel;
                        string os = string.IsNullOrWhiteSpace(rawOs) || rawOs.Contains("error", StringComparison.OrdinalIgnoreCase) ? "Android" : "Android " + rawOs;
                        return (Connected: true, Model: model, Os: os);
                    });

                    UpdateSidebarUI(deviceInfo.Connected);
                    if (deviceInfo.Connected)
                    {
                        lastDeviceModel = deviceInfo.Model;
                        lastDeviceOs = deviceInfo.Os;

                        if (lblSideModel != null) lblSideModel.Text = lastDeviceModel + " · " + lastDeviceOs;
                        if (lblStatusMsg != null && !lblStatusMsg.Text.Contains("기기 연결됨"))
                        {
                            lblStatusMsg.Text = $"상태: 기기 연결됨 ({lastDeviceModel})";
                            lblStatusMsg.ForeColor = Globals.Success;
                        }

                        if (lblHomeConn != null)
                        {
                            lblHomeConn.Text = "연결됨"; lblHomeConn.ForeColor = Globals.Success;
                            if (dotHomeConn != null) { dotHomeConn.FillColor = Globals.Success; dotHomeConn.Invalidate(); }
                            lblHomeModel.Text = lastDeviceModel; lblHomeOs.Text = lastDeviceOs;
                            if (lblLogDevice != null) lblLogDevice.Text = lastDeviceModel + " · " + lastDeviceOs;
                            RefreshHomeDeviceMeta();
                            RefreshLogMetaLabels();
                        }
                    }
                    else
                    {
                        if (lblSideModel != null) lblSideModel.Text = "기기 정보 확인 중...";
                        if (lblStatusMsg != null && !lblStatusMsg.Text.Contains("기기 확인 중"))
                        {
                            lblStatusMsg.Text = "상태: 기기 확인 중... (연결 대기)";
                            lblStatusMsg.ForeColor = Globals.TextFaint;
                        }

                        if (lblHomeConn != null)
                        {
                            lblHomeConn.Text = "연결 대기"; lblHomeConn.ForeColor = Globals.Danger;
                            if (dotHomeConn != null) { dotHomeConn.FillColor = Globals.Danger; dotHomeConn.Invalidate(); }
                            lblHomeModel.Text = "-"; lblHomeOs.Text = "-";
                            if (lblLogDevice != null) lblLogDevice.Text = "연결된 디바이스 없음";
                        }
                    }
                }
                catch
                {
                    // 다음 틱에서 다시 확인한다.
                }
                finally
                {
                    statusCheckInProgress = false;
                }
            };
            statusTimer.Start();

            appiumServerTimer = new Timer { Interval = 1500 };
            appiumServerTimer.Tick += async (_, _) => await RefreshAppiumServerUiAsync();
            appiumServerTimer.Start();

            consoleTimer = new Timer { Interval = 40 };
            consoleTimer.Tick += (_, _) => DrainLogQueueToConsole();

            botStatusTimer = new Timer { Interval = 200 };
            botStatusTimer.Tick += (s, e) => {
                string sf = Path.Combine(SysPath, "bot_status.txt");
                string ef = Path.Combine(SysPath, "bot_error.log");
                try
                {
                    if (File.Exists(ef) && new FileInfo(ef).Length > 0)
                    {
                        string errMsg = ReadFileShared(ef);
                        lblBotStatusMessage.ForeColor = Globals.Danger;
                        lblBotStatusMessage.Text = "[ 오류 발생 ]\n" + errMsg;
                        if (!historyLogged)
                        {
                            RecordTestHistory(currentRunScenario, currentRunSteps, false, errMsg);
                            historyLogged = true;
                            _ = StopBotRunRecordingAsync();
                            HandleBatchRunCompletion(false, errMsg, "FAIL");
                        }
                        botStatusTimer.Stop();
                        liveTimer?.Stop();
                        return;
                    }
                    if (File.Exists(sf))
                    {
                        string msg = ReadFileShared(sf);
                        if (lblBotStatusMessage.Text != msg) { lblBotStatusMessage.ForeColor = Globals.Success; lblBotStatusMessage.Text = msg; }
                        if (!historyLogged && msg.Contains("모든 시나리오가 성공적으로 끝났습니다"))
                        {
                            RecordTestHistory(currentRunScenario, currentRunSteps, true, null);
                            historyLogged = true;
                            botStatusTimer.Stop();
                            liveTimer?.Stop();
                            _ = StopBotRunRecordingAsync();
                            HandleBatchRunCompletion(true, null, "PASS");
                        }
                    }
                }
                catch (IOException)
                {
                    // 파이썬이 파일을 쓰는 바로 그 순간과 겹친 것 - 다음 200ms 틱에 다시 시도
                }
            };


            FormClosing += (s, e) =>
            {
                bool stopOwnedAppium = false;
                if (AppiumServerManager.OwnsRunningServer)
                {
                    stopOwnedAppium = MessageBox.Show(
                        this,
                        "Appium Builder가 시작한 Appium 서버가 실행 중입니다.\n프로그램과 함께 서버도 종료할까요?",
                        "Appium 서버 종료",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question) == DialogResult.Yes;
                }
                CleanupRunningResources(stopOwnedAppium);
            };
        }

        private void CleanupRunningResources(bool stopOwnedAppiumServer = false)
        {
            statusTimer?.Stop();
            consoleTimer?.Stop();
            botStatusTimer?.Stop();
            appiumServerTimer?.Stop();
            liveTimer?.Stop();

            BotEngine.StopCurrentRun(SysPath, out _);
            AdbEngine.TryKill(logProcess);
            AdbEngine.TryKill(recProcess);
            AdbEngine.TryKill(soloRecProcess);
            AdbEngine.TryKill(dashboardProcess);
            AdbEngine.TryKill(botRecordingProcess);
            if (stopOwnedAppiumServer) AppiumServerManager.StopOwnedServer(out _);

            if (isRecording || isSoloRecording || botRecordingProcess != null)
            {
                AdbEngine.RunCommand("shell pkill -INT screenrecord", 3000);
                AdbEngine.RunCommand("shell settings put system show_touches 0", 3000);
            }
        }

        private void SwitchTab(Panel pnl, RoundedButton btn)
        {
            pnlTabHome.Visible = pnlTabLog.Visible = pnlTabUtil.Visible = pnlTabAuto.Visible = false;
            btnTabHome.FillColor = btnTabLog.FillColor = btnTabUtil.FillColor = btnTabAuto.FillColor = Globals.Sidebar;
            btnTabHome.ForeColor = btnTabLog.ForeColor = btnTabUtil.ForeColor = btnTabAuto.ForeColor = Globals.SidebarTextMuted;
            btnTabHome.IconColor = btnTabLog.IconColor = btnTabUtil.IconColor = btnTabAuto.IconColor = Globals.SidebarTextMuted;
            btnTabHome.Invalidate(); btnTabLog.Invalidate(); btnTabUtil.Invalidate(); btnTabAuto.Invalidate();
            pnl.Visible = true;
            btn.FillColor = Globals.SidebarActive; btn.ForeColor = Globals.SidebarTextActive; btn.IconColor = Globals.SidebarTextActive; btn.Invalidate();
            navIndicator.Top = btn.Top;
            if (pnl == pnlTabHome)
            {
                RefreshTestDashboard();
                RefreshHomeDeviceMeta();
            }
            else if (pnl == pnlTabLog && lblLogDevice != null)
            {
                lblLogDevice.Text = lastDeviceModel == "-"
                    ? "연결된 디바이스 없음"
                    : lastDeviceModel + " · " + lastDeviceOs;
                RefreshLogMetaLabels();
            }
            else if (pnl == pnlTabAuto)
            {
                RefreshSavedScenariosList();
            }
        }

        // ===== 테스트 이력 기록 =====
        private static string HistoryFilePath => TestHistoryStore.GetHistoryPath(Globals.LogFolder);

        private void RecordTestHistory(string scenario, int totalSteps, bool pass, string? failMessage, string? statusOverride = null)
        {
            try
            {
                DateTime completedAt = DateTime.Now;
                DateTime startedAt = currentRunStartedAt == DateTime.MinValue ? completedAt : currentRunStartedAt;
                string status = string.IsNullOrWhiteSpace(statusOverride)
                    ? (pass ? "PASS" : "FAIL")
                    : statusOverride.Trim().ToUpperInvariant();

                List<TestStepRecord> stepRecords = LoadCurrentStepResults();
                if (status == "STOPPED" && stepRecords.Count < totalSteps)
                {
                    int nextIndex = stepRecords.Count == 0 ? 1 : stepRecords.Max(step => step.index) + 1;
                    stepRecords.Add(new TestStepRecord
                    {
                        index = nextIndex,
                        loop = 1,
                        raw = "사용자 중지",
                        status = "STOPPED",
                        startedAt = completedAt.ToString("o"),
                        timestamp = completedAt.ToString("o"),
                        durationMs = 0,
                        message = "사용자가 실행을 중지했습니다."
                    });
                }

                var record = new TestRunRecord
                {
                    runId = Guid.NewGuid().ToString("N"),
                    batchId = currentBatchId,
                    scenario = scenario,
                    startedAt = startedAt.ToString("o"),
                    timestamp = completedAt.ToString("o"),
                    totalSteps = totalSteps,
                    pass = pass,
                    status = status,
                    durationMs = Math.Max(0L, (long)(completedAt - startedAt).TotalMilliseconds),
                    deviceSerial = AdbEngine.SelectedSerial ?? string.Empty,
                    deviceModel = lastDeviceModel == "-" ? string.Empty : lastDeviceModel,
                    osVersion = lastDeviceOs == "-" ? string.Empty : lastDeviceOs,
                    failMessage = pass ? null : failMessage,
                    steps = stepRecords
                };
                TestHistoryStore.Append(Globals.LogFolder, record, 1000);
                currentRunStartedAt = DateTime.MinValue;
            }
            catch { /* 이력 기록 실패는 봇 실행 자체에 영향 주지 않도록 무시 */ }
        }

        private void RecordSkippedHistory(string scenario, int totalSteps, string reason, string? batchId)
        {
            try
            {
                DateTime now = DateTime.Now;
                TestHistoryStore.Append(Globals.LogFolder, new TestRunRecord
                {
                    runId = Guid.NewGuid().ToString("N"),
                    batchId = batchId,
                    scenario = scenario,
                    startedAt = now.ToString("o"),
                    timestamp = now.ToString("o"),
                    totalSteps = totalSteps,
                    pass = false,
                    status = "SKIPPED",
                    durationMs = 0,
                    deviceSerial = AdbEngine.SelectedSerial ?? string.Empty,
                    deviceModel = lastDeviceModel == "-" ? string.Empty : lastDeviceModel,
                    osVersion = lastDeviceOs == "-" ? string.Empty : lastDeviceOs,
                    failMessage = reason
                }, 1000);
            }
            catch { }
        }

        private List<TestStepRecord> LoadCurrentStepResults()
        {
            string path = Path.Combine(SysPath, "step_results.jsonl");
            var records = new List<TestStepRecord>();
            try
            {
                if (!File.Exists(path)) return records;
                foreach (string line in File.ReadLines(path, Encoding.UTF8))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        TestStepRecord? step = JsonSerializer.Deserialize<TestStepRecord>(line, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (step != null) records.Add(step);
                    }
                    catch { }
                }
            }
            catch { }
            return records;
        }

        private List<TestRunRecord> LoadTestHistory() => TestHistoryStore.Load(Globals.LogFolder);

        private void EnqueueLogLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            ArchiveLogLine(line);
            logQueue.Enqueue(line);
            int count = System.Threading.Interlocked.Increment(ref pendingLogLineCount);
            while (count > MaxPendingLogLines && logQueue.TryDequeue(out _))
            {
                count = System.Threading.Interlocked.Decrement(ref pendingLogLineCount);
            }
        }

        private void TrimConsoleBuffer()
        {
            if (txtConsole == null || txtConsole.IsDisposed) return;
            int lineCount = txtConsole.Lines.Length;
            if (lineCount <= MaxConsoleLines) return;
            int removeLines = Math.Max(1000, lineCount - MaxConsoleLines);
            int charIndex = txtConsole.GetFirstCharIndexFromLine(removeLines);
            if (charIndex <= 0) return;
            txtConsole.Select(0, charIndex);
            txtConsole.SelectedText = string.Empty;
            txtConsole.SelectionStart = txtConsole.TextLength;
        }

        private async void CaptureScreen()
        {
            if (!await System.Threading.Tasks.Task.Run(AdbEngine.IsDeviceConnected))
            {
                MessageBox.Show("기기가 연결되지 않았습니다.");
                return;
            }

            string fileName = $"Shot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string localPath = Path.Combine(Globals.LogFolder, fileName);
            lblStatusMsg.Text = "상태: 화면 캡처 중...";

            try
            {
                string result = await System.Threading.Tasks.Task.Run(() =>
                {
                    string remotePath = "/sdcard/" + fileName;
                    string output = AdbEngine.RunCommand($"shell screencap -p {remotePath}", 10000);
                    output += AdbEngine.RunCommand($"pull {remotePath} \"{localPath}\"", 30000);
                    AdbEngine.RunCommand($"shell rm {remotePath}", 5000);
                    return output;
                });

                if (!File.Exists(localPath))
                {
                    string detail = string.IsNullOrWhiteSpace(result)
                        ? "캡처 파일이 생성되지 않았습니다."
                        : result.Trim();
                    throw new InvalidOperationException(detail);
                }

                lblStatusMsg.Text = "상태: 화면 캡처 완료 · " + fileName;
            }
            catch (Exception ex)
            {
                lblStatusMsg.Text = "상태: 화면 캡처 실패";
                MessageBox.Show("화면 캡처에 실패했습니다.\n" + ex.Message, "캡처 실패");
            }
        }

        private async void DumpSystem()
        {
            if (!await System.Threading.Tasks.Task.Run(AdbEngine.IsDeviceConnected))
            {
                MessageBox.Show("기기가 연결되지 않았습니다.");
                return;
            }

            string fileName = $"SysDump_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
            string outputPath = Path.Combine(Globals.LogFolder, fileName);
            lblStatusMsg.Text = "상태: 시스템 덤프 수집 중... 오래 걸릴 수 있습니다.";

            try
            {
                string result = await System.Threading.Tasks.Task.Run(() =>
                    AdbEngine.RunCommand($"bugreport \"{outputPath}\"", 180000));

                if (!File.Exists(outputPath) && result.Contains("Error", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(result.Trim());

                lblStatusMsg.Text = "상태: 시스템 덤프 수집 완료 · " + fileName;
            }
            catch (Exception ex)
            {
                lblStatusMsg.Text = "상태: 시스템 덤프 수집 실패";
                MessageBox.Show("시스템 덤프 수집에 실패했습니다.\n" + ex.Message, "수집 실패");
            }
        }

        private string GetText(TextBox txt) => txt.Text == (txt.Tag?.ToString() ?? "") ? "" : txt.Text;

        // 파이썬이 쓰기 잠금을 걸어둔 상태에서도 안전하게 읽을 수 있게 공유 모드로 파일을 연다
        private static string ReadFileShared(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs, Encoding.UTF8);
            return reader.ReadToEnd();
        }
    }
}