namespace Chess
{
    partial class PromotionDialog
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.lblHeading = new System.Windows.Forms.Label();
            this.btnQueen = new Chess.Ui.RoundedButton();
            this.btnRook = new Chess.Ui.RoundedButton();
            this.btnBishop = new Chess.Ui.RoundedButton();
            this.btnKnight = new Chess.Ui.RoundedButton();
            this.SuspendLayout();
            //
            // lblHeading
            //
            this.lblHeading.BackColor = System.Drawing.Color.Transparent;
            this.lblHeading.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Bold);
            this.lblHeading.ForeColor = System.Drawing.Color.WhiteSmoke;
            this.lblHeading.Location = new System.Drawing.Point(0, 16);
            this.lblHeading.Name = "lblHeading";
            this.lblHeading.Size = new System.Drawing.Size(456, 30);
            this.lblHeading.TabIndex = 0;
            this.lblHeading.Text = "兵升变 · 请选择新棋子";
            this.lblHeading.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // btnQueen
            //
            this.btnQueen.CornerRadius = 10;
            this.btnQueen.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnQueen.Location = new System.Drawing.Point(24, 60);
            this.btnQueen.Name = "btnQueen";
            this.btnQueen.Size = new System.Drawing.Size(96, 104);
            this.btnQueen.TabIndex = 1;
            this.btnQueen.UseVisualStyleBackColor = false;
            this.btnQueen.Click += new System.EventHandler(this.BtnQueen_Click);
            //
            // btnRook
            //
            this.btnRook.CornerRadius = 10;
            this.btnRook.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnRook.Location = new System.Drawing.Point(132, 60);
            this.btnRook.Name = "btnRook";
            this.btnRook.Size = new System.Drawing.Size(96, 104);
            this.btnRook.TabIndex = 2;
            this.btnRook.UseVisualStyleBackColor = false;
            this.btnRook.Click += new System.EventHandler(this.BtnRook_Click);
            //
            // btnBishop
            //
            this.btnBishop.CornerRadius = 10;
            this.btnBishop.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnBishop.Location = new System.Drawing.Point(240, 60);
            this.btnBishop.Name = "btnBishop";
            this.btnBishop.Size = new System.Drawing.Size(96, 104);
            this.btnBishop.TabIndex = 3;
            this.btnBishop.UseVisualStyleBackColor = false;
            this.btnBishop.Click += new System.EventHandler(this.BtnBishop_Click);
            //
            // btnKnight
            //
            this.btnKnight.CornerRadius = 10;
            this.btnKnight.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnKnight.Location = new System.Drawing.Point(348, 60);
            this.btnKnight.Name = "btnKnight";
            this.btnKnight.Size = new System.Drawing.Size(96, 104);
            this.btnKnight.TabIndex = 4;
            this.btnKnight.UseVisualStyleBackColor = false;
            this.btnKnight.Click += new System.EventHandler(this.BtnKnight_Click);
            //
            // PromotionDialog
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(26)))), ((int)(((byte)(23)))), ((int)(((byte)(21)))));
            this.ClientSize = new System.Drawing.Size(468, 184);
            this.Controls.Add(this.btnKnight);
            this.Controls.Add(this.btnBishop);
            this.Controls.Add(this.btnRook);
            this.Controls.Add(this.btnQueen);
            this.Controls.Add(this.lblHeading);
            this.DoubleBuffered = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "PromotionDialog";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "升变";
            this.Load += new System.EventHandler(this.PromotionDialog_Load);
            this.Paint += new System.Windows.Forms.PaintEventHandler(this.PromotionDialog_Paint);
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.Label lblHeading;
        private Chess.Ui.RoundedButton btnQueen;
        private Chess.Ui.RoundedButton btnRook;
        private Chess.Ui.RoundedButton btnBishop;
        private Chess.Ui.RoundedButton btnKnight;
    }
}
