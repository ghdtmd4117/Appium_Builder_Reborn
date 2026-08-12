using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace AppiumBuilder.UI
{
    /// <summary>
    /// Minimal Slate 전용 선형 아이콘 세트.
    /// 외부 폰트나 이미지 리소스 없이 동일한 선 굵기와 여백으로 그린다.
    /// </summary>
    public static class LineIcons
    {
        public static void Draw(Graphics g, string name, RectangleF rect, Color color, float strokeWidth = 1.6F)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(color, strokeWidth)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            using var brush = new SolidBrush(color);

            float x = rect.X;
            float y = rect.Y;
            float w = rect.Width;
            float h = rect.Height;
            float cx = x + w / 2F;
            float cy = y + h / 2F;

            switch (name)
            {
                case "home":
                {
                    using var path = new GraphicsPath();
                    path.AddLine(x + w * 0.08F, y + h * 0.48F, cx, y + h * 0.10F);
                    path.AddLine(cx, y + h * 0.10F, x + w * 0.92F, y + h * 0.48F);
                    g.DrawPath(pen, path);
                    g.DrawRectangle(pen, x + w * 0.20F, y + h * 0.43F, w * 0.60F, h * 0.47F);
                    g.DrawRectangle(pen, x + w * 0.43F, y + h * 0.63F, w * 0.14F, h * 0.27F);
                    break;
                }
                case "log":
                case "terminal":
                {
                    g.DrawRectangle(pen, x + w * 0.08F, y + h * 0.13F, w * 0.84F, h * 0.72F);
                    g.DrawLine(pen, x + w * 0.22F, y + h * 0.35F, x + w * 0.34F, y + h * 0.47F);
                    g.DrawLine(pen, x + w * 0.34F, y + h * 0.47F, x + w * 0.22F, y + h * 0.59F);
                    g.DrawLine(pen, x + w * 0.45F, y + h * 0.60F, x + w * 0.72F, y + h * 0.60F);
                    break;
                }
                case "video":
                {
                    g.DrawRectangle(pen, x + w * 0.08F, y + h * 0.25F, w * 0.56F, h * 0.50F);
                    PointF[] camera =
                    {
                        new PointF(x + w * 0.69F, y + h * 0.38F),
                        new PointF(x + w * 0.92F, y + h * 0.24F),
                        new PointF(x + w * 0.92F, y + h * 0.76F),
                        new PointF(x + w * 0.69F, y + h * 0.62F)
                    };
                    g.DrawPolygon(pen, camera);
                    break;
                }
                case "tools":
                case "wrench":
                {
                    g.DrawLine(pen, x + w * 0.22F, y + h * 0.78F, x + w * 0.66F, y + h * 0.34F);
                    g.DrawEllipse(pen, x + w * 0.12F, y + h * 0.68F, w * 0.20F, h * 0.20F);
                    g.DrawArc(pen, x + w * 0.52F, y + h * 0.10F, w * 0.36F, h * 0.36F, 25, 215);
                    g.DrawLine(pen, x + w * 0.68F, y + h * 0.10F, x + w * 0.78F, y + h * 0.22F);
                    break;
                }
                case "settings":
                {
                    float r = Math.Min(w, h) * 0.19F;
                    g.DrawEllipse(pen, cx - r, cy - r, r * 2F, r * 2F);
                    for (int i = 0; i < 8; i++)
                    {
                        double a = i * Math.PI / 4D;
                        float x1 = cx + (float)Math.Cos(a) * r * 1.45F;
                        float y1 = cy + (float)Math.Sin(a) * r * 1.45F;
                        float x2 = cx + (float)Math.Cos(a) * r * 2.05F;
                        float y2 = cy + (float)Math.Sin(a) * r * 2.05F;
                        g.DrawLine(pen, x1, y1, x2, y2);
                    }
                    break;
                }
                case "robot":
                case "appium":
                {
                    g.DrawRectangle(pen, x + w * 0.15F, y + h * 0.31F, w * 0.70F, h * 0.52F);
                    g.DrawLine(pen, cx, y + h * 0.31F, cx, y + h * 0.13F);
                    g.FillEllipse(brush, cx - w * 0.035F, y + h * 0.09F, w * 0.07F, h * 0.07F);
                    g.FillEllipse(brush, x + w * 0.30F, y + h * 0.50F, w * 0.08F, h * 0.08F);
                    g.FillEllipse(brush, x + w * 0.62F, y + h * 0.50F, w * 0.08F, h * 0.08F);
                    g.DrawLine(pen, x + w * 0.34F, y + h * 0.69F, x + w * 0.66F, y + h * 0.69F);
                    g.DrawLine(pen, x + w * 0.15F, y + h * 0.58F, x + w * 0.05F, y + h * 0.58F);
                    g.DrawLine(pen, x + w * 0.85F, y + h * 0.58F, x + w * 0.95F, y + h * 0.58F);
                    break;
                }
                case "android":
                {
                    g.DrawArc(pen, x + w * 0.18F, y + h * 0.22F, w * 0.64F, h * 0.54F, 180, 180);
                    g.DrawRectangle(pen, x + w * 0.18F, y + h * 0.48F, w * 0.64F, h * 0.32F);
                    g.DrawLine(pen, x + w * 0.32F, y + h * 0.24F, x + w * 0.22F, y + h * 0.09F);
                    g.DrawLine(pen, x + w * 0.68F, y + h * 0.24F, x + w * 0.78F, y + h * 0.09F);
                    g.FillEllipse(brush, x + w * 0.34F, y + h * 0.36F, w * 0.06F, h * 0.06F);
                    g.FillEllipse(brush, x + w * 0.60F, y + h * 0.36F, w * 0.06F, h * 0.06F);
                    g.DrawLine(pen, x + w * 0.30F, y + h * 0.80F, x + w * 0.30F, y + h * 0.94F);
                    g.DrawLine(pen, x + w * 0.70F, y + h * 0.80F, x + w * 0.70F, y + h * 0.94F);
                    break;
                }
                case "link":
                case "connection":
                {
                    g.DrawArc(pen, x + w * 0.06F, y + h * 0.30F, w * 0.52F, h * 0.42F, 35, 290);
                    g.DrawArc(pen, x + w * 0.42F, y + h * 0.30F, w * 0.52F, h * 0.42F, 215, 290);
                    g.DrawLine(pen, x + w * 0.36F, cy, x + w * 0.64F, cy);
                    break;
                }
                case "phone":
                case "device":
                {
                    g.DrawRectangle(pen, x + w * 0.25F, y + h * 0.05F, w * 0.50F, h * 0.90F);
                    g.DrawLine(pen, x + w * 0.42F, y + h * 0.13F, x + w * 0.58F, y + h * 0.13F);
                    g.FillEllipse(brush, cx - w * 0.03F, y + h * 0.84F, w * 0.06F, h * 0.06F);
                    break;
                }
                case "camera":
                {
                    g.DrawRectangle(pen, x + w * 0.07F, y + h * 0.28F, w * 0.86F, h * 0.57F);
                    using var top = new GraphicsPath();
                    top.AddLine(x + w * 0.28F, y + h * 0.28F, x + w * 0.39F, y + h * 0.13F);
                    top.AddLine(x + w * 0.39F, y + h * 0.13F, x + w * 0.61F, y + h * 0.13F);
                    top.AddLine(x + w * 0.61F, y + h * 0.13F, x + w * 0.72F, y + h * 0.28F);
                    g.DrawPath(pen, top);
                    float r = Math.Min(w, h) * 0.17F;
                    g.DrawEllipse(pen, cx - r, y + h * 0.56F - r, r * 2F, r * 2F);
                    break;
                }
                case "record":
                {
                    g.DrawEllipse(pen, x + w * 0.12F, y + h * 0.12F, w * 0.76F, h * 0.76F);
                    g.FillEllipse(brush, x + w * 0.34F, y + h * 0.34F, w * 0.32F, h * 0.32F);
                    break;
                }
                case "trash":
                case "clear":
                {
                    g.DrawRectangle(pen, x + w * 0.25F, y + h * 0.27F, w * 0.50F, h * 0.61F);
                    g.DrawLine(pen, x + w * 0.18F, y + h * 0.22F, x + w * 0.82F, y + h * 0.22F);
                    g.DrawLine(pen, x + w * 0.38F, y + h * 0.13F, x + w * 0.62F, y + h * 0.13F);
                    g.DrawLine(pen, x + w * 0.40F, y + h * 0.40F, x + w * 0.40F, y + h * 0.73F);
                    g.DrawLine(pen, x + w * 0.60F, y + h * 0.40F, x + w * 0.60F, y + h * 0.73F);
                    break;
                }
                case "file":
                {
                    g.DrawRectangle(pen, x + w * 0.18F, y + h * 0.08F, w * 0.64F, h * 0.84F);
                    g.DrawLine(pen, x + w * 0.55F, y + h * 0.08F, x + w * 0.82F, y + h * 0.35F);
                    g.DrawLine(pen, x + w * 0.55F, y + h * 0.08F, x + w * 0.55F, y + h * 0.35F);
                    g.DrawLine(pen, x + w * 0.55F, y + h * 0.35F, x + w * 0.82F, y + h * 0.35F);
                    g.DrawLine(pen, x + w * 0.30F, y + h * 0.54F, x + w * 0.70F, y + h * 0.54F);
                    g.DrawLine(pen, x + w * 0.30F, y + h * 0.70F, x + w * 0.64F, y + h * 0.70F);
                    break;
                }
                case "code":
                {
                    g.DrawLine(pen, x + w * 0.34F, y + h * 0.24F, x + w * 0.14F, cy);
                    g.DrawLine(pen, x + w * 0.14F, cy, x + w * 0.34F, y + h * 0.76F);
                    g.DrawLine(pen, x + w * 0.66F, y + h * 0.24F, x + w * 0.86F, cy);
                    g.DrawLine(pen, x + w * 0.86F, cy, x + w * 0.66F, y + h * 0.76F);
                    g.DrawLine(pen, x + w * 0.58F, y + h * 0.14F, x + w * 0.42F, y + h * 0.86F);
                    break;
                }
                case "save":
                {
                    g.DrawRectangle(pen, x + w * 0.10F, y + h * 0.10F, w * 0.80F, h * 0.80F);
                    g.DrawRectangle(pen, x + w * 0.28F, y + h * 0.10F, w * 0.44F, h * 0.28F);
                    g.DrawRectangle(pen, x + w * 0.26F, y + h * 0.58F, w * 0.48F, h * 0.32F);
                    break;
                }
                case "monitor":
                {
                    g.DrawRectangle(pen, x + w * 0.07F, y + h * 0.12F, w * 0.86F, h * 0.58F);
                    g.DrawLine(pen, x + w * 0.42F, y + h * 0.70F, x + w * 0.42F, y + h * 0.84F);
                    g.DrawLine(pen, x + w * 0.58F, y + h * 0.70F, x + w * 0.58F, y + h * 0.84F);
                    g.DrawLine(pen, x + w * 0.25F, y + h * 0.88F, x + w * 0.75F, y + h * 0.88F);
                    break;
                }
                case "dump":
                case "archive":
                {
                    PointF[] top =
                    {
                        new PointF(cx, y + h * 0.08F),
                        new PointF(x + w * 0.85F, y + h * 0.27F),
                        new PointF(cx, y + h * 0.46F),
                        new PointF(x + w * 0.15F, y + h * 0.27F)
                    };
                    g.DrawPolygon(pen, top);
                    g.DrawLine(pen, x + w * 0.15F, y + h * 0.27F, x + w * 0.15F, y + h * 0.72F);
                    g.DrawLine(pen, x + w * 0.85F, y + h * 0.27F, x + w * 0.85F, y + h * 0.72F);
                    g.DrawLine(pen, x + w * 0.15F, y + h * 0.72F, cx, y + h * 0.92F);
                    g.DrawLine(pen, x + w * 0.85F, y + h * 0.72F, cx, y + h * 0.92F);
                    g.DrawLine(pen, cx, y + h * 0.46F, cx, y + h * 0.92F);
                    break;
                }
                case "folder":
                {
                    using var path = new GraphicsPath();
                    path.AddLine(x + w * 0.08F, y + h * 0.28F, x + w * 0.38F, y + h * 0.28F);
                    path.AddLine(x + w * 0.38F, y + h * 0.28F, x + w * 0.48F, y + h * 0.18F);
                    path.AddLine(x + w * 0.48F, y + h * 0.18F, x + w * 0.92F, y + h * 0.18F);
                    path.AddLine(x + w * 0.92F, y + h * 0.18F, x + w * 0.92F, y + h * 0.82F);
                    path.AddLine(x + w * 0.92F, y + h * 0.82F, x + w * 0.08F, y + h * 0.82F);
                    path.CloseFigure();
                    g.DrawPath(pen, path);
                    break;
                }
                case "usb":
                {
                    g.DrawLine(pen, cx, y + h * 0.10F, cx, y + h * 0.76F);
                    g.DrawLine(pen, cx, y + h * 0.18F, x + w * 0.35F, y + h * 0.33F);
                    g.DrawLine(pen, x + w * 0.35F, y + h * 0.33F, x + w * 0.35F, y + h * 0.55F);
                    g.DrawRectangle(pen, x + w * 0.29F, y + h * 0.55F, w * 0.12F, h * 0.12F);
                    g.DrawLine(pen, cx, y + h * 0.30F, x + w * 0.67F, y + h * 0.47F);
                    g.FillEllipse(brush, x + w * 0.62F, y + h * 0.44F, w * 0.10F, h * 0.10F);
                    PointF[] arrow =
                    {
                        new PointF(cx, y + h * 0.06F),
                        new PointF(x + w * 0.43F, y + h * 0.17F),
                        new PointF(x + w * 0.57F, y + h * 0.17F)
                    };
                    g.FillPolygon(brush, arrow);
                    g.DrawLine(pen, cx, y + h * 0.76F, x + w * 0.39F, y + h * 0.87F);
                    g.DrawLine(pen, cx, y + h * 0.76F, x + w * 0.61F, y + h * 0.87F);
                    g.DrawLine(pen, x + w * 0.39F, y + h * 0.87F, x + w * 0.61F, y + h * 0.87F);
                    break;
                }
                case "wifi":
                {
                    g.DrawArc(pen, x + w * 0.08F, y + h * 0.20F, w * 0.84F, h * 0.78F, 210, 120);
                    g.DrawArc(pen, x + w * 0.27F, y + h * 0.42F, w * 0.46F, h * 0.42F, 210, 120);
                    g.FillEllipse(brush, cx - w * 0.045F, y + h * 0.76F, w * 0.09F, h * 0.09F);
                    break;
                }
                case "info":
                {
                    g.DrawEllipse(pen, x + w * 0.08F, y + h * 0.08F, w * 0.84F, h * 0.84F);
                    g.DrawLine(pen, cx, y + h * 0.44F, cx, y + h * 0.72F);
                    g.FillEllipse(brush, cx - w * 0.04F, y + h * 0.26F, w * 0.08F, h * 0.08F);
                    break;
                }
                case "disconnect":
                {
                    g.DrawArc(pen, x + w * 0.13F, y + h * 0.24F, w * 0.48F, h * 0.50F, 70, 220);
                    g.DrawArc(pen, x + w * 0.39F, y + h * 0.24F, w * 0.48F, h * 0.50F, 250, 220);
                    g.DrawLine(pen, x + w * 0.16F, y + h * 0.84F, x + w * 0.84F, y + h * 0.16F);
                    break;
                }
                case "play":
                {
                    PointF[] triangle =
                    {
                        new PointF(x + w * 0.30F, y + h * 0.18F),
                        new PointF(x + w * 0.78F, cy),
                        new PointF(x + w * 0.30F, y + h * 0.82F)
                    };
                    g.DrawPolygon(pen, triangle);
                    break;
                }
                case "stop":
                {
                    g.DrawRectangle(pen, x + w * 0.22F, y + h * 0.22F, w * 0.56F, h * 0.56F);
                    break;
                }
                case "refresh":
                {
                    g.DrawArc(pen, x + w * 0.14F, y + h * 0.16F, w * 0.72F, h * 0.72F, 30, 285);
                    PointF[] arrow =
                    {
                        new PointF(x + w * 0.74F, y + h * 0.12F),
                        new PointF(x + w * 0.91F, y + h * 0.18F),
                        new PointF(x + w * 0.80F, y + h * 0.32F)
                    };
                    g.FillPolygon(brush, arrow);
                    break;
                }
                case "search":
                {
                    g.DrawEllipse(pen, x + w * 0.12F, y + h * 0.10F, w * 0.58F, h * 0.58F);
                    g.DrawLine(pen, x + w * 0.62F, y + h * 0.62F, x + w * 0.88F, y + h * 0.88F);
                    break;
                }
                case "filter":
                {
                    PointF[] funnel =
                    {
                        new PointF(x + w * 0.08F, y + h * 0.18F),
                        new PointF(x + w * 0.92F, y + h * 0.18F),
                        new PointF(x + w * 0.61F, y + h * 0.52F),
                        new PointF(x + w * 0.61F, y + h * 0.84F),
                        new PointF(x + w * 0.39F, y + h * 0.72F),
                        new PointF(x + w * 0.39F, y + h * 0.52F)
                    };
                    g.DrawPolygon(pen, funnel);
                    break;
                }
                case "pause":
                {
                    g.DrawRectangle(pen, x + w * 0.24F, y + h * 0.18F, w * 0.16F, h * 0.64F);
                    g.DrawRectangle(pen, x + w * 0.60F, y + h * 0.18F, w * 0.16F, h * 0.64F);
                    break;
                }
                case "plus":
                {
                    g.DrawLine(pen, cx, y + h * 0.18F, cx, y + h * 0.82F);
                    g.DrawLine(pen, x + w * 0.18F, cy, x + w * 0.82F, cy);
                    break;
                }
                case "target":
                {
                    g.DrawEllipse(pen, x + w * 0.10F, y + h * 0.10F, w * 0.80F, h * 0.80F);
                    g.DrawEllipse(pen, x + w * 0.30F, y + h * 0.30F, w * 0.40F, h * 0.40F);
                    g.DrawLine(pen, cx, y + h * 0.02F, cx, y + h * 0.24F);
                    g.DrawLine(pen, cx, y + h * 0.76F, cx, y + h * 0.98F);
                    g.DrawLine(pen, x + w * 0.02F, cy, x + w * 0.24F, cy);
                    g.DrawLine(pen, x + w * 0.76F, cy, x + w * 0.98F, cy);
                    break;
                }
                case "download":
                {
                    g.DrawLine(pen, cx, y + h * 0.12F, cx, y + h * 0.63F);
                    g.DrawLine(pen, cx, y + h * 0.63F, x + w * 0.30F, y + h * 0.43F);
                    g.DrawLine(pen, cx, y + h * 0.63F, x + w * 0.70F, y + h * 0.43F);
                    g.DrawLine(pen, x + w * 0.14F, y + h * 0.82F, x + w * 0.86F, y + h * 0.82F);
                    break;
                }
                case "sparkles":
                {
                    DrawSpark(g, pen, cx, y + h * 0.30F, w * 0.23F);
                    DrawSpark(g, pen, x + w * 0.28F, y + h * 0.70F, w * 0.13F);
                    DrawSpark(g, pen, x + w * 0.75F, y + h * 0.69F, w * 0.10F);
                    break;
                }
                case "list":
                {
                    for (int i = 0; i < 3; i++)
                    {
                        float yy = y + h * (0.24F + i * 0.26F);
                        g.FillEllipse(brush, x + w * 0.10F, yy - h * 0.035F, w * 0.07F, h * 0.07F);
                        g.DrawLine(pen, x + w * 0.27F, yy, x + w * 0.88F, yy);
                    }
                    break;
                }
                case "check":
                {
                    g.DrawLine(pen, x + w * 0.15F, y + h * 0.55F, x + w * 0.42F, y + h * 0.82F);
                    g.DrawLine(pen, x + w * 0.42F, y + h * 0.82F, x + w * 0.88F, y + h * 0.20F);
                    break;
                }
                case "x":
                {
                    g.DrawLine(pen, x + w * 0.18F, y + h * 0.18F, x + w * 0.82F, y + h * 0.82F);
                    g.DrawLine(pen, x + w * 0.82F, y + h * 0.18F, x + w * 0.18F, y + h * 0.82F);
                    break;
                }
                case "bolt":
                {
                    PointF[] points =
                    {
                        new PointF(x + w * 0.59F, y + h * 0.05F),
                        new PointF(x + w * 0.22F, y + h * 0.55F),
                        new PointF(x + w * 0.47F, y + h * 0.55F),
                        new PointF(x + w * 0.40F, y + h * 0.95F),
                        new PointF(x + w * 0.82F, y + h * 0.42F),
                        new PointF(x + w * 0.57F, y + h * 0.42F)
                    };
                    g.FillPolygon(brush, points);
                    break;
                }
                case "chevron-up":
                {
                    g.DrawLine(pen, x + w * 0.20F, y + h * 0.65F, cx, y + h * 0.30F);
                    g.DrawLine(pen, cx, y + h * 0.30F, x + w * 0.80F, y + h * 0.65F);
                    break;
                }
                case "chevron-down":
                {
                    g.DrawLine(pen, x + w * 0.20F, y + h * 0.35F, cx, y + h * 0.70F);
                    g.DrawLine(pen, cx, y + h * 0.70F, x + w * 0.80F, y + h * 0.35F);
                    break;
                }
            }
        }

        private static void DrawSpark(Graphics g, Pen pen, float x, float y, float radius)
        {
            g.DrawLine(pen, x - radius, y, x + radius, y);
            g.DrawLine(pen, x, y - radius, x, y + radius);
            float diagonal = radius * 0.58F;
            g.DrawLine(pen, x - diagonal, y - diagonal, x + diagonal, y + diagonal);
            g.DrawLine(pen, x + diagonal, y - diagonal, x - diagonal, y + diagonal);
        }
    }
}
