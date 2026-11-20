using System.Text;

namespace Chess.Core
{
    /// <summary>
    /// 生成标准代数记谱法（SAN, Standard Algebraic Notation），
    /// 也就是 PGN 棋谱中使用的走法写法。
    /// </summary>
    public static class San
    {
        /// <summary>
        /// 把一步棋转换为 SAN 记号。传入的 board 必须处于“走这一步之前”的局面。
        /// </summary>
        public static string ToSan(Board board, Move move)
        {
            var legal = board.GenerateLegalMoves();
            string text = BuildText(board, move, legal);

            int mover = board.SideToMove;
            board.MakeMove(move);
            bool inCheck = board.IsInCheck(Pc.Other(mover));
            bool hasReply = board.GenerateLegalMoves().Count > 0;
            board.UnmakeMove();

            if (inCheck) text += hasReply ? "+" : "#";
            return text;
        }

        private static string BuildText(Board board, Move move, List<Move> legal)
        {
            if (move.IsCastle)
                return move.Kind == MoveKind.CastleKingSide ? "O-O" : "O-O-O";

            int piece = board.Squares[move.From];
            int type = Pc.Type(piece);
            int color = Pc.Color(piece);

            bool isCapture = move.Kind == MoveKind.EnPassant || board.Squares[move.To] != 0;

            if (type == Pc.Pawn)
            {
                var sb = new StringBuilder(8);
                if (isCapture)
                {
                    sb.Append((char)('a' + (move.From & 7)));
                    sb.Append('x');
                }
                sb.Append(Pc.SquareName(move.To));
                if (move.Promotion != 0) sb.Append('=').Append(Pc.Letter(move.Promotion));
                return sb.ToString();
            }

            var builder = new StringBuilder(8);
            builder.Append(Pc.Letter(type));
            builder.Append(Disambiguate(board, legal, move, type, color));
            if (isCapture) builder.Append('x');
            builder.Append(Pc.SquareName(move.To));
            return builder.ToString();
        }

        /// <summary>
        /// 当同一种棋子可以走到同一个格子时，用起点所在纵线 / 横线 / 完整起点来消歧。
        /// </summary>
        private static string Disambiguate(Board board, List<Move> legal, Move move, int type, int color)
        {
            bool needFile = false;
            bool needRank = false;
            bool ambiguous = false;

            foreach (var other in legal)
            {
                if (other.From == move.From || other.To != move.To) continue;
                int p = board.Squares[other.From];
                if (p == 0 || Pc.Type(p) != type || Pc.Color(p) != color) continue;

                ambiguous = true;
                if ((other.From & 7) == (move.From & 7)) needRank = true;
                else needFile = true;
            }

            if (!ambiguous) return string.Empty;

            // 若所有竞争者都不同纵线，只写纵线即可；否则若都不同横线，只写横线；再否则写完整起点。
            if (!needRank && needFile) return ((char)('a' + (move.From & 7))).ToString();
            if (needRank && !needFile) return ((char)('1' + (move.From >> 3))).ToString();
            return Pc.SquareName(move.From);
        }
    }
}
