using System.Text;

namespace Chess.Core
{
    /// <summary>
    /// 棋盘与走子规则引擎。
    /// 格子编号：a1 = 0, h1 = 7, a8 = 56, h8 = 63；
    /// index = rank * 8 + file，其中 rank 0 为第 1 横排。
    /// </summary>
    public sealed class Board
    {
        public const string StartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        public const int CastleWhiteKingSide = 1;
        public const int CastleWhiteQueenSide = 2;
        public const int CastleBlackKingSide = 4;
        public const int CastleBlackQueenSide = 8;
        public const int CastleAll = 15;

        public int[] Squares = new int[64];
        public int SideToMove = Pc.White;
        public int CastlingRights = CastleAll;
        public int EnPassantSquare = -1;
        public int HalfmoveClock;
        public int FullmoveNumber = 1;

        private UndoState[] _undo = new UndoState[512];
        private int _undoCount;

        #region 静态表

        private static readonly int[] CaptureFiles = { -1, 1 };
        private static readonly int[] DoubleRank = { -2, 2 };

        private static readonly int[,] KnightDeltas =
        {
            { 1, 2 }, { 2, 1 }, { 2, -1 }, { 1, -2 }, { -1, -2 }, { -2, -1 }, { -2, 1 }, { -1, 2 }
        };

        private static readonly int[,] KingDeltas =
        {
            { 1, 0 }, { 1, 1 }, { 0, 1 }, { -1, 1 }, { -1, 0 }, { -1, -1 }, { 0, -1 }, { 1, -1 }
        };

        private static readonly int[,] BishopDirs = { { 1, 1 }, { 1, -1 }, { -1, 1 }, { -1, -1 } };
        private static readonly int[,] RookDirs = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };

        /// <summary>走子或吃子发生在这些格子上时，需要清除对应的易位权。</summary>
        private static readonly int[] CastleMask = BuildCastleMask();

        private static int[] BuildCastleMask()
        {
            var mask = new int[64];
            for (int i = 0; i < 64; i++) mask[i] = CastleAll;
            mask[4] = CastleAll & ~(CastleWhiteKingSide | CastleWhiteQueenSide);   // e1
            mask[7] = CastleAll & ~CastleWhiteKingSide;                            // h1
            mask[0] = CastleAll & ~CastleWhiteQueenSide;                           // a1
            mask[60] = CastleAll & ~(CastleBlackKingSide | CastleBlackQueenSide);  // e8
            mask[63] = CastleAll & ~CastleBlackKingSide;                           // h8
            mask[56] = CastleAll & ~CastleBlackQueenSide;                          // a8
            return mask;
        }

        #endregion

        #region 初始化

        public void LoadStartPosition() => LoadFen(StartFen);

        public void Clear()
        {
            Array.Clear(Squares);
            SideToMove = Pc.White;
            CastlingRights = 0;
            EnPassantSquare = -1;
            HalfmoveClock = 0;
            FullmoveNumber = 1;
            _undoCount = 0;
        }

        public void LoadFen(string fen)
        {
            Clear();
            string[] parts = fen.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 1) throw new ArgumentException("FEN 格式不正确。", nameof(fen));

            int rank = 7;
            int file = 0;
            foreach (char c in parts[0])
            {
                if (c == '/')
                {
                    rank--;
                    file = 0;
                    if (rank < 0) throw new ArgumentException("FEN 行数过多。", nameof(fen));
                    continue;
                }
                if (char.IsDigit(c))
                {
                    file += c - '0';
                    if (file > 8) throw new ArgumentException("FEN 每行超过 8 格。", nameof(fen));
                    continue;
                }
                int piece = CharToPiece(c);
                if (piece == 0) throw new ArgumentException($"FEN 中出现无法识别的字符 '{c}'。", nameof(fen));
                if (file > 7) throw new ArgumentException("FEN 每行超过 8 格。", nameof(fen));
                Squares[rank * 8 + file] = piece;
                file++;
            }

            if (parts.Length > 1)
                SideToMove = parts[1].Length > 0 && char.ToLowerInvariant(parts[1][0]) == 'b' ? Pc.Black : Pc.White;

