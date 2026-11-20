namespace Chess.Core
{
    public enum MoveKind
    {
        /// <summary>普通走子 / 吃子。</summary>
        Normal,
        /// <summary>兵首次前进两格。</summary>
        DoublePush,
        /// <summary>吃过路兵。</summary>
        EnPassant,
        /// <summary>短易位（王翼）。</summary>
        CastleKingSide,
        /// <summary>长易位（后翼）。</summary>
        CastleQueenSide
    }

    /// <summary>
    /// 一步棋的描述。只记录“从哪里到哪里、升变成什么、特殊类型”，
    /// 执行时需要的还原信息保存在 <see cref="Board"/> 内部的历史栈里。
    /// </summary>
    public readonly struct Move : IEquatable<Move>
    {
        public readonly int From;
        public readonly int To;
        /// <summary>升变棋子类型（0 表示不升变）。</summary>
        public readonly int Promotion;
        public readonly MoveKind Kind;

        public Move(int from, int to, int promotion = 0, MoveKind kind = MoveKind.Normal)
        {
            From = from;
            To = to;
            Promotion = promotion;
            Kind = kind;
        }

        public bool IsCastle => Kind == MoveKind.CastleKingSide || Kind == MoveKind.CastleQueenSide;

        public bool Equals(Move other)
            => From == other.From && To == other.To && Promotion == other.Promotion && Kind == other.Kind;

        public override bool Equals(object? obj) => obj is Move m && Equals(m);

        public override int GetHashCode() => From | (To << 6) | (Promotion << 12) | ((int)Kind << 15);

        public override string ToString()
            => Pc.SquareName(From) + Pc.SquareName(To) + (Promotion != 0 ? "=" + Pc.Letter(Promotion) : "");
    }
}
