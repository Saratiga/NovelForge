# Editor ScriptedImporter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give `.nfscript` files a real Unity asset path: a `ScriptedImporter` that validates the DSL on import (parse errors become import errors with a line number, not silent runtime failures) and produces a `NovelScriptAsset` the game can reference and lazily compile at runtime.

**Architecture:** A brand-new, editor-only assembly `NovelForge.Editor` holding two small classes: `NovelScriptAsset` (a `ScriptableObject` that stores only the raw script text) and `NovelScriptImporter` (a `ScriptedImporter` for the `.nfscript` extension that calls `ScriptCompiler.Compile(...)` purely to validate — catching `ParseException` and reporting it via `AssetImportContext.LogImportError` — then always creates the asset with the source text regardless of validation outcome). No existing assembly is touched; this is purely additive.

**Tech Stack:** C#, Unity 6000.6, `UnityEditor.AssetImporters` (`ScriptedImporter` API), NUnit (Unity Test Framework, EditMode).

**Spec:** [docs/superpowers/specs/2026-09-10-editor-scripted-importer-design.md](../specs/2026-09-10-editor-scripted-importer-design.md). Out of scope for this plan (per the spec): the script editor window (syntax highlighting/autocomplete), the branch graph editor (`GraphView`, round-trip serialization) — both are separate future phases, decomposed out of the original "editor tools" phase during brainstorming. No custom Inspector for `NovelScriptAsset` — the default `[TextArea]` field display is sufficient for this phase.

## Global Constraints

- Unity Editor version: **6000.6.0f1**, at `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe`. Package id `com.novelforge.core`, package root `G:\ClaudeProjects\NovelForge`.
- This plan is purely additive — it creates a new `Editor/` folder and a new `Tests/Editor/` folder, and does not modify any existing file in `Runtime/`, `UI/`, or `Tests/Runtime/`/`Tests/UI/`. No existing test may need to change.
- `NovelScriptAsset.Source` is a **public field** (not `[SerializeField] private` + property) — the calling game reads it directly to call `ScriptCompiler.Compile(asset.Source)` itself; this asset is a plain text container, not a component with hidden behavior.
- `NovelScriptImporter` always creates the `NovelScriptAsset` with the source text, whether or not the script is valid — a parse error becomes a **Console import error** (via `ctx.LogImportError`, which takes only a `string message` in this plan — do NOT pass `ctx.assetPath` as a second argument, `LogImportError`'s second parameter is a `UnityEngine.Object` context reference, not a path string, and passing a string there is a compile error), not a build-blocking exception and not a missing asset.
- `ParseException.Message` already contains `"Line N: ..."` (see `Runtime/Parsing/ParseException.cs`) — the importer's error message is `$"NovelForge: {e.Message}"`, giving e.g. `"NovelForge: Line 1: Undefined label 'nowhere'."` with no extra formatting needed.
- Import-time validation uses `new ScriptCompiler().Compile(source)` with the default constructor (i.e. `CommandRegistry.CreateDefault()`) — there is no project-specific custom-command registry mechanism yet, and this plan does not add one.
- Git commit messages end with: `Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>`.
- Every `git add` must be of the whole containing folder (e.g. `git add Editor`), never individual bare file paths — Unity generates `.meta` companions on import, and folder-add recurses into them automatically. A *new* folder's own sibling `.meta` (e.g. `Editor.meta`, living next to the `Editor/` folder) is not caught by `git add Editor` — this project has hit that gotcha repeatedly (`UI.meta`, `Tests/UI.meta`, `Runtime/Actors.meta`, `Runtime/SaveLoad.meta`, `Runtime/Localization.meta`, and their `Tests/` counterparts); check for it explicitly (`git status`) after the task creates `Editor/` and `Tests/Editor/`.
- **`.meta` files must be genuinely Unity-generated, never hand-authored.** A prior phase in this project had two rejected fix rounds because an implementer hand-typed `.meta` file content instead of letting Unity generate it via a real import — this was caught by comparing GUIDs (a hand-typed GUID often shows a visible repeating/cyclic pattern; a real Unity GUID is opaque random hex) and by checking `.cs.meta` files are exactly 2 lines (`fileFormatVersion: 2` + `guid: ...`, no `MonoImporter:` block). Let Unity generate every `.meta` file in this plan by actually running the test suite (which imports the project); never type one by hand under any circumstances.
- Use the working directory's own `TestProject~`, addressed with absolute paths for every argument to the Unity test command — a relative `TestProject~` for `-testResults`/`-logFile` gets silently doubled by Unity into a nonexistent nested path. Get the worktree root with `git rev-parse --show-toplevel` and build every path from that:
  ```powershell
  & "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe" -batchmode -projectPath "<worktree-root>\TestProject~" -runTests -testPlatform EditMode -testResults "<worktree-root>\TestProject~\TestResults.xml" -logFile "<worktree-root>\TestProject~\Logs\RunTests.log"
  ```
  Deliberately no `-quit`. After each run, read `TestResults.xml` and confirm the `<test-run>` root element's `failed` attribute is `"0"` and `passed` matches the expected running total; if missing, check `RunTests.log`. The launching process can return before Unity's actual test run finishes (Unity relaunches itself internally) — if the results file isn't there yet, wait and re-check rather than assuming failure. This is referred to below as "the EditMode test command."
- **Environment note, already resolved once in this project:** a freshly created worktree's `TestProject~/Assets` folder does not exist yet (it's gitignored, empty, and Unity 6000.6.0f1 does not auto-create it — it fails with a misleading "Couldn't set project path" error instead). If the EditMode test command fails with that error, create the folder first: `New-Item -ItemType Directory -Force -Path "<worktree-root>\TestProject~\Assets"`, then retry.
- Do not hand-author `.meta` files. Unity generates them automatically on import (during the test run).
- Baseline before this plan: **164 passing tests** (confirmed on current `master`). The task below states the expected running total after it.