            if (parts.Length > 2 && parts[2] != "-")
            {
                CastlingRights = 0;
                foreach (char c in parts[2])
                {
                    CastlingRights |= c switch
                    {
                        'K' => CastleWhiteKingSide,
                        'Q' => CastleWhiteQueenSide,
                        'k' => CastleBlackKingSide,
                        'q' => CastleBlackQueenSide,
                        _ => 0
                    };
                }
            }
            else if (parts.Length > 2)
            {
                CastlingRights = 0;
            }

            EnPassantSquare = -1;
            if (parts.Length > 3 && parts[3] != "-")
            {
                int sq = ParseSquare(parts[3]);
                if (sq >= 0) EnPassantSquare = sq;
            }

            HalfmoveClock = parts.Length > 4 && int.TryParse(parts[4], out int hm) ? hm : 0;
            FullmoveNumber = parts.Length > 5 && int.TryParse(parts[5], out int fm) ? fm : 1;
        }

        private static int CharToPiece(char c) => c switch
        {
            'P' => Pc.Pawn, 'N' => Pc.Knight, 'B' => Pc.Bishop,
            'R' => Pc.Rook, 'Q' => Pc.Queen, 'K' => Pc.King,
            'p' => -Pc.Pawn, 'n' => -Pc.Knight, 'b' => -Pc.Bishop,
            'r' => -Pc.Rook, 'q' => -Pc.Queen, 'k' => -Pc.King,
            _ => 0
        };

        private static char PieceToChar(int p) => Pc.Type(p) switch
        {
            Pc.Pawn => 'p', Pc.Knight => 'n', Pc.Bishop => 'b',
            Pc.Rook => 'r', Pc.Queen => 'q', Pc.King => 'k',
            _ => '?'
        };

        private static int ParseSquare(string name)
        {
            if (name.Length < 2) return -1;
            int f = char.ToLowerInvariant(name[0]) - 'a';
            int r = name[1] - '1';
            if (f < 0 || f > 7 || r < 0 || r > 7) return -1;
            return r * 8 + f;
        }

        public Board Clone()
        {
            var copy = new Board
            {
                SideToMove = SideToMove,
                CastlingRights = CastlingRights,
                EnPassantSquare = EnPassantSquare,
                HalfmoveClock = HalfmoveClock,
                FullmoveNumber = FullmoveNumber
            };
            Array.Copy(Squares, copy.Squares, 64);
            return copy;
        }

        #endregion

        #region FEN 输出

        public string ToFen()
        {
            var sb = new StringBuilder(96);
            for (int rank = 7; rank >= 0; rank--)
            {
                int empty = 0;
                for (int file = 0; file < 8; file++)
                {
                    int p = Squares[rank * 8 + file];
                    if (p == 0) { empty++; continue; }
                    if (empty > 0) { sb.Append(empty); empty = 0; }
                    char c = PieceToChar(p);
                    sb.Append(p > 0 ? char.ToUpperInvariant(c) : c);
                }
                if (empty > 0) sb.Append(empty);
                if (rank > 0) sb.Append('/');
            }

            sb.Append(' ').Append(SideToMove == Pc.White ? 'w' : 'b').Append(' ');

            if (CastlingRights == 0) sb.Append('-');
            else
            {
                if ((CastlingRights & CastleWhiteKingSide) != 0) sb.Append('K');
                if ((CastlingRights & CastleWhiteQueenSide) != 0) sb.Append('Q');
                if ((CastlingRights & CastleBlackKingSide) != 0) sb.Append('k');
                if ((CastlingRights & CastleBlackQueenSide) != 0) sb.Append('q');
            }

            sb.Append(' ').Append(EnPassantSquare < 0 ? "-" : Pc.SquareName(EnPassantSquare));
            sb.Append(' ').Append(HalfmoveClock).Append(' ').Append(FullmoveNumber);
            return sb.ToString();
        }

        /// <summary>用于重复局面判定的局面键（不含回合计数）。</summary>
        public string PositionKey()
        {
            string fen = ToFen();
            int space = 0;
            int cut = -1;
            for (int i = 0; i < fen.Length; i++)
            {
                if (fen[i] != ' ') continue;
                space++;
                if (space == 4) { cut = i; break; }
            }
            return cut > 0 ? fen[..cut] : fen;
        }

        #endregion

        #region 走法生成

        /// <summary>生成所有伪合法走法（不检查走完之后己方王是否被将）。</summary>
        public void GenerateMoves(List<Move> moves, bool capturesOnly = false)
        {
            int color = SideToMove;
            for (int sq = 0; sq < 64; sq++)
            {
                int piece = Squares[sq];
                if (piece == 0 || Pc.Color(piece) != color) continue;

                switch (Pc.Type(piece))
                {
                    case Pc.Pawn:
                        GeneratePawnMoves(moves, sq, color, capturesOnly);
                        break;
                    case Pc.Knight:
                        GenerateStepMoves(moves, sq, color, KnightDeltas, capturesOnly);
                        break;
                    case Pc.King:
                        GenerateStepMoves(moves, sq, color, KingDeltas, capturesOnly);
                        break;
                    case Pc.Bishop:
                        GenerateSlideMoves(moves, sq, color, BishopDirs, capturesOnly);
                        break;
                    case Pc.Rook:
                        GenerateSlideMoves(moves, sq, color, RookDirs, capturesOnly);
                        break;
                    case Pc.Queen:
                        GenerateSlideMoves(moves, sq, color, BishopDirs, capturesOnly);
                        GenerateSlideMoves(moves, sq, color, RookDirs, capturesOnly);
                        break;
                }
            }

            if (!capturesOnly) GenerateCastleMoves(moves, color);
        }

        /// <summary>生成全部合法走法（已过滤掉会导致己方被将的走法）。</summary>
        public List<Move> GenerateLegalMoves()
        {
            var pseudo = new List<Move>(64);
            GenerateMoves(pseudo);
            var legal = new List<Move>(pseudo.Count);
            foreach (var m in pseudo)
            {
                int mover = SideToMove;
                MakeMove(m);
                if (!IsInCheck(mover)) legal.Add(m);
                UnmakeMove();
            }
            return legal;
        }

        private void GeneratePawnMoves(List<Move> moves, int from, int color, bool capturesOnly)
        {
            int f = from & 7;
            int r = from >> 3;
            int step = color == Pc.White ? 1 : -1;
            int startRank = color == Pc.White ? 1 : 6;
            int promoRank = color == Pc.White ? 7 : 0;

            int nr = r + step;
            if (nr >= 0 && nr <= 7)
            {
                int one = nr * 8 + f;
                if (Squares[one] == 0)
                {
                    if (!capturesOnly) AddPawnMove(moves, from, one, nr == promoRank);

                    if (r == startRank && !capturesOnly)
                    {
                        int two = (nr + step) * 8 + f;
                        if (Squares[two] == 0)
                            moves.Add(new Move(from, two, 0, MoveKind.DoublePush));
                    }
                }

                foreach (int df in CaptureFiles)
                {
                    int nf = f + df;
                    if (nf < 0 || nf > 7) continue;
                    int to = nr * 8 + nf;
                    int target = Squares[to];

                    if (target != 0 && Pc.Color(target) != color)
                        AddPawnMove(moves, from, to, nr == promoRank);
                    else if (target == 0 && to == EnPassantSquare)
                        moves.Add(new Move(from, to, 0, MoveKind.EnPassant));
                }
            }
        }

        private static void AddPawnMove(List<Move> moves, int from, int to, bool isPromotion)
        {
            if (!isPromotion)
            {
                moves.Add(new Move(from, to));
                return;
            }
            moves.Add(new Move(from, to, Pc.Queen));
            moves.Add(new Move(from, to, Pc.Rook));
            moves.Add(new Move(from, to, Pc.Bishop));
            moves.Add(new Move(from, to, Pc.Knight));
        }

        private void GenerateStepMoves(List<Move> moves, int from, int color, int[,] deltas, bool capturesOnly)
        {
            int f = from & 7;
            int r = from >> 3;
            int count = deltas.GetLength(0);
            for (int i = 0; i < count; i++)
            {
                int nf = f + deltas[i, 0];
                int nr = r + deltas[i, 1];
                if (nf < 0 || nf > 7 || nr < 0 || nr > 7) continue;

                int to = nr * 8 + nf;
                int target = Squares[to];
                if (target == 0)
                {
                    if (!capturesOnly) moves.Add(new Move(from, to));
                }
                else if (Pc.Color(target) != color)
                {
                    moves.Add(new Move(from, to));
                }
            }
        }

        private void GenerateSlideMoves(List<Move> moves, int from, int color, int[,] dirs, bool capturesOnly)
        {
            int f = from & 7;
            int r = from >> 3;
            int count = dirs.GetLength(0);
            for (int i = 0; i < count; i++)
            {
                int df = dirs[i, 0];
                int dr = dirs[i, 1];
                int nf = f + df;
                int nr = r + dr;
                while (nf >= 0 && nf <= 7 && nr >= 0 && nr <= 7)
                {
                    int to = nr * 8 + nf;
                    int target = Squares[to];
                    if (target == 0)
                    {
                        if (!capturesOnly) moves.Add(new Move(from, to));
                    }
                    else
                    {
                        if (Pc.Color(target) != color) moves.Add(new Move(from, to));
                        break;
                    }
                    nf += df;
                    nr += dr;
                }
            }
        }

        private void GenerateCastleMoves(List<Move> moves, int color)
        {
            if (color == Pc.White)
            {
                if ((CastlingRights & CastleWhiteKingSide) != 0
                    && Squares[4] == Pc.King && Squares[7] == Pc.Rook
                    && Squares[5] == 0 && Squares[6] == 0
                    && !IsSquareAttacked(4, Pc.Black)
                    && !IsSquareAttacked(5, Pc.Black)
                    && !IsSquareAttacked(6, Pc.Black))
                {
                    moves.Add(new Move(4, 6, 0, MoveKind.CastleKingSide));
                }

                if ((CastlingRights & CastleWhiteQueenSide) != 0
                    && Squares[4] == Pc.King && Squares[0] == Pc.Rook
                    && Squares[1] == 0 && Squares[2] == 0 && Squares[3] == 0
                    && !IsSquareAttacked(4, Pc.Black)
                    && !IsSquareAttacked(3, Pc.Black)
                    && !IsSquareAttacked(2, Pc.Black))
                {
                    moves.Add(new Move(4, 2, 0, MoveKind.CastleQueenSide));
                }
            }
            else
            {
                if ((CastlingRights & CastleBlackKingSide) != 0
                    && Squares[60] == -Pc.King && Squares[63] == -Pc.Rook
                    && Squares[61] == 0 && Squares[62] == 0
                    && !IsSquareAttacked(60, Pc.White)
                    && !IsSquareAttacked(61, Pc.White)
                    && !IsSquareAttacked(62, Pc.White))
                {
                    moves.Add(new Move(60, 62, 0, MoveKind.CastleKingSide));
                }

                if ((CastlingRights & CastleBlackQueenSide) != 0
                    && Squares[60] == -Pc.King && Squares[56] == -Pc.Rook
                    && Squares[57] == 0 && Squares[58] == 0 && Squares[59] == 0
                    && !IsSquareAttacked(60, Pc.White)
                    && !IsSquareAttacked(59, Pc.White)
                    && !IsSquareAttacked(58, Pc.White))
                {
                    moves.Add(new Move(60, 58, 0, MoveKind.CastleQueenSide));
                }
            }
        }

        #endregion

        #region 攻击判定

        /// <summary>判断 square 是否正被 byColor 一方的任意棋子攻击。</summary>
        public bool IsSquareAttacked(int square, int byColor)
        {
            int f = square & 7;
            int r = square >> 3;

            // 兵：白兵在 (f±1, r-1) 处攻击 (f, r)，黑兵在 (f±1, r+1) 处攻击。
            int pawnRank = byColor == Pc.White ? r - 1 : r + 1;
            if (pawnRank >= 0 && pawnRank <= 7)
            {
                int pawn = byColor == Pc.White ? Pc.Pawn : -Pc.Pawn;
                foreach (int df in CaptureFiles)
                {
                    int pf = f + df;
                    if (pf < 0 || pf > 7) continue;
                    if (Squares[pawnRank * 8 + pf] == pawn) return true;
                }
            }

            // 马
            int knight = byColor == Pc.White ? Pc.Knight : -Pc.Knight;
            for (int i = 0; i < 8; i++)
            {
                int nf = f + KnightDeltas[i, 0];
                int nr = r + KnightDeltas[i, 1];
                if (nf < 0 || nf > 7 || nr < 0 || nr > 7) continue;
                if (Squares[nr * 8 + nf] == knight) return true;
            }

            // 王
            int king = byColor == Pc.White ? Pc.King : -Pc.King;
            for (int i = 0; i < 8; i++)
            {
                int nf = f + KingDeltas[i, 0];
                int nr = r + KingDeltas[i, 1];
                if (nf < 0 || nf > 7 || nr < 0 || nr > 7) continue;
                if (Squares[nr * 8 + nf] == king) return true;
            }

            // 车 / 后（直线）
            int rook = byColor == Pc.White ? Pc.Rook : -Pc.Rook;
            int queen = byColor == Pc.White ? Pc.Queen : -Pc.Queen;
            for (int i = 0; i < 4; i++)
            {
                int nf = f + RookDirs[i, 0];
                int nr = r + RookDirs[i, 1];
                while (nf >= 0 && nf <= 7 && nr >= 0 && nr <= 7)
                {
                    int p = Squares[nr * 8 + nf];
                    if (p != 0)
                    {
                        if (p == rook || p == queen) return true;
                        break;
                    }
                    nf += RookDirs[i, 0];
                    nr += RookDirs[i, 1];
                }
            }

            // 象 / 后（斜线）
            int bishop = byColor == Pc.White ? Pc.Bishop : -Pc.Bishop;
            for (int i = 0; i < 4; i++)
            {
                int nf = f + BishopDirs[i, 0];
                int nr = r + BishopDirs[i, 1];
                while (nf >= 0 && nf <= 7 && nr >= 0 && nr <= 7)
                {
                    int p = Squares[nr * 8 + nf];
                    if (p != 0)
                    {
                        if (p == bishop || p == queen) return true;
                        break;
                    }
                    nf += BishopDirs[i, 0];
                    nr += BishopDirs[i, 1];
                }
            }

            return false;
        }

        public int KingSquare(int color)
        {
            int king = color == Pc.White ? Pc.King : -Pc.King;
            for (int sq = 0; sq < 64; sq++)
                if (Squares[sq] == king) return sq;
            return -1;
        }

        public bool IsInCheck(int color)
        {
            int king = KingSquare(color);
            if (king < 0) return false;
            return IsSquareAttacked(king, Pc.Other(color));
        }

        #endregion

        #region 执行 / 撤销走子

        private struct UndoState
        {
            public int From, To;
            public int MovedPiece;
            public int CapturedPiece;
            public int CapturedSquare;
            public int CastlingRights;
            public int EnPassantSquare;
            public int HalfmoveClock;
            public int FullmoveNumber;
            public bool HasRookMove;
            public int RookFrom, RookTo, RookPiece;
        }

        public void MakeMove(Move move)
        {
            int piece = Squares[move.From];
            int color = Pc.Color(piece);
            int type = Pc.Type(piece);

            var undo = new UndoState
            {
                From = move.From,
                To = move.To,
                MovedPiece = piece,
                CapturedPiece = 0,
                CapturedSquare = -1,
                CastlingRights = CastlingRights,
                EnPassantSquare = EnPassantSquare,
                HalfmoveClock = HalfmoveClock,
                FullmoveNumber = FullmoveNumber,
                HasRookMove = false
            };

            // 1) 处理被吃掉的子
            if (move.Kind == MoveKind.EnPassant)
            {
                int capSq = move.To + (color == Pc.White ? -8 : 8);
                undo.CapturedSquare = capSq;
                undo.CapturedPiece = Squares[capSq];
                Squares[capSq] = 0;
            }
            else if (Squares[move.To] != 0)
            {
                undo.CapturedSquare = move.To;
                undo.CapturedPiece = Squares[move.To];
            }

            // 2) 移动棋子（含升变）
            Squares[move.From] = 0;
            Squares[move.To] = move.Promotion != 0 ? Pc.Make(move.Promotion, color) : piece;

            // 3) 易位时同时移动车
            if (move.IsCastle)
            {
                int rookFrom = move.Kind == MoveKind.CastleKingSide ? move.To + 1 : move.To - 2;
                int rookTo = move.Kind == MoveKind.CastleKingSide ? move.To - 1 : move.To + 1;
                undo.HasRookMove = true;
                undo.RookFrom = rookFrom;
                undo.RookTo = rookTo;
                undo.RookPiece = Squares[rookFrom];
                Squares[rookFrom] = 0;
                Squares[rookTo] = undo.RookPiece;
            }

            // 4) 过路兵目标格
            EnPassantSquare = move.Kind == MoveKind.DoublePush ? (move.From + move.To) / 2 : -1;

            // 5) 易位权
            CastlingRights &= CastleMask[move.From];
            CastlingRights &= CastleMask[move.To];

            // 6) 回合计数
            if (type == Pc.Pawn || undo.CapturedPiece != 0) HalfmoveClock = 0;
            else HalfmoveClock++;

            if (SideToMove == Pc.Black) FullmoveNumber++;
            SideToMove = Pc.Other(SideToMove);

            if (_undoCount == _undo.Length) Array.Resize(ref _undo, _undo.Length * 2);
            _undo[_undoCount++] = undo;
        }

        public void UnmakeMove()
        {
            if (_undoCount == 0) throw new InvalidOperationException("没有可撤销的走子。");
            var undo = _undo[--_undoCount];

            SideToMove = Pc.Other(SideToMove);
            CastlingRights = undo.CastlingRights;
            EnPassantSquare = undo.EnPassantSquare;
            HalfmoveClock = undo.HalfmoveClock;
            FullmoveNumber = undo.FullmoveNumber;

            if (undo.HasRookMove)
            {
                Squares[undo.RookTo] = 0;
                Squares[undo.RookFrom] = undo.RookPiece;
            }

            Squares[undo.To] = 0;
            Squares[undo.From] = undo.MovedPiece;

            if (undo.CapturedPiece != 0 && undo.CapturedSquare >= 0)
                Squares[undo.CapturedSquare] = undo.CapturedPiece;
        }

        /// <summary>当前棋盘上已经走过的步数（用于撤销）。</summary>
        public int HistoryCount => _undoCount;

        #endregion

        #region 判定辅助

        /// <summary>
        /// 判定“死局”（任何合法走子序列都无法形成将杀）：
        /// 王对王、王加单象对王、王加单马对王、以及双方都只剩同色格的象。
        /// </summary>
        public bool IsInsufficientMaterial()
        {
            int whiteBishops = 0, blackBishops = 0;
            int whiteKnights = 0, blackKnights = 0;
            int bishopColorMask = 0;   // bit0 = 有深色格象，bit1 = 有浅色格象

            for (int sq = 0; sq < 64; sq++)
            {
                int p = Squares[sq];
                if (p == 0) continue;
                bool isWhite = p > 0;
                switch (Pc.Type(p))
                {
                    case Pc.Pawn:
                    case Pc.Rook:
                    case Pc.Queen:
                        return false;      // 有兵/车/后就不可能自动和棋
                    case Pc.Bishop:
                        if (isWhite) whiteBishops++; else blackBishops++;
                        bishopColorMask |= (((sq >> 3) + (sq & 7)) & 1) == 0 ? 1 : 2;
                        break;
                    case Pc.Knight:
                        if (isWhite) whiteKnights++; else blackKnights++;
                        break;
                }
            }

            int whiteMinor = whiteBishops + whiteKnights;
            int blackMinor = blackBishops + blackKnights;

            // 王对王
            if (whiteMinor == 0 && blackMinor == 0) return true;

            // 王加单马 / 王加单象 对 孤王
            if (whiteMinor == 1 && blackMinor == 0 && whiteBishops + whiteKnights == 1) return true;
            if (blackMinor == 1 && whiteMinor == 0 && blackBishops + blackKnights == 1) return true;

            // 双方都只有象，且所有象都在同一种颜色的格子上
            if (whiteKnights == 0 && blackKnights == 0
                && whiteBishops > 0 && blackBishops > 0
                && (bishopColorMask == 1 || bishopColorMask == 2))
            {
                return true;
            }

            return false;
        }

        #endregion
    }
}
