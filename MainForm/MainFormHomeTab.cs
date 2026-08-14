using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using AppiumBuilder.Core;
using AppiumBuilder.UI;
using AppiumBuilder.Utils;

namespace AppiumBuilder
{
    public partial class MainForm
    {
        private void SetupHomeTab()
        {
            pnlTabHome = new DoubleBufferedPanel
            {
                Dock = DockStyle.Fill,
                Visible = true,
                BackColor = Globals.Bg,
                Padding = new Padding(20, 14, 20, 14),
                AutoScroll = true
            };

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Globals.Bg,
                ColumnCount = 1,
                RowCount = 6,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            root.ColumnStyles.Add(ColPct(100));
            root.RowStyles.Add(Abs(88));
            root.RowStyles.Add(Abs(152));
            root.RowStyles.Add(Abs(116));
            root.RowStyles.Add(Pct(100));
            root.RowStyles.Add(Abs(108));
            root.RowStyles.Add(Abs(104));

            root.Controls.Add(CreatePageHeader(
                "홈 대시보드",
                "연결된 디바이스 상태와 테스트 실행 현황을 한눈에 확인하세요."), 0, 0);

            var deviceGrid = EqualColumnGrid(3);
            var homeConnectionCard = CreateHomeConnectionCard();
            var homeModelCard = CreateHomeInfoCard("phone", "디바이스 모델", out lblHomeModel, out lblHomeModelMeta);
            var homeOsCard = CreateHomeInfoCard("android", "OS 버전", out lblHomeOs, out lblHomeOsMeta);
            Control[] deviceCards = { homeConnectionCard, homeModelCard, homeOsCard };
            deviceGrid.Controls.Add(homeConnectionCard, 0, 0);
            deviceGrid.Controls.Add(homeModelCard, 1, 0);
            deviceGrid.Controls.Add(homeOsCard, 2, 0);
            root.Controls.Add(deviceGrid, 0, 1);

            var statGrid = EqualColumnGrid(4);
            lblStatTotal = AddMetricCard(statGrid, 0, "list", "전체 실행 수", Globals.Info, "전체 테스트 실행 수", out lblStatTotalTrend);
            lblStatPass = AddMetricCard(statGrid, 1, "check", "성공 (Pass)", Globals.Success, "성공한 실행 수", out lblStatPassTrend);
            lblStatFail = AddMetricCard(statGrid, 2, "x", "실패 (Fail)", Globals.Danger, "실패한 실행 수", out lblStatFailTrend);
            lblStatRate = AddMetricCard(statGrid, 3, "monitor", "성공률", Globals.Accent, "전체 성공률", out lblStatRateTrend);
            Control[] statCards = Enumerable.Range(0, 4).Select(i => statGrid.GetControlFromPosition(i, 0)!).ToArray();
            root.Controls.Add(statGrid, 0, 2);

            root.Controls.Add(CreateRecentRunsCard(), 0, 3);

            var quickCard = CreateCardDock();
            var quickLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(12, 8, 12, 10),
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            quickLayout.RowStyles.Add(Abs(30));
            quickLayout.RowStyles.Add(Pct(100));
            quickLayout.Controls.Add(new Label
            {
                Text = "빠른 실행",
                Dock = DockStyle.Fill,
                Font = Globals.FontSub,
                ForeColor = Globals.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            var quickGrid = EqualColumnGrid(4);
            var qCapture = CreateQuickAction("camera", "화면 캡처", "현재 화면을 캡처합니다.");
            var qDashboard = CreateQuickAction("monitor", "대시보드 시작", "기존 Appium 대시보드를 엽니다.");
            var qLog = CreateQuickAction("terminal", "로그 뷰어", "실행 로그를 확인합니다.");
            var qDump = CreateQuickAction("dump", "덤프 수집", "디바이스 덤프를 수집합니다.");
            qCapture.Click += (_, _) => CaptureScreen();
            qDashboard.Click += (_, _) => StartDashboard();
            qLog.Click += (_, _) => SwitchTab(pnlTabLog, btnTabLog);
            qDump.Click += (_, _) => DumpSystem();
            Control[] quickActions = { qCapture, qDashboard, qLog, qDump };
            quickGrid.Controls.Add(qCapture, 0, 0);
            quickGrid.Controls.Add(qDashboard, 1, 0);
            quickGrid.Controls.Add(qLog, 2, 0);
            quickGrid.Controls.Add(qDump, 3, 0);
            quickLayout.Controls.Add(quickGrid, 0, 1);
            quickCard.Controls.Add(quickLayout);
            root.Controls.Add(quickCard, 0, 4);

            var botStrip = CreateHomeBotStrip();
            root.Controls.Add(botStrip, 0, 5);

            void ApplyHomeResponsiveLayout()
            {
                int available = Math.Max(1, pnlTabHome.ClientSize.Width - pnlTabHome.Padding.Horizontal);
                bool compact = available < Ui(1120);
                bool narrow = available < Ui(860);

                root.SuspendLayout();
                if (!compact)
                {
                    SetResponsivePageMode(pnlTabHome, root, false, 0);
                    ReflowEqualGrid(deviceGrid, 3, deviceCards);
                    ReflowEqualGrid(statGrid, 4, statCards);
                    ReflowEqualGrid(quickGrid, 4, quickActions);
                    SetAbsoluteRow(root, 0, 88);
                    SetAbsoluteRow(root, 1, 152);
                    SetAbsoluteRow(root, 2, 116);
                    SetPercentRow(root, 3);
                    SetAbsoluteRow(root, 4, 108);
                    SetAbsoluteRow(root, 5, 104);
                }
                else
                {
                    // 작은 창에서는 카드 폭을 줄이는 대신 줄바꿈/재배치하고 페이지 전체를 스크롤한다.
                    SetResponsivePageMode(pnlTabHome, root, true, narrow ? 1460 : 1080);
                    ReflowEqualGrid(deviceGrid, narrow ? 1 : 3, deviceCards);
                    ReflowEqualGrid(statGrid, 2, statCards);
                    ReflowEqualGrid(quickGrid, narrow ? 1 : 2, quickActions);
                    SetAbsoluteRow(root, 0, 88);
                    SetAbsoluteRow(root, 1, narrow ? 438 : 152);
                    SetAbsoluteRow(root, 2, 226);
                    SetAbsoluteRow(root, 3, 310);
                    SetAbsoluteRow(root, 4, narrow ? 340 : 196);
                    SetAbsoluteRow(root, 5, narrow ? 156 : 124);
                }
                root.ResumeLayout(true);
            }

            pnlTabHome.Resize += (_, _) => ApplyHomeResponsiveLayout();
            pnlTabHome.Controls.Add(root);
            pnlContent.Controls.Add(pnlTabHome);
            ApplyHomeResponsiveLayout();
            RefreshTestDashboard();
        }

        private RoundedPanel CreateHomeConnectionCard()
        {
            var card = CreateCardDock();
            var grid = CreateHomeTopCardGrid("wifi", "연결 상태", Globals.Accent, out Panel valueHost, out Panel metaHost);
            var statusFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 2, 0, 0),
                Margin = new Padding(0)
            };
            dotHomeConn = Dot(Globals.Danger, 9);
            dotHomeConn.Margin = new Padding(0, 8, 8, 0);
            lblHomeConn = new Label
            {
                Text = "연결 대기",
                AutoSize = true,
                Font = Globals.FontTitle,
                ForeColor = Globals.Danger,
                Margin = new Padding(0, 0, 0, 0)
            };
            statusFlow.Controls.Add(dotHomeConn);
            statusFlow.Controls.Add(lblHomeConn);
            valueHost.Controls.Add(statusFlow);
            lblHomeConnMeta = CreateHomeMetaLabel("ADB 연결을 확인해주세요.\n마지막 확인 · 연결 대기");
            metaHost.Controls.Add(lblHomeConnMeta);
            card.Controls.Add(grid);
            return card;
        }

