# README/USAGE and Samples~ Demo Scene Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the last MVP gap from the master spec — a general-purpose `NovelRunner`
runtime component, a working `Samples~/GettingStarted` demo scene exercising every
subsystem, and `README.md`/`USAGE.md` at the package root.

**Architecture:** `NovelRunner` (new `UI/NovelRunner.cs`) is a thin MonoBehaviour that
compiles a `NovelScriptAsset`, wires a `StoryContext` from inspector-assigned presenters,
implements `ITimingPresenter` itself, and drives `PlaybackController` via a coroutine.
Everything save/load- and navigation-related is demo-only (`DemoFlow.cs`, lives only in
`Samples~`). The demo is built once under a normal (non-tilde) staging folder inside
`TestProject~/Assets` so Unity's AssetDatabase can process Sprite/AudioClip imports and
Inspector wiring, verified live in Play Mode, then moved wholesale into `Samples~/GettingStarted`
(tilde folders are invisible to AssetDatabase, so authoring must happen outside one first).

**Tech Stack:** Unity 6000.6, UGUI, TextMeshPro, NUnit EditMode tests, C# Editor
batch-mode scripts (`-executeMethod`) for scriptable asset/scene generation.

**Spec:** `docs/superpowers/specs/2026-09-14-samples-and-docs-design.md`

## Global Constraints

- Unity version: 6000.6 (per `package.json`).
- No new third-party dependencies.
- `git add` whole folders (`Runtime/`, `UI/`, `Tests/`, `Samples~/`, `Editor/`), never
  individual `.cs` files — Unity-generated `.meta` companions must always be staged
  alongside their file. Verify with `git status --porcelain` before every commit.
- `internal` fields/methods that a different assembly's tests must reach rely on existing
  `InternalsVisibleTo` grants (`Runtime/AssemblyInfo.cs` → `NovelForge.Runtime.Tests`,
  `NovelForge.UI.Tests`; `UI/AssemblyInfo.cs` → `NovelForge.UI.Tests`). Do not add new
  `InternalsVisibleTo` grants for throwaway generator scripts — use `SerializedObject` to
  populate `internal` fields from a generic (non-package) Editor assembly instead, the
  same technique already used in `Editor/CharacterEditorWindow.cs`.
- "Missing wiring" errors always use `Debug.LogError` and degrade gracefully (skip, don't
  throw) — the established pattern in every presenter and command in this codebase.
- Unity batch-mode is launched via PowerShell's `Start-Process` (the Bash tool refuses
  direct `Unity.exe` invocation while worktree-isolated). Unity path:
  `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe`. Test project path:
  `<worktree root>\TestProject~`.
- `TestProject~/Assets` is tracked by git (not gitignored) — the staging folder created in
  Task 2/3 is a normal tracked path until Task 4 moves it out.

---

### Task 1: `NovelRunner` runtime component

**Files:**
- Create: `UI/NovelRunner.cs`
- Test: `Tests/UI/NovelRunnerTests.cs`

**Interfaces:**
- Consumes: `NovelForge.Runtime.NovelScriptAsset` (`public string Source`),
  `ScriptCompiler().Compile(string) : NovelScript`, `StoryContext` (settable properties
  `Dialogue`/`Choices`/`Audio`/`Backgrounds`/`Timing`/`Localization`), `PlaybackController`
  (`CreateSnapshot`/`RestoreSnapshot`/`RunAll(): IEnumerator`/`IsFinished`), `ITimingPresenter`
  (`IEnumerator Wait(float)`), `UI.DialoguePresenter`, `UI.ChoiceView`,
  `Runtime.AudioPresenter`, `Runtime.BackgroundPresenter` (all already implement their
  respective presenter interfaces), `Runtime.LocalizationTable.FromJson(string)`.
- Produces: `NovelRunner.Prepare(NovelScriptAsset = null)`, `.Begin()`, `.Play(NovelScriptAsset = null)`,
  `.Stop()`, `.Context` (get), `.Playback` (get), `.OnFinished` (event `Action`),
  `internal IEnumerator RunAndNotify()` (the coroutine body `Begin()` starts — exposed
  `internal` so EditMode tests can pump it with `.MoveNext()` without needing Play Mode,
  the same reason `Tests/UI/ChoiceViewTests.cs` pumps `IEnumerator`s returned by
  `ChoiceView.PresentChoices` directly instead of going through Unity's coroutine
  scheduler). Later tasks (`DemoFlow.cs`) consume `Prepare`/`Begin`/`Play`/`Context`/
  `Playback`/`OnFinished` — not `RunAndNotify`, which is test-only surface.

- [ ] **Step 1: Write the failing tests**

```csharp
// Tests/UI/NovelRunnerTests.cs
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

        private static void Pump(IEnumerator routine)
        {
            while (routine.MoveNext()) { }
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

            Pump(runner.RunAndNotify());

            Assert.IsTrue(runner.Playback.IsFinished);
            Assert.AreEqual(1, finishedCount);
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
```

- [ ] **Step 2: Run tests to verify they fail**

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe"
$project = "<worktree root>\TestProject~"
$resultsFile = "<worktree root>\TestProject~\TestResults.xml"
if (Test-Path $resultsFile) { Remove-Item $resultsFile }
Start-Process -FilePath $unity -ArgumentList "-batchmode","-projectPath",$project,"-runTests","-testPlatform","EditMode","-testResults",$resultsFile,"-logFile","<worktree root>\TestProject~\test_run.log" -Wait -NoNewWindow
```

Expected: compile error (`NovelRunner` does not exist) or test failures — `Tests/UI/NovelRunnerTests.cs` references a type that doesn't exist yet.

- [ ] **Step 3: Implement `NovelRunner`**

```csharp
// UI/NovelRunner.cs
using System;
using System.Collections;
using NovelForge.Runtime;
using UnityEngine;

namespace NovelForge.UI
{
    public class NovelRunner : MonoBehaviour, ITimingPresenter
    {
        [SerializeField] internal NovelScriptAsset script;
        [SerializeField] internal DialoguePresenter dialogue;
        [SerializeField] internal ChoiceView choices;
        [SerializeField] internal AudioPresenter audio;
        [SerializeField] internal BackgroundPresenter backgrounds;
        [SerializeField] internal TextAsset localizationJson;

        public StoryContext Context { get; private set; }
        public PlaybackController Playback { get; private set; }
        public event Action OnFinished;

        public void Prepare(NovelScriptAsset scriptToPlay = null)
        {
            Context = null;
            Playback = null;

            NovelScriptAsset target = scriptToPlay != null ? scriptToPlay : script;
            if (target == null)
            {
                Debug.LogError("NovelForge: NovelRunner has no script assigned — call Prepare with a NovelScriptAsset.");
                return;
            }

            NovelScript compiled;
            try
            {
                compiled = new ScriptCompiler().Compile(target.Source);
            }
            catch (ParseException e)
            {
                Debug.LogError($"NovelForge: could not compile script '{target.name}' — {e.Message}");
                return;
            }

            Context = new StoryContext
            {
                Dialogue = dialogue,
                Choices = choices,
                Audio = audio,
                Backgrounds = backgrounds,
                Timing = this,
                Localization = localizationJson != null ? LocalizationTable.FromJson(localizationJson.text) : null,
            };
            Playback = new PlaybackController(compiled, Context);
        }