---

### Task 1: `NovelForge.Editor` assembly, `NovelScriptAsset`, `NovelScriptImporter`

**Files:**
- Create: `Editor/NovelForge.Editor.asmdef`
- Create: `Editor/NovelScriptAsset.cs`
- Create: `Editor/NovelScriptImporter.cs`
- Create: `Tests/Editor/NovelForge.Editor.Tests.asmdef`
- Create: `Tests/Editor/NovelScriptImporterTests.cs`

**Interfaces:**
- Consumes: `NovelForge.Runtime.ScriptCompiler.Compile(string)` (public, existing) and `NovelForge.Runtime.ParseException` (public, existing, has `Message` and `LineNumber`).
- Produces: `NovelForge.Editor.NovelScriptAsset` (`public class NovelScriptAsset : ScriptableObject { public string Source; }`) and `NovelForge.Editor.NovelScriptImporter` (`[ScriptedImporter(1, "nfscript")] public class NovelScriptImporter : ScriptedImporter`). Nothing in this plan consumes these from a later task — this is the plan's only task.

This whole task lands in one commit: the test file references `NovelScriptAsset`/`NovelScriptImporter`, which don't exist until the asmdef and both classes are created, so there's no way to get an intermediate compiling state with only half the production code — write the tests first (per TDD), confirm they fail to compile, then add all three new production files together.

- [ ] **Step 1: Write the failing tests**

Create `Tests/Editor/NovelScriptImporterTests.cs`:

```csharp
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace NovelForge.Editor.Tests
{
    public class NovelScriptImporterTests
    {
        private const string TestFolder = "Assets/Temp_NovelScriptImporterTests";

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TestFolder))
                AssetDatabase.DeleteAsset(TestFolder);
        }

        private static string WriteScriptFile(string fileName, string source)
        {
            string absoluteFolder = Path.Combine(Application.dataPath, "Temp_NovelScriptImporterTests");
            Directory.CreateDirectory(absoluteFolder);
            File.WriteAllText(Path.Combine(absoluteFolder, fileName), source);
            return $"{TestFolder}/{fileName}";
        }

        [Test]
        public void ValidScript_ImportsCleanlyWithSourcePreserved()
        {
            string assetPath = WriteScriptFile("valid.nfscript", "label start\nAlice: Hello!\n");

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            var asset = AssetDatabase.LoadAssetAtPath<NovelScriptAsset>(assetPath);

            Assert.IsNotNull(asset);
            Assert.AreEqual("label start\nAlice: Hello!\n", asset.Source);
        }

        [Test]
        public void InvalidScript_StillCreatesAssetAndLogsImportError()
        {
            string assetPath = WriteScriptFile("invalid.nfscript", "jump nowhere\n");

            LogAssert.Expect(LogType.Error, "NovelForge: Line 1: Undefined label 'nowhere'.");
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

            var asset = AssetDatabase.LoadAssetAtPath<NovelScriptAsset>(assetPath);

            Assert.IsNotNull(asset);
            Assert.AreEqual("jump nowhere\n", asset.Source);
        }
    }
}
```

