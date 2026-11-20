using System.Drawing;
using System.Windows.Forms;
using Chess.Ai;
using Chess.Core;
using Chess.Recording;
using Chess.Ui;

namespace Chess
{
    /// <summary>
    /// 对局页：左侧棋盘，右侧状态与棋谱。与主菜单共用同一个窗口，可随窗口缩放。
    /// 同一个页面也用于「导入棋谱」的回放（此时棋盘只读，并提供逐步查看按钮）。
    /// </summary>
    public sealed class GameView : UserControl
    {
        /// <summary>电脑走子前的固定等待时间，让它看起来是在思考。</summary>
        private const int AiMoveDelayMs = 3000;

        /// <summary>一步棋的滑行时长。</summary>
        private const int MoveSlideMs = 280;

        /// <summary>回放单步滑行时长，也是跳转时统一的一次滑行时长。</summary>
        private const int ReviewStepMs = 280;

        private bool _vsAi;
        private Difficulty _difficulty;
        private AiEngine? _engine;
        private readonly bool _reviewMode;

        private readonly GameSession _game = new();
        private Board? _reviewBoard;
        private ImportedGame? _imported;
        private int _reviewIndex;
        private readonly System.Windows.Forms.Timer _autoTimer = new();
        private bool _autoPlaying;

        private readonly BoardView _board = new();
        private readonly Panel _sidebar = new();
        private readonly Label _lblMode = new();
        private readonly Label _lblStatus = new();
        private readonly ListBox _moveList = new();
        private readonly Label _lblRecord = new();
        private readonly Label _lblTip = new();
        private readonly RoundedButton _undo = new();
        private readonly RoundedButton _next = new();
        private readonly RoundedButton _auto = new();
        private readonly RoundedButton _back = new();

        private PgnRecorder? _recorder;
        private List<Move> _legalMoves = new();
        private int _selected = -1;
        private bool _aiThinking;
        private bool _closing;

        public event EventHandler? BackRequested;

        /// <summary>新建一局（双人对战 / 人机对战）。</summary>
        public GameView(bool vsAi, Difficulty difficulty)
        {
            _vsAi = vsAi;
            _difficulty = difficulty;
            _engine = vsAi ? new AiEngine(difficulty) : null;
            _reviewMode = false;

            InitializeUi();
            StartNewGame();
        }

        /// <summary>导入已有棋谱进行回放。</summary>
        public GameView(ImportedGame game)
        {
            _imported = game;
            _reviewMode = true;

            InitializeUi();
            LoadImported();
        }

        #region 界面构建

