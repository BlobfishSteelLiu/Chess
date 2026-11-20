using System.Diagnostics;
using Chess.Core;

namespace Chess.Ai
{
    public enum Difficulty
    {
        Easy,
        Normal,
        Hard
    }

    public static class DifficultyNames
    {
        public static string Chinese(Difficulty difficulty) => difficulty switch
        {
            Difficulty.Easy => "简单",
            Difficulty.Normal => "普通",
            _ => "困难"
        };

        public static string Description(Difficulty difficulty) => difficulty switch
        {
            Difficulty.Easy => "只会看一两步，偶尔会走漏着",
            Difficulty.Normal => "能算出常见的捉子与兑子",
            _ => "深度搜索，会做长远规划"
        };
    }

    /// <summary>
    /// 以 Alpha-Beta 剪枝 + 静态搜索（quiescence）实现的对弈引擎。
    /// </summary>
    public sealed class AiEngine
    {
        private const int MaxPly = 128;

        private readonly int _maxDepth;
        private readonly int _timeLimitMs;
        private readonly double _randomMoveRate;
        private readonly Random _random = new();

        private readonly List<Move>[] _moveBuffer = new List<Move>[MaxPly + 1];
        private readonly int[][] _scoreBuffer = new int[MaxPly + 1][];
        private readonly int[] _killer1 = new int[MaxPly + 1];
        private readonly int[] _killer2 = new int[MaxPly + 1];
        private readonly int[,] _history = new int[64, 64];

        private readonly Stopwatch _clock = new();
        private long _nodes;
        private bool _aborted;

        public AiEngine(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Easy:
                    _maxDepth = 2;
                    _timeLimitMs = 500;
                    _randomMoveRate = 0.45;
                    break;
                case Difficulty.Normal:
                    _maxDepth = 4;
                    _timeLimitMs = 1800;
                    _randomMoveRate = 0.0;
                    break;
                default:
                    _maxDepth = 6;
                    _timeLimitMs = 4500;
                    _randomMoveRate = 0.0;
                    break;
            }

            for (int i = 0; i <= MaxPly; i++)
            {
                _moveBuffer[i] = new List<Move>(64);
                _scoreBuffer[i] = new int[64];
            }
        }

        public Difficulty Difficulty
        {
            get => _maxDepth <= 2 ? Difficulty.Easy : _maxDepth <= 4 ? Difficulty.Normal : Difficulty.Hard;
        }

        /// <summary>一次搜索的结果摘要，用于强度评估或显示思考信息。</summary>
        public sealed class SearchInfo
        {
            /// <summary>根节点得分，正数表示走棋方占优（单位约等于 1/100 兵）。</summary>
            public int Score;
            /// <summary>实际完成搜索的层数。</summary>
            public int Depth;
            public long Nodes;
            public int ElapsedMs;
            public int LegalMoves;
            public bool Aborted;
            public bool Random;
        }

        /// <summary>为当前局面找出引擎认为最好的一步；没有合法走法时返回 null。</summary>
        public Move? FindBestMove(Board position) => FindBestMove(position, out _);

        /// <summary>同上，并返回本次搜索的统计信息。</summary>
        public Move? FindBestMove(Board position, out SearchInfo info)
        {
            info = new SearchInfo();
            var board = position.Clone();
            var rootMoves = board.GenerateLegalMoves();
            info.LegalMoves = rootMoves.Count;

            if (rootMoves.Count == 0) return null;
            if (rootMoves.Count == 1)
            {
                info.Nodes = 1;
                return rootMoves[0];
            }

            if (_randomMoveRate > 0 && _random.NextDouble() < _randomMoveRate)
            {
                info.Random = true;
                return rootMoves[_random.Next(rootMoves.Count)];
            }

            _clock.Restart();
            _nodes = 0;
            _aborted = false;
            Array.Clear(_history);
            Array.Clear(_killer1);
            Array.Clear(_killer2);

            Move best = rootMoves[0];
            int bestScore = -Evaluator.Infinity;
            int completedDepth = 0;
            var scores = new int[rootMoves.Count];
            var order = new int[rootMoves.Count];
            for (int i = 0; i < order.Length; i++) order[i] = i;

            for (int depth = 1; depth <= _maxDepth; depth++)
            {
                int alpha = -Evaluator.Infinity;
                int iterBestScore = -Evaluator.Infinity;
                Move iterBest = default;
                bool completed = true;

                for (int i = 0; i < order.Length; i++)
                {
                    var move = rootMoves[order[i]];
                    board.MakeMove(move);
                    int value = -AlphaBeta(board, depth - 1, -Evaluator.Infinity, -alpha, 1);
                    board.UnmakeMove();

                    if (_aborted) { completed = false; break; }

                    scores[order[i]] = value;
                    if (value > iterBestScore)
                    {
                        iterBestScore = value;
                        iterBest = move;
                    }
                    if (value > alpha) alpha = value;
                }

                // 只采用完整搜完的那一层的结果，避免超时中断带来的错误结论
                if (completed && iterBestScore > -Evaluator.Infinity)
                {
                    best = iterBest;
                    bestScore = iterBestScore;
                    completedDepth = depth;
                }

                if (!completed) break;

                // 按本层得分重新排序，下一层先搜更有希望的走法
                Array.Sort(order, (a, b) => scores[b].CompareTo(scores[a]));

                if (bestScore > Evaluator.MateScore - 1000) break;   // 已经找到杀棋
                if (_clock.ElapsedMilliseconds > _timeLimitMs) break;
            }

            info.Score = bestScore;
            info.Depth = completedDepth;
            info.Nodes = _nodes;
            info.ElapsedMs = (int)_clock.ElapsedMilliseconds;
            info.Aborted = _aborted;
            return best;
        }

