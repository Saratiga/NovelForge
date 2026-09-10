using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;

namespace NovelForge.Editor
{
    public static class DslSyntaxHighlighter
    {
        private static readonly Regex DialogueLine = new(@"^([A-Za-z_][A-Za-z0-9_]*):\s+(.+)$", RegexOptions.Compiled);
        private static readonly Regex ChoiceOptionLine = new(@"^(""[^""]*"")(?:\s*(@[A-Za-z_][A-Za-z0-9_]*))?\s*->\s*([A-Za-z_][A-Za-z0-9_]*)$", RegexOptions.Compiled);
        private static readonly Regex EmotionTag = new(@"#\w+", RegexOptions.Compiled);
        private static readonly Regex IdTag = new(@"(?<=^|\s)@[A-Za-z_][A-Za-z0-9_]*\b", RegexOptions.Compiled);
        private static readonly Regex PositionKeyword = new(@"\b(left|right|center)\b$", RegexOptions.Compiled);

        private static readonly HashSet<string> Keywords = new()
        {
            "label", "jump", "gosub", "set", "if", "return", "else", "endif", "choice",
        };

        public static string ToRichText(string source)
        {
            if (string.IsNullOrEmpty(source))
                return source ?? string.Empty;

            string[] lines = source.Replace("\r\n", "\n").Split('\n');
            var result = new StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0) result.Append('\n');
                result.Append(HighlightLine(lines[i]));
            }
            return result.ToString();
        }

        private static string HighlightLine(string line)
        {
            string trimmed = line.TrimStart();
            int indent = line.Length - trimmed.Length;
            var spans = new List<(int start, int length, string color)>();

            if (trimmed.StartsWith("//"))
            {
                spans.Add((indent, trimmed.Length, CommentColor));
                return Render(line, spans);
            }

            string firstWord = FirstWord(trimmed);
            if (Keywords.Contains(firstWord))
                spans.Add((indent, firstWord.Length, KeywordColor));

            var choiceMatch = ChoiceOptionLine.Match(trimmed);
            if (choiceMatch.Success)
            {
                var quoteGroup = choiceMatch.Groups[1];
                spans.Add((indent + quoteGroup.Index, quoteGroup.Length, StringLiteralColor));

                var idGroup = choiceMatch.Groups[2];
                if (idGroup.Success)
                    spans.Add((indent + idGroup.Index, idGroup.Length, TagColor));

                var labelGroup = choiceMatch.Groups[3];
                spans.Add((indent + labelGroup.Index, labelGroup.Length, LabelColor));

                return Render(line, spans);
            }

            var dialogueMatch = DialogueLine.Match(trimmed);
            if (dialogueMatch.Success)
            {
                var idGroup = dialogueMatch.Groups[1];
                spans.Add((indent + idGroup.Index, idGroup.Length, CharacterIdColor));

                Group restGroup = dialogueMatch.Groups[2];
                string rest = restGroup.Value;
                int restOffset = indent + restGroup.Index;

                var emotionMatch = EmotionTag.Match(rest);
                if (emotionMatch.Success)
                    spans.Add((restOffset + emotionMatch.Index, emotionMatch.Length, TagColor));

                var idTagMatch = IdTag.Match(rest);
                if (idTagMatch.Success)
                    spans.Add((restOffset + idTagMatch.Index, idTagMatch.Length, TagColor));

                var posMatch = PositionKeyword.Match(rest);
                if (posMatch.Success)
                    spans.Add((restOffset + posMatch.Index, posMatch.Length, KeywordColor));

                return Render(line, spans);
            }

            if (spans.Count == 0 && firstWord.Length > 0)
            {
                // Generic content command (bg/cg/music/sfx/wait/custom, or an unrecognized word) —
                // colored so it reads visually distinct even before the DSL is fully valid.
                spans.Add((indent, firstWord.Length, CommandColor));
            }

            return Render(line, spans);
        }

        private static string Render(string line, List<(int start, int length, string color)> spans)
        {
            if (spans.Count == 0)
                return Escape(line);

            spans.Sort((a, b) => a.start.CompareTo(b.start));

            var sb = new StringBuilder();
            int cursor = 0;
            foreach (var span in spans)
            {
                if (span.start < cursor)
                    continue;
                sb.Append(Escape(line.Substring(cursor, span.start - cursor)));
                sb.Append("<color=").Append(span.color).Append('>');
                sb.Append(Escape(line.Substring(span.start, span.length)));
                sb.Append("</color>");
                cursor = span.start + span.length;
            }
            sb.Append(Escape(line.Substring(cursor)));
            return sb.ToString();
        }

        private static string Escape(string text) =>
            text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        private static string FirstWord(string trimmed)
        {
            int spaceIndex = trimmed.IndexOf(' ');
            return spaceIndex < 0 ? trimmed : trimmed.Substring(0, spaceIndex);
        }

        private static string KeywordColor => EditorGUIUtility.isProSkin ? "#569CD6" : "#0000FF";
        private static string CommentColor => EditorGUIUtility.isProSkin ? "#6A9955" : "#008000";
        private static string CharacterIdColor => EditorGUIUtility.isProSkin ? "#4EC9B0" : "#267F99";
        private static string CommandColor => EditorGUIUtility.isProSkin ? "#DCDCAA" : "#795E26";
        private static string TagColor => EditorGUIUtility.isProSkin ? "#C586C0" : "#AF00DB";
        private static string StringLiteralColor => EditorGUIUtility.isProSkin ? "#CE9178" : "#A31515";
        private static string LabelColor => EditorGUIUtility.isProSkin ? "#D7BA7D" : "#800000";
    }
}