        private void InitializeUi()
        {
            AutoScaleMode = AutoScaleMode.None;
            SetStyle(ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint
                     | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Background;
            DoubleBuffered = true;

            BuildSidebar();

            _board.Board = _reviewMode ? null : _game.Board;
            _board.SquareClicked += (_, square) => OnSquareClicked(square);

            Controls.Add(_board);
            Controls.Add(_sidebar);

            _autoTimer.Interval = 900;
            _autoTimer.Tick += (_, _) => AutoAdvance();

            ApplyLayout();
        }

        private void BuildSidebar()
        {
            _sidebar.BackColor = Theme.Panel;

            _lblMode.Font = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold);
            _lblMode.ForeColor = Theme.Accent;
            _lblMode.TextAlign = ContentAlignment.MiddleLeft;
            _lblMode.BackColor = Color.Transparent;
            _lblMode.AutoEllipsis = true;

            _lblStatus.Font = new Font("Microsoft YaHei UI", 14f, FontStyle.Bold);
            _lblStatus.ForeColor = Theme.Text;
            _lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            _lblStatus.BackColor = Color.Transparent;

            _moveList.BackColor = Color.FromArgb(32, 28, 25);
            _moveList.ForeColor = Theme.Text;
            _moveList.Font = Theme.MonoFont;
            _moveList.BorderStyle = BorderStyle.None;
            _moveList.IntegralHeight = false;
            _moveList.HorizontalScrollbar = false;
            _moveList.SelectionMode = SelectionMode.None;
            _moveList.DrawMode = DrawMode.OwnerDrawFixed;
            _moveList.DrawItem += MoveList_DrawItem;
            _moveList.MouseDown += MoveList_MouseDown;
            _moveList.Cursor = _reviewMode ? Cursors.Hand : Cursors.Default;
            _moveList.Resize += (_, _) => _moveList.Invalidate();

            _lblRecord.Font = Theme.SmallFont;
            _lblRecord.ForeColor = Theme.TextDim;
            _lblRecord.TextAlign = ContentAlignment.MiddleLeft;
            _lblRecord.BackColor = Color.Transparent;

            _lblTip.Font = Theme.SmallFont;
            _lblTip.ForeColor = Color.FromArgb(120, 110, 100);
            _lblTip.TextAlign = ContentAlignment.MiddleLeft;
            _lblTip.BackColor = Color.Transparent;

            _undo.Text = _reviewMode ? "上一步" : "悔棋";
            _undo.Font = Theme.BodyFont;
            _undo.CornerRadius = 8;
            _undo.Click += (_, _) =>
            {
                if (_reviewMode) StepBackward();
                else UndoMove();
            };

            _next.Text = _reviewMode ? "下一步" : "新对局";
            _next.Font = Theme.BodyFont;
            _next.CornerRadius = 8;
            _next.Click += (_, _) =>
            {
                if (_reviewMode) StepForward();
                else NewGame();
            };

            _auto.Text = "自动";
            _auto.Font = Theme.BodyFont;
            _auto.CornerRadius = 8;
            _auto.Visible = _reviewMode;
            _auto.Click += (_, _) => ToggleAutoPlay();

            _back.Text = "返回";
            _back.Font = Theme.BodyFont;
            _back.CornerRadius = 8;
            _back.Click += (_, _) => RequestBack();

            _lblTip.Text = _reviewMode
                ? "点击棋谱里的任意一手可跳到该局面"
                : "点击棋子，再点亮起的格子即可走子";

            _sidebar.Controls.Add(_lblMode);
            _sidebar.Controls.Add(_lblStatus);
            _sidebar.Controls.Add(_moveList);
            _sidebar.Controls.Add(_lblRecord);
            _sidebar.Controls.Add(_lblTip);
            _sidebar.Controls.Add(_undo);
            _sidebar.Controls.Add(_next);
            if (_reviewMode) _sidebar.Controls.Add(_auto);
            _sidebar.Controls.Add(_back);
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

            int pad = this.Px(14);
            int gap = this.Px(14);

            int minSidebar = this.Px(270);
            int maxSidebar = this.Px(400);
            int sidebarWidth = Math.Clamp((int)(width * 0.26), minSidebar, maxSidebar);
            if (width - sidebarWidth - gap - pad * 2 < this.Px(280))
                sidebarWidth = Math.Max(this.Px(200), width - gap - pad * 2 - this.Px(280));

            int available = width - sidebarWidth - gap - pad * 2;
            int boardSize = Math.Max(this.Px(120), Math.Min(available, height - pad * 2));

            int originX = pad + Math.Max(0, (available - boardSize) / 2);
            int originY = pad + Math.Max(0, (height - pad * 2 - boardSize) / 2);

            _board.SetBounds(originX, originY, boardSize, boardSize);

            int sideX = originX + boardSize + gap;
            int sideWidth = Math.Max(this.Px(180), Math.Min(sidebarWidth, width - sideX - pad));
            int sideHeight = boardSize;
            _sidebar.SetBounds(sideX, originY, sideWidth, sideHeight);

            LayoutSidebar(sideWidth, sideHeight);
        }

