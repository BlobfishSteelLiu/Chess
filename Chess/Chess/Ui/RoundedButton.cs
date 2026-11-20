using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Chess.Ui
{
    /// <summary>圆角、带悬停高亮的按钮，用于替代系统灰白的默认外观。</summary>
    public class RoundedButton : Button
    {
        private bool _hover;
        private bool _pressed;

        [DefaultValue(false)]
        [Category("外观")]
        [Description("是否使用金色强调样式。")]
        public bool Accent { get; set; }

        /// <summary>可选的第二行说明文字。</summary>
        [DefaultValue("")]
        [Category("外观")]
        [Description("按钮上显示的第二行说明文字。")]
        public string SubText { get; set; } = string.Empty;

        [DefaultValue(10)]
        [Category("外观")]
        [Description("圆角半径（像素）。")]
        public int CornerRadius { get; set; } = 10;

        [DefaultValue(1f)]
        [Category("外观")]
        [Description("边框宽度（像素），0 表示不画边框。")]
        public float BorderWidth { get; set; } = 1f;

        public RoundedButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint
                     | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Theme.Panel;
            ForeColor = Theme.Text;
            Font = Theme.BodyFont;
            Cursor = Cursors.Hand;
            UseVisualStyleBackColor = false;
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
            _pressed = true;
            Invalidate();
            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            _pressed = false;
            Invalidate();
            base.OnMouseUp(mevent);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            var g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? Theme.Background);

            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);

            Color fill;
            Color border;
            Color text;

            if (Accent)
            {
                fill = !Enabled ? Color.FromArgb(96, 82, 62)
                     : _pressed ? Theme.AccentPressed
                     : _hover ? Theme.AccentHover
                     : Theme.Accent;
                border = fill;
                text = _hover || _pressed ? Color.FromArgb(32, 26, 20) : Color.FromArgb(38, 30, 20);
            }
            else
            {
                fill = !Enabled ? Color.FromArgb(52, 47, 43)
                     : _pressed ? Color.FromArgb(68, 60, 53)
                     : _hover ? Theme.PanelHover
                     : Theme.Panel;
                border = !Enabled ? Theme.Border : _hover ? Theme.Accent : Theme.Border;
                text = Enabled ? Theme.Text : Theme.TextDim;
            }

            using (var path = Theme.RoundedRect(rect, CornerRadius))
            {
                using (var brush = new SolidBrush(fill))
                    g.FillPath(brush, path);

                if (BorderWidth > 0f)
                {
                    using var pen = new Pen(border, BorderWidth);
                    g.DrawPath(pen, path);
                }
            }

            if (string.IsNullOrEmpty(SubText))
            {
                TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, Width, Height), text,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
            else
            {
                int upperHeight = (int)(Height * 0.56f);
                using var subFont = new Font(Font.FontFamily, Font.Size - 4f, FontStyle.Regular);
                var subColor = Accent
                    ? Color.FromArgb(96, 70, 28)
                    : (Enabled ? Theme.TextDim : Color.FromArgb(110, 102, 94));

                TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, Width, upperHeight), text,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Bottom | TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(g, SubText, subFont, new Rectangle(0, upperHeight + 2, Width, Height - upperHeight - 4), subColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);
            }
        }
    }
}