**On the exact `LogAssert.Expect` string in `InvalidScript_StillCreatesAssetAndLogsImportError`:** this plan predicts that `AssetImportContext.LogImportError("NovelForge: Line 1: Undefined label 'nowhere'.")` surfaces to the Console/log stream as exactly that string with no extra decoration from Unity, since that is standard `ScriptedImporter` behavior. This has not been confirmed empirically against this exact Unity version yet. **If this test fails only on the `LogAssert.Expect` line** (i.e. the asset itself is created correctly with the right `Source`, but the expected log wasn't observed, or a differently-formatted error was seen instead) — check `TestProject~\Logs\RunTests.log` for the actual text Unity logged, update the `LogAssert.Expect` string in this file to match verbatim, and **update this plan file's test code block in place** to match, the same practice this project has followed for other environment unknowns (package versions, path handling). Do not weaken the assertion to a substring/regex match unless the exact string is genuinely unpredictable (e.g. contains something Unity generates dynamically) — here it is fully our own controlled text, so an exact match should be achievable.

- [ ] **Step 2: Run tests to verify they fail**

Run the EditMode test command from Global Constraints.
Expected: compile error (`NovelScriptAsset` does not exist yet).

- [ ] **Step 3: Create the `NovelForge.Editor` assembly definition**

Create `Editor/NovelForge.Editor.asmdef`:

```json
{
    "name": "NovelForge.Editor",
    "rootNamespace": "NovelForge.Editor",
    "references": [
        "NovelForge.Runtime"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 4: Implement `NovelScriptAsset`**

Create `Editor/NovelScriptAsset.cs`:

```csharp
using UnityEngine;

namespace NovelForge.Editor
{
    public class NovelScriptAsset : ScriptableObject
    {
        [TextArea(10, 40)]
        public string Source;
    }
}
```

- [ ] **Step 5: Implement `NovelScriptImporter`**

Create `Editor/NovelScriptImporter.cs`:

```csharp
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;
using NovelForge.Runtime;

namespace NovelForge.Editor
{
    [ScriptedImporter(1, "nfscript")]
    public class NovelScriptImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            string source = File.ReadAllText(ctx.assetPath);

            try
            {
                new ScriptCompiler().Compile(source);
            }
            catch (ParseException e)
            {
                ctx.LogImportError($"NovelForge: {e.Message}");
            }

            var asset = ScriptableObject.CreateInstance<NovelScriptAsset>();
            asset.Source = source;
            ctx.AddObjectToAsset("main", asset);
            ctx.SetMainObject(asset);
        }
    }
}
```

- [ ] **Step 6: Create the `NovelForge.Editor.Tests` assembly definition**

Create `Tests/Editor/NovelForge.Editor.Tests.asmdef`:

```json
{
    "name": "NovelForge.Editor.Tests",
    "rootNamespace": "NovelForge.Editor.Tests",
    "references": [
        "NovelForge.Runtime",
        "NovelForge.Editor"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 7: Run tests to verify they pass**

Run the EditMode test command from Global Constraints.
Expected: `failed="0"`, 2 new tests passing (running total from 164: **166**). Follow the contingency note under Step 1 if `InvalidScript_StillCreatesAssetAndLogsImportError` fails only on the log assertion.

- [ ] **Step 8: Commit**

```bash
git add Editor Tests/Editor
git commit -m "$(cat <<'EOF'
Add NovelForge.Editor assembly: NovelScriptAsset + validating ScriptedImporter for .nfscript

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

Check `git status` after this commit — if `Editor.meta` or `Tests/Editor.meta` (the new folders' own sibling metas) weren't picked up by the `git add` above, add them explicitly before committing.
