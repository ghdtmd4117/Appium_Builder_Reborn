using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AppiumBuilder.Utils;

namespace AppiumBuilder.UI
{
    internal static class PaintSurface
    {
        public static Color ResolveOpaqueBackColor(Control? control)
        {
            Control? current = control;
            int guard = 0;
            while (current != null && guard++ < 32)
            {
                Color color = current.BackColor;
                if (color.A == 255 && color != Color.Transparent)
                    return color;
                current = current.Parent;
            }
            return Globals.Bg;
        }
    }

    public class RoundedPanel : Panel
    {
        private Color _fillColor = Globals.Surface;
        private Color _borderColor = Globals.Border;
        private int _borderRadius = 10;
        private int _borderThickness = 1;

        public Color FillColor
        {
            get => _fillColor;
            set { _fillColor = value; BackColor = value; Invalidate(); }
        }

        public Color BorderColor
        {
            get => _borderColor;
            set { _borderColor = value; Invalidate(); }
        }

        public int BorderRadius
        {
            get => _borderRadius;
            set { _borderRadius = Math.Max(0, value); Invalidate(); }
        }

        public int BorderThickness
        {
            get => _borderThickness;
            set { _borderThickness = Math.Max(0, value); Invalidate(); }
        }

        public RoundedPanel()
        {
            BackColor = _fillColor;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.OptimizedDoubleBuffer, true);
        }

