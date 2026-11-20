using Chess.Core;
using Chess.Ui;

namespace Chess
{
    /// <summary>兵升变时选择升变棋子的对话框。</summary>
    public partial class PromotionDialog : Form
    {
        public int SelectedPiece { get; private set; } = Pc.Queen;

        public PromotionDialog(int color)
        {
            InitializeComponent();
            _color = color;
        }

        private readonly int _color;

        private void PromotionDialog_Load(object? sender, EventArgs e)
        {
            lblHeading.Font = Theme.HeadingFont;
            lblHeading.ForeColor = Theme.Text;

            var font = new Font("Segoe UI Symbol", 30f, FontStyle.Regular);

            btnQueen.Font = font;
            btnRook.Font = font;
            btnBishop.Font = font;
            btnKnight.Font = font;

            btnQueen.Text = Pc.Glyph(Pc.Make(Pc.Queen, _color));
            btnRook.Text = Pc.Glyph(Pc.Make(Pc.Rook, _color));
            btnBishop.Text = Pc.Glyph(Pc.Make(Pc.Bishop, _color));
            btnKnight.Text = Pc.Glyph(Pc.Make(Pc.Knight, _color));

            btnQueen.SubText = "后";
            btnRook.SubText = "车";
            btnBishop.SubText = "象";
            btnKnight.SubText = "马";

            foreach (var button in new[] { btnQueen, btnRook, btnBishop, btnKnight })
            {
                button.ForeColor = Theme.Text;
                button.Accent = button == btnQueen;
            }

            btnQueen.Focus();
        }

        private void PromotionDialog_Paint(object? sender, PaintEventArgs e)
        {
            using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                ClientRectangle, Theme.Background, Theme.BackgroundSoft, 90f);
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }

        private void Choose(int type)
        {
            SelectedPiece = type;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void BtnQueen_Click(object? sender, EventArgs e) => Choose(Pc.Queen);

        private void BtnRook_Click(object? sender, EventArgs e) => Choose(Pc.Rook);

        private void BtnBishop_Click(object? sender, EventArgs e) => Choose(Pc.Bishop);

        private void BtnKnight_Click(object? sender, EventArgs e) => Choose(Pc.Knight);

        /// <summary>弹出对话框；用户取消时返回 0。</summary>
        public static int Ask(IWin32Window owner, int color)
        {
            using var dialog = new PromotionDialog(color);
            return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.SelectedPiece : 0;
        }
    }
}
