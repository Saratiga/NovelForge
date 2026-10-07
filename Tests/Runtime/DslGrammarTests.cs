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
