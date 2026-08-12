using System;
using System.Drawing;
using System.Windows.Forms;
using AppiumBuilder.Utils;
using AppiumBuilder.UI;

namespace AppiumBuilder
{
    public partial class MainForm
    {
        // ===== Soft Blue Office 디자인 헬퍼 (외부 의존성 없음) =====
        private static Color Lighten(Color c, float amount)
        {
            amount = Math.Clamp(amount, 0f, 1f);
            int r = c.R + (int)((255 - c.R) * amount);
            int g = c.G + (int)((255 - c.G) * amount);
            int b = c.B + (int)((255 - c.B) * amount);
            return Color.FromArgb(c.A, Math.Min(255, r), Math.Min(255, g), Math.Min(255, b));
        }

        private static Color Darken(Color c, float amount)
        {
            amount = Math.Clamp(amount, 0f, 1f);
            return Color.FromArgb(
                c.A,
                Math.Max(0, (int)(c.R * (1f - amount))),
                Math.Max(0, (int)(c.G * (1f - amount))),
                Math.Max(0, (int)(c.B * (1f - amount))));
        }

        private static bool IsNeutralButton(Color color) =>
            color == Globals.Surface ||
            color == Globals.SurfaceAlt ||
            color == Globals.SurfaceRaised ||
            color == Globals.Sidebar ||
            color == Globals.SidebarActive ||
            color == Globals.SuccessSoft ||
            color == Globals.WarningSoft ||
            color == Globals.DangerSoft ||
            color == Globals.InfoSoft;

        private static Color ResolveButtonForeground(Color color)
        {
            if (color == Globals.SuccessSoft) return Globals.Success;
            if (color == Globals.WarningSoft) return Globals.Warning;
            if (color == Globals.DangerSoft) return Globals.Danger;
            if (color == Globals.InfoSoft || color == Globals.SidebarActive) return Globals.Accent;
            if (IsNeutralButton(color)) return Globals.TextPrimary;
            return Color.White;
        }

        private static Color ResolveHover(Color color)
        {
            if (color == Globals.Accent) return Globals.AccentHover;
            if (color == Globals.Surface || color == Globals.SurfaceAlt) return Globals.SurfaceRaised;
            if (color == Globals.Sidebar) return Globals.SidebarActive;
            if (color == Globals.SuccessSoft || color == Globals.WarningSoft ||
                color == Globals.DangerSoft || color == Globals.InfoSoft || color == Globals.SidebarActive)
                return Darken(color, 0.04f);
            return Lighten(color, 0.10f);
        }

        private static Color ResolvePressed(Color color)
        {
            if (color == Globals.Accent) return Globals.AccentPressed;
            return Darken(color, 0.10f);
        }

        private RoundedPanel CreateCardDock(Color? fill = null)
        {
            return new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(6),
                FillColor = fill ?? Globals.Surface,
                BorderColor = Globals.Border,
                BorderThickness = 1,
                BorderRadius = Globals.Radius
            };
        }

        private RoundedPanel CreateCard(int w, int h, int x, int y, Color? fill = null)
        {
            return new RoundedPanel
            {
                Size = new Size(w, h),
                Location = new Point(x, y),
                FillColor = fill ?? Globals.Surface,
                BorderColor = Globals.Border,
                BorderThickness = 1,
                BorderRadius = Globals.Radius
            };
        }

