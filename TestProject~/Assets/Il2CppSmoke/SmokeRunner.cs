using System;
using System.Collections.Generic;
using System.IO;
using NovelForge.Runtime;
using UnityEngine;

namespace NovelForgeSmoke
{
    // Runs inside the IL2CPP player built by BuildSmoke: compiles a script that uses a
    // reflection-only custom command and round-trips a save through Newtonsoft, then quits
    // with exit code 0 (pass) or 1 (fail). The verdict line is "NOVELFORGE_SMOKE ...".
    public class SmokeRunner : MonoBehaviour
    {
        private const string SlotId = "il2cpp-smoke";

        private void Start()
        {
            var failures = new List<string>();
            try
            {
                CheckCustomCommand(failures);
                CheckSaveLoadRoundTrip(failures);
            }
            catch (Exception e)
            {
                failures.Add(e.ToString());
            }

            Debug.Log(failures.Count == 0
                ? "NOVELFORGE_SMOKE PASS"
                : "NOVELFORGE_SMOKE FAIL: " + string.Join(" | ", failures));
            Application.Quit(failures.Count == 0 ? 0 : 1);
        }

        private static void CheckCustomCommand(List<string> failures)
        {
            NovelScript script = new ScriptCompiler().Compile("label start\nsmoke_echo 42\n");
            if (!(script.Commands[0] is SmokeEchoCommand))
                failures.Add($"custom command compiled to {script.Commands[0]?.GetType().FullName ?? "null"}");
            if (SmokeEchoCommand.LastArgs != "42")
                failures.Add($"custom command got args '{SmokeEchoCommand.LastArgs}'");
        }

        private static void CheckSaveLoadRoundTrip(List<string> failures)
        {
            NovelScript script = new ScriptCompiler().Compile("label start\nAlice: one\nAlice: two\nAlice: three\n");
            var storage = new JsonSaveStorage();

            var context = new StoryContext();
            context.Variables.Set("score", 3);
            context.Scene.Background = "room";
            context.Scene.Actors["left"] = new ActorState { CharacterId = "Alice", Emotion = "happy" };
            var playback = new PlaybackController(script, context);
            playback.RestoreSnapshot(new PlaybackSnapshot { PointerIndex = 2, CallStack = new[] { 1 } });
            new SaveLoadController(playback, context, "smoke", storage).SaveTo(SlotId);

            var loadedContext = new StoryContext();
            var loadedPlayback = new PlaybackController(script, loadedContext);
            SaveLoadResult result = new SaveLoadController(loadedPlayback, loadedContext, "smoke", storage).LoadInto(SlotId);
            File.Delete(Path.Combine(Application.persistentDataPath, SlotId + ".json"));

            if (result.Status != SaveLoadStatus.Success)
            {
                failures.Add($"load status {result.Status}");
                return;
            }
            if (loadedContext.Variables.GetInt("score") != 3)
                failures.Add("variable not restored");
            if (loadedContext.Scene.Background != "room")
                failures.Add("scene background not restored");
            if (!loadedContext.Scene.Actors.TryGetValue("left", out ActorState actor) || actor.Emotion != "happy")
                failures.Add("scene actor not restored");
            if (loadedPlayback.CurrentIndex != 2)
                failures.Add($"pointer restored to {loadedPlayback.CurrentIndex}");
        }
    }
}
