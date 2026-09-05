using NUnit.Framework;

namespace NovelForge.Runtime.Tests
{
    public class ContentCommandsTests
    {
        [Test]
        public void SayLine_DelegatesToDialoguePresenter()
        {
            var dialogue = new RecordingDialoguePresenter();
            var context = new StoryContext { Dialogue = dialogue };
            var command = new SayLineCommand("Alice", "Привет!", "happy", "left");

            CoroutineTestUtil.RunToCompletion(command.Execute(context, new StoryPointer()));

            Assert.AreEqual(1, dialogue.Calls.Count);
            Assert.AreEqual(("Alice", "Привет!", "happy", "left"), dialogue.Calls[0]);
        }

        [Test]
        public void PlayMusic_DelegatesToAudioPresenter()
        {
            var audio = new RecordingAudioPresenter();
            var context = new StoryContext { Audio = audio };
            var command = new PlayMusicCommand("theme_calm");

            CoroutineTestUtil.RunToCompletion(command.Execute(context, new StoryPointer()));

            CollectionAssert.AreEqual(new[] { "theme_calm" }, audio.MusicCalls);
        }

        [Test]
        public void PlaySfx_DelegatesToAudioPresenter()
        {
            var audio = new RecordingAudioPresenter();
            var context = new StoryContext { Audio = audio };
            var command = new PlaySfxCommand("door_open");

            CoroutineTestUtil.RunToCompletion(command.Execute(context, new StoryPointer()));

            CollectionAssert.AreEqual(new[] { "door_open" }, audio.SfxCalls);
        }

        [Test]
        public void ShowBackground_DelegatesToBackgroundPresenter()
        {
            var backgrounds = new RecordingBackgroundPresenter();
            var context = new StoryContext { Backgrounds = backgrounds };
            var command = new ShowBackgroundCommand("park_day");

            CoroutineTestUtil.RunToCompletion(command.Execute(context, new StoryPointer()));

            CollectionAssert.AreEqual(new[] { "park_day" }, backgrounds.BackgroundCalls);
        }

        [Test]
        public void ShowCg_DelegatesToBackgroundPresenter()
        {
            var backgrounds = new RecordingBackgroundPresenter();
            var context = new StoryContext { Backgrounds = backgrounds };
            var command = new ShowCgCommand("intro_cg");

            CoroutineTestUtil.RunToCompletion(command.Execute(context, new StoryPointer()));

            CollectionAssert.AreEqual(new[] { "intro_cg" }, backgrounds.CgCalls);
        }

        [Test]
        public void Wait_DelegatesToTimingPresenter()
        {
            var timing = new RecordingTimingPresenter();
            var context = new StoryContext { Timing = timing };
            var command = new WaitCommand(1.5f);

            CoroutineTestUtil.RunToCompletion(command.Execute(context, new StoryPointer()));

            CollectionAssert.AreEqual(new[] { 1.5f }, timing.Calls);
        }
    }
}
