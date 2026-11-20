using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using Chess.Core;

namespace Chess.Ui
{
    /// <summary>
    /// 用 Unicode 国际象棋字符绘制棋子。
    /// 白方使用空心字符（♔♕♖♗♘♙），黑方使用实心字符（♚♛♜♝♞♟）。
    /// 为了在深浅两种棋盘格上都清晰可见，棋子轮廓会额外描边。
    /// </summary>
    public static class PieceRenderer
    {
        private static readonly Dictionary<(string Glyph, int Size), GraphicsPath> PathCache = new();
        private static readonly FontFamily Family = ResolveFamily();

        private static FontFamily ResolveFamily()
        {
            foreach (var name in new[] { "Segoe UI Symbol", "Segoe UI", "Arial Unicode MS", "SimSun", "Times New Roman" })
            {
                try
                {
                    var family = new FontFamily(name);
                    return family;
                }
                catch
                {
                    // 尝试下一个
                }
            }
            return FontFamily.GenericSansSerif;
        }

        private static GraphicsPath GetPath(string glyph, int size)
        {
            var key = (glyph, size);
            if (PathCache.TryGetValue(key, out var cached)) return cached;

            var path = new GraphicsPath();
            using (var format = new StringFormat(StringFormat.GenericTypographic))
            {
                format.FormatFlags = StringFormatFlags.NoClip | StringFormatFlags.NoWrap;
                try
                {
                    path.AddString(glyph, Family, (int)FontStyle.Regular, size, new PointF(0, 0), format);
                }
                catch
                {
                    path.AddEllipse(0, -size, size, size);
                }
            }

            var bounds = path.GetBounds();
            if (bounds.Width <= 0f || bounds.Height <= 0f)
            {
                path.Dispose();
                path = new GraphicsPath();
                path.AddEllipse(0, -size, size, size);
            }

            PathCache[key] = path;
            return path;
        }

        /// <summary>在指定矩形内居中绘制一个棋子。</summary>
        public static void Draw(Graphics g, int piece, RectangleF area, float scale = 0.86f)
        {
            if (piece == 0) return;

            string glyph = Pc.SolidGlyph(piece);
            int baseSize = 100;
            var path = GetPath(glyph, baseSize);
            var bounds = path.GetBounds();

            float target = Math.Min(area.Width, area.Height) * scale;
            float factor = Math.Min(target / bounds.Width, target / bounds.Height);
            if (factor <= 0f || float.IsNaN(factor)) return;

            var state = g.Save();
            try
            {
                using var matrix = new Matrix();
                matrix.Translate(area.X + area.Width / 2f, area.Y + area.Height / 2f);
                matrix.Scale(factor, factor);
                matrix.Translate(-(bounds.X + bounds.Width / 2f), -(bounds.Y + bounds.Height / 2f));
                g.Transform = matrix;

                bool white = piece > 0;
                Color fill = white ? Color.FromArgb(252, 251, 247) : Color.FromArgb(26, 24, 23);
                Color edge = white ? Color.FromArgb(28, 24, 20) : Color.FromArgb(206, 196, 182);

                // 先画较粗的描边，再用填充色覆盖内侧，形成一圈干净的外轮廓
                using (var pen = new Pen(edge, 3.6f))
                {
                    pen.LineJoin = LineJoin.Round;
                    pen.Alignment = PenAlignment.Center;
                    g.DrawPath(pen, path);
                }

                using (var brush = new SolidBrush(fill))
                    g.FillPath(brush, path);
            }
            finally
            {
                g.Restore(state);
            }
        }

        /// <summary>用于菜单上的装饰性大字。</summary>
        public static void DrawDecor(Graphics g, string glyph, RectangleF area, Color color, float scale = 0.9f)
        {
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            float pixelSize = Math.Min(area.Width, area.Height) * scale;
            using var scaled = new Font(Family, pixelSize, FontStyle.Regular, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(color);
            using var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString(glyph, scaled, brush, area, format);
        }
    }
}
