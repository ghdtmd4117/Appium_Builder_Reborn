using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using AppiumBuilder.Core;
using AppiumBuilder.UI;
using AppiumBuilder.Utils;

namespace AppiumBuilder
{
    public partial class MainForm
    {
        private void SetupLogTab()
        {
            pnlTabLog = new DoubleBufferedPanel
            {
                Dock = DockStyle.Fill,
                Visible = false,
                BackColor = Globals.Bg,
                Padding = new Padding(20, 14, 20, 14),
                AutoScroll = true
            };

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Globals.Bg,
                ColumnCount = 1,
                RowCount = 7,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            root.ColumnStyles.Add(ColPct(100));
            root.RowStyles.Add(Abs(88));
            root.RowStyles.Add(Abs(122));
            root.RowStyles.Add(Abs(62));
            root.RowStyles.Add(Abs(94));
            root.RowStyles.Add(Abs(52));
            root.RowStyles.Add(Pct(100));
            root.RowStyles.Add(Abs(52));
            root.Controls.Add(CreatePageHeader(
                "로그 / 미디어",
                "Android logcat을 실시간으로 확인하고, 화면 녹화와 함께 로그를 수집할 수 있습니다."), 0, 0);

            var deviceCard = CreateCardDock();
            var deviceGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                Padding = new Padding(16, 12, 16, 12),
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            deviceGrid.ColumnStyles.Add(ColPct(40));
            deviceGrid.ColumnStyles.Add(ColPct(19));
            deviceGrid.ColumnStyles.Add(ColPct(19));
            deviceGrid.ColumnStyles.Add(ColPct(22));
            var logDeviceSummary = CreateLogDeviceSummary();
            var logCollectionSummary = CreateCompactSummary("로그 수집", "대기", Globals.Info, out lblLogCollectionState);
            var logRecordingSummary = CreateCompactSummary("화면 녹화", "대기", Globals.Warning, out lblLogRecordingState);
            var logSavedSummary = CreateCompactSummary("최근 저장", "없음", Globals.Accent, out lblLogSavedFile);
            Control[] deviceSummaries = { logDeviceSummary, logCollectionSummary, logRecordingSummary, logSavedSummary };
            deviceGrid.Controls.Add(logDeviceSummary, 0, 0);
            deviceGrid.Controls.Add(logCollectionSummary, 1, 0);
            deviceGrid.Controls.Add(logRecordingSummary, 2, 0);
            deviceGrid.Controls.Add(logSavedSummary, 3, 0);
            lblLogSavedFile.Cursor = Cursors.Hand;
            lblLogSavedFile.Click += (_, _) => OpenLogFolder();
            deviceCard.Controls.Add(deviceGrid);
            root.Controls.Add(deviceCard, 0, 1);

            var actionGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                Margin = new Padding(6, 4, 6, 4),
                Padding = new Padding(0),
                BackColor = Globals.Bg
            };
            for (int i = 0; i < 4; i++) actionGrid.ColumnStyles.Add(ColPct(25));
            var btnLog = CreateModernButton("실시간 로그 시작", Globals.Accent, 0, 0, 180, 42, "terminal");
            var btnLogRec = CreateModernButton("로그 + 화면 녹화", Globals.SurfaceAlt, 0, 0, 190, 42, "record");
            var btnClear = CreateModernButton("터미널 비우기", Globals.SurfaceAlt, 0, 0, 150, 42, "trash");
            var btnLogSave = CreateModernButton("로그 PC 저장", Globals.SurfaceAlt, 0, 0, 154, 42, "save");
            foreach (var button in new[] { btnLog, btnLogRec, btnClear, btnLogSave })
            {
                button.Dock = DockStyle.Fill;
                button.Margin = new Padding(0, 8, 10, 8);
                button.TextAlign = ContentAlignment.MiddleCenter;
            }
            btnLogSave.Margin = new Padding(0, 8, 0, 8);
            Control[] logActions = { btnLog, btnLogRec, btnClear, btnLogSave };
            actionGrid.Controls.Add(btnLog, 0, 0);
            actionGrid.Controls.Add(btnLogRec, 1, 0);
            actionGrid.Controls.Add(btnClear, 2, 0);
            actionGrid.Controls.Add(btnLogSave, 3, 0);
            root.Controls.Add(actionGrid, 0, 2);

