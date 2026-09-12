using System.Text;

namespace NovelForge.Editor
{
    public static class GraphDocumentFormatter
    {
        public static string Serialize(GraphDocument document)
        {
            var sb = new StringBuilder();
            bool first = true;
            foreach (GraphNode node in document.Nodes)
            {
                if (!first)
                    sb.Append('\n');
                first = false;
                AppendNode(sb, node);
            }
            return sb.ToString();
        }

        private static void AppendNode(StringBuilder sb, GraphNode node)
        {
            if (node.LeadingComment != null)
            {
                foreach (string line in node.LeadingComment.Split('\n'))
                    sb.Append("// ").Append(line).Append('\n');
            }

            if (node.LabelName != null)
                sb.Append("label ").Append(node.LabelName).Append('\n');

            if (!string.IsNullOrEmpty(node.Body))
                sb.Append(node.Body).Append('\n');

            if (node.EndsInReturn)
            {
                sb.Append("return\n");
            }
            else if (node.TrailingJumpTarget != null)
            {
                sb.Append(node.TrailingIsGosub ? "gosub " : "jump ").Append(node.TrailingJumpTarget).Append('\n');
            }
            else if (node.Choices.Count > 0)
            {
                sb.Append("choice\n");
                foreach (ChoiceOption choice in node.Choices)
                {
                    sb.Append("  \"").Append(choice.Text).Append('"');
                    if (choice.ExplicitId != null)
                        sb.Append(" @").Append(choice.ExplicitId);
                    sb.Append(" -> ").Append(choice.TargetLabel).Append('\n');
                }
            }
        }
    }
}
