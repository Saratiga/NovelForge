using NUnit.Framework;

namespace NovelForge.Runtime.Tests
{
    public class DslGrammarTests
    {
        [Test]
        public void TryParseLabel_Valid()
        {
            Assert.IsTrue(DslGrammar.TryParseLabel("label start", out string name));
            Assert.AreEqual("start", name);
        }

        [Test]
        public void TryParseLabel_TabSeparated_IsRejected()
        {
            Assert.IsFalse(DslGrammar.TryParseLabel("label\tstart", out _));
        }

        [Test]
        public void TryParseLabel_NoName_IsRejected()
        {
            Assert.IsFalse(DslGrammar.TryParseLabel("label", out _));
        }

        [Test]
        public void TryParseJump_Jump()
        {
            Assert.IsTrue(DslGrammar.TryParseJump("jump end", out string target, out bool isGosub));
            Assert.AreEqual("end", target);
            Assert.IsFalse(isGosub);
        }

        [Test]
        public void TryParseJump_Gosub()
        {
            Assert.IsTrue(DslGrammar.TryParseJump("gosub sub", out string target, out bool isGosub));
            Assert.AreEqual("sub", target);
            Assert.IsTrue(isGosub);
        }

        [Test]
        public void TryParseJump_OtherLine()
        {
            Assert.IsFalse(DslGrammar.TryParseJump("jumper x", out _, out _));
        }

        [TestCase("Hello there left", "left")]
        [TestCase("I went right", "right")]
        [TestCase("Stand in the center", "center")]
        public void TrailingPosition_MatchesOnlyAtTheEnd(string text, string expected)
        {
            var match = DslGrammar.TrailingPosition.Match(text);

            Assert.IsTrue(match.Success);
            Assert.AreEqual(expected, match.Groups[1].Value);
        }

        [TestCase("left behind")]
        [TestCase("leftover")]
        [TestCase("upright")]
        public void TrailingPosition_IgnoresPositionWordsNotAtTheEndOrInsideWords(string text)
        {
            Assert.IsFalse(DslGrammar.TrailingPosition.IsMatch(text));
        }

        [TestCase("Hi @greet_1", "greet_1")]
        [TestCase("@first then text", "first")]
        public void IdTag_ReadsStandaloneId(string text, string expected)
        {
            var match = DslGrammar.IdTag.Match(text);

            Assert.IsTrue(match.Success);
            Assert.AreEqual(expected, match.Groups[1].Value);
        }

        [TestCase("Mail bob@example.com")]
        [TestCase("Price @5")]
        public void IdTag_IgnoresEmailsAndNonIdentifiers(string text)
        {
            Assert.IsFalse(DslGrammar.IdTag.IsMatch(text));
        }

        [Test]
        public void Keywords_CannotBeMutatedThroughACast()
        {
            Assert.IsNotInstanceOf<string[]>(DslGrammar.Keywords);
        }

        [Test]
        public void ChoiceOptionLine_Groups()
        {
            var match = DslGrammar.ChoiceOptionLine.Match("\"Go\" @go_id -> forest");

            Assert.IsTrue(match.Success);
            Assert.AreEqual("Go", match.Groups[1].Value);
            Assert.AreEqual("go_id", match.Groups[2].Value);
            Assert.AreEqual("forest", match.Groups[3].Value);
        }
    }
}
