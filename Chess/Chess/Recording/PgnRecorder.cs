using System.Text;
using System.Text.RegularExpressions;
using Chess.Core;

namespace Chess.Recording
{
    /// <summary>
    /// 把对局保存为标准 PGN 格式棋谱。
    /// 文件位于应用程序所在目录下的 Records\ 子目录，
    /// 文件名为“对局开始时的时间戳”，形如 2026_05_20_1.rcd（日期 + 当日流水号）。
    /// </summary>
    public sealed class PgnRecorder
    {
        public const string FolderName = "Records";
        public const string Extension = ".rcd";

        private static readonly Encoding FileEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

        public string FilePath { get; }
        public string FileName => Path.GetFileName(FilePath);

        private readonly string _event;
        private readonly string _white;
        private readonly string _black;
        private readonly string _date;
        private readonly string _round;
        private readonly string _termination;
        private readonly int _mode;

        public PgnRecorder(string eventName, string white, string black, DateTime startTime, string termination,
            int mode = GameModes.TwoPlayer)
        {
            _event = eventName;
            _white = white;
            _black = black;
            _date = startTime.ToString("yyyy.MM.dd");
            _termination = termination;
            _mode = GameModes.IsValid(mode) ? mode : GameModes.TwoPlayer;

            string directory = Path.Combine(AppContext.BaseDirectory, FolderName);
            FilePath = CreateRecordPath(directory, startTime);
            _round = ExtractSequence(FilePath).ToString();
        }

        /// <summary>从文件名末尾取出当天的流水号，作为 PGN 的 Round 标签。</summary>
        private static int ExtractSequence(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            int underscore = name.LastIndexOf('_');
            if (underscore >= 0 && int.TryParse(name[(underscore + 1)..], out int number)) return number;
            return 1;
        }

        /// <summary>应用程序目录下的 Records 目录。</summary>
        public static string RecordsDirectory => Path.Combine(AppContext.BaseDirectory, FolderName);

        /// <summary>
        /// 按 “yyyy_MM_dd_流水号.rcd” 生成一个当天尚未使用的文件名。
        /// </summary>
        public static string CreateRecordPath(string directory, DateTime when)
        {
            Directory.CreateDirectory(directory);

            string prefix = when.ToString("yyyy_MM_dd");
            var pattern = new Regex("^" + Regex.Escape(prefix) + @"_(\d+)" + Regex.Escape(Extension) + "$",
                RegexOptions.IgnoreCase);

            int next = 1;
            foreach (string existing in Directory.EnumerateFiles(directory, "*" + Extension))
            {
                var match = pattern.Match(Path.GetFileName(existing));
                if (match.Success && int.TryParse(match.Groups[1].Value, out int number) && number >= next)
                    next = number + 1;
            }

            return Path.Combine(directory, $"{prefix}_{next}{Extension}");
        }

        /// <summary>生成完整的 PGN 文本并写入文件（每走一步都会重写，保证随时可读）。</summary>
        public void Write(IReadOnlyList<string> sanMoves, string resultTag, string? termination = null)
        {
            string text = BuildPgn(sanMoves, resultTag, termination ?? _termination);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath, text, FileEncoding);
            }
            catch (IOException)
            {
                // 磁盘忙或被占用时忽略：下一次走子会再次尝试写入。
            }
            catch (UnauthorizedAccessException)
            {
                // 同上，不因为写记录失败而影响对局。
            }
        }

        public string BuildPgn(IReadOnlyList<string> sanMoves, string resultTag, string? termination = null)
        {
            var sb = new StringBuilder(512);
            AppendTag(sb, "Event", _event);
            AppendTag(sb, "Site", "Chess · 本地对局");
            AppendTag(sb, "Date", _date);
            AppendTag(sb, "Round", _round);
            AppendTag(sb, "White", _white);
            AppendTag(sb, "Black", _black);
            AppendTag(sb, "Result", resultTag);
            AppendTag(sb, "Mode", _mode.ToString());          // 0 双人 / 1 简单 / 2 普通 / 3 困难
            AppendTag(sb, "TimeControl", "-");
            AppendTag(sb, "Termination", termination ?? _termination);
            sb.Append('\n');

            sb.Append(GameSession.BuildMoveText(sanMoves, resultTag));
            sb.Append('\n');
            return sb.ToString();
        }

        private static void AppendTag(StringBuilder sb, string name, string value)
        {
            sb.Append('[').Append(name).Append(" \"").Append(EscapeTagValue(value)).Append("\"]\n");
        }

        private static string EscapeTagValue(string value)
            => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
