using System.Text;
using Chess.Ai;
using Chess.Core;

namespace Chess.Recording
{
    /// <summary>
    /// 对局模式编号：0 = 双人对战，1/2/3 = 人机对战的简单 / 普通 / 困难。
    /// 写在 PGN 的 [Mode "n"] 标签里；没有该标签时按双人对战处理。
    /// </summary>
    public static class GameModes
    {
        public const int TwoPlayer = 0;

        public static int From(bool vsAi, Difficulty difficulty)
            => vsAi ? 1 + (int)difficulty : TwoPlayer;

        public static string Name(int mode) => mode switch
        {
            1 => "人机对战（简单）",
            2 => "人机对战（普通）",
            3 => "人机对战（困难）",
            _ => "双人对战"
        };

        public static bool IsValid(int mode) => mode >= 0 && mode <= 3;
    }

    /// <summary>从 .rcd 文件读入的一局棋，用于回放。</summary>
    public sealed class ImportedGame
    {
        public string FilePath { get; set; } = string.Empty;
        public string Event { get; set; } = string.Empty;
        public string White { get; set; } = string.Empty;
        public string Black { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string ResultTag { get; set; } = "*";

        /// <summary>0 双人 / 1 简单 / 2 普通 / 3 困难。</summary>
        public int Mode { get; set; } = GameModes.TwoPlayer;

        public string ModeName => GameModes.Name(Mode);

        public List<Move> Moves { get; } = new();

        /// <summary>规范化后的 SAN 记谱，与 Moves 一一对应。</summary>
        public List<string> SanMoves { get; } = new();

        public string ResultText => ResultTag switch
        {
            "1-0" => "白方获胜",
            "0-1" => "黑方获胜",
            "1/2-1/2" => "和棋",
            _ => "未结束"
        };
    }

    /// <summary>PGN（.rcd）解析与 SAN 走法匹配。</summary>
    public static class PgnImporter
    {
        public static ImportedGame Load(string path)
        {
            string text;
            try
            {
                text = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (DecoderFallbackException)
            {
                text = File.ReadAllText(path, Encoding.Default);
            }

            var game = Parse(text);
            game.FilePath = path;
            return game;
        }

        public static ImportedGame Parse(string text)
        {
            var game = new ImportedGame();
            var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var moveText = new StringBuilder();

            bool inMovetext = false;
            foreach (string rawLine in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0) continue;
                if (line[0] == '%') continue;   // PGN 转义行

                if (!inMovetext && line[0] == '[')
                {
                    var tag = ParseTag(line);
                    if (tag is { } pair) tags[pair.Name] = pair.Value;
                    continue;
                }

                inMovetext = true;
                moveText.Append(stripTrailingComment(line)).Append('\n');
            }

            game.Event = GetTag(tags, "Event");
            game.White = GetTag(tags, "White");
            game.Black = GetTag(tags, "Black");
            game.Date = GetTag(tags, "Date");
            game.ResultTag = NormalizeResult(GetTag(tags, "Result"));

            string modeText = GetTag(tags, "Mode");
            if (int.TryParse(modeText, out int mode) && GameModes.IsValid(mode))
                game.Mode = mode;
            else
                game.Mode = GameModes.TwoPlayer;   // 没有标记时按双人对战处理

            Replay(game, moveText.ToString());
            return game;
        }

        private static string stripTrailingComment(string line)
        {
            int semicolon = line.IndexOf(';');
            return semicolon >= 0 ? line[..semicolon] : line;
        }

        private static string GetTag(Dictionary<string, string> tags, string name)
            => tags.TryGetValue(name, out string? value) ? value : string.Empty;

        private static string NormalizeResult(string value) => value switch
        {
            "1-0" => "1-0",
            "0-1" => "0-1",
            "1/2-1/2" => "1/2-1/2",
            "½-½" => "1/2-1/2",
            "*" => "*",
            _ => "*"
        };

        private static (string Name, string Value)? ParseTag(string line)
        {
            int open = line.IndexOf('[');
            if (open < 0) return null;
            int firstQuote = line.IndexOf('"', open + 1);
            if (firstQuote < 0) return null;
            int lastQuote = line.LastIndexOf('"');
            if (lastQuote <= firstQuote) return null;

            string name = line[(open + 1)..firstQuote].Trim();
            if (name.Length == 0) return null;

            string value = line[(firstQuote + 1)..lastQuote]
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
            return (name, value);
        }

        private static void Replay(ImportedGame game, string moveText)
        {
            var board = new Board();
            board.LoadStartPosition();

            foreach (string token in Tokenize(moveText))
            {
                var move = SanMatcher.FindMove(board, token);
                if (move is not { } found) break;   // 遇到无法识别的走法就停在这里

                string san = San.ToSan(board, found);
                board.MakeMove(found);
                game.Moves.Add(found);
                game.SanMoves.Add(san);
            }

            if (game.ResultTag == "*")
                game.ResultTag = DeriveResult(board);
        }