        internal static GraphicsPath RoundRectPath(Rectangle rect, int radius)
        {
            if (radius <= 0)
            {
                var square = new GraphicsPath();
                square.AddRectangle(rect);
                return square;
            }

            int d = Math.Max(1, radius * 2);
            if (d > rect.Width) d = rect.Width;
            if (d > rect.Height) d = rect.Height;

            var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using var brush = new SolidBrush(PaintSurface.ResolveOpaqueBackColor(Parent));
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color behind = PaintSurface.ResolveOpaqueBackColor(Parent);
            using (var bgBrush = new SolidBrush(behind))
                g.FillRectangle(bgBrush, ClientRectangle);

            if (Width <= 1 || Height <= 1) return;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = RoundRectPath(rect, BorderRadius);
            using var fillBrush = new SolidBrush(FillColor);
            g.FillPath(fillBrush, path);

            if (BorderThickness > 0)
            {
                using var pen = new Pen(BorderColor, BorderThickness) { Alignment = PenAlignment.Inset };
                g.DrawPath(pen, path);
            }
        }
    }

    /// <summary>
    /// Soft Blue Office 공통 버튼. Hover / Pressed / Disabled / keyboard focus 상태를 분리한다.
    /// </summary>
    public class RoundedButton : Button
    {
        private bool _hover;
        private bool _pressed;
        private Color _fillColor = Globals.SurfaceAlt;

        public Color FillColor
        {
            get => _fillColor;
            set
            {
                _fillColor = value;
                if (value.A == 255)
                {
                    BackColor = value;
                    FlatAppearance.MouseDownBackColor = value;
                    FlatAppearance.MouseOverBackColor = value;
                }
                Invalidate();
            }
        }
        public Color HoverColor { get; set; } = Color.Empty;
        public Color PressedColor { get; set; } = Color.Empty;
        public Color BorderColor { get; set; } = Color.Transparent;
        public int BorderThickness { get; set; }
        public Color FocusColor { get; set; } = Globals.Accent;
        public Color DisabledFillColor { get; set; } = Globals.SurfaceRaised;
        public Color DisabledForeColor { get; set; } = Globals.TextFaint;
        public int BorderRadius { get; set; } = 8;
        public string? IconName { get; set; }
        public Color IconColor { get; set; } = Color.Empty;
        public int IconSize { get; set; } = 14;
        public int HorizontalPadding { get; set; } = 10;
        public int IconGap { get; set; } = 6;
        public bool AutoFitText { get; set; } = true;
        public float MinimumFontSize { get; set; } = 8F;

        public RoundedButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.Selectable, true);

            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            FlatAppearance.MouseDownBackColor = _fillColor;
            FlatAppearance.MouseOverBackColor = _fillColor;
            BackColor = _fillColor;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            if (mevent.Button == MouseButtons.Left) _pressed = true;
            Invalidate();
            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            _pressed = false;
            Invalidate();
            base.OnMouseUp(mevent);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            Invalidate();
            base.OnGotFocus(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            Invalidate();
            base.OnLostFocus(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            Invalidate();
            base.OnEnabledChanged(e);
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

        private TextFormatFlags GetTextFlags(bool multiline)
        {
            var flags = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            if (multiline)
                flags |= TextFormatFlags.WordBreak;
            else
                flags |= TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;

            switch (TextAlign)
            {
                case ContentAlignment.MiddleLeft:
                    flags |= TextFormatFlags.Left;
                    break;
                case ContentAlignment.MiddleRight:
                    flags |= TextFormatFlags.Right;
                    break;
                default:
                    flags |= TextFormatFlags.HorizontalCenter;
                    break;
            }

            return flags;
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            bool multiline = !string.IsNullOrEmpty(Text) && (Text.Contains('\n') || Text.Contains('\r'));
            TextFormatFlags flags = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            flags |= multiline ? TextFormatFlags.WordBreak : TextFormatFlags.SingleLine;

            int maxWidth = proposedSize.Width > 0 ? proposedSize.Width : int.MaxValue;
            int textLimit = maxWidth == int.MaxValue
                ? 10000
                : Math.Max(1, maxWidth - HorizontalPadding * 2 - (string.IsNullOrWhiteSpace(IconName) ? 0 : Math.Max(12, IconSize) + IconGap));
            Size measured = string.IsNullOrWhiteSpace(Text)
                ? Size.Empty
                : TextRenderer.MeasureText(Text, Font, new Size(textLimit, 10000), flags);

            int iconWidth = string.IsNullOrWhiteSpace(IconName) ? 0 : Math.Max(12, IconSize);
            int gap = iconWidth > 0 && measured.Width > 0 ? Math.Max(0, IconGap) : 0;
            int width = measured.Width + iconWidth + gap + Math.Max(0, HorizontalPadding) * 2 + 4;
            int contentHeight = Math.Max(measured.Height, iconWidth);
            int height = Math.Max(MinimumSize.Height, contentHeight + (multiline ? 12 : 10));

            return new Size(Math.Max(MinimumSize.Width, width), height);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using var brush = new SolidBrush(PaintSurface.ResolveOpaqueBackColor(Parent));
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color behind = PaintSurface.ResolveOpaqueBackColor(Parent);
            using (var bgBrush = new SolidBrush(behind))
                g.FillRectangle(bgBrush, ClientRectangle);

            if (Width <= 1 || Height <= 1) return;

            Color fill;
            Color textColor;

            if (!Enabled)
            {
                fill = DisabledFillColor;
                textColor = DisabledForeColor;
            }
            else
            {
                fill = FillColor;
                if (_pressed)
                    fill = PressedColor != Color.Empty ? PressedColor : Darken(FillColor, 0.12f);
                else if (_hover)
                    fill = HoverColor != Color.Empty ? HoverColor : FillColor;

                textColor = ForeColor;
            }

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = RoundedPanel.RoundRectPath(rect, BorderRadius);
            using (var fillBrush = new SolidBrush(fill))
                g.FillPath(fillBrush, path);

            if (BorderThickness > 0 && BorderColor != Color.Transparent)
            {
                using var borderPen = new Pen(BorderColor, BorderThickness) { Alignment = PenAlignment.Inset };
                g.DrawPath(borderPen, path);
            }

            // 키보드 탐색 시에만 내부 포커스 링이 보이도록 한다.
            if (Focused && ShowFocusCues && Enabled)
            {
                var focusRect = Rectangle.Inflate(rect, -2, -2);
                using var focusPath = RoundedPanel.RoundRectPath(focusRect, Math.Max(2, BorderRadius - 2));
                using var focusPen = new Pen(FocusColor, 2f) { Alignment = PenAlignment.Inset };
                g.DrawPath(focusPen, focusPath);
            }

            var textRect = Rectangle.Inflate(ClientRectangle, -HorizontalPadding, 0);
            Color iconPaint = !Enabled
                ? DisabledForeColor
                : (IconColor == Color.Empty ? textColor : IconColor);

            int iconSize = string.IsNullOrEmpty(IconName) ? 0 : Math.Max(12, IconSize);
            int iconGap = iconSize > 0 && !string.IsNullOrEmpty(Text) ? Math.Max(0, IconGap) : 0;
            int availableTextWidth = Math.Max(1, textRect.Width - iconSize - iconGap);

            bool multiline = !string.IsNullOrEmpty(Text) && (Text.Contains('\n') || Text.Contains('\r'));
            Font drawFont = Font;
            bool disposeFont = false;
            if (AutoFitText && !string.IsNullOrEmpty(Text))
            {
                float size = Font.Size;
                while (size > MinimumFontSize)
                {
                    TextFormatFlags measureFlags = multiline
                        ? TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding
                        : TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
                    Size measured = TextRenderer.MeasureText(
                        Text,
                        drawFont,
                        new Size(availableTextWidth, Math.Max(1, Height)),
                        measureFlags);
                    if (measured.Width <= availableTextWidth && measured.Height <= Math.Max(1, Height - 6)) break;

                    if (disposeFont) drawFont.Dispose();
                    size = Math.Max(MinimumFontSize, size - 0.5F);
                    drawFont = new Font(Font.FontFamily, size, Font.Style, GraphicsUnit.Point);
                    disposeFont = true;
                }
            }

            TextFormatFlags textMeasureFlags = multiline
                ? TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding
                : TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            Size textSize = string.IsNullOrEmpty(Text)
                ? Size.Empty
                : TextRenderer.MeasureText(
                    Text,
                    drawFont,
                    new Size(availableTextWidth, Math.Max(1, Height)),
                    textMeasureFlags);

            if (iconSize > 0)
            {
                if (string.IsNullOrEmpty(Text))
                {
                    var centerRect = new RectangleF(
                        (Width - iconSize) / 2F,
                        (Height - iconSize) / 2F,
                        iconSize,
                        iconSize);
                    LineIcons.Draw(g, IconName!, centerRect, iconPaint);
                    if (disposeFont) drawFont.Dispose();
                    return;
                }

                int iconX;
                int textX;
                if (TextAlign == ContentAlignment.MiddleCenter)
                {
                    int groupWidth = Math.Min(Width - HorizontalPadding * 2, iconSize + iconGap + textSize.Width);
                    iconX = Math.Max(HorizontalPadding, (Width - groupWidth) / 2);
                    textX = iconX + iconSize + iconGap;
                    textRect = new Rectangle(textX, 0, Math.Max(1, Width - textX - HorizontalPadding), Height);
                }
                else if (TextAlign == ContentAlignment.MiddleRight)
                {
                    int groupWidth = Math.Min(Width - HorizontalPadding * 2, iconSize + iconGap + textSize.Width);
                    iconX = Math.Max(HorizontalPadding, Width - HorizontalPadding - groupWidth);
                    textX = iconX + iconSize + iconGap;
                    textRect = new Rectangle(textX, 0, Math.Max(1, Width - textX - HorizontalPadding), Height);
                }
                else
                {
                    iconX = HorizontalPadding;
                    textX = iconX + iconSize + iconGap;
                    textRect = new Rectangle(textX, 0, Math.Max(1, Width - textX - HorizontalPadding), Height);
                }

                int iconY = (Height - iconSize) / 2;
                LineIcons.Draw(g, IconName!, new RectangleF(iconX, iconY, iconSize, iconSize), iconPaint);
            }

            TextFormatFlags drawFlags = GetTextFlags(multiline);
            if (iconSize > 0 && TextAlign != ContentAlignment.MiddleLeft)
            {
                drawFlags &= ~(TextFormatFlags.HorizontalCenter | TextFormatFlags.Right);
                drawFlags |= TextFormatFlags.Left;
            }
            if (multiline)
            {
                int measuredHeight = Math.Min(Math.Max(1, textSize.Height), Math.Max(1, Height - 4));
                textRect = new Rectangle(textRect.X, Math.Max(2, (Height - measuredHeight) / 2), textRect.Width, measuredHeight);
            }
            TextRenderer.DrawText(g, Text, drawFont, textRect, textColor, drawFlags);
            if (disposeFont) drawFont.Dispose();
        }
    }

    /// <summary>
    /// WinForms 기본 TextBox의 검은 네이티브 테두리를 제거하고
    /// Soft Blue Office 색상으로 일관된 1px 포커스 테두리를 그린다.
    /// </summary>
    public class ModernTextBox : TextBox
    {
        private const int WmPaint = 0x000F;
        private const int WmNcPaint = 0x0085;
        private const int EmSetMargins = 0x00D3;
        private const int EcLeftMargin = 0x0001;
        private const int EcRightMargin = 0x0002;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public Color BorderColor { get; set; } = Globals.Border;
        public Color FocusBorderColor { get; set; } = Globals.Accent;
        public int HorizontalTextPadding { get; set; } = 9;

        public ModernTextBox()
        {
            BorderStyle = BorderStyle.None;
            AutoSize = false;
            BackColor = Globals.SurfaceAlt;
            ForeColor = Globals.TextPrimary;
            Font = Globals.FontBody;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyMargins();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (IsHandleCreated) ApplyMargins();
            Invalidate();
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        private void ApplyMargins()
        {
            int pad = Math.Max(0, HorizontalTextPadding);
            int packed = (pad & 0xffff) | ((pad & 0xffff) << 16);
            SendMessage(Handle, EmSetMargins, (IntPtr)(EcLeftMargin | EcRightMargin), (IntPtr)packed);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg != WmPaint && m.Msg != WmNcPaint) return;
            if (!IsHandleCreated || Width <= 1 || Height <= 1) return;
            using Graphics g = CreateGraphics();
            using var pen = new Pen(Focused ? FocusBorderColor : BorderColor, Focused ? 2F : 1F)
            {
                Alignment = PenAlignment.Inset
            };
            g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }

    /// <summary>
    /// 네이티브 드롭다운 버튼을 덮어 그리는 Soft Blue Office 콤보박스.
    /// 데이터/선택 API는 기본 ComboBox와 동일하다.
    /// </summary>
    public sealed class ModernComboBox : ComboBox
    {
        private const int WmPaint = 0x000F;
        private const int WmNcPaint = 0x0085;

        public Color BorderColor { get; set; } = Globals.Border;
        public Color FocusBorderColor { get; set; } = Globals.Accent;
        public Color ArrowColor { get; set; } = Globals.TextMuted;

        public ModernComboBox()
        {
            DropDownStyle = ComboBoxStyle.DropDownList;
            FlatStyle = FlatStyle.Flat;
            DrawMode = DrawMode.OwnerDrawFixed;
            ItemHeight = 28;
            IntegralHeight = false;
            BackColor = Globals.SurfaceAlt;
            ForeColor = Globals.TextPrimary;
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color fill = selected ? Globals.AccentSoft : BackColor;
            using var brush = new SolidBrush(fill);
            e.Graphics.FillRectangle(brush, e.Bounds);
            string text = GetItemText(Items[e.Index]) ?? string.Empty;
            TextRenderer.DrawText(
                e.Graphics,
                text,
                Font,
                new Rectangle(e.Bounds.X + 10, e.Bounds.Y, Math.Max(1, e.Bounds.Width - 20), e.Bounds.Height),
                selected ? Globals.Accent : ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            if ((e.State & DrawItemState.Focus) == DrawItemState.Focus) e.DrawFocusRectangle();
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if ((m.Msg == WmPaint || m.Msg == WmNcPaint) && !DroppedDown)
                DrawClosedState();
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        private void DrawClosedState()
        {
            if (!IsHandleCreated || Width <= 1 || Height <= 1) return;
            using Graphics g = CreateGraphics();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var fill = new SolidBrush(BackColor)) g.FillRectangle(fill, ClientRectangle);

            int arrowWidth = 28;
            Rectangle textRect = new Rectangle(10, 0, Math.Max(1, Width - arrowWidth - 14), Height);
            TextRenderer.DrawText(
                g,
                Text,
                Font,
                textRect,
                Enabled ? ForeColor : Globals.TextFaint,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

            using var separator = new Pen(BorderColor, 1F);
            g.DrawLine(separator, Width - arrowWidth, 5, Width - arrowWidth, Height - 6);
            LineIcons.Draw(
                g,
                "chevron-down",
                new RectangleF(Width - arrowWidth + 8, (Height - 12) / 2F, 12, 12),
                Enabled ? ArrowColor : Globals.TextFaint,
                1.4F);

            using var border = new Pen(Focused ? FocusBorderColor : BorderColor, Focused ? 2F : 1F)
            {
                Alignment = PenAlignment.Inset
            };
            g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        }
    }

    /// <summary>
    /// WinForms 기본 TextBox를 둥근 입력 필드 안에 배치해 포커스 경계를 제공한다.
    /// 실제 값은 Input 속성으로 접근한다.
    /// </summary>
    public sealed class ModernTextBoxHost : Panel
    {
        private Color _fillColor = Globals.SurfaceAlt;
        private Color _borderColor = Globals.Border;
        private Color _focusBorderColor = Globals.Accent;

        public TextBox Input { get; }
        public int BorderRadius { get; set; } = 8;

        public Color FillColor
        {
            get => _fillColor;
            set
            {
                _fillColor = value;
                BackColor = value;
                Input.BackColor = value;
                Invalidate();
            }
        }

        public Color BorderColor
        {
            get => _borderColor;
            set { _borderColor = value; Invalidate(); }
        }

        public Color FocusBorderColor
        {
            get => _focusBorderColor;
            set { _focusBorderColor = value; Invalidate(); }
        }

        public ModernTextBoxHost()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.OptimizedDoubleBuffer, true);

            BackColor = _fillColor;
            Padding = new Padding(12, 6, 12, 5);
            TabStop = false;

            Input = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Multiline = false,
                AutoSize = false,
                BackColor = _fillColor,
                ForeColor = Globals.TextPrimary,
                Font = new Font("Malgun Gothic", 10F, FontStyle.Regular),
                Dock = DockStyle.Fill,
                TabStop = true
            };

            Input.GotFocus += (_, _) => Invalidate();
            Input.LostFocus += (_, _) => Invalidate();
            Controls.Add(Input);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using var brush = new SolidBrush(PaintSurface.ResolveOpaqueBackColor(Parent));
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color behind = PaintSurface.ResolveOpaqueBackColor(Parent);
            using (var bgBrush = new SolidBrush(behind))
                g.FillRectangle(bgBrush, ClientRectangle);

            if (Width <= 1 || Height <= 1) return;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = RoundedPanel.RoundRectPath(rect, BorderRadius);
            using (var fillBrush = new SolidBrush(FillColor))
                g.FillPath(fillBrush, path);

            using var borderPen = new Pen(Input.Focused ? FocusBorderColor : BorderColor, Input.Focused ? 2f : 1f)
            {
                Alignment = PenAlignment.Inset
            };
            g.DrawPath(borderPen, path);
        }
    }

    public sealed class ModernToggleSwitch : Control
    {
        private bool _checked;
        public event EventHandler? CheckedChanged;

        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked == value) return;
                _checked = value;
                Invalidate();
                CheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public Color OnColor { get; set; } = Globals.Accent;
        public Color OffColor { get; set; } = Globals.BorderStrong;
        public Color ThumbColor { get; set; } = Color.White;

        public ModernToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Size = new Size(38, 20);
            TabStop = true;
        }

        protected override void OnClick(EventArgs e)
        {
            Checked = !Checked;
            base.OnClick(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                Checked = !Checked;
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using var brush = new SolidBrush(PaintSurface.ResolveOpaqueBackColor(Parent));
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            int radius = Math.Max(2, Height / 2);
            using var track = RoundedPanel.RoundRectPath(rect, radius);
            using var trackBrush = new SolidBrush(Checked ? OnColor : OffColor);
            e.Graphics.FillPath(trackBrush, track);

            int diameter = Math.Max(8, Height - 6);
            int y = (Height - diameter) / 2;
            int x = Checked ? Width - diameter - 3 : 3;
            using var thumbBrush = new SolidBrush(ThumbColor);
            e.Graphics.FillEllipse(thumbBrush, x, y, diameter, diameter);
        }
    }

}