        private void LayoutSidebar(int width, int height)
        {
            int inset = this.Px(16);
            int innerWidth = Math.Max(this.Px(80), width - inset * 2);

            int modeHeight = this.Px(30);
            int statusHeight = this.Px(34);
            int buttonHeight = this.Px(44);
            int recordHeight = this.Px(32);
            int tipHeight = this.Px(22);

            int y = this.Px(14);
            _lblMode.SetBounds(inset, y, innerWidth, modeHeight);
            y += modeHeight;
            _lblStatus.SetBounds(inset, y, innerWidth, statusHeight);
            y += statusHeight + this.Px(8);

            int buttonsTop = height - buttonHeight - this.Px(14);
            int tipTop = buttonsTop - tipHeight - this.Px(4);
            int recordTop = tipTop - recordHeight;
            int listHeight = Math.Max(this.Px(60), recordTop - this.Px(8) - y);

            _moveList.ItemHeight = this.Px(21);
            _moveList.SetBounds(inset, y, innerWidth, listHeight);

            _lblRecord.SetBounds(inset, recordTop, innerWidth, recordHeight);
            _lblTip.SetBounds(inset, tipTop, innerWidth, tipHeight);

            var buttons = new List<RoundedButton> { _undo, _next };
            if (_reviewMode) buttons.Add(_auto);
            buttons.Add(_back);

            int gap = this.Px(8);
            int each = Math.Max(this.Px(48), (innerWidth - gap * (buttons.Count - 1)) / buttons.Count);
            for (int i = 0; i < buttons.Count; i++)
                buttons[i].SetBounds(inset + (each + gap) * i, buttonsTop, each, buttonHeight);
        }

        /// <summary>当前显示在列表里的棋谱（正常对局用实时记录，回放用导入的记录）。</summary>
        private IReadOnlyList<string> CurrentSanMoves
            => _reviewMode
                ? (IReadOnlyList<string>?)_imported?.SanMoves ?? Array.Empty<string>()
                : _game.SanMoves;

        /// <summary>两个着法列的像素宽度（左列含回合号）。</summary>
        private int MoveColumnWidth(int rowWidth)
            => Math.Max(1, (rowWidth - this.Px(12)) / 2);

        private void MoveList_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _moveList.Items.Count) return;

            var moves = CurrentSanMoves;
            int row = e.Index;
            int whitePly = row * 2;
            int blackPly = row * 2 + 1;
            bool hasWhite = whitePly < moves.Count;
            bool hasBlack = blackPly < moves.Count;

            bool currentRow = _reviewMode && _reviewIndex > 0
                              && (_reviewIndex == whitePly + 1 || _reviewIndex == blackPly + 1);

            var background = currentRow ? Color.FromArgb(84, 66, 36) : _moveList.BackColor;

            using (var brush = new SolidBrush(background))
                e.Graphics.FillRectangle(brush, e.Bounds);

            if (currentRow)
            {
                using var accent = new SolidBrush(Theme.Accent);
                e.Graphics.FillRectangle(accent, e.Bounds.X, e.Bounds.Y, this.Px(3), e.Bounds.Height);
            }

            int inset = this.Px(6);
            int columnWidth = MoveColumnWidth(e.Bounds.Width);
            int height = e.Bounds.Height;

            var numberBounds = new Rectangle(e.Bounds.X + inset, e.Bounds.Y, this.Px(34), height);
            var whiteBounds = new Rectangle(e.Bounds.X + inset + this.Px(34), e.Bounds.Y,
                columnWidth - this.Px(34), height);
            var blackBounds = new Rectangle(e.Bounds.X + inset + columnWidth, e.Bounds.Y,
                columnWidth - inset, height);

            var flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;
            var dimColor = Theme.TextDim;

            TextRenderer.DrawText(e.Graphics, $"{row + 1}.", _moveList.Font, numberBounds, dimColor, flags);

            if (hasWhite)
            {
                bool current = _reviewMode && _reviewIndex == whitePly + 1;
                TextRenderer.DrawText(e.Graphics, moves[whitePly], _moveList.Font, whiteBounds,
                    current ? Theme.AccentHover : Theme.Text, flags);
            }