        private int AlphaBeta(Board board, int depth, int alpha, int beta, int ply)
        {
            if (ply >= MaxPly - 2) return Evaluator.Evaluate(board);

            if ((++_nodes & 1023) == 0 && _clock.ElapsedMilliseconds > _timeLimitMs)
            {
                _aborted = true;
                return 0;
            }

            bool inCheck = board.IsInCheck(board.SideToMove);
            if (inCheck && ply < 40) depth++;      // 将军延伸，但限制深度避免无限循环
            if (depth <= 0) return Quiescence(board, alpha, beta, ply);

            var moves = _moveBuffer[ply];
            moves.Clear();
            board.GenerateMoves(moves);

            if (moves.Count == 0)
                return inCheck ? -Evaluator.MateScore + ply : 0;

            var scores = GetScoreBuffer(ply, moves.Count);
            for (int i = 0; i < moves.Count; i++) scores[i] = ScoreMove(board, moves[i], ply);

            int best = -Evaluator.Infinity;
            int legalCount = 0;

            for (int i = 0; i < moves.Count; i++)
            {
                int pick = i;
                for (int j = i + 1; j < moves.Count; j++)
                    if (scores[j] > scores[pick]) pick = j;

                if (pick != i)
                {
                    (moves[i], moves[pick]) = (moves[pick], moves[i]);
                    (scores[i], scores[pick]) = (scores[pick], scores[i]);
                }

                var move = moves[i];
                int mover = board.SideToMove;
                board.MakeMove(move);
                if (board.IsInCheck(mover))
                {
                    board.UnmakeMove();
                    continue;
                }

                legalCount++;
                int value = -AlphaBeta(board, depth - 1, -beta, -alpha, ply + 1);
                board.UnmakeMove();

                if (_aborted) return 0;
                if (value > best) best = value;
                if (value > alpha) alpha = value;

                if (alpha >= beta)
                {
                    if (!IsCapture(board, move))
                    {
                        int key = move.From * 64 + move.To;
                        _killer2[ply] = _killer1[ply];
                        _killer1[ply] = key;
                        _history[move.From, move.To] += depth * depth;
                    }
                    break;
                }
            }

            if (legalCount == 0)
                return inCheck ? -Evaluator.MateScore + ply : 0;

            return best;
        }

        private int Quiescence(Board board, int alpha, int beta, int ply)
        {
            if (ply >= MaxPly - 2) return Evaluator.Evaluate(board);

            if ((++_nodes & 1023) == 0 && _clock.ElapsedMilliseconds > _timeLimitMs)
            {
                _aborted = true;
                return 0;
            }

            int standPat = Evaluator.Evaluate(board);
            if (standPat >= beta) return beta;
            if (standPat > alpha) alpha = standPat;

            var moves = _moveBuffer[ply];
            moves.Clear();
            board.GenerateMoves(moves, capturesOnly: true);

            var scores = GetScoreBuffer(ply, moves.Count);
            for (int i = 0; i < moves.Count; i++) scores[i] = ScoreMove(board, moves[i], ply);

            for (int i = 0; i < moves.Count; i++)
            {
                int pick = i;
                for (int j = i + 1; j < moves.Count; j++)
                    if (scores[j] > scores[pick]) pick = j;

                if (pick != i)
                {
                    (moves[i], moves[pick]) = (moves[pick], moves[i]);
                    (scores[i], scores[pick]) = (scores[pick], scores[i]);
                }

                var move = moves[i];
                int mover = board.SideToMove;
                board.MakeMove(move);
                if (board.IsInCheck(mover))
                {
                    board.UnmakeMove();
                    continue;
                }

                int value = -Quiescence(board, -beta, -alpha, ply + 1);
                board.UnmakeMove();

                if (_aborted) return 0;
                if (value >= beta) return beta;
                if (value > alpha) alpha = value;
            }

            return alpha;
        }

        private int[] GetScoreBuffer(int ply, int needed)
        {
            var buffer = _scoreBuffer[ply];
            if (buffer.Length < needed)
            {
                buffer = new int[Math.Max(needed, 128)];
                _scoreBuffer[ply] = buffer;
            }
            return buffer;
        }

        private static bool IsCapture(Board board, Move move)
            => move.Kind == MoveKind.EnPassant || board.Squares[move.To] != 0;

        private int ScoreMove(Board board, Move move, int ply)
        {
            int key = move.From * 64 + move.To;

            if (move.Kind == MoveKind.EnPassant)
                return 100_000 + Evaluator.PieceValue(Pc.Pawn) * 10 - Evaluator.PieceValue(Pc.Pawn);

            int target = board.Squares[move.To];
            if (target != 0)
            {
                int victim = Evaluator.PieceValue(Pc.Type(target));
                int attacker = Evaluator.PieceValue(Pc.Type(board.Squares[move.From]));
                return 100_000 + victim * 10 - attacker;
            }

            if (move.Promotion != 0)
                return 90_000 + Evaluator.PieceValue(move.Promotion);

            if (key == _killer1[ply]) return 80_000;
            if (key == _killer2[ply]) return 79_000;

            return _history[move.From, move.To];
        }
    }
}