        private static string DeriveResult(Board board)
        {
            if (board.GenerateLegalMoves().Count > 0) return "*";
            if (!board.IsInCheck(board.SideToMove)) return "1/2-1/2";
            return board.SideToMove == Pc.Black ? "1-0" : "0-1";
        }

        /// <summary>把 movetext 拆成一个个 SAN 记号，去掉回合号、结果与注记。</summary>
        private static IEnumerable<string> Tokenize(string moveText)
        {
            string text = RemoveComments(moveText);

            foreach (string raw in text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string token = raw.Trim();
                if (token.Length == 0) continue;
                if (token[0] == '$') continue;            // NAG 注记
                if (IsResult(token)) continue;
                if (IsMoveNumber(token)) continue;

                yield return token;
            }
        }

        private static string RemoveComments(string text)
        {
            var sb = new StringBuilder(text.Length);
            int depth = 0;
            foreach (char c in text)
            {
                if (c == '{') { depth++; continue; }
                if (c == '}')
                {
                    if (depth > 0) depth--;
                    continue;
                }
                if (depth == 0) sb.Append(c);
            }
            return sb.ToString();
        }

        private static bool IsResult(string token)
            => token is "1-0" or "0-1" or "1/2-1/2" or "½-½" or "*" or "1/2";

        private static bool IsMoveNumber(string token)
        {
            int i = 0;
            while (i < token.Length && char.IsDigit(token[i])) i++;
            if (i == 0) return false;
            while (i < token.Length && token[i] == '.') i++;
            return i == token.Length;
        }
    }

    /// <summary>把 SAN 记号匹配成当前局面下的具体走法。</summary>
    public static class SanMatcher
    {
        public static Move? FindMove(Board board, string san)
        {
            var legal = board.GenerateLegalMoves();
            if (legal.Count == 0) return null;

            string token = Normalize(san);
            if (token.Length == 0) return null;

            // 1) 与我们自己生成的 SAN 完全一致
            foreach (var move in legal)
            {
                if (Normalize(San.ToSan(board, move)) == token) return move;
            }

            // 2) 宽松匹配：只看棋子种类 + 目标格 + 升变棋子
            if (token is "O-O" or "OO")
                return FirstOrNull(legal, m => m.Kind == MoveKind.CastleKingSide);

            if (token is "O-O-O" or "OOO")
                return FirstOrNull(legal, m => m.Kind == MoveKind.CastleQueenSide);

            int promotion = 0;
            int equals = token.IndexOf('=');
            if (equals >= 0 && equals + 1 < token.Length)
            {
                promotion = token[equals + 1] switch
                {
                    'Q' => Pc.Queen,
                    'R' => Pc.Rook,
                    'B' => Pc.Bishop,
                    'N' => Pc.Knight,
                    _ => 0
                };
                token = token[..equals];
            }

            char pieceLetter = token[0] is 'K' or 'Q' or 'R' or 'B' or 'N' ? token[0] : '\0';
            int pieceType = pieceLetter switch
            {
                'K' => Pc.King,
                'Q' => Pc.Queen,
                'R' => Pc.Rook,
                'B' => Pc.Bishop,
                'N' => Pc.Knight,
                _ => Pc.Pawn
            };

            int target = -1;
            for (int i = token.Length - 1; i >= 1; i--)
            {
                char file = token[i - 1];
                char rank = token[i];
                if (file >= 'a' && file <= 'h' && rank >= '1' && rank <= '8')
                {
                    target = (rank - '1') * 8 + (file - 'a');
                    break;
                }
            }
            if (target < 0) return null;

            foreach (var move in legal)
            {
                if (move.To != target) continue;
                if (Pc.Type(board.Squares[move.From]) != pieceType) continue;
                if (promotion != 0 && move.Promotion != promotion) continue;
                return move;
            }

            return null;
        }

        private static Move? FirstOrNull(List<Move> moves, Func<Move, bool> predicate)
        {
            foreach (var move in moves)
                if (predicate(move)) return move;
            return null;
        }

        private static string Normalize(string san)
        {
            var sb = new StringBuilder(san.Length);
            foreach (char c in san)
            {
                if (c is '+' or '#' or '!' or '?' or ' ') continue;
                sb.Append(c == '0' ? 'O' : c);
            }

            string token = sb.ToString();
            if (token.EndsWith("e.p.", StringComparison.OrdinalIgnoreCase)) token = token[..^4];
            else if (token.EndsWith("ep", StringComparison.OrdinalIgnoreCase)) token = token[..^2];
            return token;
        }
    }
}
