using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;
using Chess.Ai;
using Chess.Ui;

namespace Chess
{
    /// <summary>主菜单页：标题、棋子装饰，以及「双人对战」「人机对战」两个入口。</summary>
    public sealed class MainMenuView : UserControl
    {
        private readonly Label _title = new();
        private readonly Label _subtitle = new();
        private readonly Label _pieces = new();
        private readonly Label _footer = new();
        private readonly RoundedButton _twoPlayer = new();
        private readonly RoundedButton _vsComputer = new();
        private readonly RoundedButton _import = new();

        public event EventHandler? TwoPlayerRequested;
        public event EventHandler? VsComputerRequested;
        public event EventHandler? ImportRequested;

        public MainMenuView()
        {
            AutoScaleMode = AutoScaleMode.None;
            SetStyle(ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint
                     | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Background;
            DoubleBuffered = true;

            _title.Text = "国际象棋";
            _title.Font = Theme.TitleFont;
            _title.ForeColor = Theme.Accent;
            _title.TextAlign = ContentAlignment.MiddleCenter;
            _title.BackColor = Color.Transparent;

            _subtitle.Text = "C H E S S   ·   标 准 国 际 象 棋 对 弈";
            _subtitle.Font = Theme.BodyFont;
            _subtitle.ForeColor = Theme.TextDim;
            _subtitle.TextAlign = ContentAlignment.MiddleCenter;
            _subtitle.BackColor = Color.Transparent;

            _pieces.Text = "\u2654  \u2655  \u2656  \u2657  \u2658  \u2659      \u265F  \u265E  \u265D  \u265C  \u265B  \u265A";
            _pieces.Font = new Font("Segoe UI Symbol", 22f);
            _pieces.ForeColor = Theme.Accent;
            _pieces.TextAlign = ContentAlignment.MiddleCenter;
            _pieces.BackColor = Color.Transparent;

            _footer.Text = "棋谱自动保存到程序目录 Records\\ 文件夹";
            _footer.Font = Theme.SmallFont;
            _footer.ForeColor = Color.FromArgb(110, 100, 90);
            _footer.TextAlign = ContentAlignment.MiddleCenter;
            _footer.BackColor = Color.Transparent;

            _twoPlayer.Text = "双 人 对 战";
            _twoPlayer.Accent = true;
            _twoPlayer.CornerRadius = 12;
            _twoPlayer.Font = new Font("Microsoft YaHei UI", 15f, FontStyle.Bold);
            _twoPlayer.ForeColor = Color.FromArgb(32, 26, 20);
            _twoPlayer.Click += (_, _) => TwoPlayerRequested?.Invoke(this, EventArgs.Empty);

            _vsComputer.Text = "人 机 对 战";
            _vsComputer.CornerRadius = 12;
            _vsComputer.Font = new Font("Microsoft YaHei UI", 15f, FontStyle.Bold);
            _vsComputer.Click += (_, _) => VsComputerRequested?.Invoke(this, EventArgs.Empty);

            _import.Text = "导 入 对 局";
            _import.CornerRadius = 12;
            _import.Font = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold);
            _import.Click += (_, _) => ImportRequested?.Invoke(this, EventArgs.Empty);

            Controls.Add(_title);
            Controls.Add(_subtitle);
            Controls.Add(_pieces);
            Controls.Add(_twoPlayer);
            Controls.Add(_vsComputer);
            Controls.Add(_import);
            Controls.Add(_footer);
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

            int titleHeight = this.Px(96);
            int subtitleHeight = this.Px(30);
            int piecesHeight = this.Px(64);
            int buttonWidth = Math.Min(this.Px(320), Math.Max(this.Px(200), width - this.Px(80)));
            int buttonHeight = this.Px(68);
            int importHeight = this.Px(54);
            int buttonGap = this.Px(16);
            int gap = this.Px(28);

            int blockHeight = titleHeight + subtitleHeight + piecesHeight + gap
                              + buttonHeight * 2 + importHeight + buttonGap * 2;
            int top = Math.Max(this.Px(20), (height - blockHeight) / 2 - this.Px(16));

            int y = top;
            _title.SetBounds(0, y, width, titleHeight);
            y += titleHeight;
            _subtitle.SetBounds(0, y, width, subtitleHeight);
            y += subtitleHeight;
            _pieces.SetBounds(0, y, width, piecesHeight);
            y += piecesHeight + gap;

            int x = (width - buttonWidth) / 2;
            _twoPlayer.SetBounds(x, y, buttonWidth, buttonHeight);
            y += buttonHeight + buttonGap;
            _vsComputer.SetBounds(x, y, buttonWidth, buttonHeight);
            y += buttonHeight + buttonGap;
            _import.SetBounds(x, y, buttonWidth, importHeight);

            _footer.SetBounds(0, height - this.Px(44), width, this.Px(26));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                       ClientRectangle, Theme.Background, Theme.BackgroundSoft, 90f))
            {
                g.FillRectangle(brush, ClientRectangle);
            }

            // 半透明棋盘纹理
            int tile = this.Px(70);
            using (var light = new SolidBrush(Color.FromArgb(9, 240, 226, 202)))
            using (var dark = new SolidBrush(Color.FromArgb(9, 167, 121, 82)))
            {
                int columns = ClientSize.Width / Math.Max(1, tile) + 1;
                int rows = ClientSize.Height / Math.Max(1, tile) + 1;
                for (int y = 0; y < rows; y++)
                {
                    for (int x = 0; x < columns; x++)
                    {
                        var brush = ((x + y) & 1) == 0 ? light : dark;
                        g.FillRectangle(brush, x * tile, y * tile, tile, tile);
                    }
                }
            }

            // 角落装饰棋子
            float decor = Math.Min(ClientSize.Width, ClientSize.Height) * 0.34f;
            float alpha = 22f;
            PieceRenderer.DrawDecor(g, "\u265E",
                new RectangleF(-decor * 0.18f, ClientSize.Height - decor * 0.86f, decor, decor),
                Color.FromArgb((int)alpha, 214, 163, 74));
            PieceRenderer.DrawDecor(g, "\u265C",
                new RectangleF(ClientSize.Width - decor * 0.86f, -decor * 0.2f, decor, decor),
                Color.FromArgb((int)alpha, 214, 163, 74));
            PieceRenderer.DrawDecor(g, "\u265B",
                new RectangleF(ClientSize.Width - decor * 0.7f, ClientSize.Height - decor * 0.7f, decor * 0.78f, decor * 0.78f),
                Color.FromArgb((int)alpha - 3, 214, 163, 74));
        }
    }
}
