using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NovelForge.Runtime.Tests
{
    public class SaveLoadIntegrationTests
    {
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
        public void SnapshotMidScript_RestoredIntoFreshController_ProducesSameOutcomeAsUninterruptedRun()
        {
            // Baseline: run the whole script uninterrupted, record what "correct" looks like.
            var baselineDialogue = new RecordingDialoguePresenter();
            var baselineScript = new ScriptCompiler().Compile(Source);
            var baselineContext = new StoryContext { Dialogue = baselineDialogue };
            var baselineController = new PlaybackController(baselineScript, baselineContext);
            LogAssert.Expect(LogType.Error, "NovelForge: 'return' with an empty call stack — ending playback.");
            CoroutineTestUtil.RunToCompletion(baselineController.RunAll());

            // First half: a fresh compile of the same script (a real load would also
            // compile fresh, not reuse an in-memory instance), stepped one command at a
            // time until the mid-gosub dialogue line has fired, then snapshotted.
            var firstHalfDialogue = new RecordingDialoguePresenter();
            var interruptedScript = new ScriptCompiler().Compile(Source);
            var firstHalfContext = new StoryContext { Dialogue = firstHalfDialogue };
            var firstHalfController = new PlaybackController(interruptedScript, firstHalfContext);

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

            var snapshot = firstHalfController.CreateSnapshot();
            var exportedVariables = firstHalfContext.Variables.Export();

            // "Load": a brand-new controller and context for the same script, exactly as
            // a real load would build — never reuse the interrupted run's own objects.
            var secondHalfDialogue = new RecordingDialoguePresenter();
            var secondHalfContext = new StoryContext { Dialogue = secondHalfDialogue };
            secondHalfContext.Variables.Import(exportedVariables);
            var secondHalfController = new PlaybackController(interruptedScript, secondHalfContext);
            secondHalfController.RestoreSnapshot(snapshot);

            LogAssert.Expect(LogType.Error, "NovelForge: 'return' with an empty call stack — ending playback.");
            CoroutineTestUtil.RunToCompletion(secondHalfController.RunAll());

            var combinedDialogue = new List<string>(firstHalfDialogue.Calls.ConvertAll(c => c.text));
            combinedDialogue.AddRange(secondHalfDialogue.Calls.ConvertAll(c => c.text));

            CollectionAssert.AreEqual(baselineDialogue.Calls.ConvertAll(c => c.text), combinedDialogue);
            Assert.AreEqual(baselineContext.Variables.GetInt("score"), secondHalfContext.Variables.GetInt("score"));
            Assert.IsTrue(secondHalfController.IsFinished);
        }
    }
}
