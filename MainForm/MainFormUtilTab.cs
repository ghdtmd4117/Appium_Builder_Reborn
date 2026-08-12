using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using AppiumBuilder.Core;
using AppiumBuilder.UI;
using AppiumBuilder.Utils;

namespace AppiumBuilder
{
    public partial class MainForm
    {
        private void SetupUtilTab()
        {
            pnlTabUtil = new DoubleBufferedPanel
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
            root.RowStyles.Add(Abs(42));
            root.RowStyles.Add(Abs(164));
            root.RowStyles.Add(Abs(46));
            root.RowStyles.Add(Abs(164));
            root.RowStyles.Add(Abs(72));
            root.RowStyles.Add(Pct(100));
            root.Controls.Add(CreatePageHeader(
                "유틸리티",
                "자주 사용하는 캡처, 녹화와 진단 도구를 빠르게 실행합니다."), 0, 0);

            root.Controls.Add(CreateUtilitySectionLabel("미디어 도구", "화면 캡처와 녹화"), 0, 1);
            var mediaGrid = EqualColumnGrid(2);
            var screenshotCard = CreateUtilityActionCard(
                "camera",
                "현재 화면 스크린샷",
                "현재 Android 화면을 PNG로 저장합니다.",
                "PC 저장",
                out var btnShot);
            var recordCard = CreateUtilityActionCard(
                "record",
                "화면 단독 녹화",
                "터치 표시와 함께 화면을 MP4로 녹화합니다.",
                "녹화 시작",
                out var btnSoloRec);
            Control[] mediaCards = { screenshotCard, recordCard };
            mediaGrid.Controls.Add(screenshotCard, 0, 0);
            mediaGrid.Controls.Add(recordCard, 1, 0);
            root.Controls.Add(mediaGrid, 0, 2);

            root.Controls.Add(CreateUtilitySectionLabel("시스템 유틸리티", "진단 정보와 작업 폴더"), 0, 3);
            var systemGrid = EqualColumnGrid(2);
            var dumpCard = CreateUtilityActionCard(
                "dump",
                "시스템 덤프 수집",
                "Android bugreport ZIP을 수집합니다.",
                "수집 시작",
                out var btnDump);
            var folderCard = CreateUtilityActionCard(
                "folder",
                "로그 저장 폴더 열기",
                "로그·캡처·녹화·자동화 결과 폴더를 엽니다.",
                "폴더 열기",
                out var btnFolder);
            Control[] systemCards = { dumpCard, folderCard };
            systemGrid.Controls.Add(dumpCard, 0, 0);
            systemGrid.Controls.Add(folderCard, 1, 0);
            root.Controls.Add(systemGrid, 0, 4);

            var tipCard = CreateCardDock(Globals.InfoSoft);
            tipCard.BorderColor = Globals.BorderStrong;
            var tipGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                Padding = new Padding(14, 0, 14, 0),
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            tipGrid.ColumnStyles.Add(ColAbs(30));
            tipGrid.ColumnStyles.Add(ColAbs(240));
            tipGrid.ColumnStyles.Add(ColPct(100));
            tipGrid.ColumnStyles.Add(ColAbs(132));
            tipGrid.Controls.Add(new IconGlyph
            {
                IconName = "info",
                IconColor = Globals.Info,
                Dock = DockStyle.Fill,
                Margin = new Padding(3, 10, 9, 10)
            }, 0, 0);
            tipGrid.Controls.Add(new Label
            {
                Text = "ADB 연결이 필요한 도구입니다",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Globals.FontSub,
                ForeColor = Globals.TextPrimary,
                AutoEllipsis = false
            }, 1, 0);
            tipGrid.Controls.Add(new Label
            {
                Text = "실행 전 사이드바 연결 상태를 확인하세요. 결과물은 Desktop/ADB_Logs에 저장됩니다.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Globals.FontMuted,
                ForeColor = Globals.TextSecondary,
                AutoEllipsis = false
            }, 2, 0);
            var btnHealth = CreateModernButton("환경 진단", Globals.SurfaceAlt, 0, 0, 116, 36, "tools");
            btnHealth.Dock = DockStyle.Fill;
            btnHealth.Margin = new Padding(8, 12, 0, 12);
            btnHealth.TextAlign = ContentAlignment.MiddleCenter;
            btnHealth.Click += (_, _) =>
            {
                using var form = new EnvironmentDiagnosticsForm();
                form.ShowDialog(this);
            };
            tipGrid.Controls.Add(btnHealth, 3, 0);
            tipCard.Controls.Add(tipGrid);
            root.Controls.Add(tipCard, 0, 5);

            void ApplyUtilityResponsiveLayout()
            {
                int available = Math.Max(1, pnlTabUtil.ClientSize.Width - pnlTabUtil.Padding.Horizontal);
                bool compact = available < Ui(980);

                root.SuspendLayout();
                if (!compact)
                {
                    SetResponsivePageMode(pnlTabUtil, root, false, 0);
                    ReflowEqualGrid(mediaGrid, 2, mediaCards);
                    ReflowEqualGrid(systemGrid, 2, systemCards);
                    SetAbsoluteRow(root, 0, 88);
                    SetAbsoluteRow(root, 1, 42);
                    SetAbsoluteRow(root, 2, 164);
                    SetAbsoluteRow(root, 3, 46);
                    SetAbsoluteRow(root, 4, 164);
                    SetAbsoluteRow(root, 5, 72);
                    SetPercentRow(root, 6);
                }
                else
                {
                    SetResponsivePageMode(pnlTabUtil, root, true, 980);
                    ReflowEqualGrid(mediaGrid, 1, mediaCards);
                    ReflowEqualGrid(systemGrid, 1, systemCards);
                    SetAbsoluteRow(root, 0, 88);
                    SetAbsoluteRow(root, 1, 42);
                    SetAbsoluteRow(root, 2, 336);
                    SetAbsoluteRow(root, 3, 46);
                    SetAbsoluteRow(root, 4, 336);
                    SetAbsoluteRow(root, 5, 96);
                    SetAbsoluteRow(root, 6, 20);
                }
                root.ResumeLayout(true);
            }

            pnlTabUtil.Resize += (_, _) => ApplyUtilityResponsiveLayout();
            pnlTabUtil.Controls.Add(root);
            pnlContent.Controls.Add(pnlTabUtil);
            ApplyUtilityResponsiveLayout();

            btnShot.Click += (_, _) => CaptureScreen();
            btnDump.Click += (_, _) => DumpSystem();

            btnFolder.Click += (_, _) => OpenLogFolder();

            btnSoloRec.Click += async (_, _) =>
            {
                if (!isSoloRecording)
                {
                    if (isRecording)
                    {
                        MessageBox.Show("로그 + 화면 녹화를 먼저 중지해주세요.");
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

                    await AdbEngine.RunCommandAsync("shell settings put system show_touches 1", 5000);
                    currentSoloRecordingName = $"SoloRecord_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
                    try
                    {
                        soloRecProcess = AdbEngine.StartAdbProcess(
                            $"shell screenrecord --bit-rate 4000000 /sdcard/{currentSoloRecordingName}");
                    }
                    catch (Exception ex)
                    {
                        currentSoloRecordingName = null;
                        await AdbEngine.RunCommandAsync("shell settings put system show_touches 0", 5000);
                        MessageBox.Show(
                            "화면 녹화를 시작하지 못했습니다.\n" + ex.Message,
                            "녹화 시작 실패",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        return;
                    }

                    if (soloRecProcess == null)
                    {
                        currentSoloRecordingName = null;
                        await AdbEngine.RunCommandAsync("shell settings put system show_touches 0", 5000);
                        MessageBox.Show("화면 녹화 프로세스를 시작하지 못했습니다.");
                        return;
                    }

                    isSoloRecording = true; // 즉시 종료도 Exited에서 감지
                    soloRecProcess.Exited += (_, _) =>
                    {
                        if (!isSoloRecording || IsDisposed) return;
                        BeginInvoke(new Action(async () =>
                        {
                            if (!isSoloRecording) return;
                            isSoloRecording = false;
                            string? completedName = currentSoloRecordingName;
                            currentSoloRecordingName = null;
                            await AdbEngine.RunCommandAsync("shell settings put system show_touches 0", 3000);
                            bool saved = false;
                            if (!string.IsNullOrWhiteSpace(completedName))
                            {
                                await AdbEngine.RunCommandAsync($"pull /sdcard/{completedName} \"{Globals.LogFolder}\"", 30000);
                                await AdbEngine.RunCommandAsync($"shell rm /sdcard/{completedName}", 5000);
                                saved = File.Exists(Path.Combine(Globals.LogFolder, completedName));
                            }
                            AdbEngine.TryKill(soloRecProcess);
                            soloRecProcess = null;
                            btnSoloRec.Text = "녹화 시작";
                            btnSoloRec.IconName = "record";
                            btnSoloRec.FillColor = Globals.SurfaceAlt;
                            btnSoloRec.HoverColor = Globals.SurfaceRaised;
                            btnSoloRec.BorderColor = Globals.Border;
                            btnSoloRec.BorderThickness = 1;
                            btnSoloRec.ForeColor = Globals.TextPrimary;
                            btnSoloRec.IconColor = Globals.TextSecondary;
                            lblStatusMsg.Text = saved
                                ? "상태: 화면 녹화가 자동 종료되어 파일 저장 및 터치 표시 원복을 완료했습니다."
                                : "상태: 화면 녹화가 종료되어 터치 표시를 원복했습니다. 저장 파일을 확인해주세요.";
                            btnSoloRec.Invalidate();
                        }));
                    };
                    soloRecProcess.EnableRaisingEvents = true;
                    btnSoloRec.Text = "녹화 중지";
                    btnSoloRec.IconName = "stop";
                    btnSoloRec.FillColor = Globals.Danger;
                    btnSoloRec.HoverColor = Lighten(Globals.Danger, 0.10f);
                    btnSoloRec.BorderThickness = 0;
                    btnSoloRec.ForeColor = Color.White;
                    btnSoloRec.IconColor = Color.White;
                    btnSoloRec.Invalidate();
                    lblStatusMsg.Text = "상태: 터치 표시를 켜고 화면 녹화 중";
                }
                else
                {
                    btnSoloRec.Enabled = false;
                    isSoloRecording = false; // 정상 정지 중 Exited 이벤트의 오류 처리를 막는다.
                    try
                    {
                        await AdbEngine.RunCommandAsync("shell pkill -INT screenrecord", 5000);
                        await System.Threading.Tasks.Task.Delay(2000);
                        if (!string.IsNullOrWhiteSpace(currentSoloRecordingName))
                        {
                            string completedName = currentSoloRecordingName;
                            await AdbEngine.RunCommandAsync($"pull /sdcard/{completedName} \"{Globals.LogFolder}\"", 30000);
                            await AdbEngine.RunCommandAsync($"shell rm /sdcard/{completedName}", 5000);
                        }
                        await AdbEngine.RunCommandAsync("shell settings put system show_touches 0", 5000);

                        isSoloRecording = false;
                        btnSoloRec.Text = "녹화 시작";
                        btnSoloRec.IconName = "record";
                        btnSoloRec.FillColor = Globals.SurfaceAlt;
                        btnSoloRec.HoverColor = Globals.SurfaceRaised;
                        btnSoloRec.BorderColor = Globals.Border;
                        btnSoloRec.BorderThickness = 1;
                        btnSoloRec.ForeColor = Globals.TextPrimary;
                        btnSoloRec.IconColor = Globals.TextSecondary;
                        btnSoloRec.Invalidate();
                        lblStatusMsg.Text = "상태: 화면 녹화 완료 · 터치 표시 원복";
                    }
                    finally
                    {
                        AdbEngine.TryKill(soloRecProcess);
                        soloRecProcess = null;
                        currentSoloRecordingName = null;
                        btnSoloRec.Enabled = true;
                    }
                }
            };
        }

        private Panel CreateUtilitySectionLabel(string title, string description)
        {
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(6, 4, 6, 4),
                BackColor = Globals.Bg
            };
            grid.ColumnStyles.Add(ColAbs(140));
            grid.ColumnStyles.Add(ColPct(100));
            grid.Controls.Add(new Label
            {
                Text = title,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Globals.FontSub,
                ForeColor = Globals.TextSecondary
            }, 0, 0);
            grid.Controls.Add(new Label
            {
                Text = description,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Globals.FontMuted,
                ForeColor = Globals.TextFaint,
                AutoEllipsis = false
            }, 1, 0);
            return grid;
        }

        private RoundedPanel CreateUtilityActionCard(string iconName, string title, string description, string buttonText, out RoundedButton actionButton)
        {
            var card = CreateCardDock();
            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Padding = new Padding(18, 14, 18, 14),
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            content.ColumnStyles.Add(ColAbs(82));
            content.ColumnStyles.Add(ColPct(100));
            content.ColumnStyles.Add(ColAbs(138));

            var iconBox = new RoundedPanel
            {
                Size = new Size(68, 68),
                Anchor = AnchorStyles.None,
                Margin = new Padding(0),
                FillColor = Globals.InfoSoft,
                BorderColor = Globals.Border,
                BorderThickness = 1,
                BorderRadius = Globals.Radius,
                Padding = new Padding(15)
            };
            iconBox.Controls.Add(new IconGlyph
            {
                IconName = iconName,
                IconColor = Globals.Accent,
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            });

            var textGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(8, 18, 12, 18),
                Padding = new Padding(0),
                BackColor = Color.Transparent
            };
            textGrid.ColumnStyles.Add(ColPct(100));
            textGrid.RowStyles.Add(Abs(34));
            textGrid.RowStyles.Add(Pct(100));
            textGrid.Controls.Add(new Label
            {
                Text = title,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Globals.FontHeading,
                ForeColor = Globals.TextPrimary,
                AutoEllipsis = false
            }, 0, 0);
            textGrid.Controls.Add(new Label
            {
                Text = description,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft,
                Font = Globals.FontMuted,
                ForeColor = Globals.TextSecondary,
                AutoEllipsis = false,
                Padding = new Padding(0, 2, 0, 0)
            }, 0, 1);

            actionButton = CreateModernButton(buttonText, Globals.Surface, 0, 0, 122, 44, iconName);
            actionButton.Size = new Size(122, 44);
            actionButton.Anchor = AnchorStyles.None;
            actionButton.Margin = new Padding(0);
            actionButton.TextAlign = ContentAlignment.MiddleCenter;
            actionButton.ForeColor = Globals.Accent;
            actionButton.IconColor = Globals.Accent;
            actionButton.BorderColor = Globals.BorderStrong;
            actionButton.BorderThickness = 1;

            content.Controls.Add(iconBox, 0, 0);
            content.Controls.Add(textGrid, 1, 0);
            content.Controls.Add(actionButton, 2, 0);
            card.Controls.Add(content);
            return card;
        }
    }
}
