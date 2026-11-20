using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using Chess.Core;

namespace Chess.Ui
{
    /// <summary>
    /// 绘制并响应鼠标的棋盘控件。棋盘下方为白方视角，四周标出 a-h / 1-8 坐标。
    /// </summary>
    public sealed class BoardView : Control
    {
        private Board? _board;
        private int _selectedSquare = -1;
        private int _lastMoveFrom = -1;
        private int _lastMoveTo = -1;
        private readonly List<int> _legalTargets = new();

        private readonly List<ActiveSlide> _slides = new();
        private readonly List<PieceHold> _holds = new();
        private readonly System.Windows.Forms.Timer _animationTimer = new();

        public event EventHandler<int>? SquareClicked;

        public BoardView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint
                     | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.BackgroundSoft;
            DoubleBuffered = true;

            _animationTimer.Interval = 16;
            _animationTimer.Tick += AnimationTick;
        }

        /// <summary>一步棋里需要滑行的一枚棋子。</summary>
        public readonly record struct PieceSlide(int Piece, int FromSquare, int ToSquare);

        /// <summary>
        /// 动画期间“暂时还在”的被吃棋子：棋局里它已经没了，
        /// 但动画期间仍画在原位，等吃子的棋子落地才消失。
        /// </summary>
        public readonly record struct PieceHold(int Piece, int Square);

        private sealed class ActiveSlide
        {
            public PieceSlide Slide;
            public long StartTicks;
            public int DurationMs;
        }

        /// <summary>当前是否正在播放滑行动画。</summary>
        [Browsable(false)]
        public bool IsAnimating => _slides.Count > 0;

        /// <summary>
        /// 播放一组滑行动画。动画期间这些棋子的目标格不会被直接绘制，
        /// 而是由动画在起点与终点之间插值绘制。
        /// </summary>
        public void AnimateSlides(IReadOnlyList<PieceSlide> slides, int durationMs,
            IReadOnlyList<PieceHold>? holds = null)
        {
            _slides.Clear();
            _holds.Clear();

            if (slides.Count > 0)
            {
                long now = Environment.TickCount64;
                int duration = Math.Max(16, durationMs);
                foreach (var slide in slides)
                    _slides.Add(new ActiveSlide { Slide = slide, StartTicks = now, DurationMs = duration });

                if (holds is not null) _holds.AddRange(holds);

                if (!_animationTimer.Enabled) _animationTimer.Start();
            }
            else
            {
                _animationTimer.Stop();
            }

            Invalidate();
        }

        private void AnimationTick(object? sender, EventArgs e)
        {
            long now = Environment.TickCount64;
            bool running = false;
            foreach (var active in _slides)
            {
                if (now - active.StartTicks < active.DurationMs) { running = true; break; }
            }

            if (!running)
            {
                _slides.Clear();
                _holds.Clear();      // 落地之后被吃子才真正消失 / 重新出现
                _animationTimer.Stop();
            }

            Invalidate();
        }

        private bool IsSlideTarget(int square)
        {
            foreach (var active in _slides)
            {
                if (active.Slide.ToSquare == square) return true;
            }
            return false;
        }

        /// <summary>画出“暂时还在”的被吃棋子（吃子方落地前它一直留在原地）。</summary>
        private void DrawHolds(Graphics g)
        {
            foreach (var hold in _holds)
            {
                if (hold.Square < 0 || hold.Square > 63) continue;
                PieceRenderer.Draw(g, hold.Piece, SquareRect(hold.Square));
            }
        }

