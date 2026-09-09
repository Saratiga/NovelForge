using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NovelForge.Runtime.Tests
{
    public class SaveLoadRoundTripTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "NovelForgeRoundTripTests_" + System.Guid.NewGuid());
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }

        private const string Source = @"
label start
  set score = 0
  gosub greet
  set score += 1
  Alice: Conversation continues.
  return

label greet
  Alice: Hello!
  set score += 1
  return
";

        [Test]
        public void CreateSnapshot_ThroughRealJsonSaveStorage_RestoresAndFinishesCorrectly()
        {
            var storage = new JsonSaveStorage(_directory);
            var script = new ScriptCompiler().Compile(Source);

            // Run partway (through the mid-gosub dialogue line), same technique as
            // the in-memory integration test in Tasks 5.
            var firstHalfDialogue = new RecordingDialoguePresenter();
            var firstHalfContext = new StoryContext { Dialogue = firstHalfDialogue };
            var firstHalfController = new PlaybackController(script, firstHalfContext);
            IEnumerator routine = firstHalfController.RunAll();
            int guard = 0;
            while (firstHalfDialogue.Calls.Count == 0)
            {
                if (++guard > 100)
                    Assert.Fail("Script never reached a dialogue line — has Source changed?");
                routine.MoveNext();
                if (routine.Current is IEnumerator nested)
                    CoroutineTestUtil.RunToCompletion(nested);
            }

            // Save: build a real SaveData from the live snapshot and write it through
            // JsonSaveStorage — this is the production usage pattern nothing else tests.
            var snapshot = firstHalfController.CreateSnapshot();
            var saveData = new SaveData
            {
                ScriptId = "test-script",
                PointerIndex = snapshot.PointerIndex,
                CallStack = snapshot.CallStack,
                Variables = new Dictionary<string, object>(firstHalfContext.Variables.Export()),
            };
            storage.Save("slot1", saveData);

            // Load: read it back through JsonSaveStorage into a brand-new controller/context.
            var result = storage.Load("slot1");
            Assert.AreEqual(SaveLoadStatus.Success, result.Status);

            var secondHalfDialogue = new RecordingDialoguePresenter();
            var secondHalfContext = new StoryContext { Dialogue = secondHalfDialogue };
            secondHalfContext.Variables.Import(result.Data.Variables);
            var secondHalfController = new PlaybackController(script, secondHalfContext);
            secondHalfController.RestoreSnapshot(new PlaybackSnapshot
            {
                PointerIndex = result.Data.PointerIndex,
                CallStack = result.Data.CallStack,
            });

            LogAssert.Expect(LogType.Error, "NovelForge: 'return' with an empty call stack — ending playback.");
            CoroutineTestUtil.RunToCompletion(secondHalfController.RunAll());

            CollectionAssert.AreEqual(new[] { "Hello!" }, firstHalfDialogue.Calls.ConvertAll(c => c.text));
            CollectionAssert.AreEqual(new[] { "Conversation continues." }, secondHalfDialogue.Calls.ConvertAll(c => c.text));
            Assert.AreEqual(2, secondHalfContext.Variables.GetInt("score"));
            Assert.IsTrue(secondHalfController.IsFinished);
        }
    }
}
