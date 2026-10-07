using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using NovelForge.Runtime;
using UnityEditor;

namespace NovelForge.Editor
{
    public static class DslSyntaxHighlighter
    {
        private static readonly HashSet<string> Keywords = new(DslGrammar.Keywords);

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

            var choiceMatch = DslGrammar.ChoiceOptionLine.Match(trimmed);
            if (choiceMatch.Success)
            {
                // Shared groups exclude the quotes and the '@'; widen the spans to color them too.
                var textGroup = choiceMatch.Groups[1];
                spans.Add((indent + textGroup.Index - 1, textGroup.Length + 2, StringLiteralColor));

                var idGroup = choiceMatch.Groups[2];
                if (idGroup.Success)
                    spans.Add((indent + idGroup.Index - 1, idGroup.Length + 1, TagColor));

                var labelGroup = choiceMatch.Groups[3];
                spans.Add((indent + labelGroup.Index, labelGroup.Length, LabelColor));

                return Render(line, spans);
            }

            var dialogueMatch = DslGrammar.DialogueLine.Match(trimmed);
            if (dialogueMatch.Success)
            {
                var idGroup = dialogueMatch.Groups[1];
                spans.Add((indent + idGroup.Index, idGroup.Length, CharacterIdColor));

                Group restGroup = dialogueMatch.Groups[2];
                string rest = restGroup.Value;
                int restOffset = indent + restGroup.Index;

                var emotionMatch = DslGrammar.EmotionTag.Match(rest);
                if (emotionMatch.Success)
                    spans.Add((restOffset + emotionMatch.Index, emotionMatch.Length, TagColor));

                var idTagMatch = DslGrammar.IdTag.Match(rest);
                if (idTagMatch.Success)
                    spans.Add((restOffset + idTagMatch.Index, idTagMatch.Length, TagColor));

                var posMatch = DslGrammar.TrailingPosition.Match(rest);
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

        // No escaping: Unity's rich text tag parser does not decode HTML entities
        // (&lt;/&gt;/&amp; display as those literal characters, not </>/&), so escaping
        // this way would corrupt ordinary DSL syntax on screen — "->" (used on every
        // choice-option line) would show as "-&gt;" instead of an arrow. '<'/'>'/'&' are
        // otherwise inert to Unity's tag parser unless they form a complete recognized tag
        // (<color=...>, <b>, etc.), which normal dialogue text is not expected to contain.
        private static string Render(string line, List<(int start, int length, string color)> spans)
        {
            if (spans.Count == 0)
                return line;

            spans.Sort((a, b) => a.start.CompareTo(b.start));

            var sb = new StringBuilder();
            int cursor = 0;
            foreach (var span in spans)
            {
                if (span.start < cursor)
                    continue;
                sb.Append(line, cursor, span.start - cursor);
                sb.Append("<color=").Append(span.color).Append('>');
                sb.Append(line, span.start, span.length);
                sb.Append("</color>");
                cursor = span.start + span.length;
            }
            sb.Append(line, cursor, line.Length - cursor);
            return sb.ToString();
        }

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
