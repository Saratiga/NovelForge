using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NovelForge.Runtime
{
    // Single source of truth for the DSL's lexical rules, shared by the runtime compiler
    // and every Editor tool that reads scripts (graph parser, highlighter, autocomplete).
    public static class DslGrammar
    {
        public const string Identifier = "[A-Za-z_][A-Za-z0-9_]*";

        public static readonly Regex DialogueLine = new($@"^({Identifier}):\s+(.+)$", RegexOptions.Compiled);
        public static readonly Regex SetLine = new($@"^set\s+({Identifier})\s*(=|\+=|-=)\s*(.+)$", RegexOptions.Compiled);
        public static readonly Regex IfLine = new($@"^if\s+({Identifier})\s*(==|!=|>=|<=|>|<)\s*(.+)$", RegexOptions.Compiled);

        // Groups: 1 option text (without quotes), 2 explicit id (without '@'), 3 target label.
        public static readonly Regex ChoiceOptionLine = new($@"^""([^""]*)""(?:\s*@({Identifier}))?\s*->\s*({Identifier})$", RegexOptions.Compiled);

        public static readonly Regex EmotionTag = new(@"#(\w+)", RegexOptions.Compiled);

        // Anchored to a whitespace-delimited token so an "@" inside a word (an email
        // address like "bob@example.com") is not read as a localization id.
        public static readonly Regex IdTag = new($@"(?<=^|\s)@({Identifier})\b", RegexOptions.Compiled);

        public static readonly Regex TrailingPosition = new(@"\b(left|right|center)\b$", RegexOptions.Compiled);

        private static readonly Regex IdentifierOnly = new($"^{Identifier}$", RegexOptions.Compiled);

        public static readonly IReadOnlyList<string> Keywords = new[]
        {
            "label", "jump", "gosub", "set", "if", "return", "else", "endif", "choice",
        };

        public static bool IsIdentifier(string text) => text != null && IdentifierOnly.IsMatch(text);

        public static bool TryParseLabel(string trimmedLine, out string name)
        {
            name = null;
            if (!trimmedLine.StartsWith("label ", StringComparison.Ordinal))
                return false;
            name = trimmedLine.Substring("label ".Length).Trim();
            return name.Length > 0;
        }

        public static bool TryParseJump(string trimmedLine, out string target, out bool isGosub)
        {
            isGosub = trimmedLine.StartsWith("gosub ", StringComparison.Ordinal);
            if (!isGosub && !trimmedLine.StartsWith("jump ", StringComparison.Ordinal))
            {
                target = null;
                return false;
            }
            target = trimmedLine.Substring(isGosub ? "gosub ".Length : "jump ".Length).Trim();
            return true;
        }
    }
}