        private RoundedButton CreateModernButton(
            string text,
            Color color,
            int x,
            int y,
            int w,
            int h,
            string? icon = null)
        {
            bool neutral = IsNeutralButton(color);
            var button = new RoundedButton
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, h),
                FillColor = color,
                HoverColor = ResolveHover(color),
                PressedColor = ResolvePressed(color),
                ForeColor = ResolveButtonForeground(color),
                IconColor = ResolveButtonForeground(color),
                Font = Globals.FontSub,
                MinimumFontSize = 8.5F,
                AutoFitText = false,
                BorderRadius = Globals.RadiusSm,
                BorderColor = neutral ? Globals.Border : Color.Transparent,
                BorderThickness = neutral ? 1 : 0,
                TextAlign = icon != null ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleCenter,
                IconName = icon,
                IconSize = 14,
                IconGap = 6,
                HorizontalPadding = 10,
                TabStop = true
            };

            // 고정 폭보다 실제 문구 폭을 우선한다. 부모가 좁다면 Responsive Layout이 재배치하거나 스크롤한다.
            Size preferred = button.GetPreferredSize(Size.Empty);
            button.MinimumSize = new Size(Math.Max(0, preferred.Width), Math.Max(32, h));
            return button;
        }

        private RoundedButton CreateModernButtonSize(
            string text,
            Color color,
            int w,
            int h,
            string? icon = null)
        {
            var button = CreateModernButton(text, color, 0, 0, w, h, icon);
            button.Margin = new Padding(0, 0, 8, 0);
            return button;
        }

        private RoundedButton CreateMenuButton(string text, string icon, int y)
        {
            return new RoundedButton
            {
                Text = text,
                Location = new Point(8, y),
                Size = new Size(Math.Max(140, Globals.SidebarWidth - 16), Globals.MenuHeight),
                FillColor = Globals.Sidebar,
                HoverColor = Globals.SurfaceAlt,
                PressedColor = Globals.AccentSoft,
                ForeColor = Globals.SidebarTextMuted,
                IconColor = Globals.SidebarTextMuted,
                IconName = icon,
                IconSize = 17,
                HorizontalPadding = 14,
                Font = Globals.FontSub,
                MinimumFontSize = 7.5F,
                BorderRadius = Globals.RadiusSm,
                TextAlign = ContentAlignment.MiddleLeft,
                TabStop = true
            };
        }

        private TextBox CreatePlaceholderTextBox(string placeholder, int x, int y, int w, int h)
        {
            var t = new ModernTextBox
            {
                Multiline = false,
                AutoSize = false,
                WordWrap = false,
                Text = placeholder,
                Tag = placeholder,
                Location = new Point(x, y),
                Size = new Size(w, h),
                ForeColor = Globals.TextFaint,
                Font = Globals.FontBody,
                BackColor = Globals.SurfaceAlt,
                BorderColor = Globals.Border,
                FocusBorderColor = Globals.Accent,
                TabStop = true
            };

            t.Enter += (_, _) =>
            {
                if (t.Text == placeholder)
                {
                    t.Text = string.Empty;
                    t.ForeColor = Globals.TextPrimary;
                }
            };
            t.Leave += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(t.Text))
                {
                    t.Text = placeholder;
                    t.ForeColor = Globals.TextFaint;
                }
            };
            t.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter) e.SuppressKeyPress = true;
            };
            return t;
        }

        private TextBox CreatePlaceholderTextBoxDock(string placeholder)
        {
            var t = new ModernTextBox
            {
                Multiline = false,
                AutoSize = false,
                WordWrap = false,
                Dock = DockStyle.Fill,
                Text = placeholder,
                Tag = placeholder,
                ForeColor = Globals.TextFaint,
                Font = Globals.FontBody,
                BackColor = Globals.SurfaceAlt,
                BorderColor = Globals.Border,
                FocusBorderColor = Globals.Accent,
                TabStop = true
            };

            t.Enter += (_, _) =>
            {
                if (t.Text == placeholder)
                {
                    t.Text = string.Empty;
                    t.ForeColor = Globals.TextPrimary;
                }
            };
            t.Leave += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(t.Text))
                {
                    t.Text = placeholder;
                    t.ForeColor = Globals.TextFaint;
                }
            };
            t.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter) e.SuppressKeyPress = true;
            };
            return t;
        }

        private ComboBox CreateFlatCombo(int x, int y, int w, int h)
        {
            return new ModernComboBox
            {
                Location = new Point(x, y),
                Size = new Size(w, h),
                BackColor = Globals.SurfaceAlt,
                ForeColor = Globals.TextPrimary,
                Font = Globals.FontBody,
                TabStop = true
            };
        }

        private Label SectionLabel(string text, int x, int y, Color? color = null)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                Font = Globals.FontSub,
                ForeColor = color ?? Globals.TextSecondary,
                AutoSize = true
            };
        }

        private RoundedPanel Dot(Color color, int size = 10)
        {
            return new RoundedPanel
            {
                Size = new Size(size, size),
                FillColor = color,
                BorderRadius = size / 2,
                BorderThickness = 0
            };
        }

        /// <summary>공통 페이지 헤더: 제목 + 한 줄 설명.</summary>
        private Panel CreatePageHeader(string title, string description)
        {
            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Globals.Bg,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0, 2, 0, 6)
            };
            header.ColumnStyles.Add(ColPct(100));
            header.ColumnStyles.Add(ColAbs(196));
            header.RowStyles.Add(Pct(100));

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
            textGrid.RowStyles.Add(Abs(40));
            textGrid.RowStyles.Add(Abs(24));
            textGrid.Controls.Add(new Label
            {
                Text = title,
                Dock = DockStyle.Fill,
                Font = Globals.FontPageTitle,
                ForeColor = Globals.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = false
            }, 0, 0);
            textGrid.Controls.Add(new Label
            {
                Text = description,
                Dock = DockStyle.Fill,
                Font = Globals.FontMuted,
                ForeColor = Globals.TextMuted,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = false
            }, 0, 1);
            header.Controls.Add(textGrid, 0, 0);

            var actionGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0, 8, 0, 12),
                BackColor = Color.Transparent
            };
            actionGrid.ColumnStyles.Add(ColPct(50));
            actionGrid.ColumnStyles.Add(ColPct(50));

            var btnRefresh = CreateModernButton("새로고침", Globals.Surface, 0, 0, 88, 34, "refresh");
            btnRefresh.Size = new Size(88, 38);
            btnRefresh.Anchor = AnchorStyles.None;
            btnRefresh.Margin = new Padding(0);
            btnRefresh.ForeColor = Globals.TextSecondary;
            btnRefresh.IconColor = Globals.TextSecondary;
            btnRefresh.BorderColor = Globals.Border;
            btnRefresh.BorderThickness = 1;
            btnRefresh.TextAlign = ContentAlignment.MiddleCenter;
            btnRefresh.Click += (_, _) => RefreshCurrentWorkspace();

            var btnSettings = CreateModernButton("설정", Globals.Surface, 0, 0, 82, 34, "settings");
            btnSettings.Size = new Size(82, 38);
            btnSettings.Anchor = AnchorStyles.None;
            btnSettings.Margin = new Padding(0);
            btnSettings.ForeColor = Globals.TextSecondary;
            btnSettings.IconColor = Globals.TextSecondary;
            btnSettings.BorderColor = Globals.Border;
            btnSettings.BorderThickness = 1;
            btnSettings.TextAlign = ContentAlignment.MiddleCenter;
            btnSettings.Click += (_, _) => OpenWorkspaceSettings();

            actionGrid.Controls.Add(btnRefresh, 0, 0);
            actionGrid.Controls.Add(btnSettings, 1, 0);
            header.Controls.Add(actionGrid, 1, 0);
            return header;
        }

        private static RowStyle Abs(int px) => new RowStyle(SizeType.Absolute, px);
        private static RowStyle Pct(float pct) => new RowStyle(SizeType.Percent, pct);
        private static ColumnStyle ColAbs(int px) => new ColumnStyle(SizeType.Absolute, px);
        private static ColumnStyle ColPct(float pct) => new ColumnStyle(SizeType.Percent, pct);

        private TableLayoutPanel EqualColumnGrid(int columns, int gap = 12)
        {
            var t = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = columns,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            t.RowStyles.Add(Pct(100));
            for (int i = 0; i < columns; i++) t.ColumnStyles.Add(ColPct(100f / columns));
            return t;
        }

        private static string ShowInputDialog(string prompt, string title)
        {
            using var dlg = new Form
            {
                Text = title,
                Size = new Size(440, 205),
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Globals.Surface,
                ForeColor = Globals.TextPrimary,
                Font = Globals.FontBody
            };

            var lbl = new Label
            {
                Text = prompt,
                Location = new Point(16, 16),
                Size = new Size(400, 46),
                ForeColor = Globals.TextPrimary
            };
            var txt = new ModernTextBox
            {
                Location = new Point(16, 70),
                Size = new Size(400, 36),
                BackColor = Globals.SurfaceAlt,
                ForeColor = Globals.TextPrimary,
                BorderColor = Globals.Border,
                FocusBorderColor = Globals.Accent
            };
            var btnOk = new RoundedButton
            {
                Text = "확인",
                Location = new Point(250, 116),
                Size = new Size(80, 36),
                DialogResult = DialogResult.OK,
                FillColor = Globals.Accent,
                HoverColor = Globals.AccentHover,
                PressedColor = Globals.AccentPressed,
                ForeColor = Color.White,
                BorderRadius = Globals.RadiusSm,
                TextAlign = ContentAlignment.MiddleCenter
            };
            var btnCancel = new RoundedButton
            {
                Text = "취소",
                Location = new Point(336, 116),
                Size = new Size(80, 36),
                DialogResult = DialogResult.Cancel,
                FillColor = Globals.Surface,
                HoverColor = Globals.SurfaceRaised,
                PressedColor = Globals.SurfaceAlt,
                ForeColor = Globals.TextSecondary,
                BorderColor = Globals.Border,
                BorderThickness = 1,
                BorderRadius = Globals.RadiusSm,
                TextAlign = ContentAlignment.MiddleCenter
            };

            dlg.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnCancel });
            dlg.AcceptButton = btnOk;
            dlg.CancelButton = btnCancel;
            return dlg.ShowDialog() == DialogResult.OK ? txt.Text : string.Empty;
        }
    }

    public class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }
    }
}
