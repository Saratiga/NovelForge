using NUnit.Framework;

namespace NovelForge.Editor.Tests
{
    public class GraphDocumentFormatterTests
    {
        [Test]
        public void Serialize_EmptyDocument_ReturnsEmptyString()
        {
            var doc = GraphDocumentParser.Parse(string.Empty);

            Assert.AreEqual(string.Empty, GraphDocumentFormatter.Serialize(doc));
        }

        [Test]
        public void Serialize_SingleNodeWithJump_RoundTripsExactly()
        {
            string source = "label start\nAlice: Hello!\njump ending\n";

            var doc = GraphDocumentParser.Parse(source);

            Assert.AreEqual(source, GraphDocumentFormatter.Serialize(doc));
        }

        [Test]
        public void Serialize_NodeWithGosub_RoundTripsExactly()
        {
            string source = "label a\ngosub helper\n";

            var doc = GraphDocumentParser.Parse(source);

            Assert.AreEqual(source, GraphDocumentFormatter.Serialize(doc));
        }

        [Test]
        public void Serialize_NodeWithReturn_RoundTripsExactly()
        {
            string source = "label a\nAlice: Bye!\nreturn\n";

            var doc = GraphDocumentParser.Parse(source);

            Assert.AreEqual(source, GraphDocumentFormatter.Serialize(doc));
        }

        [Test]
        public void Serialize_NodeWithChoiceOptions_RoundTripsExactly()
        {
            string source = "label a\nAlice: Pick one.\nchoice\n  \"Yes\" @yes_opt -> good\n  \"No\" -> bad\n";

            var doc = GraphDocumentParser.Parse(source);

            Assert.AreEqual(source, GraphDocumentFormatter.Serialize(doc));
        }

        [Test]
        public void Serialize_SingleLineLeadingComment_RoundTripsExactly()
        {
            string source = "// a note\nlabel a\nAlice: Hi!\n";

            var doc = GraphDocumentParser.Parse(source);

            Assert.AreEqual(source, GraphDocumentFormatter.Serialize(doc));
        }

        [Test]
        public void Serialize_MultiLineLeadingComment_RoundTripsExactly()
        {
            string source = "// first line\n// second line\nlabel a\nAlice: Hi!\n";

            var doc = GraphDocumentParser.Parse(source);

            Assert.AreEqual(source, GraphDocumentFormatter.Serialize(doc));
        }

        [Test]
        public void Serialize_MultiNodeScriptWithBlankLineSeparators_RoundTripsExactly()
        {
            string source = "label a\nAlice: Hi!\njump b\n\nlabel b\nAlice: Bye!\nreturn\n";

            var doc = GraphDocumentParser.Parse(source);

            Assert.AreEqual(source, GraphDocumentFormatter.Serialize(doc));
        }

        [Test]
        public void Serialize_FallThroughNodeWithBlankLineSeparator_RoundTripsExactly()
        {
            string source = "label a\nAlice: Hi!\n\nlabel b\nAlice: Bye!\nreturn\n";

            var doc = GraphDocumentParser.Parse(source);

            Assert.AreEqual(source, GraphDocumentFormatter.Serialize(doc));
        }

        [Test]
        public void Serialize_OrphanCommentNotAttachedToNextLabel_StaysInPreviousNodesBody()
        {
            string source = "label a\nAlice: Hi!\n// orphan, stays in previous block's body\n\nlabel b\nAlice: Bye!\n";

            var doc = GraphDocumentParser.Parse(source);

            Assert.AreEqual(source, GraphDocumentFormatter.Serialize(doc));
        }

        [Test]
        public void Serialize_ThenReparse_IsIdempotent_ForPreambleWithNoBlankLineBeforeFirstLabel()
        {
            // The formatter always inserts a blank line before every node including the first
            // real label after an unlabeled preamble, so the very first Serialize() of
            // hand-authored text without that blank line won't be byte-identical to the input —
            // but re-parsing its own output must reproduce an equivalent document, proving the
            // formatter's own conventions are stable under repeated save/reload.
            string source = "set intro_seen = true\nlabel start\nAlice: Hi!\n";
            var original = GraphDocumentParser.Parse(source);

            string serialized = GraphDocumentFormatter.Serialize(original);
            var reparsed = GraphDocumentParser.Parse(serialized);

            Assert.AreEqual(original.Nodes.Count, reparsed.Nodes.Count);
            for (int i = 0; i < original.Nodes.Count; i++)
            {
                Assert.AreEqual(original.Nodes[i].LabelName, reparsed.Nodes[i].LabelName);
                Assert.AreEqual(original.Nodes[i].Body, reparsed.Nodes[i].Body);
            }

            string reserialized = GraphDocumentFormatter.Serialize(reparsed);
            Assert.AreEqual(serialized, reserialized);
        }
    }
}
