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