        /// <summary>把正在滑动的棋子按缓入缓出的速度画在起点与终点之间。</summary>
        private void DrawSlides(Graphics g)
        {
            if (_slides.Count == 0) return;

            long now = Environment.TickCount64;
            foreach (var active in _slides)
            {
                float t = Math.Clamp((now - active.StartTicks) / (float)active.DurationMs, 0f, 1f);
                // 平滑曲线：起步和收尾都放缓，全程速度均衡，不会“一冲到位”
                float eased = t * t * (3f - 2f * t);

                if (active.Slide.FromSquare < 0 || active.Slide.FromSquare > 63) continue;
                if (active.Slide.ToSquare < 0 || active.Slide.ToSquare > 63) continue;

                var from = SquareRect(active.Slide.FromSquare);
                var to = SquareRect(active.Slide.ToSquare);
                var rect = new RectangleF(
                    from.X + (to.X - from.X) * eased,
                    from.Y + (to.Y - from.Y) * eased,
                    from.Width, from.Height);

                // 滑行途中略微放大，让“某枚棋子在走”更醒目
                float lift = 1f + 0.12f * MathF.Sin(MathF.PI * t);
                var lifted = new RectangleF(
                    rect.X - rect.Width * (lift - 1f) / 2f,
                    rect.Y - rect.Height * (lift - 1f) / 2f,
                    rect.Width * lift,
                    rect.Height * lift);

                PieceRenderer.Draw(g, active.Slide.Piece, lifted);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animationTimer.Stop();
                _animationTimer.Dispose();
            }
            base.Dispose(disposing);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public Board? Board
        {
            get => _board;
            set
            {
                _board = value;
                Invalidate();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public int SelectedSquare
        {
            get => _selectedSquare;
            set
            {
                if (_selectedSquare == value) return;
                _selectedSquare = value;
                Invalidate();
            }
        }

        public void SetLastMove(int from, int to)
        {
            _lastMoveFrom = from;
            _lastMoveTo = to;
            Invalidate();
        }

        public void SetLegalTargets(IEnumerable<int> targets)
        {
            _legalTargets.Clear();
            _legalTargets.AddRange(targets);
            Invalidate();
        }

        public void ClearLegalTargets()
        {
            if (_legalTargets.Count == 0) return;
            _legalTargets.Clear();
            Invalidate();
        }

        #region 坐标换算

        /// <summary>坐标区宽度，按控件尺寸等比缩放，保证高 DPI 下同样协调。</summary>
        private float MarginPx
            => Math.Max(8f, Math.Min(ClientSize.Width, ClientSize.Height) * 0.042f);

        private float SquareSize
            => Math.Max(1f, (Math.Min(ClientSize.Width, ClientSize.Height) - MarginPx * 2) / 8f);

        private float BoardOriginX => (ClientSize.Width - SquareSize * 8) / 2f;

        private float BoardOriginY => (ClientSize.Height - SquareSize * 8) / 2f;

        private RectangleF SquareRect(int square)
        {
            int file = square & 7;
            int rank = square >> 3;
            float size = SquareSize;
            float x = BoardOriginX + file * size;
            float y = BoardOriginY + (7 - rank) * size;   // 白方在下
            return new RectangleF(x, y, size, size);
        }

        public int SquareAt(Point location)
        {
            float size = SquareSize;
            float x = location.X - BoardOriginX;
            float y = location.Y - BoardOriginY;
            if (x < 0 || y < 0) return -1;

            int file = (int)(x / size);
            int displayRank = (int)(y / size);
            if (file < 0 || file > 7 || displayRank < 0 || displayRank > 7) return -1;

            return (7 - displayRank) * 8 + file;
        }

        #endregion

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            int square = SquareAt(e.Location);
            if (square < 0) return;
            SquareClicked?.Invoke(this, square);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            using (var background = new SolidBrush(Theme.BackgroundSoft))
                g.FillRectangle(background, ClientRectangle);

            var board = _board;
            float size = SquareSize;
            float originX = BoardOriginX;
            float originY = BoardOriginY;

            var boardRect = new RectangleF(originX, originY, size * 8, size * 8);

            // 棋盘阴影
            using (var shadow = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
            using (var shadowPath = Theme.RoundedRect(
                       new RectangleF(boardRect.X + 4, boardRect.Y + 5, boardRect.Width, boardRect.Height), 8))
            {
                g.FillPath(shadow, shadowPath);
            }

            // 棋盘格
            for (int sq = 0; sq < 64; sq++)
            {
                int file = sq & 7;
                int rank = sq >> 3;
                bool light = ((file + rank) & 1) == 1;
                var rect = SquareRect(sq);

                using var brush = new SolidBrush(light ? Theme.LightSquare : Theme.DarkSquare);
                g.FillRectangle(brush, rect);
            }

            // 上一步走子的起点与终点
            if (_lastMoveFrom >= 0)
            {
                using var fromBrush = new SolidBrush(Theme.MoveFromHighlight);
                g.FillRectangle(fromBrush, SquareRect(_lastMoveFrom));
            }

            if (_lastMoveTo >= 0)
            {
                using var toBrush = new SolidBrush(Theme.MoveToHighlight);
                g.FillRectangle(toBrush, SquareRect(_lastMoveTo));
            }

            // 被将军的王
            if (board is not null && board.IsInCheck(board.SideToMove))
            {
                int kingSquare = board.KingSquare(board.SideToMove);
                if (kingSquare >= 0)
                {
                    using var checkBrush = new SolidBrush(Theme.CheckHighlight);
                    using var checkPath = Theme.RoundedRect(SquareRect(kingSquare), 6);
                    g.FillPath(checkBrush, checkPath);
                }
            }

            // 被选中的格子
            if (_selectedSquare >= 0)
            {
                using var selectedBrush = new SolidBrush(Theme.SelectedHighlight);
                g.FillRectangle(selectedBrush, SquareRect(_selectedSquare));
            }

            // 棋子（正在滑行的棋子先跳过，交给动画绘制）
            if (board is not null)
            {
                for (int sq = 0; sq < 64; sq++)
                {
                    if (IsSlideTarget(sq)) continue;
                    int piece = board.Squares[sq];
                    if (piece == 0) continue;
                    PieceRenderer.Draw(g, piece, SquareRect(sq));
                }
            }

            DrawHolds(g);
            DrawSlides(g);

            // 可走位置标记
            foreach (int target in _legalTargets)
            {
                var rect = SquareRect(target);
                bool occupied = board is not null && board.Squares[target] != 0;

                if (occupied)
                {
                    using var pen = new Pen(Color.FromArgb(120, 30, 24, 18), Math.Max(2.5f, size * 0.07f));
                    float inset = size * 0.08f;
                    g.DrawEllipse(pen, rect.X + inset, rect.Y + inset, rect.Width - inset * 2, rect.Height - inset * 2);
                }
                else
                {
                    using var brush = new SolidBrush(Theme.LegalDot);
                    float diameter = size * 0.28f;
                    g.FillEllipse(brush,
                        rect.X + (rect.Width - diameter) / 2f,
                        rect.Y + (rect.Height - diameter) / 2f,
                        diameter, diameter);
                }
            }

            // 边框与坐标
            using (var pen = new Pen(Theme.Border))
                g.DrawRectangle(pen, boardRect.X, boardRect.Y, boardRect.Width, boardRect.Height);

            using var labelBrush = new SolidBrush(Color.FromArgb(112, Theme.TextDim));
            float labelPx = Math.Min(size * 0.16f, 22f);
            float labelPt = labelPx * 72f / Math.Max(96, DeviceDpi);
            using var font = new Font("Microsoft YaHei UI", Math.Max(5f, labelPt), FontStyle.Regular);
            using var center = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            for (int file = 0; file < 8; file++)
            {
                string name = ((char)('a' + file)).ToString();
                var bottom = new RectangleF(originX + file * size, originY + size * 8, size, MarginPx);
                var top = new RectangleF(originX + file * size, originY - MarginPx, size, MarginPx);
                g.DrawString(name, font, labelBrush, bottom, center);
                g.DrawString(name, font, labelBrush, top, center);
            }

            for (int rank = 0; rank < 8; rank++)
            {
                string name = (rank + 1).ToString();
                float y = originY + (7 - rank) * size;
                var left = new RectangleF(originX - MarginPx, y, MarginPx, size);
                var right = new RectangleF(originX + size * 8, y, MarginPx, size);
                g.DrawString(name, font, labelBrush, left, center);
                g.DrawString(name, font, labelBrush, right, center);
            }
        }
    }
}
