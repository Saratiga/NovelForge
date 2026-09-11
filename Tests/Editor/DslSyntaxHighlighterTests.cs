using System.Text.RegularExpressions;
using NUnit.Framework;

namespace NovelForge.Editor.Tests
{
    public class DslSyntaxHighlighterTests
    {
        private static void AssertColored(string richText, string expectedToken)
        {
            var pattern = $@"<color=#[0-9A-Fa-f]{{6}}>{Regex.Escape(expectedToken)}</color>";
            StringAssert.IsMatch(pattern, richText);
        }

        [Test]
        public void CommentLine_IsColoredAsWhole()
        {
            string result = DslSyntaxHighlighter.ToRichText("// a note");

            AssertColored(result, "// a note");
        }

        [Test]
        public void LabelLine_ColorsOnlyTheKeyword()
        {
            string result = DslSyntaxHighlighter.ToRichText("label start");

            AssertColored(result, "label");
            StringAssert.Contains(" start", result);
        }

        [Test]
        public void DialogueLine_ColorsCharacterIdEmotionIdTagAndPosition()
        {
            string result = DslSyntaxHighlighter.ToRichText("Alice: Привет! #happy @greet left");

            AssertColored(result, "Alice");
            AssertColored(result, "#happy");
            AssertColored(result, "@greet");
            AssertColored(result, "left");
        }

        [Test]
        public void ChoiceOptionLine_ColorsQuotedTextExplicitIdAndTargetLabel()
        {
            string result = DslSyntaxHighlighter.ToRichText("\"Yes\" @yes_opt -> yes_label");

            AssertColored(result, "\"Yes\"");
            AssertColored(result, "@yes_opt");
            AssertColored(result, "yes_label");
        }

        [Test]
        public void GenericCommandLine_ColorsFirstWord()
        {
            string result = DslSyntaxHighlighter.ToRichText("bg park_day");

            AssertColored(result, "bg");
            StringAssert.Contains(" park_day", result);
        }

        [Test]
        public void UnrecognizedWordLine_StillColorsFirstWord()
        {
            string result = DslSyntaxHighlighter.ToRichText("frobnicate foo");

            AssertColored(result, "frobnicate");
        }

        [Test]
        public void EmptyOrNullSource_ReturnsEmptyStringWithoutThrowing()
        {
            Assert.AreEqual(string.Empty, DslSyntaxHighlighter.ToRichText(null));
            Assert.AreEqual(string.Empty, DslSyntaxHighlighter.ToRichText(string.Empty));
        }

        [Test]
        public void TextContainingAngleBracketsAndAmpersand_IsNotEscaped()
        {
            // Unity's rich text tag parser does not decode HTML entities, so escaping
            // '<'/'>'/'&' would display the literal entity text instead of the original
            // characters — this would corrupt "->" (choice-option syntax) on every line
            // that uses it. Plain, unescaped characters render correctly instead.
            string result = DslSyntaxHighlighter.ToRichText("Alice: 1 < 2 & 3 > 0");

            StringAssert.Contains("1 < 2 & 3 > 0", result);
        }

        [Test]
        public void ChoiceOptionLine_ArrowIsNotEscaped()
        {
            string result = DslSyntaxHighlighter.ToRichText("\"Yes\" -> yes_label");

            StringAssert.Contains("->", result);
            StringAssert.DoesNotContain("&gt;", result);
        }
    }
}
