using System;
using System.Collections.Generic;
using System.Linq;
using NovelForge.Runtime;
using UnityEngine;

namespace NovelForge.Editor
{
    public static class DslAutocompleteProvider
    {
        public static IReadOnlyList<string> GetCandidates(string source, int cursorPosition, IEnumerable<string> commands, IEnumerable<string> characters)
        {
            string partial = ExtractPartialWord(source, cursorPosition);

            var allCandidates = new List<string>();
            allCandidates.AddRange(commands ?? Enumerable.Empty<string>());
            allCandidates.AddRange(characters ?? Enumerable.Empty<string>());
            allCandidates.AddRange(ExtractLabels(source));

            var startsWith = new List<string>();
            var contains = new List<string>();
            var seen = new HashSet<string>();

            foreach (string candidate in allCandidates)
            {
                if (string.IsNullOrEmpty(candidate) || !seen.Add(candidate))
                    continue;

                if (candidate.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
                    startsWith.Add(candidate);
                else if (candidate.IndexOf(partial, StringComparison.OrdinalIgnoreCase) >= 0)
                    contains.Add(candidate);
            }

            startsWith.Sort(StringComparer.OrdinalIgnoreCase);
            contains.Sort(StringComparer.OrdinalIgnoreCase);

            startsWith.AddRange(contains);
            return startsWith;
        }

        private static string ExtractPartialWord(string source, int cursorPosition)
        {
            if (string.IsNullOrEmpty(source) || cursorPosition <= 0)
                return string.Empty;

            int clampedCursor = Mathf.Clamp(cursorPosition, 0, source.Length);
            int start = clampedCursor;
            while (start > 0 && IsWordChar(source[start - 1]))
                start--;

            return source.Substring(start, clampedCursor - start);
        }

        private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

        private static IEnumerable<string> ExtractLabels(string source)
        {
            if (string.IsNullOrEmpty(source))
                yield break;

            foreach (string line in source.Replace("\r\n", "\n").Split('\n'))
            {
                if (DslGrammar.TryParseLabel(line.Trim(), out string name))
                    yield return name;
            }
        }
    }
}