        private RoundedPanel CreateHomeInfoCard(string icon, string caption, out Label valueLabel, out Label metaLabel)
        {
            var card = CreateCardDock();
            var grid = CreateHomeTopCardGrid(icon, caption, Globals.Accent, out Panel valueHost, out Panel metaHost);
            valueLabel = new Label
            {
                Text = "-",
                Dock = DockStyle.Fill,
                Font = Globals.FontTitle,
                ForeColor = Globals.Accent,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            metaLabel = CreateHomeMetaLabel("정보 확인 중...\n-");
            valueHost.Controls.Add(valueLabel);
            metaHost.Controls.Add(metaLabel);
            card.Controls.Add(grid);
            return card;
        }

        private TableLayoutPanel CreateHomeTopCardGrid(string icon, string caption, Color iconColor, out Panel valueHost, out Panel metaHost)
        {
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                Padding = new Padding(16, 14, 16, 12),
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            grid.ColumnStyles.Add(ColAbs(70));
            grid.ColumnStyles.Add(ColPct(100));
            grid.RowStyles.Add(Abs(22));
            grid.RowStyles.Add(Abs(34));
            grid.RowStyles.Add(Abs(50));
            var iconBox = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 14, 4),
                Padding = new Padding(15),
                FillColor = Globals.InfoSoft,
                BorderColor = Globals.Border,
                BorderThickness = 1,
                BorderRadius = 28
            };
            iconBox.Controls.Add(new IconGlyph { IconName = icon, IconColor = iconColor, Dock = DockStyle.Fill });
            grid.Controls.Add(iconBox, 0, 0);
            grid.SetRowSpan(iconBox, 3);
            grid.Controls.Add(new Label
            {
                Text = caption,
                Dock = DockStyle.Fill,
                Font = Globals.FontSub,
                ForeColor = Globals.TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft
            }, 1, 0);
            valueHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            metaHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            grid.Controls.Add(valueHost, 1, 1);
            grid.Controls.Add(metaHost, 1, 2);
            return grid;
        }

