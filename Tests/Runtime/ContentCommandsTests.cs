using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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

        [Test]
        public void SayLine_NullDialoguePresenter_LogsErrorAndDoesNotThrow()
        {
            var context = new StoryContext();
            var command = new SayLineCommand("Alice", "Привет!", "happy", "left");

            LogAssert.Expect(LogType.Error, "NovelForge: no IDialoguePresenter wired — skipping dialogue line.");
            Assert.DoesNotThrow(() => CoroutineTestUtil.RunToCompletion(command.Execute(context, new StoryPointer())));
        }

        [Test]
        public void PlayMusic_NullAudioPresenter_LogsErrorAndDoesNotThrow()
        {
            var context = new StoryContext();
            var command = new PlayMusicCommand("theme_calm");

            LogAssert.Expect(LogType.Error, "NovelForge: no IAudioPresenter wired — skipping music.");
            Assert.DoesNotThrow(() => CoroutineTestUtil.RunToCompletion(command.Execute(context, new StoryPointer())));
        }

        [Test]
        public void PlaySfx_NullAudioPresenter_LogsErrorAndDoesNotThrow()
        {
            var context = new StoryContext();
            var command = new PlaySfxCommand("door_open");

            LogAssert.Expect(LogType.Error, "NovelForge: no IAudioPresenter wired — skipping sfx.");
            Assert.DoesNotThrow(() => CoroutineTestUtil.RunToCompletion(command.Execute(context, new StoryPointer())));
        }

        [Test]
        public void ShowBackground_NullBackgroundPresenter_LogsErrorAndDoesNotThrow()
        {
            var context = new StoryContext();
            var command = new ShowBackgroundCommand("park_day");

            LogAssert.Expect(LogType.Error, "NovelForge: no IBackgroundPresenter wired — skipping background change.");
            Assert.DoesNotThrow(() => CoroutineTestUtil.RunToCompletion(command.Execute(context, new StoryPointer())));
        }

        [Test]
        public void ShowCg_NullBackgroundPresenter_LogsErrorAndDoesNotThrow()
        {
            var context = new StoryContext();
            var command = new ShowCgCommand("intro_cg");

            LogAssert.Expect(LogType.Error, "NovelForge: no IBackgroundPresenter wired — skipping CG.");
            Assert.DoesNotThrow(() => CoroutineTestUtil.RunToCompletion(command.Execute(context, new StoryPointer())));
        }

        [Test]
        public void Wait_NullTimingPresenter_LogsErrorAndDoesNotThrow()
        {
            var context = new StoryContext();
            var command = new WaitCommand(1.5f);

            LogAssert.Expect(LogType.Error, "NovelForge: no ITimingPresenter wired — skipping wait.");
            Assert.DoesNotThrow(() => CoroutineTestUtil.RunToCompletion(command.Execute(context, new StoryPointer())));
        }
    }
}
