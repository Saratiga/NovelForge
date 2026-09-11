using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NovelForge.Editor
{
    public readonly struct ChoiceOption
    {
        public string Text { get; }
        public string ExplicitId { get; }
        public string TargetLabel { get; }

        public ChoiceOption(string text, string explicitId, string targetLabel)
        {
            Text = text;
            ExplicitId = explicitId;
            TargetLabel = targetLabel;
        }
    }

    public class GraphNode
    {
        public string LabelName { get; }
        public string Body { get; }
        public IReadOnlyList<ChoiceOption> Choices { get; }
        public string TrailingJumpTarget { get; }
        public bool TrailingIsGosub { get; }
        public bool EndsInReturn { get; }
        public string LeadingComment { get; }

        public GraphNode(string labelName, string body, IReadOnlyList<ChoiceOption> choices,
            string trailingJumpTarget, bool trailingIsGosub, bool endsInReturn, string leadingComment)
        {
            LabelName = labelName;
            Body = body;
            Choices = choices;
            TrailingJumpTarget = trailingJumpTarget;
            TrailingIsGosub = trailingIsGosub;
            EndsInReturn = endsInReturn;
            LeadingComment = leadingComment;
        }
    }

    public class GraphDocument
    {
        public IReadOnlyList<GraphNode> Nodes { get; }

        public GraphDocument(IReadOnlyList<GraphNode> nodes)
        {
            Nodes = nodes;
        }

        // The DSL has no explicit "end of block" marker — a block that ends without
        // jump/gosub/choice/return simply falls through into whatever is physically next,
        // exactly like the compiled runtime does (ScriptCompiler never stops appending
        // commands at a label boundary). Surfaced separately from the other trailing fields
        // so the window can render it as a distinct (dashed) edge.
        public string GetFallThroughTarget(int nodeIndex)
        {
            if (nodeIndex < 0 || nodeIndex >= Nodes.Count - 1)
                return null;
            GraphNode node = Nodes[nodeIndex];
            if (node.EndsInReturn || node.TrailingJumpTarget != null || node.Choices.Count > 0)
                return null;
            return Nodes[nodeIndex + 1].LabelName;
        }

        public IReadOnlyList<(string SourceLabel, string TargetLabel)> GetBrokenReferences()
        {
            var defined = new HashSet<string>();
            foreach (GraphNode n in Nodes)
                if (n.LabelName != null)
                    defined.Add(n.LabelName);

            var broken = new List<(string, string)>();
            foreach (GraphNode node in Nodes)
            {
                if (node.TrailingJumpTarget != null && !defined.Contains(node.TrailingJumpTarget))
                    broken.Add((node.LabelName, node.TrailingJumpTarget));
                foreach (ChoiceOption choice in node.Choices)
                    if (!defined.Contains(choice.TargetLabel))
                        broken.Add((node.LabelName, choice.TargetLabel));
            }
            return broken;
        }

        public IReadOnlyList<string> GetUnreachableLabels()
        {
            var referenced = new HashSet<string>();
            for (int i = 0; i < Nodes.Count; i++)
            {
                GraphNode node = Nodes[i];
                if (node.TrailingJumpTarget != null)
                    referenced.Add(node.TrailingJumpTarget);
                foreach (ChoiceOption choice in node.Choices)
                    referenced.Add(choice.TargetLabel);

                string fallThrough = GetFallThroughTarget(i);
                if (fallThrough != null)
                    referenced.Add(fallThrough);
            }

            var unreachable = new List<string>();
            for (int i = 1; i < Nodes.Count; i++) // node 0 is the entry point, always reachable
            {
                if (Nodes[i].LabelName != null && !referenced.Contains(Nodes[i].LabelName))
                    unreachable.Add(Nodes[i].LabelName);
            }
            return unreachable;
        }
    }

    public static class GraphDocumentParser
    {
        private static readonly Regex ChoiceOptionLine = new(@"^""([^""]*)""(?:\s*@([A-Za-z_][A-Za-z0-9_]*))?\s*->\s*([A-Za-z_][A-Za-z0-9_]*)$", RegexOptions.Compiled);

        public static GraphDocument Parse(string source)
        {
            var nodes = new List<GraphNode>();
            if (string.IsNullOrEmpty(source))
                return new GraphDocument(nodes);

            string[] lines = source.Replace("\r\n", "\n").Split('\n');

            var labelStarts = new List<(int LineIndex, string Name, int CommentStart)>();
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();
                if (!trimmed.StartsWith("label ", StringComparison.Ordinal))
                    continue;

                string name = trimmed.Substring("label ".Length).Trim();
                int commentStart = i;
                int j = i - 1;
                while (j >= 0 && lines[j].Trim().StartsWith("//", StringComparison.Ordinal))
                {
                    commentStart = j;
                    j--;
                }
                labelStarts.Add((i, name, commentStart));
            }

            // Content before the first label (rare in practice — every script in this project
            // starts with a label) becomes an unlabeled leading node rather than being dropped
            // or throwing.
            int firstBoundary = labelStarts.Count > 0 ? labelStarts[0].CommentStart : lines.Length;
            if (HasContent(lines, 0, firstBoundary))
                nodes.Add(BuildNode(lines, 0, firstBoundary, null, null));

            for (int k = 0; k < labelStarts.Count; k++)
            {
                (int lineIndex, string name, int commentStart) = labelStarts[k];
                string leadingComment = ExtractComment(lines, commentStart, lineIndex);
                int blockStart = lineIndex + 1;
                int blockEnd = k + 1 < labelStarts.Count ? labelStarts[k + 1].CommentStart : lines.Length;
                nodes.Add(BuildNode(lines, blockStart, blockEnd, name, leadingComment));
            }

            return new GraphDocument(nodes);
        }

        private static bool HasContent(string[] lines, int start, int end)
        {
            for (int i = start; i < end; i++)
            {
                string trimmed = lines[i].Trim();
                if (trimmed.Length > 0 && !trimmed.StartsWith("//", StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static string ExtractComment(string[] lines, int start, int end)
        {
            if (start >= end)
                return null;
            var commentLines = new List<string>();
            for (int i = start; i < end; i++)
                commentLines.Add(lines[i].Trim().Substring(2).Trim());
            return string.Join("\n", commentLines);
        }

        // Finds the block's trailing control-flow element (return / jump / gosub / choice)
        // by scanning backward from the last non-blank, non-comment line — everything before
        // it is Body, preserved verbatim. A block with none of these (or where a choice-looking
        // line has no matching "choice" keyword above it) has no trailing element at all: it's
        // either a fall-through (GraphDocument.GetFallThroughTarget handles that) or malformed
        // input that's simply kept as opaque Body text rather than rejected.
        private static GraphNode BuildNode(string[] lines, int start, int end, string labelName, string leadingComment)
        {
            int lastContentIndex = -1;
            for (int i = end - 1; i >= start; i--)
            {
                string trimmed = lines[i].Trim();
                if (trimmed.Length > 0 && !trimmed.StartsWith("//", StringComparison.Ordinal))
                {
                    lastContentIndex = i;
                    break;
                }
            }

            if (lastContentIndex < 0)
                return new GraphNode(labelName, TrimTrailingBlankLines(lines, start, end), Array.Empty<ChoiceOption>(), null, false, false, leadingComment);

            string lastTrimmed = lines[lastContentIndex].Trim();

            if (lastTrimmed == "return")
                return new GraphNode(labelName, JoinLines(lines, start, lastContentIndex), Array.Empty<ChoiceOption>(), null, false, true, leadingComment);

            if (lastTrimmed.StartsWith("jump ", StringComparison.Ordinal))
                return new GraphNode(labelName, JoinLines(lines, start, lastContentIndex), Array.Empty<ChoiceOption>(), lastTrimmed.Substring("jump ".Length).Trim(), false, false, leadingComment);

            if (lastTrimmed.StartsWith("gosub ", StringComparison.Ordinal))
                return new GraphNode(labelName, JoinLines(lines, start, lastContentIndex), Array.Empty<ChoiceOption>(), lastTrimmed.Substring("gosub ".Length).Trim(), true, false, leadingComment);

            if (ChoiceOptionLine.IsMatch(lastTrimmed))
            {
                var options = new List<ChoiceOption>();
                int i = lastContentIndex;
                int choiceKeywordIndex = -1;
                while (i >= start)
                {
                    string trimmed = lines[i].Trim();
                    if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal))
                    {
                        i--;
                        continue;
                    }
                    if (trimmed == "choice")
                    {
                        choiceKeywordIndex = i;
                        break;
                    }
                    Match match = ChoiceOptionLine.Match(trimmed);
                    if (!match.Success)
                        break;
                    string explicitId = match.Groups[2].Success ? match.Groups[2].Value : null;
                    options.Insert(0, new ChoiceOption(match.Groups[1].Value, explicitId, match.Groups[3].Value));
                    i--;
                }

                if (choiceKeywordIndex >= 0)
                    return new GraphNode(labelName, JoinLines(lines, start, choiceKeywordIndex), options, null, false, false, leadingComment);
            }

            // No trailing control-flow element: a fall-through block (or malformed input we
            // don't try to interpret further). Preserve everything as Body, trimming only
            // fully-blank trailing lines (the canonical blank-line separator before the next
            // label) — a trailing comment that isn't immediately attached to the *next* label
            // (see ExtractComment) still belongs to *this* block's Body, verbatim, so it must
            // NOT be trimmed the way a blank line is.
            return new GraphNode(labelName, TrimTrailingBlankLines(lines, start, end), Array.Empty<ChoiceOption>(), null, false, false, leadingComment);
        }

        private static string TrimTrailingBlankLines(string[] lines, int start, int end)
        {
            int trimmedEnd = end;
            while (trimmedEnd > start && lines[trimmedEnd - 1].Trim().Length == 0)
                trimmedEnd--;
            return JoinLines(lines, start, trimmedEnd);
        }

        private static string JoinLines(string[] lines, int start, int end)
        {
            if (end <= start)
                return string.Empty;
            return string.Join("\n", lines, start, end - start);
        }
    }
}
