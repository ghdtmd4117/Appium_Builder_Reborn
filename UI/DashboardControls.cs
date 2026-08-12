using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using AppiumBuilder.Utils;

namespace AppiumBuilder.UI
{
    public sealed class DashboardTrendPoint
    {
        public string Label { get; set; } = string.Empty;
        public int Pass { get; set; }
        public int Fail { get; set; }
        public int Stopped { get; set; }
        public int Total => Pass + Fail + Stopped;
        public double PassRate => Total == 0 ? 0D : Pass * 100D / Total;
    }

    /// <summary>
    /// 외부 차트 라이브러리 없이 최근 실행 추이를 표시하는 가벼운 WinForms 차트입니다.
    /// 막대는 실행 건수, 선은 PASS 비율을 의미합니다.
    /// </summary>
    public sealed class WeeklyTrendChart : Control
    {
        private IReadOnlyList<DashboardTrendPoint> _points = Array.Empty<DashboardTrendPoint>();

        public IReadOnlyList<DashboardTrendPoint> Points
        {
            get => _points;
            set
            {
                _points = value ?? Array.Empty<DashboardTrendPoint>();
                Invalidate();
            }
        }

        public WeeklyTrendChart()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            MinimumSize = new Size(220, 130);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Rectangle chart = new Rectangle(12, 18, Math.Max(10, Width - 24), Math.Max(10, Height - 48));
            if (_points.Count == 0 || _points.All(point => point.Total == 0))
            {
                TextRenderer.DrawText(
                    g,
                    "최근 7일 실행 이력이 없습니다.",
                    Globals.FontMuted,
                    ClientRectangle,
                    Globals.TextMuted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                return;
            }

            using var gridPen = new Pen(Globals.Border, 1F);
            for (int i = 0; i < 4; i++)
            {
                int y = chart.Top + (int)(chart.Height * i / 3D);
                g.DrawLine(gridPen, chart.Left, y, chart.Right, y);
            }

            int maxTotal = Math.Max(1, _points.Max(point => point.Total));
            float slotWidth = chart.Width / Math.Max(1F, _points.Count);
            float barWidth = Math.Min(24F, slotWidth * 0.40F);
            var linePoints = new List<PointF>();

            using var emptyBrush = new SolidBrush(Globals.SurfaceRaised);
            using var passBrush = new SolidBrush(Globals.Success);
            using var failBrush = new SolidBrush(Globals.Danger);
            using var stoppedBrush = new SolidBrush(Globals.Warning);
            using var linePen = new Pen(Globals.Accent, 2F)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            using var pointBrush = new SolidBrush(Globals.Accent);

            for (int i = 0; i < _points.Count; i++)
            {
                DashboardTrendPoint point = _points[i];
                float centerX = chart.Left + slotWidth * i + slotWidth / 2F;
                float totalHeight = point.Total == 0 ? 3F : Math.Max(6F, chart.Height * point.Total / maxTotal);
                float top = chart.Bottom - totalHeight;
                var fullBar = new RectangleF(centerX - barWidth / 2F, top, barWidth, totalHeight);
                using (GraphicsPath path = RoundedPanel.RoundRectPath(Rectangle.Round(fullBar), 4))
                    g.FillPath(emptyBrush, path);

                if (point.Total > 0)
                {
                    float passHeight = totalHeight * point.Pass / point.Total;
                    float failHeight = totalHeight * point.Fail / point.Total;
                    float stoppedHeight = totalHeight * point.Stopped / point.Total;
                    float currentBottom = chart.Bottom;

                    if (point.Pass > 0)
                    {
                        g.FillRectangle(passBrush, centerX - barWidth / 2F, currentBottom - passHeight, barWidth, passHeight);
                        currentBottom -= passHeight;
                    }
                    if (point.Fail > 0)
                    {
                        g.FillRectangle(failBrush, centerX - barWidth / 2F, currentBottom - failHeight, barWidth, failHeight);
                        currentBottom -= failHeight;
                    }
                    if (point.Stopped > 0)
                    {
                        g.FillRectangle(stoppedBrush, centerX - barWidth / 2F, currentBottom - stoppedHeight, barWidth, stoppedHeight);
                    }
                }

                float rateY = chart.Bottom - (float)(chart.Height * point.PassRate / 100D);
                linePoints.Add(new PointF(centerX, rateY));

                var labelRect = new Rectangle((int)(centerX - slotWidth / 2F), chart.Bottom + 7, (int)slotWidth, 20);
                TextRenderer.DrawText(
                    g,
                    point.Label,
                    Globals.FontMuted,
                    labelRect,
                    Globals.TextMuted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            }

            if (linePoints.Count > 1) g.DrawLines(linePen, linePoints.ToArray());
            foreach (PointF point in linePoints)
                g.FillEllipse(pointBrush, point.X - 3F, point.Y - 3F, 6F, 6F);

            TextRenderer.DrawText(
                g,
                "막대 실행 수  ·  선 PASS 비율",
                Globals.FontMuted,
                new Rectangle(12, 0, Math.Max(10, Width - 24), 18),
                Globals.TextFaint,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        }
    }
}
