using System.Drawing;
using System.Windows.Forms;
using Chess.Ai;
using Chess.Ui;

namespace Chess
{
    /// <summary>人机对战的强度选择页（与主菜单、对局页共用同一个窗口）。</summary>
    public sealed class DifficultyView : UserControl
    {
        private readonly Label _heading = new();
        private readonly Label _hint = new();
        private readonly RoundedButton _easy = new();
        private readonly RoundedButton _normal = new();
        private readonly RoundedButton _hard = new();
        private readonly RoundedButton _back = new();

        public event EventHandler<Difficulty>? DifficultyChosen;
        public event EventHandler? BackRequested;

        public DifficultyView()
        {
            AutoScaleMode = AutoScaleMode.None;
            SetStyle(ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint
                     | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Background;
            DoubleBuffered = true;

            _heading.Text = "选择电脑强度";
            _heading.Font = Theme.HeadingFont;
            _heading.ForeColor = Theme.Text;
            _heading.TextAlign = ContentAlignment.MiddleCenter;
            _heading.BackColor = Color.Transparent;

            _hint.Text = "难度越高，电脑算得越深、越不容易犯错";
            _hint.Font = Theme.SmallFont;
            _hint.ForeColor = Theme.TextDim;
            _hint.TextAlign = ContentAlignment.MiddleCenter;
            _hint.BackColor = Color.Transparent;

            Configure(_easy, Difficulty.Easy, accent: false);
            Configure(_normal, Difficulty.Normal, accent: true);
            Configure(_hard, Difficulty.Hard, accent: false);

            _back.Text = "返回";
            _back.CornerRadius = 8;
            _back.Font = Theme.BodyFont;
            _back.ForeColor = Theme.TextDim;
            _back.Click += (_, _) => BackRequested?.Invoke(this, EventArgs.Empty);

            Controls.Add(_heading);
            Controls.Add(_hint);
            Controls.Add(_easy);
            Controls.Add(_normal);
            Controls.Add(_hard);
            Controls.Add(_back);
        }

        private void Configure(RoundedButton button, Difficulty difficulty, bool accent)
        {
            button.Text = DifficultyNames.Chinese(difficulty);
            button.SubText = DifficultyNames.Description(difficulty);
            button.Accent = accent;
            button.CornerRadius = 10;
            button.Font = new Font("Microsoft YaHei UI", 14f, FontStyle.Bold);
            button.Click += (_, _) => DifficultyChosen?.Invoke(this, difficulty);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyLayout();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ApplyLayout();
        }

        private void ApplyLayout()
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

            int width = ClientSize.Width;
            int height = ClientSize.Height;

            int headingHeight = this.Px(40);
            int hintHeight = this.Px(26);
            int buttonWidth = Math.Min(this.Px(460), Math.Max(this.Px(240), width - this.Px(120)));
            int buttonHeight = this.Px(80);
            int buttonGap = this.Px(16);
            int gap = this.Px(18);
            int backHeight = this.Px(44);

            int blockHeight = headingHeight + hintHeight + gap
                              + buttonHeight * 3 + buttonGap * 2
                              + this.Px(24) + backHeight;
            int top = Math.Max(this.Px(20), (height - blockHeight) / 2);

            int y = top;
            _heading.SetBounds(0, y, width, headingHeight);
            y += headingHeight;
            _hint.SetBounds(0, y, width, hintHeight);
            y += hintHeight + gap;

            int x = (width - buttonWidth) / 2;
            _easy.SetBounds(x, y, buttonWidth, buttonHeight);
            y += buttonHeight + buttonGap;
            _normal.SetBounds(x, y, buttonWidth, buttonHeight);
            y += buttonHeight + buttonGap;
            _hard.SetBounds(x, y, buttonWidth, buttonHeight);
            y += buttonHeight + this.Px(24);

            _back.SetBounds((width - this.Px(140)) / 2, y, this.Px(140), backHeight);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                ClientRectangle, Theme.Background, Theme.BackgroundSoft, 90f);
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }
    }
}