            var stateGrid = EqualColumnGrid(4);
            stateGrid.Controls.Add(CreateLogStateCard("실시간 수집", "준비", Globals.Success, "수집 시간", out lblLogCaptureMetric), 0, 0);
            stateGrid.Controls.Add(CreateLogStateCard("화면 녹화", "준비", Globals.Info, "8 Mbps · 30fps", out lblLogRecordingMetric), 1, 0);
            stateGrid.Controls.Add(CreateLogStateCard("로그 라인 수", "0", Globals.Accent, "현재 세션", out lblLogLineCount), 2, 0);
            stateGrid.Controls.Add(CreateLogStateCard("저장 위치", Globals.LogFolder, Globals.Accent, "로그/미디어", out lblLogSavePathMetric), 3, 0);
            Control[] logStateCards = Enumerable.Range(0, 4).Select(i => stateGrid.GetControlFromPosition(i, 0)!).ToArray();
            root.Controls.Add(stateGrid, 0, 3);

            var consoleHeader = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(6, 4, 6, 4),
                FillColor = Globals.Surface,
                BorderColor = Globals.Border,
                BorderThickness = 1,
                BorderRadius = Globals.RadiusSm
            };
            var headerGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                Padding = new Padding(14, 6, 10, 6),
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            headerGrid.ColumnStyles.Add(ColPct(100));
            headerGrid.ColumnStyles.Add(ColAbs(150));
            headerGrid.ColumnStyles.Add(ColAbs(210));
            headerGrid.ColumnStyles.Add(ColAbs(112));
            headerGrid.Controls.Add(new Label
            {
                Text = "실시간 로그 (logcat -v time)",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Globals.FontSub,
                ForeColor = Globals.TextPrimary
            }, 0, 0);
            cmbLogLevelFilter = CreateFlatCombo(0, 0, 140, 32);
            cmbLogLevelFilter.Dock = DockStyle.Fill;
            cmbLogLevelFilter.Margin = new Padding(4, 0, 6, 0);
            cmbLogLevelFilter.Items.AddRange(new object[] { "전체", "V", "D", "I", "W", "E", "F" });
            cmbLogLevelFilter.SelectedIndex = 0;
            headerGrid.Controls.Add(cmbLogLevelFilter, 1, 0);
            txtLogSearch = CreatePlaceholderTextBoxDock("로그 검색");
            txtLogSearch.Margin = new Padding(4, 1, 6, 1);
            headerGrid.Controls.Add(txtLogSearch, 2, 0);
            btnLogPause = CreateModernButton("일시 중지", Globals.Surface, 0, 0, 108, 32, "pause");
            btnLogPause.Dock = DockStyle.Fill;
            btnLogPause.Margin = new Padding(4, 0, 0, 0);
            btnLogPause.ForeColor = Globals.Accent;
            btnLogPause.IconColor = Globals.Accent;
            btnLogPause.BorderColor = Globals.Border;
            btnLogPause.BorderThickness = 1;
            btnLogPause.TextAlign = ContentAlignment.MiddleCenter;
            headerGrid.Controls.Add(btnLogPause, 3, 0);
            consoleHeader.Controls.Add(headerGrid);
            root.Controls.Add(consoleHeader, 0, 4);

            cmbLogLevelFilter.SelectedIndexChanged += (_, _) => RebuildLogConsole();
            txtLogSearch.TextChanged += (_, _) =>
            {
                if (txtLogSearch.Text == txtLogSearch.Tag?.ToString()) return;
                RebuildLogConsole();
            };
            btnLogPause.Click += (_, _) =>
            {
                isLogPaused = !isLogPaused;
                btnLogPause.Text = isLogPaused ? "계속 보기" : "일시 중지";
                btnLogPause.IconName = isLogPaused ? "play" : "pause";
                btnLogPause.FillColor = isLogPaused ? Globals.AccentSoft : Globals.Surface;
                if (!isLogPaused) DrainLogQueueToConsole();
                btnLogPause.Invalidate();
            };

