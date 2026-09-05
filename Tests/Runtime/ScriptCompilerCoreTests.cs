using System.Collections.Generic;
using NUnit.Framework;

namespace NovelForge.Runtime.Tests
{
    public class ScriptCompilerCoreTests
    {
        private static NovelScript Compile(string source) => new ScriptCompiler().Compile(source);

        [Test]
        public void Label_RecordsCommandIndexWithoutEmittingACommand()
        {
            var script = Compile("label start\njump start\n");

            Assert.AreEqual(1, script.Commands.Count);
            Assert.AreEqual(0, script.Labels["start"]);
        }

        [Test]
        public void Jump_ResolvesForwardLabelReference()
        {
            var script = Compile("jump later\nlabel later\nreturn\n");

            Assert.IsInstanceOf<JumpCommand>(script.Commands[0]);
            var jump = script.Commands[0] as JumpCommand;
            Assert.AreEqual(1, jump.TargetIndex);
        }

        [Test]
        public void Jump_ToUndefinedLabel_Throws()
        {
            var ex = Assert.Throws<ParseException>(() => Compile("jump nowhere\n"));
            StringAssert.Contains("nowhere", ex.Message);
        }

        [Test]
        public void Gosub_ResolvesToLabelIndex()
        {
            var script = Compile("gosub common\nlabel common\nreturn\n");

            Assert.IsInstanceOf<GosubCommand>(script.Commands[0]);
            var gosub = script.Commands[0] as GosubCommand;
            Assert.AreEqual(1, gosub.TargetIndex);
        }

        [Test]
        public void Set_ParsesAssignAddSubtractAndLiteralTypes()
        {
            var script = Compile("set relationship = 3\nset relationship += 1\nset relationship -= 2\nset metAlice = true\nset playerName = \"Kai\"\n");

            Assert.AreEqual(5, script.Commands.Count);
            Assert.IsInstanceOf<SetVariableCommand>(script.Commands[0]);
        }

        [Test]
        public void DialogueLine_ParsesCharacterTextEmotionAndPosition()
        {
            var script = Compile("Alice: Привет! #happy left\n");

            Assert.IsInstanceOf<SayLineCommand>(script.Commands[0]);
            var say = script.Commands[0] as SayLineCommand;
            var context = new StoryContext { Dialogue = new RecordingDialoguePresenter() };
            CoroutineTestUtil.RunToCompletion(say.Execute(context, new StoryPointer()));
            var recorded = ((RecordingDialoguePresenter)context.Dialogue).Calls[0];

            Assert.AreEqual("Alice", recorded.characterId);
            Assert.AreEqual("Привет!", recorded.text);
            Assert.AreEqual("happy", recorded.emotion);
            Assert.AreEqual("left", recorded.position);
        }

        [Test]
        public void DialogueLine_WithoutTags_LeavesEmotionAndPositionNull()
        {
            var script = Compile("Alice: Как дела?\n");

            Assert.IsInstanceOf<SayLineCommand>(script.Commands[0]);
            var say = script.Commands[0] as SayLineCommand;
            var context = new StoryContext { Dialogue = new RecordingDialoguePresenter() };
            CoroutineTestUtil.RunToCompletion(say.Execute(context, new StoryPointer()));
            var recorded = ((RecordingDialoguePresenter)context.Dialogue).Calls[0];

            Assert.AreEqual("Как дела?", recorded.text);
            Assert.IsNull(recorded.emotion);
            Assert.IsNull(recorded.position);
        }

        [Test]
        public void GenericCommand_bg_CompilesToShowBackgroundCommand()
        {
            var script = Compile("bg park_day\n");
            Assert.IsInstanceOf<ShowBackgroundCommand>(script.Commands[0]);
        }

        [Test]
        public void GenericCommand_Unknown_Throws()
        {
            var ex = Assert.Throws<ParseException>(() => Compile("frobnicate foo\n"));
            StringAssert.Contains("frobnicate", ex.Message);
        }

        [Test]
        public void CommentLine_AttachesToFollowingCommand()
        {
            var script = Compile("// remember this\njump self\nlabel self\n");
            Assert.AreEqual("remember this", script.Commands[0].SourceComment);
        }

        [Test]
        public void BlankLinesAndComments_AreSkippedBetweenStatements()
        {
            var script = Compile("label a\n\n// a comment\n\njump a\n");
            Assert.AreEqual(1, script.Commands.Count);
        }
    }
}
