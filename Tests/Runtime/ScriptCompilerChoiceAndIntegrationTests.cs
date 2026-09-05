using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NovelForge.Runtime.Tests
{
    public class ScriptCompilerChoiceAndIntegrationTests
    {
        private static NovelScript Compile(string source) => new ScriptCompiler().Compile(source);

        [Test]
        public void Choice_ParsesOptionsAndResolvesTargets()
        {
            var script = Compile(
                "choice\n" +
                "  \"Good, thanks!\" -> good_response\n" +
                "  \"Not great...\" -> bad_response\n" +
                "label good_response\n" +
                "return\n" +
                "label bad_response\n" +
                "return\n");

            Assert.IsInstanceOf<ChoiceCommand>(script.Commands[0]);
        }

        [Test]
        public void Choice_SelectingSecondOption_JumpsToItsLabel()
        {
            var choices = new RecordingChoicePresenter { NextSelection = 1 };
            var dialogue = new RecordingDialoguePresenter();
            var script = Compile(
                "choice\n" +
                "  \"Good, thanks!\" -> good_response\n" +
                "  \"Not great...\" -> bad_response\n" +
                "label good_response\n" +
                "Alice: Glad to hear it!\n" +
                "return\n" +
                "label bad_response\n" +
                "Alice: What happened?\n" +
                "return\n");
            var context = new StoryContext { Choices = choices, Dialogue = dialogue };
            var controller = new PlaybackController(script, context);

            // "return" here has no matching gosub (the script only reaches it via the
            // choice jump), so the call stack is empty when it runs — expected, see
            // Task 4's Return_WithEmptyStack test for the same pattern.
            LogAssert.Expect(LogType.Error, "NovelForge: 'return' with an empty call stack — ending playback.");
            CoroutineTestUtil.RunToCompletion(controller.RunAll());

            CollectionAssert.AreEqual(new[] { "What happened?" }, dialogue.Calls.ConvertAll(c => c.text));
        }

        [Test]
        public void Choice_WithNoOptionLines_Throws()
        {
            Assert.Throws<ParseException>(() => Compile("choice\njump nowhere\n"));
        }

        [Test]
        public void FullExampleScript_CompilesAndRunsToCompletion()
        {
            const string source = @"
label start
  bg park_day
  music theme_calm

  Alice: Привет! #happy left
  Alice: Как дела?

  choice
    ""Хорошо, спасибо!"" -> good_response
    ""Не очень..."" -> bad_response

label good_response
  Alice: Приятно слышать!
  jump chapter2

label bad_response
  set relationship -= 1
  Alice: Что случилось?
  jump chapter2

label chapter2
  gosub common_reaction
  Alice: Вот и конец главы.
  jump the_end

label common_reaction
  Alice: (кивает) #neutral
  return

label the_end
  return
";
            var script = new ScriptCompiler().Compile(source);
            var dialogue = new RecordingDialoguePresenter();
            var choices = new RecordingChoicePresenter { NextSelection = 1 };
            var audio = new RecordingAudioPresenter();
            var backgrounds = new RecordingBackgroundPresenter();
            var context = new StoryContext
            {
                Dialogue = dialogue,
                Choices = choices,
                Audio = audio,
                Backgrounds = backgrounds,
            };
            var controller = new PlaybackController(script, context);

            // The script's final "return" (under label the_end) is reached via a plain
            // jump, not gosub, so the call stack is empty when it runs — the same
            // "graceful end of story" pattern Task 4's Return_WithEmptyStack test covers,
            // and it logs the same expected error.
            LogAssert.Expect(LogType.Error, "NovelForge: 'return' with an empty call stack — ending playback.");
            CoroutineTestUtil.RunToCompletion(controller.RunAll());

            CollectionAssert.AreEqual(new[] { "park_day" }, backgrounds.BackgroundCalls);
            CollectionAssert.AreEqual(new[] { "theme_calm" }, audio.MusicCalls);
            CollectionAssert.AreEqual(
                new[] { "Привет!", "Как дела?", "Что случилось?", "(кивает)", "Вот и конец главы." },
                dialogue.Calls.ConvertAll(c => c.text));
            Assert.AreEqual(-1, context.Variables.GetInt("relationship"));
            Assert.IsTrue(controller.IsFinished);
        }
    }
}
