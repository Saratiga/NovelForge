using System.IO;
using NUnit.Framework;

namespace NovelForge.Runtime.Tests
{
    public class SaveLoadControllerTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "NovelForgeSaveLoadControllerTests_" + System.Guid.NewGuid());
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }

        private const string Source = "label start\nreturn\n";

        [Test]
        public void SaveTo_ThenLoadInto_OnFreshContext_RestoresVariables()
        {
            var storage = new JsonSaveStorage(_directory);
            var script = new ScriptCompiler().Compile(Source);

            var context = new StoryContext();
            context.Variables.Set("score", 5);
            var playback = new PlaybackController(script, context);
            var controller = new SaveLoadController(playback, context, "test-script", storage);

            controller.SaveTo("slot1");

            var newContext = new StoryContext();
            var newPlayback = new PlaybackController(script, newContext);
            var newController = new SaveLoadController(newPlayback, newContext, "test-script", storage);

            var result = newController.LoadInto("slot1");

            Assert.AreEqual(SaveLoadStatus.Success, result.Status);
            Assert.AreEqual(5, newContext.Variables.GetInt("score"));
        }

        [Test]
        public void SaveTo_ThenLoadInto_RestoresPointerAndCallStackOntoNewPlaybackController()
        {
            var storage = new JsonSaveStorage(_directory);
            var script = new ScriptCompiler().Compile(Source);

            var context = new StoryContext();
            var playback = new PlaybackController(script, context);
            var controller = new SaveLoadController(playback, context, "test-script", storage);

            controller.SaveTo("slot1");
            PlaybackSnapshot originalSnapshot = playback.CreateSnapshot();

            var newContext = new StoryContext();
            var newPlayback = new PlaybackController(script, newContext);
            var newController = new SaveLoadController(newPlayback, newContext, "test-script", storage);
            newController.LoadInto("slot1");

            PlaybackSnapshot restoredSnapshot = newPlayback.CreateSnapshot();
            Assert.AreEqual(originalSnapshot.PointerIndex, restoredSnapshot.PointerIndex);
            CollectionAssert.AreEqual(originalSnapshot.CallStack, restoredSnapshot.CallStack);
        }

        [Test]
        public void LoadInto_MissingSlot_ReturnsNotFound_AndDoesNotChangeContextOrPlayback()
        {
            var storage = new JsonSaveStorage(_directory);
            var script = new ScriptCompiler().Compile(Source);

            var context = new StoryContext();
            context.Variables.Set("score", 7);
            var playback = new PlaybackController(script, context);
            var controller = new SaveLoadController(playback, context, "test-script", storage);

            var result = controller.LoadInto("does-not-exist");

            Assert.AreEqual(SaveLoadStatus.NotFound, result.Status);
            Assert.AreEqual(7, context.Variables.GetInt("score"));
            Assert.AreEqual(0, playback.CreateSnapshot().PointerIndex);
        }

        [Test]
        public void LoadInto_IncompatibleSlot_ReturnsIncompatible_AndDoesNotChangeContextOrPlayback()
        {
            var storage = new JsonSaveStorage(_directory);
            var script = new ScriptCompiler().Compile(Source);
            Directory.CreateDirectory(_directory);
            File.WriteAllText(Path.Combine(_directory, "slot1.json"), "{\"SchemaVersion\":999,\"ScriptId\":\"x\"}");

            var context = new StoryContext();
            context.Variables.Set("score", 9);
            var playback = new PlaybackController(script, context);
            var controller = new SaveLoadController(playback, context, "test-script", storage);

            var result = controller.LoadInto("slot1");

            Assert.AreEqual(SaveLoadStatus.Incompatible, result.Status);
            Assert.AreEqual(9, context.Variables.GetInt("score"));
            Assert.AreEqual(0, playback.CreateSnapshot().PointerIndex);
        }

        [Test]
        public void LoadInto_DifferentScriptId_ReturnsScriptMismatch_AndDoesNotChangeState()
        {
            var storage = new JsonSaveStorage(_directory);
            var script = new ScriptCompiler().Compile(Source);
            var savingContext = new StoryContext();
            var savingPlayback = new PlaybackController(script, savingContext);
            new SaveLoadController(savingPlayback, savingContext, "story-a", storage).SaveTo("slot1");

            var context = new StoryContext();
            context.Variables.Set("score", 7);
            var playback = new PlaybackController(script, context);
            var result = new SaveLoadController(playback, context, "story-b", storage).LoadInto("slot1");

            Assert.AreEqual(SaveLoadStatus.ScriptMismatch, result.Status);
            Assert.AreEqual(7, context.Variables.GetInt("score"));
            Assert.AreEqual(0, playback.CurrentIndex);
        }

        [Test]
        public void LoadInto_SavedLabelRemovedFromScript_ReturnsIncompatible_AndDoesNotChangeState()
        {
            var storage = new JsonSaveStorage(_directory);
            var original = new ScriptCompiler().Compile("label start\nAlice: one\nlabel gone\nAlice: two\n");
            var savingContext = new StoryContext();
            var savingPlayback = new PlaybackController(original, savingContext);
            savingPlayback.RestoreSnapshot(new PlaybackSnapshot { PointerIndex = 1 });
            new SaveLoadController(savingPlayback, savingContext, "test-script", storage).SaveTo("slot1");

            var edited = new ScriptCompiler().Compile("label start\nAlice: one\n");
            var context = new StoryContext();
            context.Variables.Set("score", 7);
            var playback = new PlaybackController(edited, context);
            var result = new SaveLoadController(playback, context, "test-script", storage).LoadInto("slot1");

            Assert.AreEqual(SaveLoadStatus.Incompatible, result.Status);
            Assert.AreEqual(7, context.Variables.GetInt("score"));
            Assert.AreEqual(0, playback.CurrentIndex);
        }

        [Test]
        public void LoadInto_ScriptEditedAboveSavedLine_ResumesOnSameLine()
        {
            var storage = new JsonSaveStorage(_directory);
            var original = new ScriptCompiler().Compile("label start\nAlice: one\nAlice: two\n");
            var savingContext = new StoryContext();
            var savingPlayback = new PlaybackController(original, savingContext);
            savingPlayback.RestoreSnapshot(new PlaybackSnapshot { PointerIndex = 1 });
            new SaveLoadController(savingPlayback, savingContext, "test-script", storage).SaveTo("slot1");

            var edited = new ScriptCompiler().Compile("label intro\nAlice: hi\nlabel start\nAlice: one\nAlice: two\n");
            var context = new StoryContext();
            var playback = new PlaybackController(edited, context);
            var result = new SaveLoadController(playback, context, "test-script", storage).LoadInto("slot1");

            Assert.AreEqual(SaveLoadStatus.Success, result.Status);
            Assert.AreEqual(2, playback.CurrentIndex); // "two": was index 1, now index 2
        }

        [Test]
        public void LoadInto_RestoresCallStackThroughLabels()
        {
            const string original = "label main\ngosub sub\nAlice: after\njump end\nlabel sub\nBob: in sub\nreturn\nlabel end\n";
            var storage = new JsonSaveStorage(_directory);
            var savingContext = new StoryContext();
            var savingPlayback = new PlaybackController(new ScriptCompiler().Compile(original), savingContext);
            // At "in sub" (index 3) with a return address of "after" (index 1).
            savingPlayback.RestoreSnapshot(new PlaybackSnapshot { PointerIndex = 3, CallStack = new[] { 1 } });
            new SaveLoadController(savingPlayback, savingContext, "test-script", storage).SaveTo("slot1");

            var dialogue = new RecordingDialoguePresenter();
            var context = new StoryContext { Dialogue = dialogue };
            var playback = new PlaybackController(new ScriptCompiler().Compile("Alice: extra\n" + original), context);
            var result = new SaveLoadController(playback, context, "test-script", storage).LoadInto("slot1");
            CoroutineTestUtil.RunToCompletion(playback.RunAll());

            Assert.AreEqual(SaveLoadStatus.Success, result.Status);
            CollectionAssert.AreEqual(new[] { "in sub", "after" }, dialogue.Calls.ConvertAll(c => c.text));
        }

        [Test]
        public void SlotExists_DelegatesToStorage()
        {
            var storage = new JsonSaveStorage(_directory);
            var script = new ScriptCompiler().Compile(Source);
            var context = new StoryContext();
            var playback = new PlaybackController(script, context);
            var controller = new SaveLoadController(playback, context, "test-script", storage);

            Assert.IsFalse(controller.SlotExists("slot1"));
            controller.SaveTo("slot1");
            Assert.IsTrue(controller.SlotExists("slot1"));
        }
    }
}
