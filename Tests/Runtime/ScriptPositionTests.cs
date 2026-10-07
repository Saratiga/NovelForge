using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NovelForge.Runtime.Tests
{
    public class ScriptPositionTests
    {
        // Command indices: 0 intro, 1 a1, 2 a2, 3 c1. Labels: a -> 1, b -> 3, c -> 3.
        private const string Src = "Alice: intro\nlabel a\nAlice: a1\nAlice: a2\nlabel b\nlabel c\nAlice: c1\n";

        private static NovelScript Compile(string source) => new ScriptCompiler().Compile(source);

        private static void AssertPosition(string label, int offset, ScriptPosition actual)
        {
            Assert.AreEqual(label, actual.Label);
            Assert.AreEqual(offset, actual.Offset);
        }

        [Test]
        public void ToPosition_BeforeFirstLabel_HasNullLabel()
        {
            AssertPosition(null, 0, Compile(Src).ToPosition(0));
        }

        [Test]
        public void ToPosition_InsideLabel_IsOffsetFromLabel()
        {
            AssertPosition("a", 1, Compile(Src).ToPosition(2));
        }

        [Test]
        public void ToPosition_SharedIndex_PicksOrdinalSmallestName()
        {
            AssertPosition("b", 0, Compile(Src).ToPosition(3));
        }

        [Test]
        public void ToPosition_PastEnd_IsNullLabelWithCommandCount()
        {
            AssertPosition(null, 4, Compile(Src).ToPosition(int.MaxValue));
        }

        [Test]
        public void TryResolve_RoundTripsEveryIndex()
        {
            NovelScript script = Compile(Src);

            for (int i = 0; i <= 4; i++)
            {
                Assert.IsTrue(script.TryResolve(script.ToPosition(i), out int resolved), $"index {i}");
                Assert.AreEqual(i, resolved, $"index {i}");
            }
        }

        [Test]
        public void TryResolve_MissingLabel_ReturnsFalse()
        {
            Assert.IsFalse(Compile(Src).TryResolve(new ScriptPosition { Label = "zzz", Offset = 0 }, out _));
        }

        [Test]
        public void TryResolve_OffsetPastLabelBlock_ReturnsLabelStartWithWarning()
        {
            LogAssert.Expect(LogType.Warning, new Regex("'a'.*offset 5"));

            Assert.IsTrue(Compile(Src).TryResolve(new ScriptPosition { Label = "a", Offset = 5 }, out int resolved));
            Assert.AreEqual(1, resolved);
        }

        [Test]
        public void TryResolve_AfterInsertingLineAboveLabel_StillHitsSameLine()
        {
            ScriptPosition position = Compile(Src).ToPosition(2);
            NovelScript edited = Compile("Alice: new\n" + Src);

            Assert.IsTrue(edited.TryResolve(position, out int resolved));
            Assert.AreEqual(3, resolved);
        }
    }
}
