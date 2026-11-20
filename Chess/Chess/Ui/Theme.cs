using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Chess.Ui
{
    public static class Theme
    {
        public static readonly Color Background = Color.FromArgb(26, 23, 21);
        public static readonly Color BackgroundSoft = Color.FromArgb(38, 34, 31);
        public static readonly Color Panel = Color.FromArgb(44, 39, 35);
        public static readonly Color PanelHover = Color.FromArgb(58, 51, 45);
        public static readonly Color Border = Color.FromArgb(74, 65, 57);
        public static readonly Color Accent = Color.FromArgb(214, 163, 74);
        public static readonly Color AccentHover = Color.FromArgb(234, 188, 108);
        public static readonly Color AccentPressed = Color.FromArgb(186, 138, 58);
        public static readonly Color Text = Color.FromArgb(240, 234, 224);
        public static readonly Color TextDim = Color.FromArgb(158, 147, 134);

        public static readonly Color LightSquare = Color.FromArgb(240, 226, 202);
        public static readonly Color DarkSquare = Color.FromArgb(167, 121, 82);
        public static readonly Color LightSquareAlt = Color.FromArgb(246, 236, 216);
        public static readonly Color DarkSquareAlt = Color.FromArgb(176, 130, 90);

        public static readonly Color MoveFromHighlight = Color.FromArgb(150, 214, 163, 74);
        public static readonly Color MoveToHighlight = Color.FromArgb(120, 214, 163, 74);
        public static readonly Color CheckHighlight = Color.FromArgb(150, 214, 76, 76);
        public static readonly Color SelectedHighlight = Color.FromArgb(120, 110, 190, 120);
        public static readonly Color LegalDot = Color.FromArgb(70, 40, 30, 20);

        public static readonly Font TitleFont = CreateFont("Microsoft YaHei UI", 42f, FontStyle.Bold);
        public static readonly Font HeadingFont = CreateFont("Microsoft YaHei UI", 14f, FontStyle.Bold);
        public static readonly Font BodyFont = CreateFont("Microsoft YaHei UI", 10f, FontStyle.Regular);
        public static readonly Font BodyBoldFont = CreateFont("Microsoft YaHei UI", 10f, FontStyle.Bold);
        public static readonly Font SmallFont = CreateFont("Microsoft YaHei UI", 8.5f, FontStyle.Regular);
        public static readonly Font MonoFont = CreateFont("Consolas", 10f, FontStyle.Regular);

        private static Font CreateFont(string family, float size, FontStyle style)
        {
            try
            {
                var font = new Font(family, size, style);
                if (font.Name.Equals(family, StringComparison.OrdinalIgnoreCase)) return font;
                font.Dispose();
            }
            catch
            {
                // 字体不存在时退回系统默认字体
            }
            return new Font(FontFamily.GenericSansSerif, size, style);
        }

        /// <summary>生成圆角矩形路径。</summary>
        public static GraphicsPath RoundedRect(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float diameter = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
            if (diameter <= 0.5f)
            {
                path.AddRectangle(rect);
                return path;
            }

            var arc = new RectangleF(rect.X, rect.Y, diameter, diameter);
            path.AddArc(arc, 180, 90);

            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);

            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            arc.X = rect.X;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }
    }
}
