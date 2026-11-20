using System.Text;

namespace Chess.Core
{
    public enum GameState
    {
        Ongoing,
        WhiteWin,
        BlackWin,
        Draw
    }

    /// <summary>
    /// 一局棋的完整状态与规则判定：将杀、逼和、五十步、三次重复、死局。
    /// </summary>
    public sealed class GameSession
    {
        public Board Board { get; } = new();
        public List<Move> Moves { get; } = new();
        public List<string> SanMoves { get; } = new();

        public GameState State { get; private set; } = GameState.Ongoing;
        public string StatusText { get; private set; } = "";
        public string EndReason { get; private set; } = "";

        /// <summary>PGN 结果标记：1-0 / 0-1 / 1/2-1/2 / *</summary>
        public string ResultTag { get; private set; } = "*";

        private readonly Dictionary<string, int> _repetitions = new(StringComparer.Ordinal);

        public GameSession() => Reset();

        public void Reset()
        {
            Board.LoadStartPosition();
            Moves.Clear();
            SanMoves.Clear();
            _repetitions.Clear();
            State = GameState.Ongoing;
            ResultTag = "*";
            EndReason = "";
            _repetitions[Board.PositionKey()] = 1;
            StatusText = "白方走棋";
        }

        public List<Move> LegalMoves() => Board.GenerateLegalMoves();

        /// <summary>执行一步棋，返回它的 SAN 记号。</summary>
        public string Play(Move move)
        {
            string san = San.ToSan(Board, move);
            Board.MakeMove(move);
            Moves.Add(move);
            SanMoves.Add(san);

            string key = Board.PositionKey();
            _repetitions[key] = _repetitions.TryGetValue(key, out int count) ? count + 1 : 1;

            EvaluateState();
            return san;
        }

        /// <summary>撤销最近的若干步。</summary>
        public void UndoPlies(int plies)
        {
            for (int i = 0; i < plies && Moves.Count > 0; i++)
            {
                string key = Board.PositionKey();
                if (_repetitions.TryGetValue(key, out int count))
                {
                    if (count <= 1) _repetitions.Remove(key);
                    else _repetitions[key] = count - 1;
                }

                Board.UnmakeMove();
                Moves.RemoveAt(Moves.Count - 1);
                SanMoves.RemoveAt(SanMoves.Count - 1);
            }

            State = GameState.Ongoing;
            ResultTag = "*";
            EndReason = "";
            EvaluateState();
        }

        private void EvaluateState()
        {
            var legal = Board.GenerateLegalMoves();
            bool inCheck = Board.IsInCheck(Board.SideToMove);

            if (legal.Count == 0)
            {
                if (inCheck)
                {
                    bool whiteWins = Board.SideToMove == Pc.Black;
                    State = whiteWins ? GameState.WhiteWin : GameState.BlackWin;
                    ResultTag = whiteWins ? "1-0" : "0-1";
                    EndReason = "将杀";
                    StatusText = (whiteWins ? "白方" : "黑方") + "胜 · 将杀";
                }
                else
                {
                    State = GameState.Draw;
                    ResultTag = "1/2-1/2";
                    EndReason = "逼和";
                    StatusText = "和棋 · 逼和（无子可动）";
                }
                return;
            }

            if (Board.HalfmoveClock >= 100)
            {
                State = GameState.Draw;
                ResultTag = "1/2-1/2";
                EndReason = "五十步规则";
                StatusText = "和棋 · 五十步规则";
                return;
            }

            if (_repetitions.TryGetValue(Board.PositionKey(), out int repeats) && repeats >= 3)
            {
                State = GameState.Draw;
                ResultTag = "1/2-1/2";
                EndReason = "三次重复局面";
                StatusText = "和棋 · 三次重复局面";
                return;
            }

            if (Board.IsInsufficientMaterial())
            {
                State = GameState.Draw;
                ResultTag = "1/2-1/2";
                EndReason = "子力不足";
                StatusText = "和棋 · 子力不足";
                return;
            }

            State = GameState.Ongoing;
            ResultTag = "*";
            EndReason = "";
            StatusText = (Board.SideToMove == Pc.White ? "白方" : "黑方") + "走棋";
        }

        /// <summary>按 PGN 惯例把走法排列成段落，超过 80 列换行。</summary>
        public static string BuildMoveText(IReadOnlyList<string> sanMoves, string resultTag)
        {
            if (sanMoves.Count == 0)
                return resultTag == "*" ? "*" : resultTag;

            var tokens = new List<string>(sanMoves.Count + 8);
            for (int i = 0; i < sanMoves.Count; i++)
            {
                if (i % 2 == 0) tokens.Add($"{i / 2 + 1}.");
                tokens.Add(sanMoves[i]);
            }
            tokens.Add(resultTag);

            var sb = new StringBuilder();
            int lineLength = 0;
            foreach (var token in tokens)
            {
                if (lineLength == 0)
                {
                    sb.Append(token);
                    lineLength = token.Length;
                }
                else if (lineLength + 1 + token.Length > 80)
                {
                    sb.Append('\n').Append(token);
                    lineLength = token.Length;
                }
                else
                {
                    sb.Append(' ').Append(token);
                    lineLength += 1 + token.Length;
                }
            }
            return sb.ToString();
        }
    }
}