        public void Begin()
        {
            if (Playback == null)
            {
                Debug.LogError("NovelForge: NovelRunner.Begin() called before a successful Prepare().");
                return;
            }
            StartCoroutine(RunAndNotify());
        }

        public void Play(NovelScriptAsset scriptToPlay = null)
        {
            Prepare(scriptToPlay);
            if (Playback != null)
                Begin();
        }

        public void Stop() => StopAllCoroutines();

        internal IEnumerator RunAndNotify()
        {
            yield return Playback.RunAll();
            OnFinished?.Invoke();
        }

        public IEnumerator Wait(float seconds)
        {
            yield return new WaitForSeconds(seconds);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Re-run the same batch-mode command as Step 2.
Expected: `TestResults.xml` shows all `NovelRunnerTests` passing, and the rest of the
suite (233 tests: 229 existing + 4 new) still green:

```powershell
Select-String -Path $resultsFile -Pattern 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*"' | Select-Object -First 1
```

- [ ] **Step 5: Commit**

```bash
git add UI/ Tests/UI/
git status --porcelain  # confirm .meta files for both new .cs files are staged
git commit -m "Add NovelRunner: general-purpose playback+presenter wiring component"
```

---

### Task 2: Demo script, localization, and generated content assets

**Files:**
- Create: `TestProject~/Assets/GettingStartedStaging/Story.nfscript` (plain text — written
  directly, not generated)
- Create: `TestProject~/Assets/GettingStartedStaging/Localization/ru.json` (plain text)
- Create (temporary, deleted at end of task): `TestProject~/Assets/Editor/DemoAssetGenerator.cs`
- Generated by the script above: `TestProject~/Assets/GettingStartedStaging/Art/*.png`
  (+ auto `.meta`), `.../Audio/*.wav` (+ auto `.meta`),
  `.../CharacterLibrary.asset`, `.../AliceDefinition.asset`, `.../BobDefinition.asset`,
  `.../BackgroundLibrary.asset`, `.../AudioLibrary.asset`

**Interfaces:**
- Consumes: `CharacterDefinition` (internal `id`/`displayName`/`nameColor`/`poses[]`,
  `Pose{emotion,sprite}`), `CharacterLibrary` (internal `characters[]`), `BackgroundLibrary`
  (internal `backgrounds[]`/`cgs[]`, `Entry{id,sprite}`), `AudioLibrary` (internal
  `musicTracks[]`/`sfxClips[]`, `Entry{id,clip}`) — all populated via `SerializedObject`
  since the generator lives in the default `Assembly-CSharp-Editor` assembly, which has no
  `InternalsVisibleTo` grant into `NovelForge.Runtime`.
- Produces: asset files Task 3's scene-builder script assigns into `DialoguePresenter.library`,
  `BackgroundPresenter.library`, `AudioPresenter.library`, `NovelRunner.script`.

- [ ] **Step 1: Write `Story.nfscript`**

```
// Samples~/GettingStarted/Story.nfscript
label start
bg classroom
music theme
alice: Hi! I'm Alice. #happy @greet1 left
bob: And I'm Bob. #neutral @greet2 right
set trust = 0
alice: Would you like to hear a joke?
choice
"Sure!" @yes -> joke
"No thanks" @no -> skip_joke

label joke
gosub tell_joke
set trust += 1
jump after_joke

label skip_joke
bob: Suit yourself. #neutral right

label after_joke
if trust >= 1
    alice: Thanks for listening! #happy left
else
    alice: Okay then. #neutral left
endif
cg sunset
wait 0.5
alice: Let's see what's outside.
sfx door_open
jump ending

label tell_joke
bob: Why did the developer quit? #laughing right
bob: Because they didn't get arrays! #laughing right
return

label ending
alice: That's the end of this demo. #happy left
return
```

Save to `TestProject~/Assets/GettingStartedStaging/Story.nfscript` — Unity's
`NovelScriptImporter` (`[ScriptedImporter(1, "nfscript")]`) imports it automatically into a
`NovelScriptAsset` on the next `AssetDatabase.Refresh()`.

- [ ] **Step 2: Verify the script compiles**

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe"
$project = "<worktree root>\TestProject~"
Start-Process -FilePath $unity -ArgumentList "-batchmode","-projectPath",$project,"-quit","-logFile","<worktree root>\TestProject~\import.log" -Wait -NoNewWindow
Select-String -Path "<worktree root>\TestProject~\import.log" -Pattern "NovelForge:.*Story" -SimpleMatch:$false
```

Expected: no `NovelForge:` import error lines mentioning `Story`. (A `ParseException` in
`NovelScriptImporter.OnImportAsset` would show as `ctx.LogImportError` output here.)

- [ ] **Step 3: Write `Localization/ru.json`**

```json
{
  "greet1": "Привет! Я Алиса.",
  "greet2": "А я Боб.",
  "yes": "Конечно!",
  "no": "Нет, спасибо"
}
```

Save to `TestProject~/Assets/GettingStartedStaging/Localization/ru.json`. Deliberately
partial — only 4 of the script's ~13 localization ids are covered, to demonstrate both the
translated path and the graceful `Debug.LogWarning` fallback path (documented in `USAGE.md`,
Task 5). This file is **not** wired into the main demo's `NovelRunner` (Task 3) — a fully
localized default playthrough would spam warnings for every untranslated line; `USAGE.md`
instead tells the reader to assign it manually to see localization in action.

- [ ] **Step 4: Write the asset generator script**

```csharp
// TestProject~/Assets/Editor/DemoAssetGenerator.cs — temporary, deleted at the end of this task
using System.IO;
using System.Text;
using NovelForge.Runtime;
using UnityEditor;
using UnityEngine;

public static class DemoAssetGenerator
{
    private const string Root = "Assets/GettingStartedStaging";

    [MenuItem("NovelForge/Internal/Generate Demo Assets")]
    public static void Generate()
    {
        Directory.CreateDirectory($"{Root}/Art");
        Directory.CreateDirectory($"{Root}/Audio");

        Sprite aliceHappy = CreateSolidSprite($"{Root}/Art/AliceHappy.png", new Color32(255, 182, 217, 255));
        Sprite aliceNeutral = CreateSolidSprite($"{Root}/Art/AliceNeutral.png", new Color32(217, 179, 255, 255));
        Sprite bobNeutral = CreateSolidSprite($"{Root}/Art/BobNeutral.png", new Color32(179, 217, 255, 255));
        Sprite bobLaughing = CreateSolidSprite($"{Root}/Art/BobLaughing.png", new Color32(255, 243, 179, 255));
        Sprite classroomBg = CreateSolidSprite($"{Root}/Art/BgClassroom.png", new Color32(200, 230, 201, 255));
        Sprite sunsetCg = CreateSolidSprite($"{Root}/Art/CgSunset.png", new Color32(255, 171, 112, 255));

        AudioClip theme = CreateToneClip($"{Root}/Audio/Theme.wav", 440f, 1.5f);
        AudioClip doorOpen = CreateToneClip($"{Root}/Audio/DoorOpen.wav", 880f, 0.6f);

        CreateCharacterDefinition($"{Root}/AliceDefinition.asset", "alice", "Alice", new Color(1f, 0.3f, 0.6f),
            ("happy", aliceHappy), ("neutral", aliceNeutral));
        CreateCharacterDefinition($"{Root}/BobDefinition.asset", "bob", "Bob", new Color(0.3f, 0.5f, 1f),
            ("neutral", bobNeutral), ("laughing", bobLaughing));

        var alice = AssetDatabase.LoadAssetAtPath<CharacterDefinition>($"{Root}/AliceDefinition.asset");
        var bob = AssetDatabase.LoadAssetAtPath<CharacterDefinition>($"{Root}/BobDefinition.asset");
        CreateCharacterLibrary($"{Root}/CharacterLibrary.asset", alice, bob);

        CreateBackgroundLibrary($"{Root}/BackgroundLibrary.asset",
            backgrounds: new[] { ("classroom", classroomBg) },
            cgs: new[] { ("sunset", sunsetCg) });

        CreateAudioLibrary($"{Root}/AudioLibrary.asset",
            music: new[] { ("theme", theme) },
            sfx: new[] { ("door_open", doorOpen) });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("NovelForge: demo assets generated.");
    }

    private static Sprite CreateSolidSprite(string path, Color32 color)
    {
        var tex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        var pixels = new Color32[128 * 128];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = color;
        tex.SetPixels32(pixels);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = 128;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static AudioClip CreateToneClip(string path, float frequency, float duration)
    {
        const int sampleRate = 44100;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        var samples = new short[sampleCount];
        int fadeSamples = Mathf.Min(sampleCount / 2, sampleRate / 50); // 20ms fade in/out, clamped for very short clips
        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = fadeSamples > 0 ? Mathf.Clamp01(Mathf.Min(i, sampleCount - i) / (float)fadeSamples) : 1f;
            samples[i] = (short)(Mathf.Sin(2f * Mathf.PI * frequency * t) * short.MaxValue * 0.5f * envelope);
        }
        WriteWav(path, samples, sampleRate);
        AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    private static void WriteWav(string path, short[] samples, int sampleRate)
    {
        using var stream = new FileStream(path, FileMode.Create);
        using var writer = new BinaryWriter(stream);
        int byteRate = sampleRate * 2;
        int dataSize = samples.Length * 2;
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);
        foreach (short s in samples)
            writer.Write(s);
    }

    private static void CreateCharacterDefinition(string path, string id, string displayName, Color nameColor, params (string emotion, Sprite sprite)[] poses)
    {
        var instance = ScriptableObject.CreateInstance<CharacterDefinition>();
        AssetDatabase.CreateAsset(instance, path);
        var so = new SerializedObject(instance);
        so.FindProperty("id").stringValue = id;
        so.FindProperty("displayName").stringValue = displayName;
        so.FindProperty("nameColor").colorValue = nameColor;
        var posesProp = so.FindProperty("poses");
        posesProp.arraySize = poses.Length;
        for (int i = 0; i < poses.Length; i++)
        {
            var element = posesProp.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("emotion").stringValue = poses[i].emotion;
            element.FindPropertyRelative("sprite").objectReferenceValue = poses[i].sprite;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateCharacterLibrary(string path, params Object[] characters)
    {
        var instance = ScriptableObject.CreateInstance<CharacterLibrary>();
        AssetDatabase.CreateAsset(instance, path);
        var so = new SerializedObject(instance);
        var prop = so.FindProperty("characters");
        prop.arraySize = characters.Length;
        for (int i = 0; i < characters.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = characters[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateBackgroundLibrary(string path, (string id, Sprite sprite)[] backgrounds, (string id, Sprite sprite)[] cgs)
    {
        var instance = ScriptableObject.CreateInstance<BackgroundLibrary>();
        AssetDatabase.CreateAsset(instance, path);
        var so = new SerializedObject(instance);
        FillEntryArray(so.FindProperty("backgrounds"), backgrounds);
        FillEntryArray(so.FindProperty("cgs"), cgs);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateAudioLibrary(string path, (string id, AudioClip clip)[] music, (string id, AudioClip clip)[] sfx)
    {
        var instance = ScriptableObject.CreateInstance<AudioLibrary>();
        AssetDatabase.CreateAsset(instance, path);
        var so = new SerializedObject(instance);
        FillEntryArray(so.FindProperty("musicTracks"), music);
        FillEntryArray(so.FindProperty("sfxClips"), sfx);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void FillEntryArray(SerializedProperty arrayProp, (string id, Sprite sprite)[] entries)
    {
        arrayProp.arraySize = entries.Length;
        for (int i = 0; i < entries.Length; i++)
        {
            var element = arrayProp.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("id").stringValue = entries[i].id;
            element.FindPropertyRelative("sprite").objectReferenceValue = entries[i].sprite;
        }
    }

    private static void FillEntryArray(SerializedProperty arrayProp, (string id, AudioClip clip)[] entries)
    {
        arrayProp.arraySize = entries.Length;
        for (int i = 0; i < entries.Length; i++)
        {
            var element = arrayProp.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("id").stringValue = entries[i].id;
            element.FindPropertyRelative("clip").objectReferenceValue = entries[i].clip;
        }
    }
}
```

Note: `CharacterDefinition`/`CharacterLibrary`/`BackgroundLibrary`/`AudioLibrary` are
`public` classes — this generic-editor-assembly script can `using NovelForge.Runtime;` and
construct them directly with `ScriptableObject.CreateInstance<T>()` same as any other code.
Only their *fields* (`id`, `poses`, `characters`, etc.) are `internal`, which is why every
value on them is set through `SerializedObject`/`SerializedProperty` instead of plain
field assignment — the same technique `Editor/CharacterEditorWindow.cs` already uses to
touch these same fields from the (also separate) `NovelForge.Editor` assembly.

- [ ] **Step 5: Run the generator in batch mode**

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe"
$project = "<worktree root>\TestProject~"
Start-Process -FilePath $unity -ArgumentList "-batchmode","-projectPath",$project,"-executeMethod","DemoAssetGenerator.Generate","-quit","-logFile","<worktree root>\TestProject~\generate.log" -Wait -NoNewWindow
Select-String -Path "<worktree root>\TestProject~\generate.log" -Pattern "demo assets generated|Exception|Error"
```

Expected: `NovelForge: demo assets generated.` present, no `Exception`/`Error` lines.

- [ ] **Step 6: Verify the generated assets on disk**

```powershell
Get-ChildItem "<worktree root>\TestProject~\Assets\GettingStartedStaging" -Recurse -File | Select-Object -ExpandProperty Name
```

Expected: 6 `.png`, 2 `.wav`, 2 `CharacterDefinition`-derived `.asset` files
(`AliceDefinition.asset`, `BobDefinition.asset`), `CharacterLibrary.asset`,
`BackgroundLibrary.asset`, `AudioLibrary.asset`, `Story.nfscript`,
`Localization/ru.json` — each with a matching `.meta`.

- [ ] **Step 7: Delete the temporary generator script**

```bash
rm TestProject~/Assets/Editor/DemoAssetGenerator.cs TestProject~/Assets/Editor/DemoAssetGenerator.cs.meta
rmdir TestProject~/Assets/Editor 2>/dev/null || true  # only if now empty
```

- [ ] **Step 8: Commit**

```bash
git add TestProject~/Assets/GettingStartedStaging/
git status --porcelain  # confirm every generated asset has its .meta staged, and DemoAssetGenerator.cs is NOT present
git commit -m "Generate demo script, localization file, and placeholder art/audio assets"
```

---

### Task 3: Demo scene, `DemoFlow.cs`, and live Play Mode QA

**Files:**
- Create: `TestProject~/Assets/GettingStartedStaging/DemoFlow.cs`
- Create (temporary, deleted at end of task): `TestProject~/Assets/Editor/DemoSceneBuilder.cs`
- Generated by the script above: `TestProject~/Assets/GettingStartedStaging/GettingStarted.unity`

**Interfaces:**
- Consumes: `NovelRunner` (Task 1: `script`/`dialogue`/`choices`/`audio`/`backgrounds`
  fields, `Prepare()`/`Begin()`/`Context`/`Playback`/`OnFinished`), `SaveLoadController`
  (`SaveTo`/`LoadInto`/`SlotExists`), `JsonSaveStorage` (public parameterless ctor →
  `Application.persistentDataPath`), `SaveLoadView` (`SlotUI{slotId,button,label}`,
  `statusText`, `Initialize`, `ShowSaveMode`/`ShowLoadMode`, `OnSaveCompleted`/
  `OnLoadSucceeded` events), `DialoguePresenter` (`library`, `dialogueBox`,
  `positionSlots[]` — `PositionSlot{position,view}`), `ChoiceView` (`optionButtons[]`),
  `AudioPresenter` (`library`, `musicSourceA`/`musicSourceB`), `BackgroundPresenter`
  (`library`, `backgroundSlotA`/`backgroundSlotB`/`cgSlot`), `DialogueBoxView`
  (`nameText`/`bodyText`/`advanceButton`), `ActorView` (`spriteRenderer`).
- Produces: `GettingStarted.unity` — the finished demo scene, later moved into
  `Samples~/GettingStarted/` by Task 4.

- [ ] **Step 1: Write `DemoFlow.cs`**

```csharp
// Samples~/GettingStarted/DemoFlow.cs — demo-only, not part of the package's public API
using System.Linq;
using NovelForge.Runtime;
using NovelForge.UI;
using UnityEngine;
using UnityEngine.UI;

public class DemoFlow : MonoBehaviour
{
    private const string ScriptId = "getting-started";

    [SerializeField] private NovelRunner runner;
    [SerializeField] private NovelScriptAsset story;
    [SerializeField] private GameObject titleScreen;
    [SerializeField] private GameObject gameplayUi;
    [SerializeField] private GameObject saveLoadPanel;
    [SerializeField] private SaveLoadView saveLoadView;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button loadGameButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button closeSaveLoadButton;
    // Must match the slot ids DemoSceneBuilder assigns to saveLoadView.slots — used only to
    // decide whether the title screen's "Load Game" button should be enabled; SaveLoadView
    // itself is the source of truth for which of these slots (if any) is actually occupied.
    [SerializeField] private string[] slotIds = { "slot-0", "slot-1" };

    private JsonSaveStorage _storage;
    private SaveLoadController _saveLoad;

    private void Awake()
    {
        newGameButton.onClick.AddListener(OnNewGame);
        loadGameButton.onClick.AddListener(OnOpenLoad);
        saveButton.onClick.AddListener(OnOpenSave);
        closeSaveLoadButton.onClick.AddListener(() => saveLoadPanel.SetActive(false));
        saveLoadView.OnSaveCompleted += () => saveLoadPanel.SetActive(false);
        saveLoadView.OnLoadSucceeded += OnLoadSucceeded;
        runner.OnFinished += ShowTitle;
    }

    private void Start()
    {
        _storage = new JsonSaveStorage();
        ShowTitle();
    }

    private void ShowTitle()
    {
        titleScreen.SetActive(true);
        gameplayUi.SetActive(false);
        saveLoadPanel.SetActive(false);
        loadGameButton.interactable = slotIds.Any(_storage.SlotExists);
    }

    private void OnNewGame()
    {
        titleScreen.SetActive(false);
        gameplayUi.SetActive(true);
        runner.Prepare(story);
        _saveLoad = new SaveLoadController(runner.Playback, runner.Context, ScriptId, _storage);
        saveLoadView.Initialize(_saveLoad);
        runner.Begin();
    }

    private void OnOpenLoad()
    {
        runner.Prepare(story);
        _saveLoad = new SaveLoadController(runner.Playback, runner.Context, ScriptId, _storage);
        saveLoadView.Initialize(_saveLoad);
        saveLoadPanel.SetActive(true);
        saveLoadView.ShowLoadMode();
    }

    private void OnOpenSave()
    {
        saveLoadPanel.SetActive(true);
        saveLoadView.ShowSaveMode();
    }

    private void OnLoadSucceeded()
    {
        saveLoadPanel.SetActive(false);
        titleScreen.SetActive(false);
        gameplayUi.SetActive(true);
        runner.Begin();
    }
}
```

- [ ] **Step 2: Write the scene-builder script**

```csharp
// TestProject~/Assets/Editor/DemoSceneBuilder.cs — temporary, deleted at the end of this task
using NovelForge.Runtime;
using NovelForge.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class DemoSceneBuilder
{
    private const string Root = "Assets/GettingStartedStaging";

    [MenuItem("NovelForge/Internal/Build Demo Scene")]
    public static void Build()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.transform.position = new Vector3(0f, 0f, -10f);
        camera.gameObject.tag = "MainCamera";
        new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));

        // World-space visuals (backgrounds/CG/actors), sorted by SpriteRenderer.sortingOrder.
        var backgroundSlotA = CreateSpriteRenderer("BackgroundSlotA", 0);
        var backgroundSlotB = CreateSpriteRenderer("BackgroundSlotB", 0);
        var cgSlot = CreateSpriteRenderer("CgSlot", 2);
        var aliceView = CreateSpriteRenderer("AliceActor", 1);
        aliceView.transform.position = new Vector3(-2f, 0f, 0f);
        var bobView = CreateSpriteRenderer("BobActor", 1);
        bobView.transform.position = new Vector3(2f, 0f, 0f);

        var backgroundPresenterGo = new GameObject("BackgroundPresenter");
        var backgroundPresenter = backgroundPresenterGo.AddComponent<BackgroundPresenter>();
        var backgroundLibrary = AssetDatabase.LoadAssetAtPath<ScriptableObject>($"{Root}/BackgroundLibrary.asset");
        SetField(backgroundPresenter, "library", backgroundLibrary);
        SetField(backgroundPresenter, "backgroundSlotA", backgroundSlotA);
        SetField(backgroundPresenter, "backgroundSlotB", backgroundSlotB);
        SetField(backgroundPresenter, "cgSlot", cgSlot);

        var audioPresenterGo = new GameObject("AudioPresenter");
        var audioPresenter = audioPresenterGo.AddComponent<AudioPresenter>();
        var audioLibrary = AssetDatabase.LoadAssetAtPath<ScriptableObject>($"{Root}/AudioLibrary.asset");
        var musicA = audioPresenterGo.AddComponent<AudioSource>();
        musicA.playOnAwake = false;
        var musicB = audioPresenterGo.AddComponent<AudioSource>();
        musicB.playOnAwake = false;
        SetField(audioPresenter, "library", audioLibrary);
        SetField(audioPresenter, "musicSourceA", musicA);
        SetField(audioPresenter, "musicSourceB", musicB);

        var aliceActorView = aliceView.gameObject.AddComponent<ActorView>();
        SetField(aliceActorView, "spriteRenderer", aliceView);
        var bobActorView = bobView.gameObject.AddComponent<ActorView>();
        SetField(bobActorView, "spriteRenderer", bobView);

        // GameplayUI canvas: dialogue box, choice buttons, Save corner button.
        Canvas gameplayCanvas = CreateCanvas("GameplayUI");
        GameObject dialogueBoxGo = new GameObject("DialogueBox", typeof(RectTransform), typeof(Image));
        dialogueBoxGo.transform.SetParent(gameplayCanvas.transform, false);
        AnchorBottomStretch(dialogueBoxGo.GetComponent<RectTransform>(), height: 180f);
        var nameText = CreateTmpText(dialogueBoxGo.transform, "NameText", new Vector2(20, -10), 24);
        var bodyText = CreateTmpText(dialogueBoxGo.transform, "BodyText", new Vector2(20, -50), 20);
        Button advanceButton = dialogueBoxGo.AddComponent<Button>();
        var dialogueBoxView = dialogueBoxGo.AddComponent<DialogueBoxView>();
        SetField(dialogueBoxView, "nameText", nameText);
        SetField(dialogueBoxView, "bodyText", bodyText);
        SetField(dialogueBoxView, "advanceButton", advanceButton);

        var dialoguePresenterGo = new GameObject("DialoguePresenter");
        var dialoguePresenter = dialoguePresenterGo.AddComponent<DialoguePresenter>();
        var characterLibrary = AssetDatabase.LoadAssetAtPath<ScriptableObject>($"{Root}/CharacterLibrary.asset");
        SetField(dialoguePresenter, "library", characterLibrary);
        SetField(dialoguePresenter, "dialogueBox", dialogueBoxView);
        SetPositionSlots(dialoguePresenter, new (string position, ActorView view)[]
        {
            ("left", aliceActorView),
            ("right", bobActorView),
        });

        GameObject choicePanel = new GameObject("ChoicePanel", typeof(RectTransform));
        choicePanel.transform.SetParent(gameplayCanvas.transform, false);
        AnchorCenter(choicePanel.GetComponent<RectTransform>(), new Vector2(400, 120));
        Button option0 = CreateChoiceButton(choicePanel.transform, "Option0", new Vector2(0, 30));
        Button option1 = CreateChoiceButton(choicePanel.transform, "Option1", new Vector2(0, -30));
        var choiceView = dialoguePresenterGo.AddComponent<ChoiceView>();
        SetField(choiceView, "optionButtons", new[] { option0, option1 });

        Button saveButton = CreateCornerButton(gameplayCanvas.transform, "SaveButton", "Save");

        // TitleScreen canvas: New Game / Load Game.
        Canvas titleCanvas = CreateCanvas("TitleScreen");
        Button newGameButton = CreateMenuButton(titleCanvas.transform, "NewGameButton", "New Game", new Vector2(0, 20));
        Button loadGameButton = CreateMenuButton(titleCanvas.transform, "LoadGameButton", "Load Game", new Vector2(0, -30));

        // Save/Load overlay canvas: reuses SaveLoadView across both Save and Load modes.
        Canvas saveLoadCanvas = CreateCanvas("SaveLoadPanel");
        var statusText = CreateTmpText(saveLoadCanvas.transform, "StatusText", new Vector2(0, 80), 18);
        Button slot0 = CreateChoiceButton(saveLoadCanvas.transform, "Slot0", new Vector2(0, 30));
        Button slot1 = CreateChoiceButton(saveLoadCanvas.transform, "Slot1", new Vector2(0, -10));
        Button closeButton = CreateChoiceButton(saveLoadCanvas.transform, "CloseButton", new Vector2(0, -60));
        closeButton.GetComponentInChildren<TMP_Text>().text = "Back";
        var saveLoadView = saveLoadCanvas.gameObject.AddComponent<SaveLoadView>();
        SetField(saveLoadView, "statusText", statusText);
        SetSlots(saveLoadView, new (string slotId, Button button)[]
        {
            ("slot-0", slot0),
            ("slot-1", slot1),
        });

        // NovelRunner ties playback to the presenters above.
        var runnerGo = new GameObject("NovelRunner");
        var runner = runnerGo.AddComponent<NovelRunner>();
        var story = AssetDatabase.LoadAssetAtPath<NovelScriptAsset>($"{Root}/Story.nfscript");
        SetField(runner, "script", story);
        SetField(runner, "dialogue", dialoguePresenter);
        SetField(runner, "choices", choiceView);
        SetField(runner, "audio", audioPresenter);
        SetField(runner, "backgrounds", backgroundPresenter);

        // DemoFlow wires the buttons above into NovelRunner + SaveLoadController.
        var demoFlowGo = new GameObject("DemoFlow");
        var demoFlow = demoFlowGo.AddComponent<DemoFlow>();
        SetField(demoFlow, "runner", runner);
        SetField(demoFlow, "story", story);
        SetField(demoFlow, "titleScreen", titleCanvas.gameObject);
        SetField(demoFlow, "gameplayUi", gameplayCanvas.gameObject);
        SetField(demoFlow, "saveLoadPanel", saveLoadCanvas.gameObject);
        SetField(demoFlow, "saveLoadView", saveLoadView);
        SetField(demoFlow, "newGameButton", newGameButton);
        SetField(demoFlow, "loadGameButton", loadGameButton);
        SetField(demoFlow, "saveButton", saveButton);
        SetField(demoFlow, "closeSaveLoadButton", closeButton);

        EditorSceneManager.SaveScene(scene, $"{Root}/GettingStarted.unity");
        Debug.Log("NovelForge: demo scene built.");
    }

    private static SpriteRenderer CreateSpriteRenderer(string name, int sortingOrder)
    {
        var go = new GameObject(name, typeof(SpriteRenderer));
        var renderer = go.GetComponent<SpriteRenderer>();
        renderer.sortingOrder = sortingOrder;
        var color = renderer.color;
        color.a = 0f;
        renderer.color = color;
        return renderer;
    }

    private static Canvas CreateCanvas(string name)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        return canvas;
    }

    private static TMP_Text CreateTmpText(Transform parent, string name, Vector2 anchoredPos, int fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = new Vector2(600, 40);
        var text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.color = Color.white;
        return text;
    }

    private static Button CreateChoiceButton(Transform parent, string name, Vector2 anchoredPos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(300, 36);
        rect.anchoredPosition = anchoredPos;
        var button = go.AddComponent<Button>();
        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.black;
        label.text = name;
        return button;
    }

    private static Button CreateMenuButton(Transform parent, string name, string label, Vector2 anchoredPos)
        => RenameLabel(CreateChoiceButton(parent, name, anchoredPos), label);

    private static Button CreateCornerButton(Transform parent, string name, string label)
    {
        Button button = CreateChoiceButton(parent, name, Vector2.zero);
        var rect = button.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(1, 1);
        rect.sizeDelta = new Vector2(120, 32);
        rect.anchoredPosition = new Vector2(-20, -20);
        return RenameLabel(button, label);
    }

    private static Button RenameLabel(Button button, string label)
    {
        button.GetComponentInChildren<TMP_Text>().text = label;
        return button;
    }

    private static void AnchorBottomStretch(RectTransform rect, float height)
    {
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(1, 0);
        rect.pivot = new Vector2(0.5f, 0);
        rect.sizeDelta = new Vector2(0, height);
        rect.anchoredPosition = Vector2.zero;
    }

    private static void AnchorCenter(RectTransform rect, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
    }

    private static void SetPositionSlots(Object target, (string position, ActorView view)[] slots)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty("positionSlots");
        prop.arraySize = slots.Length;
        for (int i = 0; i < slots.Length; i++)
        {
            var element = prop.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("position").stringValue = slots[i].position;
            element.FindPropertyRelative("view").objectReferenceValue = slots[i].view;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetSlots(Object target, (string slotId, Button button)[] slots)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty("slots");
        prop.arraySize = slots.Length;
        for (int i = 0; i < slots.Length; i++)
        {
            var element = prop.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("slotId").stringValue = slots[i].slotId;
            element.FindPropertyRelative("button").objectReferenceValue = slots[i].button;
            element.FindPropertyRelative("label").objectReferenceValue = slots[i].button.GetComponentInChildren<TMP_Text>();
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetField(Object target, string fieldName, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(fieldName).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
```

- [ ] **Step 3: Run the scene builder in batch mode**

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe"
$project = "<worktree root>\TestProject~"
Start-Process -FilePath $unity -ArgumentList "-batchmode","-projectPath",$project,"-executeMethod","DemoSceneBuilder.Build","-quit","-logFile","<worktree root>\TestProject~\build_scene.log" -Wait -NoNewWindow
Select-String -Path "<worktree root>\TestProject~\build_scene.log" -Pattern "demo scene built|Exception|Error|NullReferenceException"
```

Expected: `NovelForge: demo scene built.`, no exceptions. If `SetField`/`SetPositionSlots`/
`SetSlots` report a missing property name, re-check the exact field names against the
`Interfaces: Consumes` list above — `SerializedObject.FindProperty` returns `null` (and the
next line NREs) on any typo, this is the most likely failure mode for this step.

- [ ] **Step 4: Delete the temporary scene-builder script**

```bash
rm TestProject~/Assets/Editor/DemoSceneBuilder.cs TestProject~/Assets/Editor/DemoSceneBuilder.cs.meta
rmdir TestProject~/Assets/Editor 2>/dev/null || true
```

- [ ] **Step 5: Commit the scene and `DemoFlow.cs`**

```bash
git add TestProject~/Assets/GettingStartedStaging/
git status --porcelain  # confirm .meta files present, DemoSceneBuilder.cs absent
git commit -m "Build GettingStarted demo scene and wire DemoFlow"
```

- [ ] **Step 6: Live Play Mode QA (computer-use)**

Open the Unity Editor on `TestProject~` (interactive, not batch mode — reuse whatever
technique previous phases used to launch Unity's Editor GUI via PowerShell `Start-Process`
without `-batchmode`). In the Project window, open
`Assets/GettingStartedStaging/GettingStarted.unity`, then enter Play Mode and walk through:

1. **Title screen**: "New Game" and "Load Game" both visible; "Load Game" is greyed out
   (no save exists yet).
2. Click **New Game** → gameplay UI appears; background fades in; a low hum (`theme` tone)
   is audible or at least no `Debug.LogError` about missing music/library appears in the
   Console.
3. Alice's line ("Hi! I'm Alice.") appears on the left with a pink actor sprite fading in;
   click the dialogue box (or press the advance button) to continue. Bob's line appears on
   the right with a blue sprite.
4. Reach the choice — two buttons show "Sure!" / "No thanks".
5. Click **"Sure!"** → Bob's two joke lines play (yellow "laughing" sprite) → "Thanks for
   listening!" (the `if trust >= 1` **true** branch — confirms `set`/`gosub`/`return`/`if`
   all worked) → orange CG fullscreen fades in → a brief pause (`wait 0.5`) → final line →
   a short tone plays (`sfx door_open`) → scene returns to the **Title screen**
   automatically (`OnFinished`). "Load Game" is still disabled — nothing was saved.
6. Click **New Game** again. Advance two or three lines, then click the **Save** button
   (top-right corner) → the save/load panel opens in Save mode, both slots show "Empty".
   Click **Slot 0** → label changes to "Occupied", panel closes automatically
   (`OnSaveCompleted`).
7. Continue playing; this time click **"No thanks"** at the choice → "Suit yourself." →
   "Okay then." (the `if trust >= 1` **false**/`else` branch — `trust` was never
   incremented on this path) → CG → ending line → back to Title.
8. **"Load Game" is now enabled.** Click it → save/load panel opens in Load mode, "Slot 0"
   shows "Occupied". Click it → gameplay UI appears and playback resumes from **the exact
   line after where Save was clicked in step 6** (not from the beginning) —
   confirms `SaveLoadController`/`PlaybackController` snapshot restore is wired correctly
   end-to-end through `NovelRunner`.
9. Click the **Back** button while the save/load panel is open (from the title screen's
   Load Game, before selecting a slot) → panel closes, title screen remains, nothing
   changed.
10. Check the Console: no unexpected `Debug.LogError` entries anywhere in the run, with
    one known exception — `ReturnCommand` (`Runtime/Commands/Builtin/ReturnCommand.cs`,
    pre-existing from an earlier phase) logs `"NovelForge: 'return' with an empty call
    stack — ending playback."` at Error severity every time a top-level script ends via
    `return`, which `Story.nfscript`'s `label ending` block does on every playthrough —
    expect exactly one such line per full run, not zero. Any *other* `Debug.LogError`
    (missing-wiring messages from presenters, `NovelRunner`, etc.) is a real bug — fix it
    before continuing.

If any step fails, fix the root cause (most likely a `SerializedObject` field-name typo in
`DemoSceneBuilder.cs` from Step 2, a `DemoFlow.cs` logic bug, or a `NovelRunner` bug from
Task 1) and re-run Steps 3–6 of this task until the full checklist passes cleanly.

---

### Task 4: Package `Samples~`, verify via Package Manager, clean up staging

**Files:**
- Move: `TestProject~/Assets/GettingStartedStaging/*` → `Samples~/GettingStarted/*`
- Modify: `package.json`

**Interfaces:**
- Consumes: nothing new — pure file relocation and a `package.json` edit.
- Produces: `Samples~/GettingStarted/` as the final packaged sample; `package.json`
  `"samples"` array, consumed by Unity's Package Manager UI.

- [ ] **Step 1: Move the staged folder into `Samples~`**

```bash
mkdir -p Samples~
git mv TestProject~/Assets/GettingStartedStaging Samples~/GettingStarted
```

`git mv` on a whole directory preserves every file's history and its paired `.meta` file
automatically. GUID references inside `.unity`/`.asset` files point to other assets by GUID
(stored in each asset's own `.meta`), not by path, so moving the whole tree together keeps
every cross-reference (sprite/audio/library assignments, the scene's component references)
intact.

- [ ] **Step 2: Add the `"samples"` entry to `package.json`**

```json
{
  "name": "com.novelforge.core",
  "version": "0.1.0",
  "displayName": "NovelForge",
  "description": "Personal Unity toolkit for building visual novels: DSL-scripted dialogue, branching, save/load, localization, and a read/write branch-graph editor.",
  "unity": "6000.6",
  "dependencies": {
    "com.unity.textmeshpro": "3.0.9",
    "com.unity.nuget.newtonsoft-json": "3.2.1"
  },
  "samples": [
    {
      "displayName": "Getting Started",
      "description": "End-to-end demo: dialogue, branching, choices, save/load, localization, audio and backgrounds.",
      "path": "Samples~/GettingStarted"
    }
  ],
  "author": {
    "name": "vova1"
  }
}
```

- [ ] **Step 3: Verify via Package Manager import (computer-use)**

With the Unity Editor open on `TestProject~` (which references this package via
`"com.novelforge.core": "file:../.."` in `TestProject~/Packages/manifest.json`):

1. Open **Window → Package Manager**, select **NovelForge** in the "In Project" list.
2. Under **Samples**, "Getting Started" should now be listed with an **Import** button.
3. Click **Import** — Unity copies the sample into
   `Assets/Samples/NovelForge/0.1.0/Getting Started/`.
4. Open the copy's `GettingStarted.unity`, enter Play Mode, and run through at minimum
   steps 1–5 of Task 3's QA checklist. This is the real acceptance test for the packaging
   itself — it confirms the sample works when consumed exactly the way a real project
   would consume it (via Package Manager import, not by editing the package source
   directly).
5. Delete `Assets/Samples/` afterward (it's a throwaway verification copy local to
   `TestProject~`, not part of the package or the plan's deliverable).

- [ ] **Step 4: Run the full EditMode suite one more time**

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe"
$project = "<worktree root>\TestProject~"
$resultsFile = "<worktree root>\TestProject~\TestResults.xml"
if (Test-Path $resultsFile) { Remove-Item $resultsFile }
Start-Process -FilePath $unity -ArgumentList "-batchmode","-projectPath",$project,"-runTests","-testPlatform","EditMode","-testResults",$resultsFile,"-logFile","<worktree root>\TestProject~\test_run.log" -Wait -NoNewWindow
Select-String -Path $resultsFile -Pattern 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*"' | Select-Object -First 1
```

Expected: `233 passed, 0 failed` (229 pre-existing + 4 `NovelRunnerTests` from Task 1) —
moving files does not change test count; this just confirms nothing broke.

- [ ] **Step 5: Commit**

```bash
git add Samples~/ package.json
git status --porcelain  # confirm TestProject~/Assets/GettingStartedStaging is gone (deleted, not just moved-looking) and Samples~ has every .meta
git commit -m "Package GettingStarted as a UPM sample"
```

---

### Task 5: `README.md` and `USAGE.md`

**Files:**
- Create: `README.md`
- Create: `USAGE.md`

**Interfaces:**
- Consumes: nothing code-level — references file paths and class names established in all
  prior phases and this plan's Tasks 1–4.
- Produces: the deliverable itself; no other task depends on these files.

- [ ] **Step 1: Write `README.md`**

```markdown
# NovelForge

Personal Unity toolkit for building visual novels: DSL-scripted dialogue, branching,
save/load, localization, and a read/write branch-graph editor. Distributed as a local/git
UPM package — built for reuse across my own projects, not for a general audience.

## Install

Add to your project's `Packages/manifest.json`:

```json
"com.novelforge.core": "file:../../NovelForge"
```

(or a git URL, if pushed to a remote — adjust the path/URL to wherever this package lives
relative to your project).

## Quick start

Window → Package Manager → NovelForge → Samples → **Getting Started** → Import. Open the
imported `GettingStarted.unity` scene and press Play. It's a ~2 minute playthrough that
exercises dialogue, character sprites/emotions, branching (`if`/`else`), a player choice,
reusable dialogue via `gosub`/`return`, backgrounds, a CG illustration, music/SFX, and
save/load.

## Features

- DSL-scripted dialogue, branching (`if`/`else`), reusable snippets (`gosub`/`return`),
  player choices
- Save/load with variables and flags
- Character sprites with emotions and screen positions
- Localization (text; JSON translation tables keyed by stable line ids)
- Music, SFX and voice audio
- Backgrounds and full-screen CG illustrations
- In-editor DSL script editor with syntax highlighting and autocomplete
- In-editor read/write branch-graph visualization
- `Character Editor` window for managing character definitions and their sprite/emotion
  mappings against actual script usage

See [USAGE.md](USAGE.md) for the DSL reference, project structure, and a step-by-step
guide to building a scene from scratch.

For the architectural reasoning behind any of this, see the design specs under
[`docs/superpowers/specs/`](docs/superpowers/specs/).
```

- [ ] **Step 2: Write `USAGE.md`**

```markdown
# NovelForge — Usage Guide

## Project structure

- `*.nfscript` files — DSL scripts, anywhere under `Assets/`. Imported automatically into
  a `NovelScriptAsset` by `NovelScriptImporter`.
- `CharacterDefinition` assets (`NovelForge/Character Definition`) — one per character:
  id, display name, name color, and a sprite per emotion. Collected into a
  `CharacterLibrary` asset (`NovelForge/Character Library`) that a scene's
  `DialoguePresenter` references.
- `BackgroundLibrary` (`NovelForge/Background Library`) — id→sprite maps for `bg` and `cg`
  commands.
- `AudioLibrary` (`NovelForge/Audio Library`) — id→clip maps for `music` and `sfx`
  commands.
- Localization files — plain JSON, `{ "line-id": "translated text", ... }`. Loaded via
  `LocalizationTable.FromJson(textAsset.text)`.
- Save data — `JsonSaveStorage` (implements `ISaveStorage`) writes one JSON file per slot
  under `Application.persistentDataPath` by default; swap in a different `ISaveStorage`
  implementation for a different backend.

## DSL syntax

| Construct | Syntax | Notes |
|---|---|---|
| Label | `label <name>` | Jump/gosub target. Resets auto-generated localization ids. |
| Comment | `// text` | Attaches to the next command; round-trips through the Branch Graph editor. |
| Dialogue | `<CharacterId>: <text> [#emotion] [@id] [left\|right\|center]` | `#emotion` selects a sprite pose; `@id` is an explicit localization id (auto-generated as `<label>_<ordinal>` if omitted); position is a bare trailing `left`/`right`/`center` word. |
| Assignment | `set <var> = <value>` / `+=` / `-=` | Value: `true`/`false`, an int, a float, or a `"quoted string"`. |
| Condition | `if <var> <op> <value>` ... `else` ... `endif` | `op`: `== != > >= < <=`. `else`/`endif` optional/required as in any C-like language. |
| Choice | `choice` then one or more `"text" [@id] -> label` lines immediately after | Player picks one; jumps to the option's label. |
| Jump | `jump <label>` | Unconditional jump. |
| Subroutine call | `gosub <label>` | Jumps, pushing a return address. |
| Return | `return` | Pops the call stack (from the nearest `gosub`) or ends the script if the stack is empty. |
| Background | `bg <id>` | Crossfades to the background registered under `<id>`. |
| CG | `cg <id>` | Fades in a full-screen illustration. |
| Music | `music <id>` | Crossfades to the track registered under `<id>`. |
| Sound effect | `sfx <id>` | Plays a one-shot clip from a pooled voice. |
| Wait | `wait <seconds>` | Pauses the script for the given duration. |

## Editor tools

- **Script Editor** (`Window → NovelForge → Script Editor`, or double-click a `.nfscript`
  asset) — syntax highlighting and autocomplete for the DSL above.
- **Character Editor** (`Window → NovelForge → Character Editor`) — browse/edit
  `CharacterDefinition` assets, preview emotion sprites, and validate character ids used
  in scripts against what's actually defined (and vice versa).
- **Branch Graph** (`Window → NovelForge → Branch Graph`, or double-click a `.nfscript`
  asset while holding the graph view open) — a read/write node graph over a script's
  `label` blocks; edits round-trip back to the original text, preserving comments.
- **`NovelScriptImporter`** — a `ScriptedImporter` that compiles every `.nfscript` on
  import and reports DSL syntax errors as Unity import errors at the exact line.

## Building a scene from scratch

1. Write a `.nfscript` file with at least one `label` and a `return`.
2. Create a `CharacterDefinition` per speaking character, a `CharacterLibrary` referencing
   them, and (if the script uses `bg`/`cg`/`music`/`sfx`) a `BackgroundLibrary`/
   `AudioLibrary`.
3. In a scene, add the presenters your script needs: `DialoguePresenter` (+ a
   `DialogueBoxView` with name/body `TMP_Text` and an advance `Button`, + an `ActorView`
   per on-screen character position, using `SpriteRenderer`s), `ChoiceView` (+ option
   `Button`s with a `TMP_Text` child each), `BackgroundPresenter` (+ two crossfade
   `SpriteRenderer` slots and a CG slot), `AudioPresenter` (+ two music `AudioSource`s for
   crossfading).
4. Add a `NovelRunner` component; assign the script asset and every presenter above to its
   fields.
5. Call `runner.Play()` from your own code (a button click, a `Start()`, wherever fits your
   game's flow) to compile and run the script.

`Samples~/GettingStarted` is a complete, working reference for all of the above — including
a title screen and save/load — see `DemoFlow.cs` for how a real game wires `NovelRunner`
together with save/load and screen navigation (`NovelRunner` itself stays minimal and
doesn't know about either).

## Save/Load

- `ISaveStorage` — the storage abstraction (`Save`/`Load`/`SlotExists`).
  `JsonSaveStorage` is the built-in implementation.
- `SaveLoadController` — orchestrates a save/load against a `PlaybackController` +
  `StoryContext` + `ISaveStorage`. Construct one after `NovelRunner.Prepare()` (its
  `.Playback`/`.Context` are only populated after `Prepare()` succeeds).
- `SaveLoadView` — a UGUI component presenting a fixed grid of named slots in either Save
  or Load mode (`ShowSaveMode()`/`ShowLoadMode()`), showing Occupied/Empty per slot.

**Important ordering for "Continue" flows:** call `NovelRunner.Prepare()` (which builds a
fresh `Playback` but does not start it), then `SaveLoadController.LoadInto()` (which
restores the snapshot onto that not-yet-running `Playback`), and only then
`NovelRunner.Begin()`. Unity runs a coroutine synchronously up to its first real suspend
point, so calling `Begin()`/`Play()` before restoring the snapshot can let a few commands
execute from the start of the script before the restore takes effect.

## Localization

- Format: flat JSON, `{ "line-id": "translated text" }`.
- Every dialogue line and every choice option has a line id — either explicit (`@id` in
  the script) or auto-generated as `<nearest label>_<ordinal>`. Ids must be unique across
  the whole script.
- Assign a `TextAsset` (the JSON file) to `NovelRunner`'s `localizationJson` field; it's
  parsed via `LocalizationTable.FromJson`. Leave it unassigned to skip localization
  entirely (dialogue/choice text is shown as written in the script, no warnings).
- A line id present in the script but missing from the translation table falls back to
  the source text with a `Debug.LogWarning` — useful for spotting incomplete translations.
  `Samples~/GettingStarted/Localization/ru.json` demonstrates this: it translates 4 of the
  script's ~13 line ids on purpose. Assign it to the demo's `NovelRunner` to see both the
  translated lines and the fallback warnings for the rest.
```

- [ ] **Step 3: Commit**

```bash
git add README.md USAGE.md
git commit -m "Add README and USAGE guide"
```

---

## Final verification (before handing off to finishing-a-development-branch)

- [ ] Full EditMode suite green (233/233) on the worktree tip.
- [ ] `git status --porcelain` clean (no stray untracked/modified files beyond the
  pre-existing, already-known `docs/*.meta`/`TestProject~/Packages/packages-lock.json`/
  `ProjectAuditorSettings.asset` noise documented in this project's prior phases).
- [ ] `Samples~/GettingStarted/` contains no leftover reference to
  `Assets/GettingStartedStaging` (check `GettingStarted.unity`/`.asset` files don't embed
  the old path anywhere it matters — Unity references by GUID, not path, so this should
  be a non-issue, but worth a quick `grep -l GettingStartedStaging Samples~/GettingStarted/*`
  sanity check).
- [ ] `TestProject~/Assets/Editor/` does not exist (both temporary generator scripts were
  deleted in Tasks 2 and 3).
