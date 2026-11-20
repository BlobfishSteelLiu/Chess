using Chess.Core;

namespace Chess.Ai
{
    /// <summary>
    /// 局面评估：子力价值 + 棋子位置表 + 少量局面常识（象对、叠兵、孤兵）。
    /// 返回值相对于“轮到走棋的一方”，正数表示走棋方占优。
    /// </summary>
    public static class Evaluator
    {
        public const int MateScore = 100_000;
        public const int Infinity = 1_000_000;

        private static readonly int[] Values = { 0, 100, 320, 330, 500, 900, 20000 };

        // 以下位置表均以白方视角书写，第 0 行对应第 8 横排（a8..h8）。
        private static readonly int[] PawnTable =
        {
              0,  0,  0,  0,  0,  0,  0,  0,
             50, 50, 50, 50, 50, 50, 50, 50,
             10, 10, 20, 30, 30, 20, 10, 10,
              5,  5, 10, 25, 25, 10,  5,  5,
              0,  0,  0, 20, 20,  0,  0,  0,
              5, -5,-10,  0,  0,-10, -5,  5,
              5, 10, 10,-20,-20, 10, 10,  5,
              0,  0,  0,  0,  0,  0,  0,  0
        };

        private static readonly int[] KnightTable =
        {
            -50,-40,-30,-30,-30,-30,-40,-50,
            -40,-20,  0,  0,  0,  0,-20,-40,
            -30,  0, 10, 15, 15, 10,  0,-30,
            -30,  5, 15, 20, 20, 15,  5,-30,
            -30,  0, 15, 20, 20, 15,  0,-30,
            -30,  5, 10, 15, 15, 10,  5,-30,
            -40,-20,  0,  5,  5,  0,-20,-40,
            -50,-40,-30,-30,-30,-30,-40,-50
        };

        private static readonly int[] BishopTable =
        {
            -20,-10,-10,-10,-10,-10,-10,-20,
            -10,  0,  0,  0,  0,  0,  0,-10,
            -10,  0,  5, 10, 10,  5,  0,-10,
            -10,  5,  5, 10, 10,  5,  5,-10,
            -10,  0, 10, 10, 10, 10,  0,-10,
            -10, 10, 10, 10, 10, 10, 10,-10,
            -10,  5,  0,  0,  0,  0,  5,-10,
            -20,-10,-10,-10,-10,-10,-10,-20
        };

        private static readonly int[] RookTable =
        {
              0,  0,  0,  0,  0,  0,  0,  0,
              5, 10, 10, 10, 10, 10, 10,  5,
             -5,  0,  0,  0,  0,  0,  0, -5,
             -5,  0,  0,  0,  0,  0,  0, -5,
             -5,  0,  0,  0,  0,  0,  0, -5,
             -5,  0,  0,  0,  0,  0,  0, -5,
             -5,  0,  0,  0,  0,  0,  0, -5,
              0,  0,  0,  5,  5,  0,  0,  0
        };

        private static readonly int[] QueenTable =
        {
            -20,-10,-10, -5, -5,-10,-10,-20,
            -10,  0,  0,  0,  0,  0,  0,-10,
            -10,  0,  5,  5,  5,  5,  0,-10,
             -5,  0,  5,  5,  5,  5,  0, -5,
              0,  0,  5,  5,  5,  5,  0, -5,
            -10,  5,  5,  5,  5,  5,  0,-10,
            -10,  0,  5,  0,  0,  0,  0,-10,
            -20,-10,-10, -5, -5,-10,-10,-20
        };

        private static readonly int[] KingMidTable =
        {
            -30,-40,-40,-50,-50,-40,-40,-30,
            -30,-40,-40,-50,-50,-40,-40,-30,
            -30,-40,-40,-50,-50,-40,-40,-30,
            -30,-40,-40,-50,-50,-40,-40,-30,
            -20,-30,-30,-40,-40,-30,-30,-20,
            -10,-20,-20,-20,-20,-20,-20,-10,
             20, 20,  0,  0,  0,  0, 20, 20,
             20, 30, 10,  0,  0, 10, 30, 20
        };

