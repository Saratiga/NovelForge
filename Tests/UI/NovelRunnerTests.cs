using System.Collections;
using NUnit.Framework;
using NovelForge.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace NovelForge.UI.Tests
{
    public class NovelRunnerTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                Object.DestroyImmediate(_go);
        }

        private NovelRunner CreateRunner()
        {
            _go = new GameObject("NovelRunner");
            var runner = _go.AddComponent<NovelRunner>();
            runner.dialogue = _go.AddComponent<DialoguePresenter>();
            runner.choices = _go.AddComponent<ChoiceView>();
            runner.audio = _go.AddComponent<AudioPresenter>();
            runner.backgrounds = _go.AddComponent<BackgroundPresenter>();
            return runner;
        }

        private static NovelScriptAsset CreateScriptAsset(string source)
        {
            var asset = ScriptableObject.CreateInstance<NovelScriptAsset>();
            asset.Source = source;
            return asset;
        }

        // Unity's real coroutine engine auto-drives a yielded IEnumerator to completion
        // before resuming its parent; a plain "while (routine.MoveNext())" does not, so
        // nested yields (e.g. RunAndNotify's "yield return Playback.RunAll()") must be
        // flattened by hand here to match production behavior.
        private static void Pump(IEnumerator routine)
        {
            while (routine.MoveNext())
            {
                if (routine.Current is IEnumerator nested)
                    Pump(nested);
            }
        }

        [Test]
        public void Prepare_WiresAssignedPresentersIntoContext()
        {
            NovelRunner runner = CreateRunner();
            runner.script = CreateScriptAsset("label start\nreturn\n");

            runner.Prepare();

            Assert.AreSame(runner.dialogue, runner.Context.Dialogue);
            Assert.AreSame(runner.choices, runner.Context.Choices);
            Assert.AreSame(runner.audio, runner.Context.Audio);
            Assert.AreSame(runner.backgrounds, runner.Context.Backgrounds);
            Assert.AreSame(runner, runner.Context.Timing);
        }

        [Test]
        public void RunAndNotify_ScriptRunsToCompletion_RaisesOnFinishedExactlyOnce()
        {
            NovelRunner runner = CreateRunner();
            runner.script = CreateScriptAsset("label start\nreturn\n");
            runner.Prepare();
            int finishedCount = 0;
            runner.OnFinished += () => finishedCount++;

            // A top-level script ends via "return" with an empty call stack — ReturnCommand
            // logs this at Error severity by design (see Runtime/Commands/Builtin/ReturnCommand.cs).
            LogAssert.Expect(LogType.Error, "NovelForge: 'return' with an empty call stack — ending playback.");
            Pump(runner.RunAndNotify());

            Assert.IsTrue(runner.Playback.IsFinished);
            Assert.AreEqual(1, finishedCount);
        }

        [Test]
        public void RunAndNotify_ReplaysSceneBeforeFirstCommand()
        {
            NovelRunner runner = CreateRunner();
            runner.script = CreateScriptAsset("label a\nAlice: hi\n");
            runner.Prepare();
            var recorder = new NovelForge.Runtime.Tests.OrderRecordingPresenter();
            runner.Context.Backgrounds = recorder;
            runner.Context.Dialogue = recorder;
            runner.Context.Scene.Background = "room";

            Pump(runner.RunAndNotify());

            CollectionAssert.AreEqual(new[] { "bg:room", "line:Alice:hi" }, recorder.Log);
        }

        [Test]
        public void Play_MissingScript_LogsError_DoesNotThrow_PlaybackStaysNull()
        {
            NovelRunner runner = CreateRunner();

            LogAssert.Expect(LogType.Error, "NovelForge: NovelRunner has no script assigned — call Prepare with a NovelScriptAsset.");
            Assert.DoesNotThrow(() => runner.Play());
            Assert.IsNull(runner.Playback);
        }

        [Test]
        public void Prepare_InvalidScript_LogsError_DoesNotThrow_PlaybackStaysNull()
        {
            NovelRunner runner = CreateRunner();
            runner.script = CreateScriptAsset("jump nowhere\n");

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("NovelForge: could not compile script"));
            Assert.DoesNotThrow(() => runner.Prepare());
            Assert.IsNull(runner.Playback);
        }
    }
}