            if (hasBlack)
            {
                bool current = _reviewMode && _reviewIndex == blackPly + 1;
                TextRenderer.DrawText(e.Graphics, moves[blackPly], _moveList.Font, blackBounds,
                    current ? Theme.AccentHover : Theme.Text, flags);
            }
        }

        /// <summary>点击棋谱里的某一手，棋盘就跳到那一手之后的局面。</summary>
        private void MoveList_MouseDown(object? sender, MouseEventArgs e)
        {
            if (!_reviewMode || _imported is null) return;
            if (e.Button != MouseButtons.Left) return;

            int row = _moveList.IndexFromPoint(e.Location);
            if (row < 0 || row >= _moveList.Items.Count) return;

            var bounds = _moveList.GetItemRectangle(row);
            int inset = this.Px(6);
            int columnWidth = MoveColumnWidth(bounds.Width);

            bool blackColumn = e.X >= bounds.X + inset + columnWidth;
            int whitePly = row * 2 + 1;
            int blackPly = row * 2 + 2;

            int target = blackColumn && blackPly <= _imported.Moves.Count ? blackPly : whitePly;
            StopAutoPlay();
            GoToPly(target);
        }

        /// <summary>
        /// 滑行到指定手数。先把目标局面一次算好，再让所有变动的棋子同时滑过去，
        /// 不会逐手回放中间过程。单步前进/后退沿用精确的“这一手”信息。
        /// </summary>
        private void GoToPly(int target)
        {
            if (_imported is null || _reviewBoard is null) return;

            target = Math.Clamp(target, 0, _imported.Moves.Count);
            if (target == _reviewIndex)
            {
                RefreshReviewUi();
                return;
            }

            var before = _reviewBoard.Clone();
            int distance = Math.Abs(target - _reviewIndex);

            List<BoardView.PieceSlide> slides;
            BoardView.PieceHold? hold = null;

            if (distance == 1)
            {
                bool forward = target > _reviewIndex;
                var move = forward ? _imported.Moves[_reviewIndex] : _imported.Moves[_reviewIndex - 1];
                slides = BuildSlides(_reviewBoard, move, forward);

                if (forward)
                {
                    hold = CapturedPieceBefore(_reviewBoard, move);   // 被吃子撑到吃子落地
                    _reviewBoard.MakeMove(move);
                    _reviewIndex++;
                }
                else
                {
                    // 反向：被还原子立刻回到棋盘上，吃子方一边退开一边露出它
                    _reviewBoard.UnmakeMove();
                    _reviewIndex--;
                }
            }
            else
            {
                // 多手跳转：先把棋盘算到目标，再按前后局面的差异一次性滑行
                _reviewIndex = target;
                SyncReviewBoard();
                slides = BuildJumpSlides(before, _reviewBoard);
            }

            if (_closing || IsDisposed) return;

            _board.AnimateSlides(slides, ReviewStepMs, hold is null ? null : new[] { hold.Value });
            RefreshReviewUi();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                ClientRectangle, Theme.Background, Theme.BackgroundSoft, 90f);
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (_reviewMode)
            {
                if (keyData == Keys.Left) { StepBackward(); return true; }
                if (keyData == Keys.Right) { StepForward(); return true; }
                if (keyData == Keys.Space) { ToggleAutoPlay(); return true; }
            }

            if (keyData == Keys.Escape)
            {
                RequestBack();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        #endregion

        #region 正常对局

        private string BuildEventName()
            => _vsAi ? $"人机对战（{DifficultyNames.Chinese(_difficulty)}）" : "双人对战";

        private void StartNewGame()
        {
            _game.Reset();
            _selected = -1;
            _legalMoves = _game.LegalMoves();
            _board.SelectedSquare = -1;
            _board.ClearLegalTargets();
            _board.SetLastMove(-1, -1);

            string white = _vsAi ? "玩家" : "玩家一（白）";
            string black = _vsAi ? $"电脑（{DifficultyNames.Chinese(_difficulty)}）" : "玩家二（黑）";

            try
            {
                int mode = GameModes.From(_vsAi, _difficulty);
                _recorder = new PgnRecorder(BuildEventName(), white, black, DateTime.Now, "未结束", mode);
                _recorder.Write(_game.SanMoves, _game.ResultTag);
                _lblRecord.Text = "棋谱：" + _recorder.FileName;
            }
            catch (Exception ex)
            {
                _recorder = null;
                _lblRecord.Text = "棋谱保存失败：" + ex.Message;
            }

            _moveList.Items.Clear();
            _lblMode.Text = _vsAi
                ? $"人机对战 · {DifficultyNames.Chinese(_difficulty)}"
                : "双人对战";

            UpdateStatus();
            _board.Invalidate();
        }

        private bool IsHumanTurn()
        {
            if (_reviewMode) return false;
            if (_game.State != GameState.Ongoing || _aiThinking) return false;
            if (!_vsAi) return true;
            return _game.Board.SideToMove == Pc.White;
        }

        private void OnSquareClicked(int square)
        {
            if (!IsHumanTurn()) return;

            int piece = _game.Board.Squares[square];
            int sideToMove = _game.Board.SideToMove;

            if (_selected >= 0)
            {
                var candidates = _legalMoves.Where(m => m.From == _selected && m.To == square).ToList();
                if (candidates.Count > 0)
                {
                    PlayHumanMove(candidates);
                    return;
                }

                if (piece != 0 && Pc.Color(piece) == sideToMove)
                {
                    SelectSquare(square);
                    return;
                }

                _selected = -1;
                _board.SelectedSquare = -1;
                _board.ClearLegalTargets();
                return;
            }

            if (piece != 0 && Pc.Color(piece) == sideToMove) SelectSquare(square);
        }

        private void SelectSquare(int square)
        {
            _selected = square;
            _board.SelectedSquare = square;

            var targets = _legalMoves
                .Where(m => m.From == square)
                .Select(m => m.To)
                .Distinct()
                .ToList();
            _board.SetLegalTargets(targets);
        }

        private void PlayHumanMove(List<Move> candidates)
        {
            Move move = candidates[0];

            if (candidates.Count > 1 && candidates.Any(m => m.Promotion != 0))
            {
                int promotion = PromotionDialog.Ask(this, _game.Board.SideToMove);
                if (promotion == 0) return;
                var chosen = candidates.FirstOrDefault(m => m.Promotion == promotion);
                if (chosen.Promotion == 0) return;
                move = chosen;
            }

            CommitMove(move);
        }

        private async void CommitMove(Move move)
        {
            // 先记录需要滑行的棋子与被吃掉的棋子（此时棋盘还是走之前的局面）
            var slides = BuildSlides(_game.Board, move, forward: true);
            var hold = CapturedPieceBefore(_game.Board, move);

            _game.Play(move);
            _selected = -1;
            _board.SelectedSquare = -1;
            _board.ClearLegalTargets();
            _board.SetLastMove(move.From, move.To);
            _board.AnimateSlides(slides, MoveSlideMs, hold is null ? null : new[] { hold.Value });
            _legalMoves = _game.LegalMoves();

            RebuildMoveHistory();
            SaveRecord();
            UpdateStatus();
            _board.Invalidate();

            if (_game.State != GameState.Ongoing)
            {
                _board.ClearLegalTargets();
                OnGameOver();
                return;
            }

            if (_vsAi && _game.Board.SideToMove == Pc.Black)
                await RunAiAsync();
        }

        private async Task RunAiAsync()
        {
            if (_engine is null || _closing) return;

            _aiThinking = true;
            UpdateStatus();
            _undo.Enabled = false;

            var board = _game.Board.Clone();
            Move? best = null;
            try
            {
                // 搜索与“至少等 3 秒”同时进行：总耗时取两者较大值，
                // 既保证电脑看起来在想棋，又不会让慢棋再叠加等待。
                var search = Task.Run(() => _engine.FindBestMove(board));
                await Task.WhenAll(search, Task.Delay(AiMoveDelayMs));
                best = search.IsCompletedSuccessfully ? search.Result : null;
            }
            catch
            {
                best = null;
            }

            _aiThinking = false;
            if (_closing || IsDisposed) return;
            _undo.Enabled = true;

            if (best.HasValue && _game.State == GameState.Ongoing)
            {
                CommitMove(best.Value);
            }
            else
            {
                UpdateStatus();
            }
        }

        private void RebuildMoveHistory()
        {
            _moveList.Items.Clear();
            int rows = (_game.SanMoves.Count + 1) / 2;
            for (int i = 0; i < rows; i++) _moveList.Items.Add(i + 1);
            if (_moveList.Items.Count > 0)
                _moveList.TopIndex = Math.Max(0, _moveList.Items.Count - 1);
            _moveList.Invalidate();
        }

        private void SaveRecord()
        {
            if (_recorder is null) return;
            string termination = _game.State == GameState.Ongoing ? "未结束" : _game.EndReason;
            _recorder.Write(_game.SanMoves, _game.ResultTag, termination);
        }

        private void UpdateStatus()
        {
            if (_game.State != GameState.Ongoing)
            {
                _lblStatus.Text = _game.StatusText;
                _lblStatus.ForeColor = _game.State == GameState.Draw ? Theme.TextDim : Theme.Accent;
                return;
            }

            if (_aiThinking)
            {
                _lblStatus.Text = "电脑思考中…";
                _lblStatus.ForeColor = Theme.Accent;
                return;
            }

            string side = _game.Board.SideToMove == Pc.White ? "白方" : "黑方";
            bool inCheck = _game.Board.IsInCheck(_game.Board.SideToMove);
            _lblStatus.Text = inCheck ? $"{side}走棋 · 被将军" : $"{side}走棋";
            _lblStatus.ForeColor = inCheck ? Color.FromArgb(226, 120, 110) : Theme.Text;
        }

        private void OnGameOver()
        {
            SaveRecord();
            UpdateStatus();

            string title = _game.State switch
            {
                GameState.WhiteWin => "白方获胜",
                GameState.BlackWin => "黑方获胜",
                _ => "和棋"
            };

            string text = $"{title}\n{_game.EndReason}\n\n棋谱已保存：{_recorder?.FileName ?? "（未保存）"}";
            MessageBox.Show(this, text, "对局结束", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void UndoMove()
        {
            if (_aiThinking || _game.Moves.Count == 0) return;

            int plies = 1;
            if (_vsAi && _game.Board.SideToMove == Pc.White) plies = Math.Min(2, _game.Moves.Count);

            _game.UndoPlies(plies);
            _legalMoves = _game.LegalMoves();
            _selected = -1;
            _board.SelectedSquare = -1;
            _board.ClearLegalTargets();

            if (_game.Moves.Count > 0)
            {
                var last = _game.Moves[^1];
                _board.SetLastMove(last.From, last.To);
            }
            else
            {
                _board.SetLastMove(-1, -1);
            }

            RebuildMoveHistory();
            SaveRecord();
            UpdateStatus();
            _board.Invalidate();
        }

        private void NewGame()
        {
            if (_aiThinking) return;
            if (_game.State == GameState.Ongoing && _game.Moves.Count > 0)
            {
                var answer = MessageBox.Show(this, "当前对局尚未结束，确定要开始新的一局吗？", "开始新对局",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer != DialogResult.Yes) return;
            }

            SaveRecord();
            StartNewGame();
        }

        #endregion

        #region 棋谱回放

        private void LoadImported()
        {
            var game = _imported;
            if (game is null) return;

            _reviewBoard = new Board();
            _reviewBoard.LoadStartPosition();
            _board.Board = _reviewBoard;
            _reviewIndex = 0;

            _lblMode.Text = "导入回放 · " + game.ModeName;
            _lblRecord.Text = "文件：" + Path.GetFileName(game.FilePath);

            RebuildReviewMoveList();
            RefreshReviewUi();
        }

        private void RebuildReviewMoveList()
        {
            _moveList.Items.Clear();
            int rows = ((_imported?.SanMoves.Count ?? 0) + 1) / 2;
            for (int i = 0; i < rows; i++) _moveList.Items.Add(i + 1);
            _moveList.TopIndex = 0;
            _moveList.Invalidate();
        }

        /// <summary>把回放棋盘重新摆到第 _reviewIndex 手（用于跨度很大的跳转）。</summary>
        private void SyncReviewBoard()
        {
            if (_reviewBoard is null || _imported is null) return;

            _reviewBoard.LoadStartPosition();
            for (int i = 0; i < _reviewIndex && i < _imported.Moves.Count; i++)
                _reviewBoard.MakeMove(_imported.Moves[i]);
        }

        /// <summary>刷新回放的状态栏、最后一手标记与棋谱列表高亮。</summary>
        private void RefreshReviewUi()
        {
            if (_reviewBoard is null || _imported is null) return;

            if (_reviewIndex > 0)
            {
                var last = _imported.Moves[_reviewIndex - 1];
                _board.SetLastMove(last.From, last.To);
            }
            else
            {
                _board.SetLastMove(-1, -1);
            }

            _board.SelectedSquare = -1;
            _board.ClearLegalTargets();
            _board.Invalidate();

            UpdateReviewStatus();
            UpdateReviewSelection();
        }

        /// <summary>
        /// 算出一手棋里哪些棋子需要滑行。正向是“走这步”，
        /// 反向用于回放后退（棋子从 to 滑回 from），易位时车也要滑。
        /// </summary>
        private static List<BoardView.PieceSlide> BuildSlides(Board board, Move move, bool forward)
        {
            var slides = new List<BoardView.PieceSlide>(2);

            int from = forward ? move.From : move.To;
            int to = forward ? move.To : move.From;

            int piece = board.Squares[from];
            if (piece != 0) slides.Add(new BoardView.PieceSlide(piece, from, to));

            if (move.Kind is MoveKind.CastleKingSide or MoveKind.CastleQueenSide)
            {
                int rookFrom, rookTo;
                if (move.Kind == MoveKind.CastleKingSide)
                {
                    rookFrom = forward ? move.To + 1 : move.To - 1;
                    rookTo = forward ? move.To - 1 : move.To + 1;
                }
                else
                {
                    rookFrom = forward ? move.To - 2 : move.To + 1;
                    rookTo = forward ? move.To + 1 : move.To - 2;
                }

                int rook = board.Squares[rookFrom];
                if (rook != 0) slides.Add(new BoardView.PieceSlide(rook, rookFrom, rookTo));
            }

            return slides;
        }

        /// <summary>走这步之前，被吃掉的棋子是什么、在哪个格子（动画期间它先留在原地）。</summary>
        private static BoardView.PieceHold? CapturedPieceBefore(Board board, Move move)
        {
            int square;
            if (move.Kind == MoveKind.EnPassant)
                square = move.To + (board.SideToMove == Pc.White ? -8 : 8);
            else if (board.Squares[move.To] != 0)
                square = move.To;
            else
                return null;

            if (square < 0 || square > 63) return null;

            int piece = board.Squares[square];
            return piece == 0 ? null : new BoardView.PieceHold(piece, square);
        }

        /// <summary>
        /// 多手跳转时按“跳转前 / 跳转后”两个局面算出需要滑行的棋子：
        /// 同色同类的棋子先抵消掉原地不动的，剩下的按最近距离配对。
        /// 配不上的旧棋子表示被吃（直接消失），配不上的新棋子表示升变产生（直接出现）。
        /// </summary>
        private static List<BoardView.PieceSlide> BuildJumpSlides(Board before, Board after)
        {
            var slides = new List<BoardView.PieceSlide>(12);

            for (int color = 0; color < 2; color++)
            {
                int sign = color == Pc.White ? 1 : -1;

                for (int type = Pc.Pawn; type <= Pc.King; type++)
                {
                    int piece = sign * type;

                    var fromSquares = new List<int>();
                    var toSquares = new List<int>();

                    for (int sq = 0; sq < 64; sq++)
                    {
                        if (before.Squares[sq] == piece) fromSquares.Add(sq);
                        if (after.Squares[sq] == piece) toSquares.Add(sq);
                    }

                    // 原地没动的先排除
                    for (int i = fromSquares.Count - 1; i >= 0; i--)
                    {
                        int same = toSquares.IndexOf(fromSquares[i]);
                        if (same < 0) continue;
                        toSquares.RemoveAt(same);
                        fromSquares.RemoveAt(i);
                    }

                    // 剩下的按棋盘距离贪心配对
                    while (fromSquares.Count > 0 && toSquares.Count > 0)
                    {
                        int bestFrom = 0, bestTo = 0, bestDistance = int.MaxValue;
                        for (int f = 0; f < fromSquares.Count; f++)
                        {
                            for (int t = 0; t < toSquares.Count; t++)
                            {
                                int distance = SquareDistance(fromSquares[f], toSquares[t]);
                                if (distance >= bestDistance) continue;
                                bestDistance = distance;
                                bestFrom = f;
                                bestTo = t;
                            }
                        }

                        slides.Add(new BoardView.PieceSlide(piece, fromSquares[bestFrom], toSquares[bestTo]));
                        fromSquares.RemoveAt(bestFrom);
                        toSquares.RemoveAt(bestTo);
                    }
                }
            }

            return slides;
        }

        private static int SquareDistance(int a, int b)
        {
            int fileGap = Math.Abs((a & 7) - (b & 7));
            int rankGap = Math.Abs((a >> 3) - (b >> 3));
            return Math.Max(fileGap, rankGap);
        }

        private void UpdateReviewStatus()
        {
            if (_imported is null || _reviewBoard is null) return;

            int total = _imported.Moves.Count;
            if (_reviewIndex >= total)
            {
                _lblStatus.Text = $"回放结束 · {_imported.ResultText}";
                _lblStatus.ForeColor = Theme.Accent;
                return;
            }

            string side = _reviewBoard.SideToMove == Pc.White ? "白方" : "黑方";
            _lblStatus.Text = $"{_reviewIndex} / {total} 步 · {side}走棋";
            _lblStatus.ForeColor = Theme.Text;
        }

        /// <summary>把当前这一手滚动到可见范围，并刷新高亮。</summary>
        private void UpdateReviewSelection()
        {
            if (_moveList.Items.Count == 0) return;

            if (_reviewIndex > 0)
            {
                int row = Math.Min((_reviewIndex - 1) / 2, _moveList.Items.Count - 1);
                int visibleRows = Math.Max(1, _moveList.ClientSize.Height / Math.Max(1, _moveList.ItemHeight));
                if (row < _moveList.TopIndex || row > _moveList.TopIndex + visibleRows - 1)
                    _moveList.TopIndex = Math.Max(0, row - visibleRows / 2);
            }

            _moveList.Invalidate();
        }

        private void StepBackward()
        {
            StopAutoPlay();
            GoToPly(_reviewIndex - 1);
        }

        private void StepForward()
        {
            StopAutoPlay();
            GoToPly(_reviewIndex + 1);
        }

        private void ToggleAutoPlay()
        {
            if (!_reviewMode || _imported is null) return;

            if (_autoPlaying)
            {
                StopAutoPlay();
                return;
            }

            if (_reviewIndex >= _imported.Moves.Count)
            {
                _reviewIndex = 0;
                SyncReviewBoard();
                RefreshReviewUi();
            }

            if (_imported.Moves.Count == 0) return;

            _autoPlaying = true;
            _auto.Text = "暂停";
            _autoTimer.Start();
        }

        private void StopAutoPlay()
        {
            if (!_autoPlaying) return;
            _autoPlaying = false;
            _auto.Text = "自动";
            _autoTimer.Stop();
        }

        private void AutoAdvance()
        {
            if (_imported is null) { StopAutoPlay(); return; }
            if (_reviewIndex >= _imported.Moves.Count) { StopAutoPlay(); return; }

            GoToPly(_reviewIndex + 1);
        }

        #endregion

        #region 生命周期

        private void RequestBack()
        {
            // 电脑思考（含固定等待）期间也允许离开：置位后 AI 的后续回调会自行放弃
            _closing = true;
            BackRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>离开对局页时把棋谱落盘，并阻止正在进行的 AI 思考继续更新界面。</summary>
        public void CloseGame()
        {
            _closing = true;
            StopAutoPlay();
            SaveRecord();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _closing = true;
                _autoTimer.Stop();
                _autoTimer.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}
