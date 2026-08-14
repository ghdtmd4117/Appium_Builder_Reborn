using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using AppiumBuilder.Utils;
using AppiumBuilder.Core;
using AppiumBuilder.UI;

namespace AppiumBuilder
{
    public partial class MainForm
    {
        private System.Windows.Forms.Timer liveTimer = null!;
        private RichTextBox rtbLiveConsole = null!;
        private int lastLineCount = 0;  // [수정] 바이트 계산 대신 안전한 라인 수 기반으로 변경!

        private static bool legacyDataMigrationAttempted;
        public static string BaseLogPath => Globals.LogFolder;
        private static string LegacyBaseLogPath => Path.Combine(Application.StartupPath, "ADS_Logs");
        public static string CsvPath => Path.Combine(BaseLogPath, "AUTO_TEST", "CSV");
        public static string TestSetPath => Path.Combine(BaseLogPath, "AUTO_TEST", "TEST_SET");
        public static string PyScriptPath => Path.Combine(BaseLogPath, "AUTO_TEST", "PY_SCRIPT");
        public static string MediaLogPath => Path.Combine(BaseLogPath, "LOG");
        public static string SysPath => Path.Combine(BaseLogPath, "SYSTEM");

        private void InitDirectories()
        {
            MigrateLegacyAutoTestData();
            Directory.CreateDirectory(CsvPath);
            Directory.CreateDirectory(TestSetPath);
            Directory.CreateDirectory(PyScriptPath);
            Directory.CreateDirectory(MediaLogPath);
            Directory.CreateDirectory(SysPath);
        }

        private static void MigrateLegacyAutoTestData()
        {
            if (legacyDataMigrationAttempted) return;
            legacyDataMigrationAttempted = true;

            try
            {
                string legacyAutoTest = Path.Combine(LegacyBaseLogPath, "AUTO_TEST");
                string currentAutoTest = Path.Combine(BaseLogPath, "AUTO_TEST");
                if (!Directory.Exists(legacyAutoTest) || Path.GetFullPath(legacyAutoTest).Equals(Path.GetFullPath(currentAutoTest), StringComparison.OrdinalIgnoreCase)) return;

                CopyDirectoryIfMissing(legacyAutoTest, currentAutoTest);
            }
            catch
            {
                // 이전 실패가 앱 실행을 막지는 않도록 한다. 기존 폴더는 그대로 보존된다.
            }
        }

        private static void CopyDirectoryIfMissing(string sourceDirectory, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);
            foreach (string sourceFile in Directory.GetFiles(sourceDirectory))
            {
                string destinationFile = Path.Combine(destinationDirectory, Path.GetFileName(sourceFile));
                if (!File.Exists(destinationFile)) File.Copy(sourceFile, destinationFile, false);
            }

            foreach (string sourceSubDirectory in Directory.GetDirectories(sourceDirectory))
            {
                string destinationSubDirectory = Path.Combine(destinationDirectory, Path.GetFileName(sourceSubDirectory));
                CopyDirectoryIfMissing(sourceSubDirectory, destinationSubDirectory);
            }
        }

        private void AppendLiveLog(string msg, Color color)
        {
            if (string.IsNullOrWhiteSpace(msg)) return;
            botLiveArchive.Add((msg, color));
            if (botLiveArchive.Count > 5000) botLiveArchive.RemoveRange(0, botLiveArchive.Count - 5000);
            if (rtbLiveConsole == null || rtbLiveConsole.IsDisposed) return;
            string filter = cmbBotLogFilter?.SelectedItem?.ToString() ?? "전체";
            if (!BotLogMatches(msg, filter)) return;
            rtbLiveConsole.SelectionStart = rtbLiveConsole.TextLength;
            rtbLiveConsole.SelectionLength = 0;
            rtbLiveConsole.SelectionColor = color;
            rtbLiveConsole.AppendText(msg + "\n");
            rtbLiveConsole.SelectionColor = Globals.TextSecondary;
            rtbLiveConsole.ScrollToCaret();
        }

        private bool StartCurrentFlow(string scenarioName)
        {
            if (!batchRunActive && lstSteps.Items.Cast<object>().Any(item => (item?.ToString() ?? string.Empty).StartsWith("[RunPython]", StringComparison.Ordinal)))
            {
                if (MessageBox.Show(
                    "이 시나리오는 Python(.py) 파일을 PC에서 직접 실행합니다. 외부에서 받은 파일은 임의 코드를 실행할 수 있으므로 신뢰하는 파일만 실행하세요.\n\n계속할까요?",
                    "Python 스크립트 실행 확인",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
                    return false;
            }
            string loopText = GetText(txtLoop);
            if (string.IsNullOrWhiteSpace(loopText)) loopText = "1";

            if (!BotEngine.ValidateScenario(lstSteps, loopText, out _, out string validationMessage))
            {
                MessageBox.Show(validationMessage, "시나리오 실행 차단", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (!AppiumServerManager.IsServerRunning())
            {
                MessageBox.Show("Appium 서버가 실행 중이 아닙니다. 상단의 '서버 시작' 버튼으로 Appium Server를 먼저 시작해주세요.", "Appium 서버 필요", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (BotEngine.IsRunning)
            {
                MessageBox.Show("이미 봇이 실행 중입니다. 먼저 정지한 뒤 다시 실행해주세요.", "중복 실행 차단", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (!batchRunActive) botLiveArchive.Clear();
            rtbLiveConsole.Clear();
            lastLineCount = 0;
            currentRunScenario = scenarioName;
            currentRunSteps = lstSteps.Items.Count;
            currentRunStartedAt = DateTime.Now;

            BotEngine.GenerateAndRun(lstSteps, loopText, null, currentRunScenario, SysPath, TestSetPath, GetBotRunOptions());
            _ = StartBotRunRecordingAsync();
            historyLogged = false;
            liveTimer.Start();
            botStatusTimer?.Start();
            return true;
        }

        private void SetupAutoTab()
        {
            InitDirectories();

            pnlTabAuto = new DoubleBufferedPanel
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
                RowCount = 3,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            root.ColumnStyles.Add(ColPct(100));
            root.RowStyles.Add(Abs(88));
            root.RowStyles.Add(Abs(96));
            root.RowStyles.Add(Pct(100));
            root.Controls.Add(CreatePageHeader(
                "Appium 봇",
                "AI 분석, 시나리오 편집, 실행 로그와 수동 액션 빌더를 하나의 작업 공간에서 사용합니다."), 0, 0);
            root.Controls.Add(CreateAppiumServerControlBar(), 0, 1);

            var workspace = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Globals.Bg,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            workspace.ColumnStyles.Add(ColAbs(350));
            workspace.ColumnStyles.Add(ColPct(100));
            workspace.RowStyles.Add(Pct(100));

            // ===== LEFT: AI assistant + saved scenarios =====
            var leftGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = Globals.Bg
            };
            leftGrid.ColumnStyles.Add(ColPct(100));
            leftGrid.RowStyles.Add(Abs(342));
            leftGrid.RowStyles.Add(Pct(100));

            var aiCard = CreateCardDock();
            var aiLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 7,
                Padding = new Padding(14, 10, 14, 12),
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            aiLayout.ColumnStyles.Add(ColPct(100));
            aiLayout.RowStyles.Add(Abs(36));
            aiLayout.RowStyles.Add(Abs(54));
            aiLayout.RowStyles.Add(Abs(44));
            aiLayout.RowStyles.Add(Abs(44));
            aiLayout.RowStyles.Add(Abs(44));
            aiLayout.RowStyles.Add(Abs(44));
            aiLayout.RowStyles.Add(Pct(100));

            var aiHeader = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0), BackColor = Color.Transparent };
            aiHeader.ColumnStyles.Add(ColPct(100));
            aiHeader.ColumnStyles.Add(ColAbs(62));
            aiHeader.Controls.Add(new Label
            {
                Text = "AI 어시스턴트",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Globals.FontHeading,
                ForeColor = Globals.TextPrimary
            }, 0, 0);
            aiHeader.Controls.Add(new Label
            {
                Text = "온라인",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = Globals.FontMuted,
                ForeColor = Globals.Success,
                BackColor = Globals.SuccessSoft
            }, 1, 0);
            aiLayout.Controls.Add(aiHeader, 0, 0);

            var welcome = new Label
            {
                Text = "안녕하세요! 시나리오 작성, 요소 분석, 오류 해결을 도와드릴게요.",
                Dock = DockStyle.Fill,
                Font = Globals.FontMuted,
                ForeColor = Globals.TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 8, 0),
                BackColor = Globals.SurfaceAlt
            };
            aiLayout.Controls.Add(welcome, 0, 1);

            RoundedButton MakeAiAction(string icon, string text)
            {
                var button = CreateModernButton(text, Globals.Surface, 0, 0, 200, 34, icon);
                button.Dock = DockStyle.Fill;
                button.Margin = new Padding(0, 4, 0, 3);
                button.ForeColor = Globals.TextSecondary;
                button.IconColor = Globals.Accent;
                button.BorderColor = Globals.Border;
                button.BorderThickness = 1;
                button.TextAlign = ContentAlignment.MiddleLeft;
                return button;
            }

            var aiScreen = MakeAiAction("sparkles", "화면 분석하여 시나리오 생성");
            var aiExplore = MakeAiAction("target", "이동/클릭 요소 추천");
            var aiError = MakeAiAction("info", "오류 원인 분석 및 해결 가이드");
            var aiData = MakeAiAction("bolt", "테스트 데이터 생성 도와줘");
            aiScreen.Click += (_, _) => PrimeBotAssistant("현재 Android 화면을 분석해서 핵심 사용자 흐름을 검증하는 Appium 시나리오를 생성해줘.", true);
            aiExplore.Click += (_, _) => ExploreCurrentUiElements();
            aiError.Click += (_, _) => AnalyzeLatestBotFailure();
            aiData.Click += (_, _) => GenerateTestDataPrompt();
            aiLayout.Controls.Add(aiScreen, 0, 2);
            aiLayout.Controls.Add(aiExplore, 0, 3);
            aiLayout.Controls.Add(aiError, 0, 4);
            aiLayout.Controls.Add(aiData, 0, 5);

            var promptGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 4, 0, 0), BackColor = Color.Transparent };
            promptGrid.ColumnStyles.Add(ColPct(100));
            promptGrid.ColumnStyles.Add(ColAbs(52));
            txtAiPrompt = CreatePlaceholderTextBoxDock("무엇을 도와드릴까요?");
            txtAiPrompt.Margin = new Padding(0, 4, 8, 4);
            btnAiAnalyze = CreateModernButton(string.Empty, Globals.AccentSoft, 0, 0, 46, 36, "play");
            btnAiAnalyze.Dock = DockStyle.Fill;
            btnAiAnalyze.Margin = new Padding(0, 4, 0, 4);
            btnAiAnalyze.IconColor = Globals.Accent;
            btnAiAnalyze.BorderColor = Globals.Border;
            btnAiAnalyze.BorderThickness = 1;
            promptGrid.Controls.Add(txtAiPrompt, 0, 0);
            promptGrid.Controls.Add(btnAiAnalyze, 1, 0);
            aiLayout.Controls.Add(promptGrid, 0, 6);
            aiCard.Controls.Add(aiLayout);
            leftGrid.Controls.Add(aiCard, 0, 0);

            var savedCard = CreateCardDock();
            var savedLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(14, 10, 14, 10),
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            savedLayout.ColumnStyles.Add(ColPct(100));
            savedLayout.RowStyles.Add(Abs(42));
            savedLayout.RowStyles.Add(Abs(46));
            savedLayout.RowStyles.Add(Pct(100));
            savedLayout.RowStyles.Add(Abs(44));
            savedLayout.RowStyles.Add(Abs(50));

            var savedHeader = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0), BackColor = Color.Transparent };
            savedHeader.ColumnStyles.Add(ColPct(100));
            savedHeader.ColumnStyles.Add(ColAbs(132));
            savedHeader.Controls.Add(new Label
            {
                Text = "저장된 시나리오",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Globals.FontHeading,
                ForeColor = Globals.TextPrimary
            }, 0, 0);
            var btnNewScenario = CreateModernButton("새 시나리오", Globals.Surface, 0, 0, 122, 32, "plus");
            btnNewScenario.Dock = DockStyle.Fill;
            btnNewScenario.Margin = new Padding(4, 1, 0, 1);
            btnNewScenario.ForeColor = Globals.Accent;
            btnNewScenario.IconColor = Globals.Accent;
            btnNewScenario.BorderColor = Globals.Border;
            btnNewScenario.BorderThickness = 1;
            btnNewScenario.TextAlign = ContentAlignment.MiddleCenter;
            btnNewScenario.Click += (_, _) => NewScenario();
            savedHeader.Controls.Add(btnNewScenario, 1, 0);
            savedLayout.Controls.Add(savedHeader, 0, 0);

            txtScenarioSearch = CreatePlaceholderTextBoxDock("시나리오 검색");
            txtScenarioSearch.Margin = new Padding(0, 5, 0, 5);
            txtScenarioSearch.TextChanged += (_, _) =>
            {
                if (txtScenarioSearch.Text == txtScenarioSearch.Tag?.ToString()) return;
                RefreshSavedScenariosList();
            };
            savedLayout.Controls.Add(txtScenarioSearch, 0, 1);

            pnlSavedScenarios = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Globals.Surface,
                Margin = new Padding(0)
            };
            savedLayout.Controls.Add(pnlSavedScenarios, 0, 2);