            var consoleCard = CreateCardDock(Globals.ConsoleBg);
            consoleCard.BorderColor = Globals.ConsoleLine;
            txtConsole = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Globals.ConsoleBg,
                ForeColor = Globals.TextSecondary,
                Font = Globals.FontMono,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                DetectUrls = false,
                WordWrap = false,
                TabStop = true,
                Text = "로그 수집을 시작하면 Android logcat이 이 영역에 표시됩니다.\n"
            };
            var consolePad = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(14, 12, 14, 12),
                BackColor = Globals.ConsoleBg
            };
            consolePad.Controls.Add(txtConsole);
            consoleCard.Controls.Add(consolePad);
            root.Controls.Add(consoleCard, 0, 5);

            var summaryCard = CreateCardDock();
            var summaryGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 7,
                RowCount = 1,
                Padding = new Padding(12, 0, 12, 0),
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            summaryGrid.ColumnStyles.Add(ColPct(100));
            for (int i = 1; i < 7; i++) summaryGrid.ColumnStyles.Add(ColAbs(58));
            summaryGrid.Controls.Add(new Label
            {
                Text = "로그 레벨 요약",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Globals.FontSub,
                ForeColor = Globals.TextSecondary
            }, 0, 0);
            summaryGrid.Controls.Add(CreateLevelChip("V", Globals.TextMuted), 1, 0);
            summaryGrid.Controls.Add(CreateLevelChip("D", Globals.Info), 2, 0);
            summaryGrid.Controls.Add(CreateLevelChip("I", Globals.Success), 3, 0);
            summaryGrid.Controls.Add(CreateLevelChip("W", Globals.Warning), 4, 0);
            summaryGrid.Controls.Add(CreateLevelChip("E", Globals.Danger), 5, 0);
            summaryGrid.Controls.Add(CreateLevelChip("F", Globals.Danger), 6, 0);
            summaryCard.Controls.Add(summaryGrid);
            root.Controls.Add(summaryCard, 0, 6);

            void ApplyLogResponsiveLayout()
            {
                int available = Math.Max(1, pnlTabLog.ClientSize.Width - pnlTabLog.Padding.Horizontal);
                bool compact = available < Ui(980);

                root.SuspendLayout();
                if (!compact)
                {
                    SetResponsivePageMode(pnlTabLog, root, false, 0);
                    ReflowWeightedGrid(deviceGrid, deviceSummaries, 40, 19, 19, 22);
                    ReflowEqualGrid(actionGrid, 4, logActions);
                    ReflowEqualGrid(stateGrid, 4, logStateCards);
                    SetAbsoluteRow(root, 0, 88);
                    SetAbsoluteRow(root, 1, 122);
                    SetAbsoluteRow(root, 2, 62);
                    SetAbsoluteRow(root, 3, 94);
                    SetAbsoluteRow(root, 4, 52);
                    SetPercentRow(root, 5);
                    SetAbsoluteRow(root, 6, 52);
                }
                else
                {
                    SetResponsivePageMode(pnlTabLog, root, true, 1120);
                    ReflowEqualGrid(deviceGrid, 2, deviceSummaries);
                    ReflowEqualGrid(actionGrid, 2, logActions);
                    ReflowEqualGrid(stateGrid, 2, logStateCards);
                    SetAbsoluteRow(root, 0, 88);
                    SetAbsoluteRow(root, 1, 220);
                    SetAbsoluteRow(root, 2, 120);
                    SetAbsoluteRow(root, 3, 196);
                    SetAbsoluteRow(root, 4, 58);
                    SetAbsoluteRow(root, 5, 380);
                    SetAbsoluteRow(root, 6, 58);
                }
                root.ResumeLayout(true);
            }

            pnlTabLog.Resize += (_, _) => ApplyLogResponsiveLayout();
            pnlTabLog.Controls.Add(root);
            pnlContent.Controls.Add(pnlTabLog);
            ApplyLogResponsiveLayout();

            btnLog.Click += async (_, _) =>
            {
                if (!isLogging)
                {
                    if (!await System.Threading.Tasks.Task.Run(AdbEngine.IsDeviceConnected))
                    {
                        MessageBox.Show("기기가 연결되지 않았습니다.");
                        return;
                    }

                    await AdbEngine.RunCommandAsync("logcat -c", 5000);
                    txtConsole.Clear();
                    while (logArchive.TryDequeue(out _)) { }
                    archivedLogLineCount = 0;
                    logSessionStartedAt = DateTime.Now;
                    isLogPaused = false;
                    if (btnLogPause != null) { btnLogPause.Text = "일시 중지"; btnLogPause.IconName = "pause"; btnLogPause.FillColor = Globals.Surface; }
                    logProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo("adb", AdbEngine.BuildAdbArguments("logcat -v time"))
                        {
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        },
                        EnableRaisingEvents = true
                    };
                    logProcess.OutputDataReceived += (_, args) =>
                    {
                        if (!string.IsNullOrEmpty(args.Data)) EnqueueLogLine(args.Data);
                    };
                    logProcess.ErrorDataReceived += (_, args) =>
                    {
                        if (!string.IsNullOrEmpty(args.Data)) EnqueueLogLine("[adb] " + args.Data);
                    };
                    logProcess.Exited += (_, _) =>
                    {
                        if (!isLogging || IsDisposed) return;
                        BeginInvoke(new Action(() =>
                        {
                            isLogging = false;
                            logSessionStartedAt = DateTime.MinValue;
                            RefreshLogMetaLabels();
                            consoleTimer.Stop();
                            btnLog.Text = "실시간 로그 시작";
                            btnLog.IconName = "terminal";
                            btnLog.FillColor = Globals.Accent;
                            btnLog.HoverColor = Globals.AccentHover;
                            lblLogCollectionState.Text = "중단됨";
                            lblLogCollectionState.ForeColor = Globals.Danger;
                            lblStatusMsg.Text = "상태: ADB logcat 프로세스가 종료되었습니다.";
                            AdbEngine.TryKill(logProcess);
                            logProcess = null;
                            btnLog.Invalidate();
                        }));
                    };
                    isLogging = true; // 즉시 종료되는 프로세스도 Exited에서 감지하도록 시작 전에 설정
                    try
                    {
                        logProcess.Start();
                        logProcess.BeginOutputReadLine();
                        logProcess.BeginErrorReadLine();
                        consoleTimer.Start();
                    }
                    catch (Exception ex)
                    {
                        isLogging = false;
                        AdbEngine.TryKill(logProcess);
                        logProcess = null;
                        MessageBox.Show("ADB logcat을 시작하지 못했습니다.\n" + ex.Message, "로그 시작 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    btnLog.Text = "로그 수집 정지";
                    btnLog.IconName = "stop";
                    btnLog.FillColor = Globals.Danger;
                    btnLog.HoverColor = Lighten(Globals.Danger, 0.10f);
                    lblLogCollectionState.Text = "수집 중";
                    lblLogCollectionState.ForeColor = Globals.Success;
                    lblStatusMsg.Text = "상태: 실시간 로그 수집 중";
                    btnLog.Invalidate();
                }
                else
                {
                    isLogging = false; // 정상 종료임을 Exited 핸들러보다 먼저 표시
                    logSessionStartedAt = DateTime.MinValue;
                    RefreshLogMetaLabels();
                    AdbEngine.TryKill(logProcess);
                    logProcess = null;
                    consoleTimer.Stop();

                    btnLog.Text = "실시간 로그 시작";
                    btnLog.IconName = "terminal";
                    btnLog.FillColor = Globals.Accent;
                    btnLog.HoverColor = Globals.AccentHover;
                    lblLogCollectionState.Text = "대기";
                    lblLogCollectionState.ForeColor = Globals.TextSecondary;
                    lblStatusMsg.Text = "상태: 로그 수집 정지";
                    btnLog.Invalidate();
                }
            };

            btnLogRec.Click += async (_, _) =>
            {
                if (!isRecording)
                {
                    if (isSoloRecording)
                    {
                        MessageBox.Show("화면 단독 녹화를 먼저 중지해주세요.");
                        return;
                    }
                    if (botRecordingProcess != null)
                    {
                        MessageBox.Show("Appium 봇 실행 영상 녹화를 먼저 중지해주세요.");
                        return;
                    }
                    if (!await System.Threading.Tasks.Task.Run(AdbEngine.IsDeviceConnected))
                    {
                        MessageBox.Show("기기가 연결되지 않았습니다.");
                        return;
                    }

                    currentCombinedRecordingName = $"ScreenRecord_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
                    try
                    {
                        recProcess = AdbEngine.StartAdbProcess(
                            $"shell screenrecord --bit-rate 4000000 /sdcard/{currentCombinedRecordingName}");
                    }
                    catch (Exception ex)
                    {
                        currentCombinedRecordingName = null;
                        MessageBox.Show(
                            "화면 녹화를 시작하지 못했습니다.\n" + ex.Message,
                            "녹화 시작 실패",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        return;
                    }

                    if (recProcess == null)
                    {
                        currentCombinedRecordingName = null;
                        MessageBox.Show("화면 녹화 프로세스를 시작하지 못했습니다.");
                        return;
                    }

                    isRecording = true; // StartAdbProcess 직후 종료되는 경우도 Exited에서 잡기 위해 먼저 설정
                    logRecordingStartedAt = DateTime.Now;
                    RefreshLogMetaLabels();
                    recProcess.Exited += (_, _) =>
                    {
                        if (!isRecording || IsDisposed) return;
                        BeginInvoke(new Action(async () =>
                        {
                            if (!isRecording) return;
                            isRecording = false;
                            logRecordingStartedAt = DateTime.MinValue;
                            RefreshLogMetaLabels();
                            string? completedName = currentCombinedRecordingName;
                            currentCombinedRecordingName = null;
                            bool stopLog = loggingStartedForRecording;
                            loggingStartedForRecording = false;

                            bool saved = false;
                            if (!string.IsNullOrWhiteSpace(completedName))
                            {
                                await AdbEngine.RunCommandAsync($"pull /sdcard/{completedName} \"{Globals.LogFolder}\"", 30000);
                                await AdbEngine.RunCommandAsync($"shell rm /sdcard/{completedName}", 5000);
                                saved = File.Exists(Path.Combine(Globals.LogFolder, completedName));
                                if (saved) lblLogSavedFile.Text = completedName;
                            }
                            if (stopLog && isLogging) btnLog.PerformClick();
                            AdbEngine.TryKill(recProcess);
                            recProcess = null;

                            lblLogRecordingState.Text = saved ? "자동 종료" : "중단됨";
                            lblLogRecordingState.ForeColor = saved ? Globals.Warning : Globals.Danger;
                            btnLogRec.Text = "로그 + 화면 녹화";
                            btnLogRec.IconName = "record";
                            btnLogRec.FillColor = Globals.SurfaceAlt;
                            btnLogRec.HoverColor = Globals.SurfaceRaised;
                            lblStatusMsg.Text = saved
                                ? "상태: 화면 녹화 프로세스가 종료되어 파일을 자동 저장했습니다."
                                : "상태: 화면 녹화 프로세스가 종료되었습니다. 저장 파일을 확인해주세요.";
                            btnLogRec.Invalidate();
                        }));
                    };
                    recProcess.EnableRaisingEvents = true;
                    loggingStartedForRecording = !isLogging;
                    if (loggingStartedForRecording) btnLog.PerformClick();

                    btnLogRec.Text = "화면 녹화 정지";
                    btnLogRec.IconName = "stop";
                    btnLogRec.FillColor = Globals.Danger;
                    btnLogRec.HoverColor = Lighten(Globals.Danger, 0.10f);
                    lblLogRecordingState.Text = "녹화 중";
                    lblLogRecordingState.ForeColor = Globals.Warning;
                    lblStatusMsg.Text = "상태: 로그와 화면을 함께 녹화 중";
                    btnLogRec.Invalidate();
                }
                else
                {
                    btnLogRec.Enabled = false;
                    isRecording = false; // Exited 이벤트가 정상 정지를 오류로 오인하지 않도록 먼저 해제
                    try
                    {
                        await AdbEngine.RunCommandAsync("shell pkill -INT screenrecord", 5000);
                        await System.Threading.Tasks.Task.Delay(2000);
                        if (!string.IsNullOrWhiteSpace(currentCombinedRecordingName))
                        {
                            string completedName = currentCombinedRecordingName;
                            await AdbEngine.RunCommandAsync($"pull /sdcard/{completedName} \"{Globals.LogFolder}\"", 30000);
                            await AdbEngine.RunCommandAsync($"shell rm /sdcard/{completedName}", 5000);
                            lblLogSavedFile.Text = completedName;
                        }

                        if (loggingStartedForRecording && isLogging) btnLog.PerformClick();
                        loggingStartedForRecording = false;
                        isRecording = false;
                        logRecordingStartedAt = DateTime.MinValue;
                        RefreshLogMetaLabels();
                        btnLogRec.Text = "로그 + 화면 녹화";
                        btnLogRec.IconName = "record";
                        btnLogRec.FillColor = Globals.SurfaceAlt;
                        btnLogRec.HoverColor = Globals.SurfaceRaised;
                        lblLogRecordingState.Text = "대기";
                        lblLogRecordingState.ForeColor = Globals.TextSecondary;
                        lblStatusMsg.Text = "상태: 화면 녹화 완료";
                        btnLogRec.Invalidate();
                    }
                    finally
                    {
                        AdbEngine.TryKill(recProcess);
                        recProcess = null;
                        currentCombinedRecordingName = null;
                        btnLogRec.Enabled = true;
                    }
                }
            };

            btnClear.Click += (_, _) =>
            {
                while (logQueue.TryDequeue(out _)) System.Threading.Interlocked.Decrement(ref pendingLogLineCount);
                while (logArchive.TryDequeue(out _)) { }
                archivedLogLineCount = 0;
                txtConsole.Clear();
                lblLogLineCount.Text = "0";
            };

            btnLogSave.Click += (_, _) =>
            {
                try
                {
                    string fileName = $"Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                    string path = Path.Combine(Globals.LogFolder, fileName);
                    string allLogText = string.Join(Environment.NewLine, logArchive.ToArray());
                    File.WriteAllText(path, allLogText);
                    lblLogSavedFile.Text = fileName;
                    lblStatusMsg.Text = "상태: 로그 저장 완료";
                    MessageBox.Show("로그를 저장했습니다.\n" + path, "저장 완료");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("로그 저장에 실패했습니다.\n" + ex.Message, "저장 실패");
                }
            };
        }

        private Panel CreateLogDeviceSummary()
        {
            var host = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                Margin = new Padding(0),
                Padding = new Padding(4, 0, 10, 0),
                BackColor = Color.Transparent
            };

            host.ColumnStyles.Add(ColAbs(42));
            host.ColumnStyles.Add(ColPct(100));

            // 위쪽 여백 / 실제 콘텐츠 / 남은 아래 여백
            host.RowStyles.Add(Abs(6));
            host.RowStyles.Add(Abs(40));
            host.RowStyles.Add(Pct(100));

            var phoneIcon = new IconGlyph
            {
                IconName = "phone",
                IconColor = Globals.Accent,

                // Fill을 사용하지 않고 크기를 직접 고정한다.
                Size = new Size(18, 22),
                Anchor = AnchorStyles.Left,
                Margin = new Padding(8, 0, 0, 0)
            };

            host.Controls.Add(phoneIcon, 0, 1);

            var textGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = Color.Transparent
            };

            textGrid.ColumnStyles.Add(ColPct(100));
            textGrid.RowStyles.Add(Abs(22));
            textGrid.RowStyles.Add(Abs(18));

            lblLogDevice = new Label
            {
                Text = "연결된 디바이스 없음",
                Dock = DockStyle.Fill,
                Font = Globals.FontSub,
                ForeColor = Globals.TextPrimary,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var sessionLabel = new Label
            {
                Text = "ADB 디바이스 / 현재 세션",
                Dock = DockStyle.Fill,
                Font = Globals.FontMuted,
                ForeColor = Globals.TextMuted,
                AutoEllipsis = false,
                TextAlign = ContentAlignment.MiddleLeft
            };

            textGrid.Controls.Add(lblLogDevice, 0, 0);
            textGrid.Controls.Add(sessionLabel, 0, 1);

            host.Controls.Add(textGrid, 1, 1);

            return host;
        }

        private Panel CreateCompactSummary(string caption, string value, Color accent, out Label valueLabel)
        {
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Margin = new Padding(0),
                Padding = new Padding(10, 0, 8, 0),
                BackColor = Color.Transparent
            };
            grid.ColumnStyles.Add(ColPct(100));
            grid.RowStyles.Add(Pct(50));
            grid.RowStyles.Add(Abs(22));
            grid.RowStyles.Add(Abs(28));
            grid.RowStyles.Add(Pct(50));
            grid.Controls.Add(new Label
            {
                Text = caption,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Globals.FontMuted,
                ForeColor = Globals.TextMuted,
                AutoEllipsis = false
            }, 0, 1);

            var valueRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            valueRow.ColumnStyles.Add(ColAbs(16));
            valueRow.ColumnStyles.Add(ColPct(100));
            var dot = Dot(accent, 7);
            dot.Anchor = AnchorStyles.None;
            dot.Margin = new Padding(0);
            valueLabel = new Label
            {
                Text = value,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Font = Globals.FontSub,
                ForeColor = Globals.TextSecondary
            };
            valueRow.Controls.Add(dot, 0, 0);
            valueRow.Controls.Add(valueLabel, 1, 0);
            grid.Controls.Add(valueRow, 0, 2);
            return grid;
        }

        private RoundedPanel CreateLogStateCard(string title, string value, Color accent, string caption)
        {
            return CreateLogStateCard(title, value, accent, caption, out _);
        }

        private RoundedPanel CreateLogStateCard(string title, string value, Color accent, string caption, out Label valueLabel)
        {
            var card = CreateCardDock();
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(14, 10, 14, 10),
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            grid.ColumnStyles.Add(ColPct(100));
            grid.RowStyles.Add(Abs(24));
            grid.RowStyles.Add(Abs(34));

            var titleRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            titleRow.ColumnStyles.Add(ColAbs(16));
            titleRow.ColumnStyles.Add(ColPct(100));
            var dot = Dot(accent, 7);
            dot.Anchor = AnchorStyles.None;
            dot.Margin = new Padding(0);
            titleRow.Controls.Add(dot, 0, 0);
            titleRow.Controls.Add(new Label
            {
                Text = title,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Globals.FontMuted,
                ForeColor = Globals.TextMuted,
                AutoEllipsis = false
            }, 1, 0);

            var valueRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            valueRow.ColumnStyles.Add(ColPct(100));
            valueRow.ColumnStyles.Add(ColAbs(92));
            valueLabel = new Label
            {
                Text = value,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Font = Globals.FontSub,
                ForeColor = Globals.TextPrimary
            };
            valueRow.Controls.Add(valueLabel, 0, 0);
            valueRow.Controls.Add(new Label
            {
                Text = caption,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                AutoEllipsis = false,
                Font = Globals.FontMuted,
                ForeColor = Globals.TextFaint
            }, 1, 0);
            grid.Controls.Add(titleRow, 0, 0);
            grid.Controls.Add(valueRow, 0, 1);
            card.Controls.Add(grid);
            return card;
        }

        private RoundedPanel CreateLevelChip(string level, Color color)
        {
            var chip = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(4, 7, 4, 7),
                FillColor = Globals.SurfaceAlt,
                BorderColor = Globals.Border,
                BorderThickness = 1,
                BorderRadius = Globals.RadiusXs
            };
            chip.Controls.Add(new Label
            {
                Text = level,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = Globals.FontSub,
                ForeColor = color
            });
            return chip;
        }
    }
}
