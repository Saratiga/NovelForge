using NUnit.Framework;

namespace NovelForge.Runtime.Tests
{
    public class ScriptCompilerConditionalsTests
    {
        private static NovelScript Compile(string source) => new ScriptCompiler().Compile(source);

        private static void Run(NovelScript script, StoryContext context)
        {
            var controller = new PlaybackController(script, context);
            CoroutineTestUtil.RunToCompletion(controller.RunAll());
        }

        [Test]
        public void IfTrue_RunsBodyAndSkipsElse()
        {
            var dialogue = new RecordingDialoguePresenter();
            var script = Compile(
                "set relationship = 5\n" +
                "if relationship >= 3\n" +
                "Alice: You did great!\n" +
                "else\n" +
                "Alice: That was rough.\n" +
                "endif\n" +
                "Alice: The end.\n");

            Run(script, new StoryContext { Dialogue = dialogue });

            CollectionAssert.AreEqual(new[] { "You did great!", "The end." },
                dialogue.Calls.ConvertAll(c => c.text));
        }

        [Test]
        public void IfFalse_RunsElseBranch()
        {
            var dialogue = new RecordingDialoguePresenter();
            var script = Compile(
                "set relationship = 1\n" +
                "if relationship >= 3\n" +
                "Alice: You did great!\n" +
                "else\n" +
                "Alice: That was rough.\n" +
                "endif\n" +
                "Alice: The end.\n");

            Run(script, new StoryContext { Dialogue = dialogue });

            CollectionAssert.AreEqual(new[] { "That was rough.", "The end." },
                dialogue.Calls.ConvertAll(c => c.text));
        }

        [Test]
        public void IfWithoutElse_FalseSkipsToEndif()
        {
            var dialogue = new RecordingDialoguePresenter();
            var script = Compile(
                "set relationship = 1\n" +
                "if relationship >= 3\n" +
                "Alice: You did great!\n" +
                "endif\n" +
                "Alice: The end.\n");

            Run(script, new StoryContext { Dialogue = dialogue });

            CollectionAssert.AreEqual(new[] { "The end." },
                dialogue.Calls.ConvertAll(c => c.text));
        }

        [Test]
        public void Endif_WithoutMatchingIf_Throws()
        {
            Assert.Throws<ParseException>(() => Compile("endif\n"));
        }

        [Test]
        public void Else_WithoutMatchingIf_Throws()
        {
            Assert.Throws<ParseException>(() => Compile("else\n"));
        }

        [Test]
        public void UnclosedIf_Throws()
        {
            Assert.Throws<ParseException>(() => Compile("if x == 1\nAlice: hi\n"));
        }
    }
}
