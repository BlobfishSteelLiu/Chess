namespace Chess.Core
{
    /// <summary>
    /// 棋子表示工具。
    /// 棋盘上的每个格子用一个 int 表示：
    ///   0        = 空格
    ///   +1..+6   = 白方 兵/马/象/车/后/王
    ///   -1..-6   = 黑方 兵/马/象/车/后/王
    /// </summary>
    public static class Pc
    {
        public const int White = 0;
        public const int Black = 1;

        public const int Pawn = 1;
        public const int Knight = 2;
        public const int Bishop = 3;
        public const int Rook = 4;
        public const int Queen = 5;
        public const int King = 6;

        public static int Make(int type, int color) => color == White ? type : -type;

        public static int Type(int piece) => piece < 0 ? -piece : piece;

        public static int Color(int piece) => piece > 0 ? White : Black;

        public static bool IsWhite(int piece) => piece > 0;

        public static bool IsBlack(int piece) => piece < 0;

        public static int Other(int color) => color ^ 1;

        /// <summary>
        /// 使用 Unicode 国际象棋字符作为棋子。
        /// 白方使用空心字符（U+2654..U+2659），黑方使用实心字符（U+265A..U+265F）。
        /// </summary>
        public static string Glyph(int piece) => Type(piece) switch
        {
            Pawn => piece > 0 ? "\u2659" : "\u265F",
            Knight => piece > 0 ? "\u2658" : "\u265E",
            Bishop => piece > 0 ? "\u2657" : "\u265D",
            Rook => piece > 0 ? "\u2656" : "\u265C",
            Queen => piece > 0 ? "\u2655" : "\u265B",
            King => piece > 0 ? "\u2654" : "\u265A",
            _ => " "
        };

        /// <summary>
        /// 棋盘绘制专用：始终取实心字符（U+265A..U+265F），
        /// 再用填充色区分黑白，这样在浅色格和深色格上都清晰可辨。
        /// </summary>
        public static string SolidGlyph(int piece) => Type(piece) switch
        {
            Pawn => "\u265F",
            Knight => "\u265E",
            Bishop => "\u265D",
            Rook => "\u265C",
            Queen => "\u265B",
            King => "\u265A",
            _ => " "
        };

        /// <summary>棋子的中文名称。</summary>
        public static string ChineseName(int piece) => Type(piece) switch
        {
            Pawn => "兵",
            Knight => "马",
            Bishop => "象",
            Rook => "车",
            Queen => "后",
            King => "王",
            _ => "空"
        };

        /// <summary>走子记谱用的字母（兵用空串）。</summary>
        public static string Letter(int type) => type switch
        {
            Knight => "N",
            Bishop => "B",
            Rook => "R",
            Queen => "Q",
            King => "K",
            _ => ""
        };

        /// <summary>把格子编号转换为国际通用名称，例如 4 -> "e1"。</summary>
        public static string SquareName(int square)
        {
            if (square < 0 || square > 63) return "-";
            return string.Concat((char)('a' + (square & 7)), (char)('1' + (square >> 3)));
        }
    }
}
