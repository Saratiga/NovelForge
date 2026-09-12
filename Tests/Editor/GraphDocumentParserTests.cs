using System.Linq;
using NUnit.Framework;

namespace NovelForge.Editor.Tests
{
    public class GraphDocumentParserTests
    {
        [Test]
        public void Parse_EmptyOrNullSource_ReturnsEmptyDocumentWithoutThrowing()
        {
            Assert.AreEqual(0, GraphDocumentParser.Parse(null).Nodes.Count);
            Assert.AreEqual(0, GraphDocumentParser.Parse(string.Empty).Nodes.Count);
        }

        [Test]
        public void Parse_SingleLabelWithBodyAndJump_ProducesOneNodeWithTrailingJump()
        {
            string source = "label start\nAlice: Hello!\njump ending\n";

            var doc = GraphDocumentParser.Parse(source);

            Assert.AreEqual(1, doc.Nodes.Count);
            var node = doc.Nodes[0];
            Assert.AreEqual("start", node.LabelName);
            Assert.AreEqual("Alice: Hello!", node.Body);
            Assert.AreEqual("ending", node.TrailingJumpTarget);
            Assert.IsFalse(node.TrailingIsGosub);
            Assert.IsFalse(node.EndsInReturn);
            CollectionAssert.IsEmpty(node.Choices);
        }

        [Test]
        public void Parse_TrailingGosub_SetsTrailingIsGosubTrue()
        {
            var doc = GraphDocumentParser.Parse("label a\ngosub helper\n");

            Assert.AreEqual("helper", doc.Nodes[0].TrailingJumpTarget);
            Assert.IsTrue(doc.Nodes[0].TrailingIsGosub);
        }

        [Test]
        public void Parse_TrailingReturn_SetsEndsInReturnTrue()
        {
            var doc = GraphDocumentParser.Parse("label a\nAlice: Bye!\nreturn\n");

            Assert.IsTrue(doc.Nodes[0].EndsInReturn);
            Assert.IsNull(doc.Nodes[0].TrailingJumpTarget);
            Assert.AreEqual("Alice: Bye!", doc.Nodes[0].Body);
        }

        [Test]
        public void Parse_CommentAfterTrailingJump_IsPreservedInBody_NotDropped()
        {
            string source = "label a\nAlice: Bye!\nreturn\n// dangling comment\n\nlabel b\nAlice: Hi!\n";

            var doc = GraphDocumentParser.Parse(source);

            Assert.IsTrue(doc.Nodes[0].EndsInReturn);
            StringAssert.Contains("// dangling comment", doc.Nodes[0].Body);
            Assert.IsNull(doc.Nodes[1].LeadingComment);
        }

        [Test]
        public void Parse_TrailingChoiceBlock_ExtractsAllOptionsWithExplicitIdAndImplicit()
        {
            string source = "label a\nAlice: Pick one.\nchoice\n  \"Yes\" @yes_opt -> good\n  \"No\" -> bad\n";

            var doc = GraphDocumentParser.Parse(source);

            var node = doc.Nodes[0];
            Assert.AreEqual("Alice: Pick one.", node.Body);
            Assert.AreEqual(2, node.Choices.Count);
            Assert.AreEqual("Yes", node.Choices[0].Text);
            Assert.AreEqual("yes_opt", node.Choices[0].ExplicitId);
            Assert.AreEqual("good", node.Choices[0].TargetLabel);
            Assert.AreEqual("No", node.Choices[1].Text);
            Assert.IsNull(node.Choices[1].ExplicitId);
            Assert.AreEqual("bad", node.Choices[1].TargetLabel);
        }

        [Test]
        public void Parse_BlockWithNoTrailingElement_LeavesTrailingFieldsNull_ForFallThrough()
        {
            string source = "label a\nAlice: Hi!\nlabel b\nAlice: Bye!\n";

            var doc = GraphDocumentParser.Parse(source);

            var first = doc.Nodes[0];
            Assert.IsNull(first.TrailingJumpTarget);
            Assert.IsFalse(first.EndsInReturn);
            CollectionAssert.IsEmpty(first.Choices);
            Assert.AreEqual("b", doc.GetFallThroughTarget(0));
        }

        [Test]
        public void Parse_LeadingCommentImmediatelyBeforeLabel_IsCapturedAndJoinedMultiline()
        {
            string source = "// first line\n// second line\nlabel a\nAlice: Hi!\n";

            var doc = GraphDocumentParser.Parse(source);

            Assert.AreEqual("first line\nsecond line", doc.Nodes[0].LeadingComment);
        }

        [Test]
        public void Parse_CommentSeparatedFromLabelByBlankLine_IsNotTreatedAsLeadingComment()
        {
            string source = "label a\nAlice: Hi!\n// orphan, stays in previous block's body\n\nlabel b\nAlice: Bye!\n";

            var doc = GraphDocumentParser.Parse(source);

            Assert.IsNull(doc.Nodes[1].LeadingComment);
            StringAssert.Contains("// orphan", doc.Nodes[0].Body);
        }

        [Test]
        public void Parse_ContentBeforeFirstLabel_BecomesUnlabeledLeadingNode()
        {
            string source = "set intro_seen = true\nlabel start\nAlice: Hi!\n";

            var doc = GraphDocumentParser.Parse(source);

            Assert.AreEqual(2, doc.Nodes.Count);
            Assert.IsNull(doc.Nodes[0].LabelName);
            Assert.AreEqual("set intro_seen = true", doc.Nodes[0].Body);
            Assert.AreEqual("start", doc.Nodes[1].LabelName);
        }

        [Test]
        public void Parse_NoContentBeforeFirstLabel_DoesNotEmitEmptyPreambleNode()
        {
            var doc = GraphDocumentParser.Parse("label start\nAlice: Hi!\n");

            Assert.AreEqual(1, doc.Nodes.Count);
            Assert.AreEqual("start", doc.Nodes[0].LabelName);
        }

        [Test]
        public void GetBrokenReferences_JumpAndChoiceToUndefinedLabel_AreBothReported()
        {
            string source = "label a\njump missing\nlabel b\nchoice\n  \"x\" -> also_missing\n";

            var doc = GraphDocumentParser.Parse(source);
            var broken = doc.GetBrokenReferences();

            Assert.AreEqual(2, broken.Count);
            Assert.IsTrue(broken.Any(r => r.SourceLabel == "a" && r.TargetLabel == "missing"));
            Assert.IsTrue(broken.Any(r => r.SourceLabel == "b" && r.TargetLabel == "also_missing"));
        }

        [Test]
        public void GetBrokenReferences_ValidTargets_ReportsNothing()
        {
            var doc = GraphDocumentParser.Parse("label a\njump b\nlabel b\nreturn\n");

            CollectionAssert.IsEmpty(doc.GetBrokenReferences());
        }

        [Test]
        public void GetUnreachableLabels_NodeWithNoIncomingReference_IsReported_ExceptTheFirstNode()
        {
            string source = "label start\nreturn\nlabel orphan\nreturn\n";

            var doc = GraphDocumentParser.Parse(source);

            CollectionAssert.AreEqual(new[] { "orphan" }, doc.GetUnreachableLabels());
        }

        [Test]
        public void GetUnreachableLabels_FallThroughTargetCountsAsReachable()
        {
            string source = "label start\nAlice: Hi!\nlabel next\nreturn\n";

            var doc = GraphDocumentParser.Parse(source);

            CollectionAssert.IsEmpty(doc.GetUnreachableLabels());
        }
    }
}