        private static readonly int[] KingEndTable =
        {
            -50,-40,-30,-20,-20,-30,-40,-50,
            -30,-20,-10,  0,  0,-10,-20,-30,
            -30,-10, 20, 30, 30, 20,-10,-30,
            -30,-10, 30, 40, 40, 30,-10,-30,
            -30,-10, 30, 40, 40, 30,-10,-30,
            -30,-10, 20, 30, 30, 20,-10,-30,
            -30,-30,  0,  0,  0,  0,-30,-30,
            -50,-30,-30,-30,-30,-30,-30,-50
        };

        public static int PieceValue(int type) => type >= 0 && type < Values.Length ? Values[type] : 0;

        private static int[] TableFor(int type) => type switch
        {
            Pc.Pawn => PawnTable,
            Pc.Knight => KnightTable,
            Pc.Bishop => BishopTable,
            Pc.Rook => RookTable,
            Pc.Queen => QueenTable,
            _ => KingMidTable
        };

        public static int Evaluate(Board board)
        {
            return EvaluateAbsolute(board) * (board.SideToMove == Pc.White ? 1 : -1);
        }

        /// <summary>以白方为正返回局面分。</summary>
        public static int EvaluateAbsolute(Board board)
        {
            int score = 0;
            int whiteBishops = 0;
            int blackBishops = 0;
            int whiteMaterialExceptKing = 0;
            int blackMaterialExceptKing = 0;

            var pawnFiles = new int[2, 8];

            for (int sq = 0; sq < 64; sq++)
            {
                int piece = board.Squares[sq];
                if (piece == 0) continue;

                int type = Pc.Type(piece);
                bool isWhite = piece > 0;
                int file = sq & 7;
                int rank = sq >> 3;

                // 白方：第 8 横排对应表的第 0 行；黑方上下镜像。
                int tableIndex = isWhite ? (7 - rank) * 8 + file : rank * 8 + file;

                int value = Values[type];
                if (type == Pc.King) value = 0;

                score += isWhite ? value : -value;

                if (type != Pc.King)
                {
                    int positional = TableFor(type)[tableIndex];
                    score += isWhite ? positional : -positional;
                    if (isWhite) whiteMaterialExceptKing += value;
                    else blackMaterialExceptKing += value;
                }

                if (type == Pc.Bishop)
                {
                    if (isWhite) whiteBishops++;
                    else blackBishops++;
                }

                if (type == Pc.Pawn) pawnFiles[isWhite ? 0 : 1, file]++;
            }

            // 象对奖励
            if (whiteBishops >= 2) score += 30;
            if (blackBishops >= 2) score -= 30;

            // 兵形
            for (int file = 0; file < 8; file++)
            {
                for (int color = 0; color < 2; color++)
                {
                    int count = pawnFiles[color, file];
                    if (count == 0) continue;

                    int sign = color == 0 ? 1 : -1;
                    if (count > 1) score -= sign * 15 * (count - 1);   // 叠兵

                    bool hasNeighbour = (file > 0 && pawnFiles[color, file - 1] > 0)
                                     || (file < 7 && pawnFiles[color, file + 1] > 0);
                    if (!hasNeighbour) score -= sign * 12;             // 孤兵
                }
            }

            // 王的位置：中局用主表，残局换成残局表
            bool endgame = whiteMaterialExceptKing + blackMaterialExceptKing <= 2600;
            score += KingPositionScore(board, Pc.White, endgame);
            score -= KingPositionScore(board, Pc.Black, endgame);

            return score;
        }

        private static int KingPositionScore(Board board, int color, bool endgame)
        {
            int sq = board.KingSquare(color);
            if (sq < 0) return 0;
            int file = sq & 7;
            int rank = sq >> 3;
            int index = color == Pc.White ? (7 - rank) * 8 + file : rank * 8 + file;
            return endgame ? KingEndTable[index] : KingMidTable[index];
        }
    }
}
