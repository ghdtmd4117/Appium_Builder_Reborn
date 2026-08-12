using System;
using System.Drawing;
using System.Windows.Forms;
using AppiumBuilder.Utils;

namespace AppiumBuilder.UI
{
    public sealed class WorkspaceSettingsForm : Form
    {
        public event EventHandler? OpenEnvironmentDiagnostics;
        public event EventHandler? OpenRetentionSettings;
        public event EventHandler? OpenVisualBaselines;
        public event EventHandler? OpenScenarioVersions;

        public WorkspaceSettingsForm()
        {
            Text = "Appium Builder 설정";
            Size = new Size(760, 620);
            MinimumSize = new Size(700, 560);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Globals.Bg;
            ForeColor = Globals.TextPrimary;
            Font = Globals.FontBody;
            AutoScaleMode = AutoScaleMode.Dpi;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(22, 18, 22, 18),
                BackColor = Globals.Bg,
                Margin = new Padding(0)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            header.Controls.Add(new Label
            {
                Text = "작업 공간 설정",
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                Font = Globals.FontTitle,
                ForeColor = Globals.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            header.Controls.Add(new Label
            {
                Text = "환경 진단과 로그 보존, Visual Assert, 시나리오 버전을 관리합니다.",
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                Font = Globals.FontMuted,
                ForeColor = Globals.TextMuted,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 1);
            root.Controls.Add(header, 0, 0);

            var actions = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            for (int i = 0; i < 4; i++) actions.RowStyles.Add(new RowStyle(SizeType.Percent, 25));

            actions.Controls.Add(CreateAction("tools", "환경 건강검진", "ADB, Python, Appium과 연결된 Android 기기 상태를 한 번에 검사합니다.", (_, _) => OpenEnvironmentDiagnostics?.Invoke(this, EventArgs.Empty)), 0, 0);
            actions.Controls.Add(CreateAction("terminal", "로그 보존 정책", "로그 보존 기간과 최대 저장 용량을 설정하고 오래된 실행 파일을 정리합니다.", (_, _) => OpenRetentionSettings?.Invoke(this, EventArgs.Empty)), 0, 1);
            actions.Controls.Add(CreateAction("camera", "Visual Baseline", "화면 비교 기준 이미지, 일치율 임계값과 동적 영역 Mask를 관리합니다.", (_, _) => OpenVisualBaselines?.Invoke(this, EventArgs.Empty)), 0, 2);
            actions.Controls.Add(CreateAction("list", "시나리오 버전", "저장된 시나리오의 변경 이력을 확인하고 원하는 버전으로 안전하게 복원합니다.", (_, _) => OpenScenarioVersions?.Invoke(this, EventArgs.Empty)), 0, 3);
            root.Controls.Add(actions, 0, 1);

            root.Controls.Add(new Label
            {
                Text = "테마 · Soft Blue Office  /  Responsive QA R9",
                Dock = DockStyle.Fill,
                Margin = new Padding(4, 8, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Globals.TextMuted,
                Font = Globals.FontMuted
            }, 0, 2);

            Controls.Add(root);
        }

        private Control CreateAction(string icon, string title, string description, EventHandler click)
        {
            var card = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 5, 0, 5),
                Padding = new Padding(16, 12, 14, 12),
                FillColor = Globals.Surface,
                BorderColor = Globals.Border,
                BorderThickness = 1,
                BorderRadius = Globals.Radius
            };

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var iconHost = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 2, 12, 2),
                Padding = new Padding(10),
                FillColor = Globals.InfoSoft,
                BorderThickness = 0,
                BorderRadius = Globals.RadiusSm
            };
            var glyph = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            glyph.Paint += (_, e) =>
            {
                float size = Math.Min(glyph.Width, glyph.Height) * 0.72f;
                if (size < 8f) size = Math.Min(glyph.Width, glyph.Height);
                var rect = new RectangleF((glyph.Width - size) / 2f, (glyph.Height - size) / 2f, size, size);
                LineIcons.Draw(e.Graphics, icon, rect, Globals.Accent);
            };
            iconHost.Controls.Add(glyph);
            grid.Controls.Add(iconHost, 0, 0);
            grid.SetRowSpan(iconHost, 2);

            grid.Controls.Add(new Label
            {
                Text = title,
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                AutoSize = false,
                Font = Globals.FontHeading,
                ForeColor = Globals.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            }, 1, 0);

            grid.Controls.Add(new Label
            {
                Text = description,
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                AutoSize = false,
                AutoEllipsis = false,
                Font = Globals.FontMuted,
                ForeColor = Globals.TextMuted,
                TextAlign = ContentAlignment.MiddleLeft
            }, 1, 1);

            var button = new RoundedButton
            {
                Text = "열기",
                Dock = DockStyle.Fill,
                Margin = new Padding(12, 12, 0, 12),
                FillColor = Globals.SurfaceAlt,
                HoverColor = Globals.SurfaceRaised,
                PressedColor = Globals.AccentSoft,
                ForeColor = Globals.Accent,
                BorderColor = Globals.Border,
                BorderThickness = 1,
                BorderRadius = Globals.RadiusSm,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoFitText = false
            };
            button.Click += click;
            grid.Controls.Add(button, 2, 0);
            grid.SetRowSpan(button, 2);

            card.Controls.Add(grid);
            return card;
        }
    }
}