        private Label CreateHomeMetaLabel(string text) => new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            Font = Globals.FontMuted,
            ForeColor = Globals.TextMuted,
            TextAlign = ContentAlignment.TopLeft,
            AutoEllipsis = false
        };

        private Label AddMetricCard(TableLayoutPanel host, int column, string icon, string caption, Color accent, string description, out Label trendLabel)
        {
            var card = CreateCardDock();
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 3,
                Padding = new Padding(14, 10, 14, 10),
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            grid.ColumnStyles.Add(ColAbs(58));
            grid.ColumnStyles.Add(ColPct(100));
            grid.ColumnStyles.Add(ColAbs(74));
            grid.RowStyles.Add(Abs(22));
            grid.RowStyles.Add(Abs(36));
            grid.RowStyles.Add(Pct(100));
            var iconBox = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 12, 3),
                Padding = new Padding(13),
                FillColor = accent == Globals.Success ? Globals.SuccessSoft : accent == Globals.Danger ? Globals.DangerSoft : Globals.InfoSoft,
                BorderThickness = 0,
                BorderRadius = 24
            };
            iconBox.Controls.Add(new IconGlyph { IconName = icon, IconColor = accent, Dock = DockStyle.Fill });
            grid.Controls.Add(iconBox, 0, 0);
            grid.SetRowSpan(iconBox, 3);
            var captionLabel = new Label
            {
                Text = caption,
                Dock = DockStyle.Fill,
                Font = Globals.FontSub,
                ForeColor = Globals.TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft
            };
            grid.Controls.Add(captionLabel, 1, 0);
            grid.SetColumnSpan(captionLabel, 2);
            var value = new Label
            {
                Text = "-",
                Dock = DockStyle.Fill,
                Font = Globals.FontStat,
                ForeColor = accent,
                TextAlign = ContentAlignment.MiddleLeft
            };
            grid.Controls.Add(value, 1, 1);
            trendLabel = new Label
            {
                Text = "-",
                Dock = DockStyle.Fill,
                Font = Globals.FontMuted,
                ForeColor = accent,
                TextAlign = ContentAlignment.MiddleRight,
                AutoEllipsis = true
            };
            grid.Controls.Add(trendLabel, 2, 1);
            grid.Controls.Add(new Label
            {
                Text = description,
                Dock = DockStyle.Fill,
                Font = Globals.FontMuted,
                ForeColor = Globals.TextMuted,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = false
            }, 1, 2);
            var trendHint = new Label
            {
                Text = "최근 7일",
                Dock = DockStyle.Fill,
                Font = Globals.FontMuted,
                ForeColor = Globals.TextFaint,
                TextAlign = ContentAlignment.MiddleRight
            };
            grid.Controls.Add(trendHint, 2, 2);
            card.Controls.Add(grid);
            host.Controls.Add(card, column, 0);
            return value;
        }

        private RoundedPanel CreateRecentRunsCard()
        {
            var card = CreateCardDock();
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(12, 8, 12, 10),
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            layout.RowStyles.Add(Abs(38));
            layout.RowStyles.Add(Abs(30));
            layout.RowStyles.Add(Pct(100));

            var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent, Margin = new Padding(0) };
            header.ColumnStyles.Add(ColPct(100));
            header.ColumnStyles.Add(ColAbs(120));
            header.Controls.Add(new Label
            {
                Text = "최근 실행 이력",
                Dock = DockStyle.Fill,
                Font = Globals.FontHeading,
                ForeColor = Globals.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            var btnAll = CreateModernButton("전체 보기", Globals.Surface, 0, 0, 112, 30, "list");
            btnAll.Dock = DockStyle.Fill;
            btnAll.Margin = new Padding(4, 2, 0, 2);
            btnAll.ForeColor = Globals.Accent;
            btnAll.IconColor = Globals.Accent;
            btnAll.BorderThickness = 0;
            btnAll.TextAlign = ContentAlignment.MiddleRight;
            btnAll.Click += (_, _) => OpenFullHistoryViewer();
            header.Controls.Add(btnAll, 1, 0);
            layout.Controls.Add(header, 0, 0);

            var columns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 7, RowCount = 1, Margin = new Padding(0), BackColor = Globals.SurfaceAlt };
            columns.ColumnStyles.Add(ColAbs(140));
            columns.ColumnStyles.Add(ColPct(100));
            columns.ColumnStyles.Add(ColAbs(220));
            columns.ColumnStyles.Add(ColAbs(90));
            columns.ColumnStyles.Add(ColAbs(90));
            columns.ColumnStyles.Add(ColAbs(100));
            columns.ColumnStyles.Add(ColAbs(32));
            columns.Controls.Add(CreateRecentColumnLabel("실행 시간", ContentAlignment.MiddleLeft), 0, 0);
            columns.Controls.Add(CreateRecentColumnLabel("시나리오 / 테스트명", ContentAlignment.MiddleLeft), 1, 0);
            columns.Controls.Add(CreateRecentColumnLabel("디바이스", ContentAlignment.MiddleLeft), 2, 0);
            columns.Controls.Add(CreateRecentColumnLabel("결과", ContentAlignment.MiddleCenter), 3, 0);
            columns.Controls.Add(CreateRecentColumnLabel("소요 시간", ContentAlignment.MiddleCenter), 4, 0);
            columns.Controls.Add(CreateRecentColumnLabel("테스트 유형", ContentAlignment.MiddleLeft), 5, 0);
            columns.Controls.Add(CreateRecentColumnLabel(string.Empty, ContentAlignment.MiddleCenter), 6, 0);
            layout.Controls.Add(columns, 0, 1);

            pnlRecentRuns = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Globals.Surface, Margin = new Padding(0) };
            layout.Controls.Add(pnlRecentRuns, 0, 2);
            card.Controls.Add(layout);
            return card;
        }

        private Label CreateRecentColumnLabel(string text, ContentAlignment align) => new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            Font = Globals.FontMuted,
            ForeColor = Globals.TextMuted,
            TextAlign = align,
            Padding = new Padding(6, 0, 6, 0)
        };

        private RoundedButton CreateQuickAction(string icon, string label, string description)
        {
            return new RoundedButton
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(5, 2, 5, 2),
                Text = label + "\n" + description,
                FillColor = Globals.Surface,
                HoverColor = Globals.InfoSoft,
                PressedColor = Globals.AccentSoft,
                ForeColor = Globals.TextPrimary,
                IconColor = Globals.Accent,
                IconName = icon,
                IconSize = 20,
                HorizontalPadding = 16,
                Font = Globals.FontMuted,
                MinimumFontSize = 7.5F,
                BorderRadius = Globals.RadiusSm,
                BorderColor = Globals.Border,
                BorderThickness = 1,
                TextAlign = ContentAlignment.MiddleLeft,
                TabStop = true
            };
        }

        private RoundedPanel CreateHomeBotStrip()
        {
            var strip = CreateCardDock(Globals.InfoSoft);
            strip.BorderColor = Color.FromArgb(191, 219, 254);
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 1,
                Padding = new Padding(12, 8, 12, 8),
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
            grid.ColumnStyles.Add(ColPct(30));
            grid.ColumnStyles.Add(ColPct(20));
            grid.ColumnStyles.Add(ColPct(20));
            grid.ColumnStyles.Add(ColPct(20));
            grid.ColumnStyles.Add(ColAbs(104));

            var intro = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = Color.Transparent, Margin = new Padding(0) };
            intro.ColumnStyles.Add(ColAbs(54)); intro.ColumnStyles.Add(ColPct(100));
            intro.RowStyles.Add(Abs(28)); intro.RowStyles.Add(Pct(100));
            var botIcon = new RoundedPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 10, 0), Padding = new Padding(12), FillColor = Globals.AccentSoft, BorderRadius = 22, BorderThickness = 0 };
            botIcon.Controls.Add(new IconGlyph { IconName = "appium", IconColor = Globals.Accent, Dock = DockStyle.Fill });
            intro.Controls.Add(botIcon, 0, 0); intro.SetRowSpan(botIcon, 2);
            intro.Controls.Add(new Label { Text = "Appium 봇  ·  준비 완료", Dock = DockStyle.Fill, Font = Globals.FontSub, ForeColor = Globals.TextPrimary, TextAlign = ContentAlignment.MiddleLeft }, 1, 0);
            intro.Controls.Add(new Label { Text = "명령어를 입력해 테스트를 자동화해 보세요.", Dock = DockStyle.Fill, Font = Globals.FontMuted, ForeColor = Globals.TextMuted, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = false }, 1, 1);
            grid.Controls.Add(intro, 0, 0);

            var natural = CreateBotFeatureButton("sparkles", "자연어 명령 지원", "한국어로 테스트를 설계하세요.");
            natural.Click += (_, _) => PrimeBotAssistant("현재 앱에서 수행할 테스트를 자연어로 설명하면 실행 가능한 Appium 단계로 만들어줘.", false);
            var explorer = CreateBotFeatureButton("target", "자동 요소 탐색", "현재 화면 요소를 찾아봅니다.");
            explorer.Click += (_, _) => ExploreCurrentUiElements();
            var smart = CreateBotFeatureButton("bolt", "스마트 테스트 생성", "화면 기반 시나리오를 만듭니다.");
            smart.Click += (_, _) => PrimeBotAssistant("현재 화면을 분석해서 핵심 사용자 흐름을 검증하는 테스트 시나리오를 자동으로 생성해줘.", true);
            grid.Controls.Add(natural, 1, 0);
            grid.Controls.Add(explorer, 2, 0);
            grid.Controls.Add(smart, 3, 0);

            var open = CreateModernButton("봇 열기", Globals.Accent, 0, 0, 100, 42, "play");
            open.Dock = DockStyle.Fill;
            open.Margin = new Padding(8, 18, 0, 18);
            open.TextAlign = ContentAlignment.MiddleCenter;
            open.Click += (_, _) => SwitchTab(pnlTabAuto, btnTabAuto);
            grid.Controls.Add(open, 4, 0);
            strip.Controls.Add(grid);
            return strip;
        }

        private RoundedButton CreateBotFeatureButton(string icon, string title, string description)
        {
            return new RoundedButton
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(4),
                Text = title + "\n" + description,
                FillColor = Globals.InfoSoft,
                HoverColor = Globals.Surface,
                PressedColor = Globals.AccentSoft,
                ForeColor = Globals.TextSecondary,
                IconColor = Globals.Accent,
                IconName = icon,
                IconSize = 18,
                HorizontalPadding = 10,
                Font = Globals.FontMuted,
                MinimumFontSize = 7.2F,
                BorderRadius = Globals.RadiusSm,
                BorderThickness = 0,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private void RefreshTestDashboard()
        {
            if (lblStatTotal == null || pnlRecentRuns == null) return;
            var history = LoadTestHistory().OrderBy(GetRecordTime).ToList();
            var executed = history.Where(r => GetRecordStatus(r) != "SKIPPED").ToList();
            int pass = executed.Count(r => GetRecordStatus(r) == "PASS");
            int fail = executed.Count(r => GetRecordStatus(r) is "FAIL" or "STOPPED");
            double rate = executed.Count == 0 ? 0D : pass * 100D / executed.Count;

            DateTime today = DateTime.Now.Date;
            DateTime weekStart = today.AddDays(-6);
            DateTime priorStart = weekStart.AddDays(-7);
            var weekRuns = executed.Where(r => GetRecordTime(r) >= weekStart).ToList();
            var priorRuns = executed.Where(r => GetRecordTime(r) >= priorStart && GetRecordTime(r) < weekStart).ToList();
            int weekPass = weekRuns.Count(r => GetRecordStatus(r) == "PASS");
            int weekFail = weekRuns.Count(r => GetRecordStatus(r) is "FAIL" or "STOPPED");
            double weekRate = weekRuns.Count == 0 ? 0D : weekPass * 100D / weekRuns.Count;
            int priorPass = priorRuns.Count(r => GetRecordStatus(r) == "PASS");
            double priorRate = priorRuns.Count == 0 ? 0D : priorPass * 100D / priorRuns.Count;
            double rateDelta = weekRate - priorRate;

            lblStatTotal.Text = executed.Count.ToString("N0");
            lblStatPass.Text = pass.ToString("N0");
            lblStatFail.Text = fail.ToString("N0");
            lblStatRate.Text = rate.ToString("0.0") + "%";
            if (lblStatTotalTrend != null) lblStatTotalTrend.Text = $"{weekRuns.Count:N0}건";
            if (lblStatPassTrend != null) lblStatPassTrend.Text = weekRuns.Count == 0 ? "-" : $"{weekPass * 100D / weekRuns.Count:0.0}%";
            if (lblStatFailTrend != null) lblStatFailTrend.Text = weekRuns.Count == 0 ? "-" : $"{weekFail * 100D / weekRuns.Count:0.0}%";
            if (lblStatRateTrend != null)
            {
                lblStatRateTrend.Text = priorRuns.Count == 0 ? $"{weekRate:0.0}%" : $"{(rateDelta >= 0 ? "+" : "")}{rateDelta:0.0}%p";
                lblStatRateTrend.ForeColor = rateDelta >= 0 ? Globals.Success : Globals.Danger;
            }
            RefreshRecentRuns(history.AsEnumerable().Reverse().Take(5).ToList());
            RefreshHomeDeviceMeta();
        }

        private void RefreshRecentRuns(System.Collections.Generic.IReadOnlyList<TestRunRecord> recent)
        {
            pnlRecentRuns.SuspendLayout();
            pnlRecentRuns.Controls.Clear();

            if (recent.Count == 0)
            {
                pnlRecentRuns.Controls.Add(new Label
                {
                    Text = "아직 실행 이력이 없습니다. Appium 봇에서 첫 시나리오를 실행해 보세요.",
                    Dock = DockStyle.Fill,
                    Margin = new Padding(0),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = Globals.FontBody,
                    ForeColor = Globals.TextMuted
                });
                pnlRecentRuns.ResumeLayout();
                return;
            }

            // DPI 환경에서 TableLayoutPanel 전체 높이를 고정값으로 계산하면
            // 행 높이와 컨테이너 높이의 스케일이 달라져 라벨의 베이스라인이 잘릴 수 있다.
            // 각 행을 독립적인 Dock=Top 컨테이너로 구성해 텍스트가 항상 행 안에서 그려지게 한다.
            int rowHeight = Math.Max(46, Globals.FontBody.Height + 24);
            for (int i = recent.Count - 1; i >= 0; i--)
            {
                TestRunRecord record = recent[i];
                string status = GetRecordStatus(record);
                string statusText = status == "PASS" ? "✓ 성공" : status == "FAIL" ? "⊗ 실패" : status == "STOPPED" ? "중지" : "건너뜀";
                string testType = record.steps != null && record.steps.Any(step => step.raw.Contains("ScreenAssert", StringComparison.OrdinalIgnoreCase)) ? "회귀 테스트" : "기능 테스트";
                DateTime time = GetRecordTime(record);

                var rowHost = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = rowHeight,
                    Margin = new Padding(0),
                    Padding = new Padding(0),
                    BackColor = i % 2 == 0 ? Globals.Surface : Globals.SurfaceAlt,
                    Cursor = Cursors.Hand
                };

                var row = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 7,
                    RowCount = 1,
                    Margin = new Padding(0),
                    Padding = new Padding(0),
                    BackColor = rowHost.BackColor
                };
                row.ColumnStyles.Add(ColAbs(140));
                row.ColumnStyles.Add(ColPct(100));
                row.ColumnStyles.Add(ColAbs(220));
                row.ColumnStyles.Add(ColAbs(90));
                row.ColumnStyles.Add(ColAbs(90));
                row.ColumnStyles.Add(ColAbs(100));
                row.ColumnStyles.Add(ColAbs(32));
                row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

                row.Controls.Add(CreateRunCell(time == DateTime.MinValue ? "-" : time.ToString("yyyy-MM-dd HH:mm:ss"), ContentAlignment.MiddleLeft, Globals.TextSecondary), 0, 0);
                row.Controls.Add(CreateRunCell(record.scenario, ContentAlignment.MiddleLeft, Globals.TextPrimary), 1, 0);
                string device = string.IsNullOrWhiteSpace(record.deviceModel) ? "-" : record.deviceModel + (string.IsNullOrWhiteSpace(record.osVersion) ? "" : " (" + record.osVersion + ")");
                row.Controls.Add(CreateRunCell(device, ContentAlignment.MiddleLeft, Globals.TextSecondary), 2, 0);
                row.Controls.Add(CreateRunStatusChip(status, statusText), 3, 0);
                row.Controls.Add(CreateRunCell(FormatDuration(record.durationMs), ContentAlignment.MiddleCenter, Globals.TextSecondary), 4, 0);
                row.Controls.Add(CreateRunCell(testType, ContentAlignment.MiddleLeft, Globals.TextSecondary), 5, 0);

                var menu = CreateRunCell("⋮", ContentAlignment.MiddleCenter, Globals.TextMuted);
                menu.Font = Globals.FontSub;
                row.Controls.Add(menu, 6, 0);

                EventHandler show = (_, _) => ShowRunDetails(record);
                rowHost.DoubleClick += show;
                row.DoubleClick += show;
                foreach (Control child in row.Controls)
                {
                    child.Cursor = Cursors.Hand;
                    child.DoubleClick += show;
                }
                menu.Click += show;

                rowHost.Controls.Add(row);
                pnlRecentRuns.Controls.Add(rowHost);
            }

            pnlRecentRuns.ResumeLayout(true);
        }

        private Control CreateRunStatusChip(string status, string text)
        {
            Color color = status == "PASS" ? Globals.Success : status == "STOPPED" ? Globals.Warning : status == "SKIPPED" ? Globals.TextMuted : Globals.Danger;
            Color fill = status == "PASS" ? Globals.SuccessSoft : status == "STOPPED" ? Globals.WarningSoft : status == "SKIPPED" ? Globals.SurfaceRaised : Globals.DangerSoft;
            var host = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(10, 8, 10, 8) };
            var chip = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                FillColor = fill,
                BorderColor = Color.Transparent,
                BorderThickness = 0,
                BorderRadius = 10,
                Padding = new Padding(0)
            };
            chip.Controls.Add(new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                Font = Globals.FontMuted,
                ForeColor = color,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            });
            host.Controls.Add(chip);
            return host;
        }

        private Label CreateRunCell(string text, ContentAlignment align, Color color) => new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            AutoSize = false,
            Font = Globals.FontMuted,
            ForeColor = color,
            TextAlign = align,
            AutoEllipsis = true,
            UseMnemonic = false,
            Padding = new Padding(6, 0, 6, 0)
        };

        private void ShowRunDetails(TestRunRecord record)
        {
            string status = GetRecordStatus(record);
            string message =
                $"시나리오: {record.scenario}\n" +
                $"결과: {status}\n" +
                $"실행 시각: {GetRecordTime(record):yyyy-MM-dd HH:mm:ss}\n" +
                $"기기: {(string.IsNullOrWhiteSpace(record.deviceModel) ? "-" : record.deviceModel)}\n" +
                $"OS: {(string.IsNullOrWhiteSpace(record.osVersion) ? "-" : record.osVersion)}\n" +
                $"단계: {record.totalSteps:N0}\n" +
                $"소요 시간: {FormatDuration(record.durationMs)}";
            TestStepRecord? failedStep = record.steps?.FirstOrDefault(step =>
                string.Equals(step.status, "FAIL", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(step.status, "STOPPED", StringComparison.OrdinalIgnoreCase));
            if (failedStep != null)
            {
                message += $"\n\n실패 단계: #{failedStep.index} · {failedStep.raw}";
                if (failedStep.durationMs > 0) message += $"\n단계 소요: {FormatDuration(failedStep.durationMs)}";
                if (failedStep.matchRate.HasValue) message += $"\n화면 일치율: {failedStep.matchRate.Value:0.00}%";
                if (!string.IsNullOrWhiteSpace(failedStep.message)) message += "\n단계 오류: " + failedStep.message;
                if (!string.IsNullOrWhiteSpace(failedStep.artifactFolder)) message += "\n결과 폴더: " + failedStep.artifactFolder;
            }
            if (!string.IsNullOrWhiteSpace(record.failMessage))
                message += "\n\n오류 내용:\n" + record.failMessage;

            MessageBox.Show(
                message,
                "실행 상세",
                MessageBoxButtons.OK,
                status == "PASS" ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        private static string GetRecordStatus(TestRunRecord record)
        {
            if (!string.IsNullOrWhiteSpace(record.status)) return record.status.Trim().ToUpperInvariant();
            if (!record.pass && string.Equals(record.failMessage?.Trim(), "사용자 중지", StringComparison.OrdinalIgnoreCase)) return "STOPPED";
            return record.pass ? "PASS" : "FAIL";
        }

        private static DateTime GetRecordTime(TestRunRecord record)
        {
            if (DateTime.TryParse(record.timestamp, out DateTime timestamp)) return timestamp.ToLocalTime();
            if (DateTime.TryParse(record.startedAt, out DateTime startedAt)) return startedAt.ToLocalTime();
            return DateTime.MinValue;
        }

        private static string FormatDuration(long durationMs)
        {
            if (durationMs <= 0) return "-";
            TimeSpan duration = TimeSpan.FromMilliseconds(durationMs);
            if (duration.TotalMinutes < 1) return duration.TotalSeconds.ToString("0.0") + "s";
            if (duration.TotalHours < 1) return $"{(int)duration.TotalMinutes:00}:{duration.Seconds:00}";
            return $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
        }

        private async void StartDashboard()
        {
            string dashPath = GetOrAskDashboardPath();
            if (string.IsNullOrWhiteSpace(dashPath)) return;

            string startFile = Path.Combine(dashPath, "start_dashboard.bat");
            if (!Directory.Exists(dashPath) || !File.Exists(startFile))
            {
                MessageBox.Show(
                    "기존 대시보드 폴더 또는 start_dashboard.bat 파일을 찾을 수 없습니다.\n\n" + dashPath,
                    "대시보드 시작 실패",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            try
            {
                // 기존 대시보드도 같은 Appium Server Manager를 사용한다.
                if (!await EnsureAppiumServerReadyAsync("기존 Appium 대시보드 실행")) return;

                if (dashboardProcess != null && !dashboardProcess.HasExited)
                    AdbEngine.TryKill(dashboardProcess);

                dashboardProcess = AdbEngine.StartProcess("cmd.exe", "/c start_dashboard.bat", false, dashPath);
                lblStatusMsg.Text = "상태: 기존 Appium 대시보드 시작 중...";
                lblStatusMsg.ForeColor = Globals.Info;

                _ = Task.Run(async () =>
                {
                    await Task.Delay(4000);
                    try
                    {
                        Process.Start(new ProcessStartInfo("http://127.0.0.1:8000") { UseShellExecute = true });
                        if (!IsDisposed && IsHandleCreated)
                        {
                            BeginInvoke(new Action(() =>
                            {
                                lblStatusMsg.Text = "상태: 기존 Appium 대시보드 실행 중 · 127.0.0.1:8000";
                                lblStatusMsg.ForeColor = Globals.Success;
                            }));
                        }
                    }
                    catch
                    {
                        if (!IsDisposed && IsHandleCreated)
                        {
                            BeginInvoke(new Action(() =>
                            {
                                lblStatusMsg.Text = "상태: 대시보드는 시작했지만 브라우저를 열지 못했습니다.";
                                lblStatusMsg.ForeColor = Globals.Warning;
                            }));
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                lblStatusMsg.Text = "상태: 기존 대시보드 시작 실패";
                lblStatusMsg.ForeColor = Globals.Danger;
                MessageBox.Show(
                    "기존 Appium 대시보드를 시작하지 못했습니다.\n\n" + ex.Message,
                    "대시보드 시작 실패",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private string GetOrAskDashboardPath()
        {
            string keyPath = Path.Combine(Globals.LogFolder, "dashboard_path.txt");
            if (File.Exists(keyPath))
            {
                string saved = File.ReadAllText(keyPath).Trim();
                if (!string.IsNullOrWhiteSpace(saved) &&
                    Directory.Exists(saved) &&
                    File.Exists(Path.Combine(saved, "start_dashboard.bat")))
                    return saved;
            }

            string input = ShowInputDialog(
                "기존 대시보드 API 폴더 경로를 입력하세요.\n(start_dashboard.bat 파일이 들어있는 폴더)",
                "대시보드 경로 설정");
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            input = input.Trim().Trim('"');
            if (!Directory.Exists(input) || !File.Exists(Path.Combine(input, "start_dashboard.bat")))
            {
                MessageBox.Show(
                    "선택한 폴더에서 start_dashboard.bat 파일을 찾을 수 없습니다.\n\n" + input,
                    "대시보드 경로 오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return string.Empty;
            }

            Directory.CreateDirectory(Globals.LogFolder);
            File.WriteAllText(keyPath, input);
            return input;
        }
    }
}