            var savedTools = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = new Padding(0), BackColor = Color.Transparent };
            savedTools.ColumnStyles.Add(ColPct(34));
            savedTools.ColumnStyles.Add(ColPct(34));
            savedTools.ColumnStyles.Add(ColPct(32));
            var btnImport = CreateModernButton("가져오기", Globals.Surface, 0, 0, 90, 32, "folder");
            var btnSaveCurrent = CreateModernButton("현재 저장", Globals.Surface, 0, 0, 90, 32, "save");
            var btnVersions = CreateModernButton("버전", Globals.Surface, 0, 0, 80, 32, "archive");
            foreach (var b in new[] { btnImport, btnSaveCurrent, btnVersions })
            {
                b.Dock = DockStyle.Fill; b.Margin = new Padding(3, 4, 3, 4); b.ForeColor = Globals.TextSecondary; b.IconColor = Globals.Accent; b.BorderColor = Globals.Border; b.BorderThickness = 1; b.TextAlign = ContentAlignment.MiddleCenter;
            }
            btnImport.Click += (_, _) => ImportScenarioFiles();
            btnSaveCurrent.Click += (_, _) => PromptSaveScenario();
            btnVersions.Click += (_, _) => { using var form = new ScenarioVersionManagerForm(TestSetPath, CsvPath, loadedScenarioName); form.ShowDialog(this); };
            savedTools.Controls.Add(btnImport, 0, 0);
            savedTools.Controls.Add(btnSaveCurrent, 1, 0);
            savedTools.Controls.Add(btnVersions, 2, 0);
            savedLayout.Controls.Add(savedTools, 0, 3);

            var btnRunChecked = CreateModernButton("선택 시나리오 실행", Globals.Accent, 0, 0, 200, 38, "play");
            btnRunChecked.Dock = DockStyle.Fill;
            btnRunChecked.Margin = new Padding(0, 6, 0, 0);
            btnRunChecked.TextAlign = ContentAlignment.MiddleCenter;
            btnRunChecked.Click += BtnRunChecked_Click;
            savedLayout.Controls.Add(btnRunChecked, 0, 4);
            savedCard.Controls.Add(savedLayout);
            leftGrid.Controls.Add(savedCard, 0, 1);
            workspace.Controls.Add(leftGrid, 0, 0);

            // ===== RIGHT: flow / live log / builder / run controls =====
            var rightGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = Globals.Bg
            };
            rightGrid.ColumnStyles.Add(ColPct(100));
            rightGrid.RowStyles.Add(Pct(100));
            rightGrid.RowStyles.Add(Abs(330));

            var upperGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = Globals.Bg
            };
            upperGrid.ColumnStyles.Add(ColPct(60));
            upperGrid.ColumnStyles.Add(ColPct(40));
            upperGrid.RowStyles.Add(Pct(100));

            var flowCard = CreateCardDock();
            var flowLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(14, 10, 14, 10),
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            flowLayout.ColumnStyles.Add(ColPct(100));
            flowLayout.RowStyles.Add(Abs(44));
            flowLayout.RowStyles.Add(Pct(100));
            flowLayout.RowStyles.Add(Abs(50));
            var flowHeader = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            flowHeader.ColumnStyles.Add(ColPct(100));
            flowHeader.ColumnStyles.Add(ColAbs(126));
            flowHeader.ColumnStyles.Add(ColAbs(176));
            lblFlowTitle = new Label
            {
                Text = "시나리오 플로우 · 새 시나리오",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Globals.FontHeading,
                ForeColor = Globals.TextPrimary,
                AutoEllipsis = true
            };
            var btnFlowNew = CreateModernButton("새 시나리오", Globals.Surface, 0, 0, 122, 32, "plus");
            btnFlowNew.Dock = DockStyle.Fill; btnFlowNew.Margin = new Padding(4, 4, 4, 4); btnFlowNew.ForeColor = Globals.Accent; btnFlowNew.IconColor = Globals.Accent; btnFlowNew.BorderColor = Globals.Border; btnFlowNew.BorderThickness = 1; btnFlowNew.TextAlign = ContentAlignment.MiddleCenter;
            btnFlowNew.Click += (_, _) => NewScenario();
            var btnFlowSaveTop = CreateModernButton("다른 이름으로 저장", Globals.Surface, 0, 0, 172, 32, "save");
            btnFlowSaveTop.Dock = DockStyle.Fill; btnFlowSaveTop.Margin = new Padding(4, 4, 0, 4); btnFlowSaveTop.ForeColor = Globals.Accent; btnFlowSaveTop.IconColor = Globals.Accent; btnFlowSaveTop.BorderColor = Globals.Border; btnFlowSaveTop.BorderThickness = 1; btnFlowSaveTop.TextAlign = ContentAlignment.MiddleCenter;
            btnFlowSaveTop.Click += (_, _) => PromptSaveScenario();
            flowHeader.Controls.Add(lblFlowTitle, 0, 0);
            flowHeader.Controls.Add(btnFlowNew, 1, 0);
            flowHeader.Controls.Add(btnFlowSaveTop, 2, 0);
            flowLayout.Controls.Add(flowHeader, 0, 0);

            var flowBody = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            flowBody.ColumnStyles.Add(ColPct(100));
            flowBody.ColumnStyles.Add(ColAbs(40));
            var flowListHost = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = Globals.Surface
            };
            flowListHost.ColumnStyles.Add(ColPct(100));
            flowListHost.RowStyles.Add(Abs(32));
            flowListHost.RowStyles.Add(Pct(100));

            var stepHeader = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(4, 0, 8, 0),
                BackColor = Globals.SurfaceAlt
            };
            stepHeader.ColumnStyles.Add(ColAbs(46));
            stepHeader.ColumnStyles.Add(ColAbs(96));
            stepHeader.ColumnStyles.Add(ColPct(46));
            stepHeader.ColumnStyles.Add(ColPct(54));
            foreach ((string text, int column) in new[] { ("단계", 0), ("액션", 1), ("대상", 2), ("설명", 3) })
            {
                stepHeader.Controls.Add(new Label
                {
                    Text = text,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Font = Globals.FontMuted,
                    ForeColor = Globals.TextMuted,
                    Padding = new Padding(column == 0 ? 4 : 0, 0, 0, 0)
                }, column, 0);
            }
            flowListHost.Controls.Add(stepHeader, 0, 0);

            lstSteps = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Globals.Surface,
                ForeColor = Globals.TextPrimary,
                Font = Globals.FontBody,
                BorderStyle = BorderStyle.None,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 44,
                IntegralHeight = false,
                TabStop = true
            };
            lstSteps.DrawItem += LstSteps_DrawItem;
            flowListHost.Controls.Add(lstSteps, 0, 1);
            flowBody.Controls.Add(flowListHost, 0, 0);
            var movePanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(6, 0, 0, 0),
                Padding = new Padding(0),
                BackColor = Color.Transparent
            };
            movePanel.ColumnStyles.Add(ColPct(100));
            movePanel.RowStyles.Add(Pct(50));
            movePanel.RowStyles.Add(Pct(50));
            var btnMoveUp = CreateModernButton(string.Empty, Globals.SurfaceAlt, 0, 0, 34, 34, "chevron-up");
            btnMoveUp.Dock = DockStyle.Fill;
            btnMoveUp.Margin = new Padding(0, 0, 0, 3);
            var btnMoveDown = CreateModernButton(string.Empty, Globals.SurfaceAlt, 0, 0, 34, 34, "chevron-down");
            btnMoveDown.Dock = DockStyle.Fill;
            btnMoveDown.Margin = new Padding(0, 3, 0, 0);
            btnMoveUp.Click += (_, _) =>
            {
                int index = lstSteps.SelectedIndex;
                if (index <= 0) return;
                object item = lstSteps.Items[index];
                lstSteps.Items.RemoveAt(index);
                lstSteps.Items.Insert(index - 1, item);
                lstSteps.SelectedIndex = index - 1;
            };
            btnMoveDown.Click += (_, _) =>
            {
                int index = lstSteps.SelectedIndex;
                if (index < 0 || index >= lstSteps.Items.Count - 1) return;
                object item = lstSteps.Items[index];
                lstSteps.Items.RemoveAt(index);
                lstSteps.Items.Insert(index + 1, item);
                lstSteps.SelectedIndex = index + 1;
            };
            movePanel.Controls.Add(btnMoveUp, 0, 0);
            movePanel.Controls.Add(btnMoveDown, 0, 1);
            flowBody.Controls.Add(movePanel, 1, 0);
            flowLayout.Controls.Add(flowBody, 0, 1);

            var flowFooter = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            flowFooter.ColumnStyles.Add(ColPct(30));
            flowFooter.ColumnStyles.Add(ColPct(38));
            flowFooter.ColumnStyles.Add(ColPct(32));
            var btnClearFlow = CreateModernButton("목록 지우기", Globals.SurfaceAlt, 0, 0, 120, 34, "trash");
            btnClearFlow.Dock = DockStyle.Fill;
            btnClearFlow.Margin = new Padding(0, 7, 4, 1);
            btnClearFlow.TextAlign = ContentAlignment.MiddleCenter;
            var btnSaveAs = CreateModernButton("다른 이름으로 저장", Globals.SurfaceAlt, 0, 0, 150, 34, "save");
            btnSaveAs.Dock = DockStyle.Fill;
            btnSaveAs.Margin = new Padding(4, 7, 4, 1);
            btnSaveAs.TextAlign = ContentAlignment.MiddleCenter;
            var btnVisual = CreateModernButton("Visual 기준", Globals.SurfaceAlt, 0, 0, 120, 34, "camera");
            btnVisual.Dock = DockStyle.Fill;
            btnVisual.Margin = new Padding(4, 7, 0, 1);
            btnVisual.TextAlign = ContentAlignment.MiddleCenter;
            btnVisual.Click += (_, _) =>
            {
                using var form = new VisualBaselineManagerForm(TestSetPath, loadedScenarioName);
                form.ShowDialog(this);
            };
            flowFooter.Controls.Add(btnClearFlow, 0, 0);
            flowFooter.Controls.Add(btnSaveAs, 1, 0);
            flowFooter.Controls.Add(btnVisual, 2, 0);
            flowLayout.Controls.Add(flowFooter, 0, 2);
            flowCard.Controls.Add(flowLayout);
            upperGrid.Controls.Add(flowCard, 0, 0);

            var consoleCard = CreateCardDock(Globals.ConsoleBg);
            consoleCard.BorderColor = Globals.ConsoleLine;
            var consoleLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(14, 10, 14, 10),
                Margin = new Padding(0),
                BackColor = Globals.ConsoleBg
            };
            consoleLayout.ColumnStyles.Add(ColPct(100));
            consoleLayout.RowStyles.Add(Abs(44));
            consoleLayout.RowStyles.Add(Abs(38));
            consoleLayout.RowStyles.Add(Pct(100));
            var liveHeader = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0),
                BackColor = Globals.ConsoleBg
            };
            liveHeader.ColumnStyles.Add(ColPct(100));
            liveHeader.ColumnStyles.Add(ColAbs(104));
            liveHeader.ColumnStyles.Add(ColAbs(96));
            liveHeader.Controls.Add(new Label
            {
                Text = "실행 로그 (라이브)  ·  ● 실행 상태",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Globals.FontHeading,
                ForeColor = Globals.TextPrimary,
                AutoEllipsis = false
            }, 0, 0);
            cmbBotLogFilter = CreateFlatCombo(0, 0, 88, 32);
            cmbBotLogFilter.Dock = DockStyle.Fill;
            cmbBotLogFilter.Margin = new Padding(4, 4, 4, 4);
            cmbBotLogFilter.Items.AddRange(new object[] { "전체", "정보", "성공", "실패" });
            cmbBotLogFilter.SelectedIndex = 0;
            cmbBotLogFilter.SelectedIndexChanged += (_, _) => RebuildBotLiveConsole();
            liveHeader.Controls.Add(cmbBotLogFilter, 1, 0);
            btnBotLogClear = CreateModernButton("지우기", Globals.Surface, 0, 0, 92, 32, "trash");
            btnBotLogClear.Dock = DockStyle.Fill;
            btnBotLogClear.Margin = new Padding(4, 4, 0, 4);
            btnBotLogClear.ForeColor = Globals.TextSecondary;
            btnBotLogClear.IconColor = Globals.TextMuted;
            btnBotLogClear.BorderColor = Globals.Border;
            btnBotLogClear.BorderThickness = 1;
            btnBotLogClear.TextAlign = ContentAlignment.MiddleCenter;
            btnBotLogClear.Click += (_, _) => { botLiveArchive.Clear(); rtbLiveConsole.Clear(); };
            liveHeader.Controls.Add(btnBotLogClear, 2, 0);
            consoleLayout.Controls.Add(liveHeader, 0, 0);
            lblBotStatusMessage = new Label
            {
                Text = "대기 중 · 시나리오를 실행하면 상태가 표시됩니다.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Font = Globals.FontMuted,
                ForeColor = Globals.TextMuted,
                BackColor = Globals.ConsoleBg
            };
            consoleLayout.Controls.Add(lblBotStatusMessage, 0, 1);
            rtbLiveConsole = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Globals.ConsoleBg,
                ForeColor = Globals.TextSecondary,
                Font = Globals.FontMono,
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                DetectUrls = false,
                WordWrap = false,
                TabStop = true,
                Text = "시나리오 실행을 기다리고 있습니다.\n"
            };
            consoleLayout.Controls.Add(rtbLiveConsole, 0, 2);
            consoleCard.Controls.Add(consoleLayout);
            upperGrid.Controls.Add(consoleCard, 1, 0);
            rightGrid.Controls.Add(upperGrid, 0, 0);

            var lowerGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = Globals.Bg
            };
            lowerGrid.ColumnStyles.Add(ColPct(70));
            lowerGrid.ColumnStyles.Add(ColPct(30));
            lowerGrid.RowStyles.Add(Pct(100));

            // ===== Manual builder =====
            var builderCard = CreateCardDock();
            var builderInner = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 8,
                Padding = new Padding(16, 10, 16, 10),
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            builderInner.ColumnStyles.Add(ColPct(50));
            builderInner.ColumnStyles.Add(ColPct(50));
            builderInner.RowStyles.Add(Abs(36));
            builderInner.RowStyles.Add(Abs(30));
            builderInner.RowStyles.Add(Abs(24));
            builderInner.RowStyles.Add(Abs(42));
            builderInner.RowStyles.Add(Abs(24));
            builderInner.RowStyles.Add(Abs(44));
            builderInner.RowStyles.Add(Abs(38));
            builderInner.RowStyles.Add(Abs(48));
            var builderTitle = new Label
            {
                Text = "수동 빌더",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Globals.FontHeading,
                ForeColor = Globals.TextPrimary
            };
            builderInner.Controls.Add(builderTitle, 0, 0);
            builderInner.SetColumnSpan(builderTitle, 2);
            var builderDescription = new Label
            {
                Text = "액션과 대상 값을 직접 입력해 플로우 단계를 추가합니다.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Globals.FontMuted,
                ForeColor = Globals.TextMuted,
                AutoEllipsis = false
            };
            builderInner.Controls.Add(builderDescription, 0, 1);
            builderInner.SetColumnSpan(builderDescription, 2);
            var actionCaption = new Label
            {
                Text = "액션 유형",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Globals.FontMuted,
                ForeColor = Globals.TextSecondary
            };
            var locatorCaption = new Label
            {
                Text = "로케이터",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Globals.FontMuted,
                ForeColor = Globals.TextSecondary,
                Padding = new Padding(5, 0, 0, 0)
            };
            builderInner.Controls.Add(actionCaption, 0, 2);
            builderInner.Controls.Add(locatorCaption, 1, 2);
            cmbAction = CreateFlatCombo(0, 0, 200, 32);
            cmbAction.Dock = DockStyle.Fill;
            cmbAction.Margin = new Padding(0, 2, 6, 2);
            cmbAction.Items.AddRange(new object[]
            {
                "클릭(Click)", "좌표 클릭(XY)", "입력(SendKeys)", "스크롤(Swipe)",
                "기기 키(Key)", "대기(Sleep)", "OTP 추출(OTP)", "보안키패드(SecurePad)",
                "물리키패드(Keypad)", "알림창(Notification)", "요소 검증(Assert)",
                "전체 화면 검증(ScreenAssert)"
            });
            cmbAction.SelectedIndex = 0;
            cmbLocator = CreateFlatCombo(0, 0, 180, 32);
            cmbLocator.Dock = DockStyle.Fill;
            cmbLocator.Margin = new Padding(6, 2, 0, 2);
            cmbLocator.Items.AddRange(new object[] { "XPath", "ID", "Accessibility ID" });
            cmbLocator.SelectedIndex = 0;
            builderInner.Controls.Add(cmbAction, 0, 3);
            builderInner.Controls.Add(cmbLocator, 1, 3);
            var fieldCaption = new Label
            {
                Text = "대상 ID / XPath",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Globals.FontMuted,
                ForeColor = Globals.TextSecondary
            };
            builderInner.Controls.Add(fieldCaption, 0, 4);
            builderInner.SetColumnSpan(fieldCaption, 2);
            var fieldHost = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 2, 0, 2),
                BackColor = Color.Transparent
            };
            txtTarget = CreatePlaceholderTextBox("대상 ID / XPath / 값", 0, 0, 200, 40);
            txtValue = CreatePlaceholderTextBox("입력 텍스트 / 도착지", 0, 0, 160, 40);
            txtX = CreatePlaceholderTextBox("X 좌표", 0, 0, 140, 40);
            txtY = CreatePlaceholderTextBox("Y 좌표", 0, 0, 140, 40);
            txtValue.Visible = false;
            txtX.Visible = false;
            txtY.Visible = false;
            fieldHost.Controls.AddRange(new Control[] { txtTarget, txtValue, txtX, txtY });
            builderInner.Controls.Add(fieldHost, 0, 5);
            builderInner.SetColumnSpan(fieldHost, 2);
            var builderTip = new Label
            {
                Text = "플로우의 단계를 더블클릭하면 편집 모드로 전환됩니다.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Globals.FontMuted,
                ForeColor = Globals.TextFaint
            };
            builderInner.Controls.Add(builderTip, 0, 6);
            builderInner.SetColumnSpan(builderTip, 2);
            var builderButtons = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = Color.Transparent
            };
            btnAddStep = CreateModernButton("단계 추가", Globals.Accent, 0, 0, 120, 38, "check");
            btnEditStep = CreateModernButton("변경 적용", Globals.Accent, 0, 0, 120, 38, "check");
            btnCancelEdit = CreateModernButton("취소", Globals.SurfaceAlt, 0, 0, 80, 38, "x");
            btnDelStep = CreateModernButton("삭제", Globals.DangerSoft, 0, 0, 80, 38, "trash");
            btnDelStep.ForeColor = Globals.Danger;
            btnDelStep.IconColor = Globals.Danger;
            btnDelStep.BorderColor = Globals.Danger;
            btnDelStep.BorderThickness = 1;
            btnEditStep.Visible = false;
            btnCancelEdit.Visible = false;
            btnDelStep.Visible = false;
            builderInner.Controls.Add(builderButtons, 0, 7);
            builderInner.SetColumnSpan(builderButtons, 2);
            builderCard.Controls.Add(builderInner);
            lowerGrid.Controls.Add(builderCard, 0, 0);

            // ===== Run controls =====
            var runCard = CreateCardDock();
            var runLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 8,
                Padding = new Padding(14, 10, 14, 10),
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            runLayout.ColumnStyles.Add(ColPct(50));
            runLayout.ColumnStyles.Add(ColPct(50));
            runLayout.RowStyles.Add(Abs(36));
            runLayout.RowStyles.Add(Abs(24));
            runLayout.RowStyles.Add(Abs(42));
            runLayout.RowStyles.Add(Abs(24));
            runLayout.RowStyles.Add(Abs(42));
            runLayout.RowStyles.Add(Abs(38));
            runLayout.RowStyles.Add(Abs(38));
            runLayout.RowStyles.Add(Abs(48));
            var runTitle = new Label
            {
                Text = "실행 제어",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Globals.FontHeading,
                ForeColor = Globals.TextPrimary
            };
            runLayout.Controls.Add(runTitle, 0, 0);
            runLayout.SetColumnSpan(runTitle, 2);
            runLayout.Controls.Add(new Label { Text = "반복 횟수", Dock = DockStyle.Fill, Font = Globals.FontMuted, ForeColor = Globals.TextSecondary, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
            runLayout.Controls.Add(new Label { Text = "실행 간 대기 (ms)", Dock = DockStyle.Fill, Font = Globals.FontMuted, ForeColor = Globals.TextSecondary, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(5,0,0,0) }, 1, 1);
            txtLoop = CreatePlaceholderTextBoxDock("반복");
            txtLoop.Text = "1"; txtLoop.ForeColor = Globals.TextPrimary; txtLoop.Margin = new Padding(0, 0, 5, 2);
            txtRunDelay = CreatePlaceholderTextBoxDock("대기 시간");
            txtRunDelay.Text = "500"; txtRunDelay.ForeColor = Globals.TextPrimary; txtRunDelay.Margin = new Padding(5, 0, 0, 2);
            runLayout.Controls.Add(txtLoop, 0, 2);
            runLayout.Controls.Add(txtRunDelay, 1, 2);
            var failLabel = new Label { Text = "실패 시 동작", Dock = DockStyle.Fill, Font = Globals.FontMuted, ForeColor = Globals.TextSecondary, TextAlign = ContentAlignment.MiddleLeft };
            runLayout.Controls.Add(failLabel, 0, 3); runLayout.SetColumnSpan(failLabel, 2);
            cmbFailureBehavior = CreateFlatCombo(0, 0, 200, 32);
            cmbFailureBehavior.Dock = DockStyle.Fill; cmbFailureBehavior.Margin = new Padding(0,0,0,2);
            cmbFailureBehavior.Items.AddRange(new object[] { "실행 중지", "다음 시나리오 계속" });
            cmbFailureBehavior.SelectedIndex = 0;
            runLayout.Controls.Add(cmbFailureBehavior, 0, 4); runLayout.SetColumnSpan(cmbFailureBehavior, 2);

            Control MakeToggleRow(string label, ModernToggleSwitch toggle)
            {
                var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0), BackColor = Color.Transparent };
                row.ColumnStyles.Add(ColPct(100)); row.ColumnStyles.Add(ColAbs(44));
                row.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, Font = Globals.FontMuted, ForeColor = Globals.TextSecondary, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
                toggle.Size = new Size(38, 20); toggle.Anchor = AnchorStyles.Right; toggle.Margin = new Padding(4, 9, 0, 9);
                row.Controls.Add(toggle, 1, 0);
                return row;
            }
            toggleStepScreenshot = new ModernToggleSwitch { Checked = true };
            toggleRunVideo = new ModernToggleSwitch { Checked = false };
            var screenshotRow = MakeToggleRow("단계별 스크린샷 저장", toggleStepScreenshot);
            var videoRow = MakeToggleRow("실행 영상 녹화", toggleRunVideo);
            runLayout.Controls.Add(screenshotRow, 0, 5); runLayout.SetColumnSpan(screenshotRow, 2);
            runLayout.Controls.Add(videoRow, 0, 6); runLayout.SetColumnSpan(videoRow, 2);

            var runButtons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 5, 0, 0), BackColor = Color.Transparent };
            runButtons.ColumnStyles.Add(ColPct(42)); runButtons.ColumnStyles.Add(ColPct(58));
            var btnStop = CreateModernButton("정지", Globals.DangerSoft, 0, 0, 90, 38, "stop");
            btnStop.Dock = DockStyle.Fill; btnStop.Margin = new Padding(0, 0, 4, 0); btnStop.ForeColor = Globals.Danger; btnStop.IconColor = Globals.Danger; btnStop.BorderColor = Globals.Danger; btnStop.BorderThickness = 1; btnStop.TextAlign = ContentAlignment.MiddleCenter;
            var btnRun = CreateModernButton("봇 실행", Globals.Accent, 0, 0, 120, 38, "play");
            btnRun.Dock = DockStyle.Fill; btnRun.Margin = new Padding(4, 0, 0, 0); btnRun.TextAlign = ContentAlignment.MiddleCenter;
            runButtons.Controls.Add(btnStop, 0, 0); runButtons.Controls.Add(btnRun, 1, 0);
            runLayout.Controls.Add(runButtons, 0, 7); runLayout.SetColumnSpan(runButtons, 2);
            runCard.Controls.Add(runLayout);
            lowerGrid.Controls.Add(runCard, 1, 0);
            rightGrid.Controls.Add(lowerGrid, 0, 1);
            workspace.Controls.Add(rightGrid, 1, 0);

            void RelayoutBuilder()
            {
                string action = cmbAction.Text;
                bool usesLocator = action == "클릭(Click)" ||
                                   action == "입력(SendKeys)" ||
                                   action == "OTP 추출(OTP)" ||
                                   action == "요소 검증(Assert)";
                cmbLocator.Enabled = usesLocator;
                locatorCaption.ForeColor = usesLocator ? Globals.TextSecondary : Globals.TextFaint;

                int width = Math.Max(240, fieldHost.ClientSize.Width);
                int gap = 8;
                int half = Math.Max(100, (width - gap) / 2);
                int third = Math.Max(72, (width - gap * 2) / 3);
                txtTarget.Visible = false;
                txtValue.Visible = false;
                txtX.Visible = false;
                txtY.Visible = false;

                if (action == "입력(SendKeys)" || action == "요소 검증(Assert)")
                {
                    txtTarget.Visible = true;
                    txtValue.Visible = true;
                    txtTarget.SetBounds(0, 2, half, 40);
                    txtValue.SetBounds(half + gap, 2, width - half - gap, 40);
                    fieldCaption.Text = "대상 / 입력 값";
                }
                else if (action == "좌표 클릭(XY)")
                {
                    txtX.Visible = true;
                    txtY.Visible = true;
                    txtX.SetBounds(0, 2, half, 40);
                    txtY.SetBounds(half + gap, 2, width - half - gap, 40);
                    fieldCaption.Text = "X 좌표 / Y 좌표";
                }
                else if (action == "스크롤(Swipe)")
                {
                    txtX.Visible = true;
                    txtY.Visible = true;
                    txtValue.Visible = true;
                    txtX.SetBounds(0, 2, third, 40);
                    txtY.SetBounds(third + gap, 2, third, 40);
                    txtValue.SetBounds((third + gap) * 2, 2, width - (third + gap) * 2, 40);
                    fieldCaption.Text = "시작 X / 시작 Y / 도착지";
                }
                else
                {
                    txtTarget.Visible = true;
                    txtTarget.SetBounds(0, 2, width, 40);
                    fieldCaption.Text = action == "대기(Sleep)" ? "대기 시간(초)" : "대상 ID / XPath / 값";
                }

                builderButtons.SuspendLayout();
                builderButtons.Controls.Clear();
                builderButtons.ColumnStyles.Clear();
                bool editMode = btnEditStep.Visible;
                if (editMode)
                {
                    builderButtons.ColumnCount = 4;
                    builderButtons.ColumnStyles.Add(ColPct(46));
                    builderButtons.ColumnStyles.Add(ColPct(18));
                    builderButtons.ColumnStyles.Add(ColPct(18));
                    builderButtons.ColumnStyles.Add(ColPct(18));
                    btnEditStep.Dock = DockStyle.Fill;
                    btnEditStep.Margin = new Padding(0, 1, 4, 1);
                    btnCancelEdit.Dock = DockStyle.Fill;
                    btnCancelEdit.Margin = new Padding(4, 1, 4, 1);
                    btnDelStep.Dock = DockStyle.Fill;
                    btnDelStep.Margin = new Padding(4, 1, 0, 1);
                    builderButtons.Controls.Add(btnEditStep, 0, 0);
                    builderButtons.SetColumnSpan(btnEditStep, 2);
                    builderButtons.Controls.Add(btnCancelEdit, 2, 0);
                    builderButtons.Controls.Add(btnDelStep, 3, 0);
                }
                else if (btnDelStep.Visible && !btnAddStep.Visible)
                {
                    builderButtons.ColumnCount = 2;
                    builderButtons.ColumnStyles.Add(ColPct(65));
                    builderButtons.ColumnStyles.Add(ColPct(35));
                    btnAddStep.Visible = true;
                    btnAddStep.Dock = DockStyle.Fill;
                    btnAddStep.Margin = new Padding(0, 1, 4, 1);
                    btnDelStep.Dock = DockStyle.Fill;
                    btnDelStep.Margin = new Padding(4, 1, 0, 1);
                    builderButtons.Controls.Add(btnAddStep, 0, 0);
                    builderButtons.Controls.Add(btnDelStep, 1, 0);
                }
                else
                {
                    builderButtons.ColumnCount = 1;
                    builderButtons.ColumnStyles.Add(ColPct(100));
                    btnAddStep.Visible = true;
                    btnAddStep.Dock = DockStyle.Fill;
                    btnAddStep.Margin = new Padding(0, 1, 0, 1);
                    builderButtons.Controls.Add(btnAddStep, 0, 0);
                }
                builderButtons.ResumeLayout();
            }

            fieldHost.Resize += (_, _) => RelayoutBuilder();
            cmbAction.SelectedIndexChanged += (_, _) => RelayoutBuilder();
            RelayoutBuilder();
            RelayoutBuilderRef = RelayoutBuilder;

            void ConfigureWorkspaceColumns(int leftLogicalWidth)
            {
                workspace.SuspendLayout();
                workspace.Controls.Clear();
                workspace.ColumnStyles.Clear();
                workspace.RowStyles.Clear();
                workspace.ColumnCount = 2;
                workspace.RowCount = 1;
                workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Ui(leftLogicalWidth)));
                workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                workspace.Controls.Add(leftGrid, 0, 0);
                workspace.Controls.Add(rightGrid, 1, 0);
                workspace.ResumeLayout(true);
            }

            void ConfigureUpperGrid(bool stacked)
            {
                if (stacked)
                    ReflowEqualGrid(upperGrid, 1, new Control[] { flowCard, consoleCard });
                else
                    ReflowWeightedGrid(upperGrid, new Control[] { flowCard, consoleCard }, 60, 40);
            }

            void ConfigureLowerGrid(bool stacked)
            {
                if (stacked)
                    ReflowEqualGrid(lowerGrid, 1, new Control[] { builderCard, runCard });
                else
                    ReflowWeightedGrid(lowerGrid, new Control[] { builderCard, runCard }, 70, 30);
            }

            void ApplyAutoResponsiveLayout()
            {
                int availableWidth = Math.Max(1, pnlTabAuto.ClientSize.Width - pnlTabAuto.Padding.Horizontal);
                int availableHeight = Math.Max(1, pnlTabAuto.ClientSize.Height - pnlTabAuto.Padding.Vertical);
                bool compactWidth = availableWidth < Ui(1050);
                bool shortHeight = availableHeight < Ui(760);

                root.SuspendLayout();
                rightGrid.SuspendLayout();

                if (compactWidth)
                {
                    // 좁은 창에서는 Flow/Log와 Builder/Run을 각각 세로로 쌓는다.
                    // 컨트롤을 압축하지 않고 페이지 스크롤로 모든 입력/버튼의 최소 크기를 보존한다.
                    SetResponsivePageMode(pnlTabAuto, root, true, 1740);
                    ConfigureWorkspaceColumns(300);
                    leftGrid.RowStyles[0].SizeType = SizeType.Absolute;
                    leftGrid.RowStyles[0].Height = Ui(370);
                    leftGrid.RowStyles[1].SizeType = SizeType.Percent;
                    leftGrid.RowStyles[1].Height = 100;

                    ConfigureUpperGrid(true);
                    ConfigureLowerGrid(true);
                    rightGrid.RowStyles[0].SizeType = SizeType.Absolute;
                    rightGrid.RowStyles[0].Height = Ui(820);
                    rightGrid.RowStyles[1].SizeType = SizeType.Absolute;
                    rightGrid.RowStyles[1].Height = Ui(720);
                }
                else
                {
                    ConfigureWorkspaceColumns(availableWidth < Ui(1200) ? 320 : 350);
                    ConfigureUpperGrid(false);
                    ConfigureLowerGrid(false);
                    leftGrid.RowStyles[0].SizeType = SizeType.Absolute;
                    leftGrid.RowStyles[0].Height = Ui(342);
                    leftGrid.RowStyles[1].SizeType = SizeType.Percent;
                    leftGrid.RowStyles[1].Height = 100;

                    if (shortHeight)
                    {
                        SetResponsivePageMode(pnlTabAuto, root, true, 1120);
                        rightGrid.RowStyles[0].SizeType = SizeType.Percent;
                        rightGrid.RowStyles[0].Height = 100;
                        rightGrid.RowStyles[1].SizeType = SizeType.Absolute;
                        rightGrid.RowStyles[1].Height = Ui(350);
                    }
                    else
                    {
                        SetResponsivePageMode(pnlTabAuto, root, false, 0);
                        rightGrid.RowStyles[0].SizeType = SizeType.Percent;
                        rightGrid.RowStyles[0].Height = 100;
                        rightGrid.RowStyles[1].SizeType = SizeType.Absolute;
                        rightGrid.RowStyles[1].Height = Ui(330);
                    }
                }

                SetAbsoluteRow(root, 0, 88);
                SetAbsoluteRow(root, 1, 96);
                SetPercentRow(root, 2);
                rightGrid.ResumeLayout(true);
                root.ResumeLayout(true);
                RelayoutBuilder();
            }

            root.Controls.Add(workspace, 0, 2);
            pnlTabAuto.Resize += (_, _) => ApplyAutoResponsiveLayout();
            pnlTabAuto.Controls.Add(root);
            pnlContent.Controls.Add(pnlTabAuto);
            ApplyAutoResponsiveLayout();

            if (botStatusTimer != null) botStatusTimer.Stop();
            liveTimer = new System.Windows.Forms.Timer { Interval = 400 };
            liveTimer.Tick += (_, _) =>
            {
                try
                {
                    string statusFile = Path.Combine(SysPath, "bot_status.txt");
                    if (File.Exists(statusFile))
                    {
                        using var stream = new FileStream(statusFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        using var reader = new StreamReader(stream, new UTF8Encoding(false));
                        var lines = new List<string>();
                        while (!reader.EndOfStream) lines.Add(reader.ReadLine() ?? string.Empty);

                        if (lines.Count < lastLineCount) lastLineCount = 0;
                        if (lines.Count > lastLineCount)
                        {
                            for (int i = lastLineCount; i < lines.Count; i++)
                            {
                                string line = lines[i].TrimEnd();
                                if (string.IsNullOrEmpty(line)) continue;
                                if (line.Contains("[FAIL]") || line.Contains("실패") || line.Contains("오류"))
                                    AppendLiveLog(line, Globals.Danger);
                                else if (line.Contains("[PASS]") || line.Contains("성공"))
                                    AppendLiveLog(line, Globals.Success);
                                else
                                    AppendLiveLog(line, Globals.TextSecondary);
                            }
                            lastLineCount = lines.Count;
                            if (lines.Any(line => line.Contains("성공적으로 끝났습니다")))
                                liveTimer.Stop();
                        }
                    }

                    string errorFile = Path.Combine(SysPath, "bot_error.log");
                    if (File.Exists(errorFile))
                    {
                        string error = File.ReadAllText(errorFile, new UTF8Encoding(false));
                        if (!string.IsNullOrWhiteSpace(error))
                        {
                            liveTimer.Stop();
                            AppendLiveLog("[FAIL] 테스트 중 오류가 발생하여 중지되었습니다.", Globals.Danger);
                            AppendLiveLog(error, Globals.Danger);
                        }
                    }
                }
                catch
                {
                    // 다음 타이머 틱에서 다시 읽는다.
                }
            };

            RefreshSavedScenariosList();

            btnAddStep.Click += (_, _) =>
            {
                string? row = BuildStepRow();
                if (row == null) return;
                lstSteps.Items.Add(row);
                txtTarget.Text = txtTarget.Tag?.ToString() ?? string.Empty;
                txtTarget.ForeColor = Globals.TextFaint;
                txtValue.Text = txtValue.Tag?.ToString() ?? string.Empty;
                txtValue.ForeColor = Globals.TextFaint;
                txtX.Text = txtX.Tag?.ToString() ?? string.Empty;
                txtX.ForeColor = Globals.TextFaint;
                txtY.Text = txtY.Tag?.ToString() ?? string.Empty;
                txtY.ForeColor = Globals.TextFaint;
            };
            btnDelStep.Click += (_, _) =>
            {
                if (lstSteps.SelectedIndex >= 0) lstSteps.Items.RemoveAt(lstSteps.SelectedIndex);
                ResetToAddMode();
            };
            lstSteps.SelectedIndexChanged += (_, _) =>
            {
                if (editingIndex == -1)
                {
                    bool hasSelection = lstSteps.SelectedIndex >= 0;
                    btnDelStep.Visible = hasSelection;
                    btnAddStep.Visible = !hasSelection;
                }
                RelayoutBuilder();
                builderInner.Refresh();
            };
            lstSteps.DoubleClick += (_, _) =>
            {
                if (lstSteps.SelectedIndex < 0) return;
                editingIndex = lstSteps.SelectedIndex;
                LoadRowIntoEditor(lstSteps.Items[editingIndex]?.ToString() ?? string.Empty);
                btnAddStep.Visible = false;
                btnEditStep.Visible = true;
                btnCancelEdit.Visible = true;
                btnDelStep.Visible = true;
                RelayoutBuilder();
            };
            btnEditStep.Click += (_, _) =>
            {
                if (editingIndex < 0) return;
                string? row = BuildStepRow();
                if (row != null) lstSteps.Items[editingIndex] = row;
                ResetToAddMode();
            };
            btnCancelEdit.Click += (_, _) => ResetToAddMode();
            btnClearFlow.Click += (_, _) =>
            {
                lstSteps.Items.Clear();
                loadedScenarioName = null;
                lblFlowTitle.Text = "시나리오 플로우 · 새 시나리오";
                ResetToAddMode();
            };
            btnSaveAs.Click += (_, _) => PromptSaveScenario();

            btnRun.Click += async (_, _) =>
            {
                try
                {
                    if (!await EnsureAppiumServerReadyAsync("Appium 봇 실행")) return;
                    if (StartCurrentFlow(loadedScenarioName ?? "수동 시나리오"))
                    {
                        lblBotStatusMessage.Text = "실행 준비 중 · Python 러너와 Appium 세션을 시작합니다.";
                        lblBotStatusMessage.ForeColor = Globals.Info;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "봇 실행 준비 중 오류 발생:\n" + ex.Message,
                        "실행 실패",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            };
            btnStop.Click += (_, _) =>
            {
                bool stopped = BotEngine.StopCurrentRun(SysPath, out string stopMessage);
                liveTimer.Stop();
                botStatusTimer?.Stop();
                _ = StopBotRunRecordingAsync();
                if (stopped)
                {
                    lblBotStatusMessage.Text = "사용자에 의해 실행이 중지되었습니다.";
                    lblBotStatusMessage.ForeColor = Globals.Danger;
                    AppendLiveLog("사용자에 의해 실행 프로세스가 중지되었습니다.", Globals.Danger);
                    if (!historyLogged)
                    {
                        RecordTestHistory(currentRunScenario, currentRunSteps, false, "사용자 중지", "STOPPED");
                        historyLogged = true;
                        HandleBatchRunCompletion(false, "사용자 중지", "STOPPED");
                    }
                }
                else
                {
                    AppendLiveLog("[안내] " + stopMessage, Globals.TextMuted);
                }
            };

            btnAiAnalyze.Click += async (_, _) =>
            {
                string prompt = GetText(txtAiPrompt);
                if (string.IsNullOrWhiteSpace(prompt))
                {
                    MessageBox.Show("명령을 입력해주세요.");
                    return;
                }

                btnAiAnalyze.Enabled = false;
                string originalText = btnAiAnalyze.Text;
                btnAiAnalyze.Text = "분석 중...";
                try
                {
                    List<string>? steps = null;
                    bool usedFallback = false;
                    if (await System.Threading.Tasks.Task.Run(AdbEngine.IsDeviceConnected))
                    {
                        string apiKey = GetOrAskGeminiKey();
                        if (!string.IsNullOrWhiteSpace(apiKey))
                        {
                            try
                            {
                                string dumpPath = Path.Combine(SysPath, "window_dump.xml");
                                await AdbEngine.RunCommandAsync("shell uiautomator dump /sdcard/window_dump.xml", 10000);
                                await AdbEngine.RunCommandAsync($"pull /sdcard/window_dump.xml \"{dumpPath}\"", 15000);
                                await AdbEngine.RunCommandAsync("shell rm /sdcard/window_dump.xml", 5000);
                                if (File.Exists(dumpPath))
                                {
                                    string dump = File.ReadAllText(dumpPath);
                                    dump = RedactUiDumpForAi(dump);
                                    if (dump.Length > 30000) dump = dump.Substring(0, 30000);
                                    steps = await CallGeminiForSteps(apiKey, prompt, dump);
                                }
                            }
                            catch
                            {
                                steps = null;
                            }
                        }
                    }

                    if (steps == null || steps.Count == 0)
                    {
                        steps = AnalyzePromptToSteps(prompt);
                        usedFallback = steps.Count > 0;
                    }
                    if (steps == null || steps.Count == 0)
                    {
                        MessageBox.Show("문장에서 동작을 인식하지 못했습니다.");
                        return;
                    }

                    foreach (string step in steps) lstSteps.Items.Add(step);
                    txtAiPrompt.Text = txtAiPrompt.Tag?.ToString() ?? string.Empty;
                    txtAiPrompt.ForeColor = Globals.TextFaint;
                    if (usedFallback)
                        lblStatusMsg.Text = "상태: 오프라인 규칙 기반 분석으로 단계를 추가했습니다.";
                }
                finally
                {
                    btnAiAnalyze.Enabled = true;
                    btnAiAnalyze.Text = originalText;
                }
            };
        }

        private RoundedPanel CreateAppiumServerControlBar()
        {
            var card = CreateCardDock();
            card.Margin = new Padding(0, 0, 0, 8);

            // 서버 상태/버튼과 안내 문구를 한 줄에 억지로 넣지 않는다.
            // 1행: 아이콘 + 서버 상태 + 제어 버튼
            // 2행: 전체 폭 안내 문구
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                Padding = new Padding(12, 8, 12, 8),
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            layout.ColumnStyles.Add(ColAbs(42));
            layout.ColumnStyles.Add(ColPct(100));
            layout.ColumnStyles.Add(ColAbs(270));
            layout.RowStyles.Add(Abs(42));
            layout.RowStyles.Add(Pct(100));

            var iconBox = new RoundedPanel
            {
                Size = new Size(36, 36),
                Anchor = AnchorStyles.None,
                Margin = new Padding(0),
                Padding = new Padding(7),
                FillColor = Globals.InfoSoft,
                BorderColor = Globals.Border,
                BorderThickness = 1,
                BorderRadius = 12
            };
            iconBox.Controls.Add(new IconGlyph
            {
                IconName = "terminal",
                IconColor = Globals.Accent,
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            });
            layout.Controls.Add(iconBox, 0, 0);

            var stateGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Margin = new Padding(4, 0, 12, 0),
                Padding = new Padding(0),
                BackColor = Color.Transparent
            };
            stateGrid.ColumnStyles.Add(ColAbs(18));
            stateGrid.ColumnStyles.Add(ColPct(100));
            stateGrid.RowStyles.Add(Abs(22));
            stateGrid.RowStyles.Add(Abs(20));

            dotAppiumServer = Dot(Globals.BorderStrong, 8);
            dotAppiumServer.Anchor = AnchorStyles.Left;
            dotAppiumServer.Margin = new Padding(2, 0, 0, 0);
            stateGrid.Controls.Add(dotAppiumServer, 0, 0);

            lblAppiumServerState = new Label
            {
                Text = "Appium 서버 · 확인 중",
                Dock = DockStyle.Fill,
                Font = Globals.FontSub,
                ForeColor = Globals.TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = false,
                Margin = new Padding(0)
            };
            stateGrid.Controls.Add(lblAppiumServerState, 1, 0);

            lblAppiumServerEndpoint = new Label
            {
                Text = "127.0.0.1:4723 · 상태 확인 중",
                Dock = DockStyle.Fill,
                Font = Globals.FontMuted,
                ForeColor = Globals.TextMuted,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = false,
                Margin = new Padding(0)
            };
            stateGrid.Controls.Add(lblAppiumServerEndpoint, 1, 1);
            layout.Controls.Add(stateGrid, 1, 0);

            var buttonGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = Color.Transparent
            };
            buttonGrid.ColumnStyles.Add(ColAbs(150));
            buttonGrid.ColumnStyles.Add(ColAbs(116));
            buttonGrid.RowStyles.Add(Pct(100));

            btnAppiumServerToggle = CreateModernButton("서버 시작", Globals.Accent, 0, 0, 140, 38, "play");
            btnAppiumServerToggle.Dock = DockStyle.Fill;
            btnAppiumServerToggle.Margin = new Padding(0, 2, 8, 2);
            btnAppiumServerToggle.TextAlign = ContentAlignment.MiddleCenter;
            btnAppiumServerToggle.Click += (_, _) => ToggleAppiumServer();
            buttonGrid.Controls.Add(btnAppiumServerToggle, 0, 0);

            btnAppiumTerminal = CreateModernButton("터미널", Globals.Surface, 0, 0, 108, 38, "terminal");
            btnAppiumTerminal.Dock = DockStyle.Fill;
            btnAppiumTerminal.Margin = new Padding(0, 2, 0, 2);
            btnAppiumTerminal.ForeColor = Globals.TextSecondary;
            btnAppiumTerminal.IconColor = Globals.Accent;
            btnAppiumTerminal.BorderColor = Globals.Border;
            btnAppiumTerminal.BorderThickness = 1;
            btnAppiumTerminal.TextAlign = ContentAlignment.MiddleCenter;
            btnAppiumTerminal.Click += (_, _) => ShowAppiumTerminal();
            buttonGrid.Controls.Add(btnAppiumTerminal, 1, 0);

            layout.Controls.Add(buttonGrid, 2, 0);

            var hint = new Label
            {
                Text = "Appium Server가 실행 중이어야 봇 세션을 시작할 수 있습니다.",
                Dock = DockStyle.Fill,
                Font = Globals.FontMuted,
                ForeColor = Globals.TextMuted,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = false,
                Margin = new Padding(4, 0, 0, 0),
                Padding = new Padding(0)
            };
            layout.Controls.Add(hint, 1, 1);
            layout.SetColumnSpan(hint, 2);

            card.Controls.Add(layout);
            return card;
        }

        private Panel CreateAutoFooterLabel(string caption, string value)
        {
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = Color.Transparent
            };
            grid.ColumnStyles.Add(ColAbs(50));
            grid.ColumnStyles.Add(ColPct(100));
            grid.Controls.Add(new Label
            {
                Text = caption,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Globals.FontMuted,
                ForeColor = Globals.TextFaint
            }, 0, 0);
            grid.Controls.Add(new Label
            {
                Text = value,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Globals.FontMuted,
                ForeColor = Globals.TextSecondary,
                AutoEllipsis = true
            }, 1, 0);
            return grid;
        }

        private void ImportScenarioFiles()
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "테스트 파일 (*.csv;*.zip;*.py)|*.csv;*.zip;*.py|모든 파일 (*.*)|*.*";
                ofd.Multiselect = true;
                ofd.Title = "CSV, Python 스크립트 또는 테스트 셋(ZIP) 가져오기";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    InitDirectories();
                    int count = 0;
                    foreach (string file in ofd.FileNames)
                    {
                        try
                        {
                            string ext = Path.GetExtension(file).ToLower();
                            if (ext == ".zip")
                            {
                                string destDir = Path.Combine(TestSetPath, Path.GetFileNameWithoutExtension(file));
                                if (!Directory.Exists(destDir))
                                {
                                    ZipFile.ExtractToDirectory(file, destDir);
                                    count++;
                                    if (!File.Exists(Path.Combine(destDir, "scenario.csv")))
                                    {
                                        MessageBox.Show($"'{Path.GetFileNameWithoutExtension(file)}' 테스트 셋에는 scenario.csv가 없습니다.\n기준 자료는 가져왔지만 실행 전 시나리오를 작성해야 합니다.", "시나리오 필요", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                    }
                                }
                                else
                                {
                                    MessageBox.Show($"이미 동일한 이름의 테스트 셋이 존재합니다: {Path.GetFileNameWithoutExtension(file)}");
                                }
                            }
                            else if (ext == ".csv")
                            {
                                string dest = Path.Combine(CsvPath, Path.GetFileName(file));
                                File.Copy(file, dest, true);
                                count++;
                            }
                            else if (ext == ".py")
                            {
                                string dest = Path.Combine(PyScriptPath, Path.GetFileName(file));
                                File.Copy(file, dest, true);
                                count++;
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"파일 처리 실패: {Path.GetFileName(file)}\n{ex.Message}");
                        }
                    }
                    if (count > 0) RefreshSavedScenariosList();
                }
            }
        }

        private async void BtnRunChecked_Click(object? sender, EventArgs e)
        {
            var checkedFiles = new List<string>();
            foreach (Control ctrl in pnlSavedScenarios.Controls)
            {
                if (ctrl is Panel row)
                {
                    var chk = row.Controls.OfType<CheckBox>().FirstOrDefault();
                    if (chk != null && chk.Checked && chk.Tag != null)
                        checkedFiles.Add(chk.Tag.ToString()!);
                }
            }

            if (checkedFiles.Count == 0)
            {
                MessageBox.Show("다중 실행할 항목을 하나 이상 체크해주세요.");
                return;
            }
            if (BotEngine.IsRunning || batchRunActive)
            {
                MessageBox.Show("이미 실행 중인 테스트가 있습니다. 먼저 현재 실행을 종료해주세요.");
                return;
            }
            if (!await EnsureAppiumServerReadyAsync("선택 시나리오 순차 실행")) return;

            var loadErrors = new List<string>();
            var runs = new List<QueuedScenarioRun>();
            bool includesPython = false;
            foreach (string file in checkedFiles)
            {
                try
                {
                    if (file.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
                    {
                        includesPython = true;
                        if (!File.Exists(file)) loadErrors.Add($"파일 없음: {file}");
                        else runs.Add(new QueuedScenarioRun
                        {
                            Name = Path.GetFileNameWithoutExtension(file),
                            SourcePath = file,
                            Steps = new List<string> { $"[RunPython] {file}" }
                        });
                        continue;
                    }

                    string targetFile = file.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                        ? file
                        : Path.Combine(file, "scenario.csv");
                    if (!File.Exists(targetFile))
                    {
                        loadErrors.Add($"scenario.csv 없음: {Path.GetFileName(file)}");
                        continue;
                    }
                    List<string> steps = ReadScenarioSteps(targetFile);
                    if (steps.Count == 0)
                    {
                        loadErrors.Add($"유효 스텝 0개: {Path.GetFileName(file)}");
                        continue;
                    }
                    string name = file.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                        ? Path.GetFileNameWithoutExtension(file)
                        : Path.GetFileName(file.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    runs.Add(new QueuedScenarioRun { Name = name, SourcePath = targetFile, Steps = steps });
                }
                catch (Exception ex)
                {
                    loadErrors.Add($"{Path.GetFileName(file)}: {ex.Message}");
                }
            }

            if (loadErrors.Count > 0 || runs.Count == 0)
            {
                MessageBox.Show("다중 실행을 시작할 수 없습니다.\n\n" + string.Join("\n", loadErrors), "시나리오 불러오기 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (includesPython && MessageBox.Show(
                    "선택 항목에 Python(.py) 스크립트가 포함되어 있습니다.\n외부에서 받은 Python 파일은 PC에서 임의 코드를 실행할 수 있습니다.\n\n신뢰하는 파일만 실행하세요. 계속할까요?",
                    "Python 스크립트 실행 확인",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            batchRunQueue.Clear();
            foreach (var run in runs) batchRunQueue.Enqueue(run);
            currentBatchId = "batch_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N")[..6];
            batchRunActive = true;
            lblFlowTitle.Text = $"시나리오 플로우 · 순차 실행 대기 ({runs.Count}개)";
            AppendLiveLog($"[BATCH] {runs.Count}개 시나리오 순차 실행 시작 · {currentBatchId}", Globals.Info);
            StartNextBatchScenario();
        }

        private void StartNextBatchScenario()
        {
            if (!batchRunActive || BotEngine.IsRunning) return;
            if (batchRunQueue.Count == 0)
            {
                batchRunActive = false;
                currentBatchId = null;
                lblFlowTitle.Text = "시나리오 플로우 · 순차 실행 완료";
                AppendLiveLog("[BATCH] 모든 선택 시나리오 실행이 완료되었습니다.", Globals.Success);
                RefreshTestDashboard();
                return;
            }

            QueuedScenarioRun next = batchRunQueue.Dequeue();
            lstSteps.Items.Clear();
            foreach (string step in next.Steps) lstSteps.Items.Add(step);
            loadedScenarioName = next.Name;
            lblFlowTitle.Text = $"시나리오 플로우 · {next.Name} · 남은 항목 {batchRunQueue.Count}개";
            ResetToAddMode();
            AppendLiveLog($"[BATCH] 시작: {next.Name}", Globals.Info);
            try
            {
                if (!StartCurrentFlow(next.Name))
                    throw new InvalidOperationException("시나리오 실행을 시작하지 못했습니다.");
            }
            catch (Exception ex)
            {
                RecordSkippedHistory(next.Name, next.Steps.Count, "실행 준비 실패: " + ex.Message, currentBatchId);
                HandleBatchRunCompletion(false, ex.Message, "FAIL");
            }
        }

        private void HandleBatchRunCompletion(bool success, string? message, string status)
        {
            if (!batchRunActive) return;
            if (!success)
            {
                if (ContinueBatchAfterFailure && !string.Equals(status, "STOPPED", StringComparison.OrdinalIgnoreCase))
                {
                    AppendLiveLog($"[BATCH] 실패 후 계속: {status} · {message}", Globals.Warning);
                    BeginInvoke(new Action(StartNextBatchScenario));
                    return;
                }

                while (batchRunQueue.Count > 0)
                {
                    QueuedScenarioRun skipped = batchRunQueue.Dequeue();
                    RecordSkippedHistory(skipped.Name, skipped.Steps.Count, $"이전 시나리오가 {status} 상태로 종료되어 미실행", currentBatchId);
                }
                batchRunActive = false;
                AppendLiveLog($"[BATCH] 중단: {status} · {message}", Globals.Danger);
                currentBatchId = null;
                RefreshTestDashboard();
                return;
            }

            BeginInvoke(new Action(StartNextBatchScenario));
        }

        private void RefreshSavedScenariosList()
        {
            if (pnlSavedScenarios == null) return;

            pnlSavedScenarios.SuspendLayout();
            pnlSavedScenarios.Controls.Clear();
            InitDirectories();

            var csvFiles = Directory.GetFiles(CsvPath, "*.csv")
                .Select(file => new { Path = file, Name = Path.GetFileNameWithoutExtension(file), Type = "CSV" });
            var setFolders = Directory.GetDirectories(TestSetPath)
                .Where(directory => !Path.GetFileName(directory).StartsWith("_", StringComparison.Ordinal))
                .Select(directory => new { Path = directory, Name = Path.GetFileName(directory), Type = "SET" });
            var pyFiles = Directory.GetFiles(PyScriptPath, "*.py")
                .Select(file => new { Path = file, Name = Path.GetFileNameWithoutExtension(file), Type = "PY" });

            var allItems = csvFiles
                .Concat(setFolders)
                .Concat(pyFiles)
                .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            string scenarioSearch = txtScenarioSearch == null ? string.Empty : txtScenarioSearch.Text.Trim();
            if (txtScenarioSearch != null && string.Equals(scenarioSearch, txtScenarioSearch.Tag?.ToString(), StringComparison.Ordinal))
                scenarioSearch = string.Empty;
            if (scenarioSearch.Length > 0)
                allItems = allItems.Where(item => item.Name.Contains(scenarioSearch, StringComparison.CurrentCultureIgnoreCase)).ToArray();

            if (allItems.Length == 0)
            {
                pnlSavedScenarios.Controls.Add(new Label
                {
                    Text = "저장된 시나리오가 없습니다.",
                    Dock = DockStyle.Top,
                    Height = 46,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = Globals.FontMuted,
                    ForeColor = Globals.TextFaint
                });
                pnlSavedScenarios.ResumeLayout();
                return;
            }

            var latestHistoryByScenario = LoadTestHistory()
                .Where(record => !string.IsNullOrWhiteSpace(record.scenario))
                .GroupBy(record => record.scenario, StringComparer.CurrentCultureIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderByDescending(GetRecordTime).First(),
                    StringComparer.CurrentCultureIgnoreCase);

            foreach (var item in allItems.Reverse())
            {
                bool active = string.Equals(loadedScenarioName, item.Name, StringComparison.OrdinalIgnoreCase);
                latestHistoryByScenario.TryGetValue(item.Name, out TestRunRecord? latestRun);
                string latestStatus = latestRun == null ? string.Empty : GetRecordStatus(latestRun);
                DateTime itemTime;
                try
                {
                    itemTime = item.Type == "SET"
                        ? Directory.GetLastWriteTime(item.Path)
                        : File.GetLastWriteTime(item.Path);
                }
                catch
                {
                    itemTime = DateTime.MinValue;
                }
                if (latestRun != null) itemTime = GetRecordTime(latestRun);

                var row = new RoundedPanel
                {
                    Dock = DockStyle.Top,
                    Height = 56,
                    Margin = new Padding(0, 0, 0, 5),
                    Padding = new Padding(0),
                    FillColor = active ? Globals.AccentSoft : Globals.Surface,
                    BorderColor = active ? Globals.Accent : Globals.Border,
                    BorderThickness = 1,
                    BorderRadius = Globals.RadiusXs,
                    Cursor = Cursors.Hand,
                    Tag = item.Name
                };

                var chk = new CheckBox
                {
                    AutoSize = true,
                    Location = new Point(9, 20),
                    Cursor = Cursors.Hand,
                    Tag = item.Path,
                    BackColor = Color.Transparent
                };
                var scenarioIcon = new IconGlyph
                {
                    IconName = item.Type == "PY" ? "code" : "file",
                    IconColor = active ? Globals.Accent : Globals.TextMuted,
                    Location = new Point(31, 17),
                    Size = new Size(20, 20),
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };
                var nameLabel = new Label
                {
                    Text = item.Name,
                    Location = new Point(59, 6),
                    Size = new Size(165, 23),
                    AutoEllipsis = true,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Font = Globals.FontSub,
                    ForeColor = active ? Globals.AccentText : Globals.TextPrimary,
                    Cursor = Cursors.Hand,
                    BackColor = Color.Transparent,
                    Tag = "scenario-name"
                };
                var metaLabel = new Label
                {
                    Text = itemTime == DateTime.MinValue ? item.Type : $"{itemTime:yyyy-MM-dd HH:mm}",
                    Location = new Point(59, 29),
                    Size = new Size(165, 18),
                    AutoEllipsis = true,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Font = Globals.FontMuted,
                    ForeColor = Globals.TextMuted,
                    Cursor = Cursors.Hand,
                    BackColor = Color.Transparent
                };
                var statusLabel = new Label
                {
                    Text = latestStatus switch
                    {
                        "PASS" => "성공",
                        "FAIL" => "실패",
                        "STOPPED" => "중지",
                        "SKIPPED" => "건너뜀",
                        _ => item.Type
                    },
                    Size = new Size(48, 22),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = Globals.FontMuted,
                    ForeColor = latestStatus switch
                    {
                        "PASS" => Globals.Success,
                        "FAIL" => Globals.Danger,
                        "STOPPED" => Globals.Warning,
                        _ => Globals.TextMuted
                    },
                    BackColor = latestStatus switch
                    {
                        "PASS" => Globals.SuccessSoft,
                        "FAIL" => Globals.DangerSoft,
                        "STOPPED" => Globals.WarningSoft,
                        _ => Globals.SurfaceAlt
                    },
                    Anchor = AnchorStyles.Top | AnchorStyles.Right
                };
                var menuLabel = new Label
                {
                    Text = "⋮",
                    Size = new Size(24, 28),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = Globals.FontSub,
                    ForeColor = Globals.TextMuted,
                    Cursor = Cursors.Hand,
                    BackColor = Color.Transparent,
                    Anchor = AnchorStyles.Top | AnchorStyles.Right
                };

                void LayoutRow()
                {
                    menuLabel.Location = new Point(Math.Max(220, row.Width - 29), 14);
                    statusLabel.Location = new Point(Math.Max(168, row.Width - 82), 17);
                    int textRight = statusLabel.Left - 8;
                    nameLabel.Width = Math.Max(72, textRight - nameLabel.Left);
                    metaLabel.Width = nameLabel.Width;
                }

                void DeleteScenario()
                {
                    if (MessageBox.Show(
                            $"'{item.Name}'을(를) 완전히 삭제하시겠습니까?",
                            "삭제 확인",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning) != DialogResult.Yes)
                        return;

                    try
                    {
                        if (item.Type == "SET") Directory.Delete(item.Path, true);
                        else File.Delete(item.Path);

                        if (string.Equals(loadedScenarioName, item.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            lstSteps.Items.Clear();
                            loadedScenarioName = null;
                            lblFlowTitle.Text = "시나리오 플로우 · 새 시나리오";
                            ResetToAddMode();
                        }
                        RefreshSavedScenariosList();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("삭제 실패: " + ex.Message);
                    }
                }

                var menu = new ContextMenuStrip
                {
                    Font = Globals.FontBody,
                    BackColor = Globals.Surface,
                    ForeColor = Globals.TextPrimary,
                    ShowImageMargin = false
                };
                menu.Items.Add("열기", null, (_, _) => LoadScenarioFile(item.Path, item.Name, item.Type));
                menu.Items.Add("다른 이름으로 저장", null, (_, _) =>
                {
                    LoadScenarioFile(item.Path, item.Name, item.Type);
                    PromptSaveScenario();
                });
                menu.Items.Add("버전 관리", null, (_, _) =>
                {
                    using var form = new ScenarioVersionManagerForm(TestSetPath, CsvPath, item.Name);
                    form.ShowDialog(this);
                });
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add("삭제", null, (_, _) => DeleteScenario());

                row.Resize += (_, _) => LayoutRow();
                LayoutRow();
                row.Controls.AddRange(new Control[] { chk, scenarioIcon, nameLabel, metaLabel, statusLabel, menuLabel });

                EventHandler loadHandler = (_, _) => LoadScenarioFile(item.Path, item.Name, item.Type);
                row.Click += loadHandler;
                scenarioIcon.Click += loadHandler;
                nameLabel.Click += loadHandler;
                metaLabel.Click += loadHandler;
                statusLabel.Click += loadHandler;
                menuLabel.Click += (_, _) => menu.Show(menuLabel, new Point(0, menuLabel.Height));

                pnlSavedScenarios.Controls.Add(row);
            }

            pnlSavedScenarios.ResumeLayout();
        }

        private void LoadScenarioFile(string path, string name, string type)
        {
            try
            {
                if (type == "PY")
                {
                    lstSteps.Items.Clear();
                    lstSteps.Items.Add($"[RunPython] {path}");
                    MessageBox.Show("현업 파이썬 원본 스크립트는 수동 빌더에서 편집할 수 없습니다.\n보관 및 체크박스 실행용으로만 제공됩니다.");
                }
                else
                {
                    lstSteps.Items.Clear();
                    string targetFile = type == "SET" ? Path.Combine(path, "scenario.csv") : path;

                    if (File.Exists(targetFile))
                    {
                        foreach (string step in ReadScenarioSteps(targetFile))
                            lstSteps.Items.Add(step);
                    }
                    else if (type == "SET")
                    {
                        MessageBox.Show("해당 테스트 셋에 scenario.csv 파일이 없어 빈 상태로 엽니다.\n작성 후 [현재 저장]을 누르면 폴더 안에 저장됩니다.");
                    }
                }

                loadedScenarioName = name;
                lblFlowTitle.Text = "시나리오 플로우 · " + name;
                ResetToAddMode();

                foreach (Control control in pnlSavedScenarios.Controls)
                {
                    if (control is not RoundedPanel row) continue;
                    bool active = string.Equals(row.Tag?.ToString(), name, StringComparison.OrdinalIgnoreCase);
                    row.FillColor = active ? Globals.AccentSoft : Globals.SurfaceAlt;
                    row.BorderColor = active ? Globals.Accent : Globals.Border;
                    foreach (Label label in row.Controls.OfType<Label>())
                    {
                        if (Equals(label.Tag, "scenario-name"))
                            label.ForeColor = active ? Globals.AccentText : Globals.TextPrimary;
                    }
                    row.Invalidate();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("불러오기 실패: " + ex.Message);
            }
        }

        private void PromptSaveScenario()
        {
            if (lstSteps.Items.Count == 0) { MessageBox.Show("저장할 시나리오가 없습니다."); return; }
            string name = ShowInputDialog("시나리오 이름을 입력하세요.\n(기존 [SET] 이름과 동일하면 해당 폴더 안에 업데이트됩니다.)", "시나리오 저장");
            if (string.IsNullOrWhiteSpace(name)) return;
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');

            string testSetFolder = Path.Combine(TestSetPath, name);
            string targetPath = Directory.Exists(testSetFolder)
                ? Path.Combine(testSetFolder, "scenario.csv")
                : Path.Combine(CsvPath, name + ".csv");

            var csvLines = new System.Collections.Generic.List<string>();
            foreach (var item in lstSteps.Items)
            {
                string row = item.ToString() ?? "";
                csvLines.Add("\"" + row.Replace("\"", "\"\"") + "\"");
            }
            if (File.Exists(targetPath))
            {
                string versionsDir;
                if (string.Equals(Path.GetFileName(targetPath), "scenario.csv", StringComparison.OrdinalIgnoreCase))
                    versionsDir = Path.Combine(Path.GetDirectoryName(targetPath)!, ".versions");
                else
                    versionsDir = Path.Combine(CsvPath, ".versions", Path.GetFileNameWithoutExtension(targetPath));
                Directory.CreateDirectory(versionsDir);
                string versionPath = Path.Combine(versionsDir, DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".csv");
                File.Copy(targetPath, versionPath, true);
            }
            File.WriteAllLines(targetPath, csvLines, Encoding.UTF8);

            loadedScenarioName = name;
            lblFlowTitle.Text = "시나리오 플로우 · " + name;
            RefreshSavedScenariosList();
            MessageBox.Show("저장 완료: " + name);
        }
    }
}