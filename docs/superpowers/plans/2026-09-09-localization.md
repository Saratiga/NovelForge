# Localization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give every dialogue line and choice option a stable id (auto-generated or explicit) and a way to resolve translated text for it at runtime, so a game can localize its script without touching the VM's presenter contracts.

**Architecture:** A new, small, standalone `LocalizationTable` (`Runtime/Localization/`) parses a flat JSON `id → text` map via the already-present `Newtonsoft.Json`. `StoryContext` gets one new optional property, `Localization`, holding the active table (`null` = no localization, show source text) — the same swap-in/swap-out pattern already used for `Audio`/`Backgrounds`/etc. `ScriptCompiler` gains a new optional `@id` DSL tag on dialogue and choice-option lines, with auto-generation (`<nearest label>_<ordinal>`) when omitted. `SayLineCommand` and `ChoiceCommand` resolve their text against `context.Localization` at `Execute` time, before calling into `IDialoguePresenter`/`IChoicePresenter` — those interfaces are untouched.

**Tech Stack:** C#, Unity 6000.6, `Newtonsoft.Json` (already a package dependency as of the Save/Load phase), NUnit (Unity Test Framework, EditMode).

**Spec:** [docs/superpowers/specs/2026-09-09-localization-design.md](../specs/2026-09-09-localization-design.md). Out of scope for this plan (per the spec): real voice-over playback / a VO path field on translations (no VO-playback feature exists yet to consume it), an engine-side locale registry or "switch by locale code" API (the calling game owns which file maps to which locale, same as it owns which `CharacterLibrary`/`AudioLibrary` asset to use), editor tooling for translators.

## Global Constraints

- Unity Editor version: **6000.6.0f1**, at `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe`. Package id `com.novelforge.core`, package root `G:\ClaudeProjects\NovelForge`.
- No new package dependency — `com.unity.nuget.newtonsoft-json` (`3.2.1`) is already in `package.json` from the Save/Load phase, and (per that phase's confirmed finding) needs no `Runtime/NovelForge.Runtime.asmdef` reference at all, since it ships no `.asmdef` of its own and Unity auto-includes precompiled DLLs when `overrideReferences` is `false`. `Runtime/SaveLoad/JsonSaveStorage.cs` already `using Newtonsoft.Json;` successfully on this exact basis — this plan's `LocalizationTable.cs` does the same, no asmdef edits needed anywhere in this plan.
- This plan touches already-merged core files (`Runtime/Parsing/ScriptCompiler.cs`, `Runtime/Commands/StoryContext.cs`, `Runtime/Commands/Builtin/SayLineCommand.cs`, `Runtime/Commands/Builtin/ChoiceCommand.cs`). `SayLineCommand`'s and `ChoiceCommand`'s constructors gain a new required parameter (`lineId` / `optionIds`) — every existing call site (production and test) is updated in the same task that changes the signature, so the assembly never sits in a non-compiling intermediate state. Every existing test not explicitly listed below as modified must keep passing unmodified, in particular `Tests/Runtime/ScriptCompilerCoreTests.cs` and `Tests/Runtime/ScriptCompilerChoiceAndIntegrationTests.cs`, which construct commands only indirectly through `ScriptCompiler.Compile(...)` and assert on presenter-recorded text/behavior, not on ids.
- Established project-wide `Debug.LogError` convention (one message per distinct failure cause, matched verbatim via `LogAssert.Expect(LogType.Error, "...")`) still applies to genuinely unwired/misbehaving dependencies. Missing-translation is **not** that kind of failure — it is an expected, non-fatal state while a translation table is incomplete — so it uses `Debug.LogWarning` instead (matched via `LogAssert.Expect(LogType.Warning, "...")`), the first use of `LogWarning` in this project. Every `Debug.LogError`/`Debug.LogWarning` message text given in this plan is exact and must match verbatim.
- Git commit messages end with: `Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>`.
- Every `git add` must be of the whole containing folder (e.g. `git add Runtime/Localization`), never individual bare file paths — Unity generates `.meta` companions on import, and folder-add recurses into them automatically. A *new* folder's own sibling `.meta` (e.g. `Runtime/Localization.meta`, living next to the `Localization/` folder) is not caught by `git add Runtime/Localization` — this project has hit that gotcha five times now (`UI.meta`, `Tests/UI.meta`, `Runtime/Actors.meta`, `Runtime/SaveLoad.meta`, `Tests/Runtime/SaveLoad.meta`); check for it explicitly (`git status`) after the first task that creates `Runtime/Localization/` and `Tests/Runtime/Localization/`.
- This plan runs inside a git worktree. **Use the working directory's own `TestProject~`, addressed with absolute paths for every argument to the Unity test command** — a relative `TestProject~` for `-testResults`/`-logFile` gets silently doubled by Unity into a nonexistent nested path. Get the worktree root with `git rev-parse --show-toplevel` and build every path from that:
  ```powershell
  & "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe" -batchmode -projectPath "<worktree-root>\TestProject~" -runTests -testPlatform EditMode -testResults "<worktree-root>\TestProject~\TestResults.xml" -logFile "<worktree-root>\TestProject~\Logs\RunTests.log"
  ```
  Deliberately no `-quit`. After each run, read `TestResults.xml` and confirm the `<test-run>` root element's `failed` attribute is `"0"` and `passed` matches the expected running total; if missing, check `RunTests.log`. The launching process can return before Unity's actual test run finishes (Unity relaunches itself internally) — if the results file isn't there yet, wait and re-check rather than assuming failure. This is referred to below as "the EditMode test command."
- Do not hand-author `.meta` files. Unity generates them automatically on import (during the test run).
- No timed/`IDeltaTimeSource` concerns in this plan — nothing here waits on frame-by-frame time or real coroutine scheduling. The existing `CoroutineTestUtil.RunToCompletion` pattern is still how every `IEnumerator`-based test in this project drives command execution — used throughout.
- Baseline before this plan: **147 passing tests** (confirmed via the current `master`). Each task states the expected running total after it.

---

### Task 1: `LocalizationTable`

**Files:**
- Create: `Runtime/Localization/LocalizationTable.cs`
- Create: `Tests/Runtime/Localization/LocalizationTableTests.cs`

**Interfaces:**
- Produces: `NovelForge.Runtime.LocalizationTable` — `public bool TryGetText(string lineId, out string text)`, `public static LocalizationTable FromJson(string json)`. Task 2 constructs instances via `LocalizationTable.FromJson(...)` and assigns them to `StoryContext.Localization`.

- [ ] **Step 1: Write the failing tests**

Create `Tests/Runtime/Localization/LocalizationTableTests.cs`:

```csharp
using NUnit.Framework;

namespace NovelForge.Runtime.Tests
{
    public class LocalizationTableTests
    {
        [Test]
        public void FromJson_ThenTryGetText_ReturnsMatchingEntry()
        {
            var table = LocalizationTable.FromJson("{\"greet_1\": \"Привет!\"}");

            Assert.IsTrue(table.TryGetText("greet_1", out string text));
            Assert.AreEqual("Привет!", text);
        }

        [Test]
        public void TryGetText_MissingId_ReturnsFalse()
        {
            var table = LocalizationTable.FromJson("{\"greet_1\": \"Привет!\"}");

            Assert.IsFalse(table.TryGetText("missing_id", out string text));
            Assert.IsNull(text);
        }

        [Test]
        public void FromJson_Null_ReturnsEmptyTable()
        {
            var table = LocalizationTable.FromJson(null);

            Assert.IsFalse(table.TryGetText("anything", out _));
        }

        [Test]
        public void FromJson_EmptyString_ReturnsEmptyTable()
        {
            var table = LocalizationTable.FromJson(string.Empty);

            Assert.IsFalse(table.TryGetText("anything", out _));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the EditMode test command from Global Constraints.
Expected: compile error / test collection failure (`LocalizationTable` does not exist yet).

- [ ] **Step 3: Implement `LocalizationTable`**

Create `Runtime/Localization/LocalizationTable.cs`:

```csharp
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NovelForge.Runtime
{
    public class LocalizationTable
    {
        private readonly Dictionary<string, string> _entries;

        private LocalizationTable(Dictionary<string, string> entries)
        {
            _entries = entries;
        }

        public bool TryGetText(string lineId, out string text) => _entries.TryGetValue(lineId, out text);

        public static LocalizationTable FromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                return new LocalizationTable(new Dictionary<string, string>());

            var entries = JsonConvert.DeserializeObject<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
            return new LocalizationTable(entries);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run the EditMode test command from Global Constraints.
Expected: `failed="0"`, 4 new tests passing (running total from 147: **151**).

- [ ] **Step 5: Commit**

```bash
git add Runtime/Localization Tests/Runtime/Localization
git commit -m "$(cat <<'EOF'
Add LocalizationTable for JSON-backed translation lookup

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

Check `git status` after this commit — if `Runtime/Localization.meta` or `Tests/Runtime/Localization.meta` (the new folders' own sibling metas) weren't picked up by the `git add` above, add them explicitly before committing.

---

### Task 2: id-aware commands + DSL support

**Files:**
- Modify: `Runtime/Commands/StoryContext.cs`
- Modify: `Runtime/Commands/Builtin/SayLineCommand.cs`
- Modify: `Runtime/Commands/Builtin/ChoiceCommand.cs`
- Modify: `Runtime/Parsing/ScriptCompiler.cs`
- Modify: `Tests/Runtime/ContentCommandsTests.cs`
- Modify: `Tests/Runtime/ChoiceCommandTests.cs`
- Create: `Tests/Runtime/ScriptCompilerLocalizationTests.cs`

**Interfaces:**
- Consumes: `LocalizationTable.TryGetText`/`LocalizationTable.FromJson` (Task 1).
- Produces: `StoryContext.Localization` (`public LocalizationTable Localization { get; set; }`); `SayLineCommand(string characterId, string text, string emotion, string position, string lineId)`; `ChoiceCommand(IReadOnlyList<string> optionTexts, IReadOnlyList<string> optionIds, int optionCount)`; DSL support for an optional `@id` tag on dialogue lines and choice-option lines, with `<nearest label>_<ordinal>` auto-generation (pseudo-label `_start` before any `label`) when omitted, and a `ParseException` on any duplicate id across the whole script.

This task changes a constructor signature that `ScriptCompiler.cs` is the sole production caller of, so the command changes and the compiler changes must land together — splitting them would leave the assembly non-compiling between commits. All new/changed tests are written first, then all production code changes together, then one full green run.

- [ ] **Step 1: Update existing tests for the new constructor signatures**

In `Tests/Runtime/ContentCommandsTests.cs`, update both existing `SayLineCommand` constructions (`SayLine_DelegatesToDialoguePresenter` and `SayLine_NullDialoguePresenter_LogsErrorAndDoesNotThrow`) from:

```csharp
var command = new SayLineCommand("Alice", "Привет!", "happy", "left");
```

to:

```csharp
var command = new SayLineCommand("Alice", "Привет!", "happy", "left", "line_1");
```

(Neither test sets `context.Localization`, so `ResolveText` falls back to the source text — behavior and assertions are unchanged; only the constructor call gains the new trailing id argument.)

In `Tests/Runtime/ChoiceCommandTests.cs`, update all three existing `ChoiceCommand` constructions:

```csharp
var command = new ChoiceCommand(new[] { "Good, thanks!", "Not great..." }, optionCount: 2);
```
becomes
```csharp
var command = new ChoiceCommand(new[] { "Good, thanks!", "Not great..." }, new[] { "opt_1", "opt_2" }, optionCount: 2);
```

and both occurrences of:
```csharp
var command = new ChoiceCommand(new[] { "Only option" }, optionCount: 1);
```
become:
```csharp
var command = new ChoiceCommand(new[] { "Only option" }, new[] { "opt_1" }, optionCount: 1);
```

- [ ] **Step 2: Write new localization-resolution tests for the commands**

Append to `Tests/Runtime/ContentCommandsTests.cs`, inside the class:

```csharp
        [Test]
        public void SayLine_WithLocalizationTableAndMatchingId_UsesTranslatedText()
        {
            var dialogue = new RecordingDialoguePresenter();
            var localization = LocalizationTable.FromJson("{\"greet_1\": \"Привет!\"}");
            var context = new StoryContext { Dialogue = dialogue, Localization = localization };
            var command = new SayLineCommand("Alice", "Hello!", "happy", "left", "greet_1");

            CoroutineTestUtil.RunToCompletion(command.Execute(context, new StoryPointer()));

            Assert.AreEqual("Привет!", dialogue.Calls[0].text);
        }

        [Test]
        public void SayLine_WithLocalizationTableAndMissingId_FallsBackToSourceTextAndLogsWarning()
        {
            var dialogue = new RecordingDialoguePresenter();
            var localization = LocalizationTable.FromJson("{}");
            var context = new StoryContext { Dialogue = dialogue, Localization = localization };
            var command = new SayLineCommand("Alice", "Hello!", "happy", "left", "greet_1");

            LogAssert.Expect(LogType.Warning, "NovelForge: no translation for line id 'greet_1' — falling back to source text.");
            CoroutineTestUtil.RunToCompletion(command.Execute(context, new StoryPointer()));

            Assert.AreEqual("Hello!", dialogue.Calls[0].text);
        }
```

Append to `Tests/Runtime/ChoiceCommandTests.cs`, inside the class:

```csharp
        [Test]
        public void Execute_WithLocalizationTableAndAllIdsMatching_PresentsTranslatedTexts()
        {
            var choices = new RecordingChoicePresenter { NextSelection = 0 };
            var localization = LocalizationTable.FromJson("{\"opt_1\": \"Хорошо, спасибо!\", \"opt_2\": \"Не очень...\"}");
            var context = new StoryContext { Choices = choices, Localization = localization };
            var command = new ChoiceCommand(new[] { "Good, thanks!", "Not great..." }, new[] { "opt_1", "opt_2" }, optionCount: 2);
            command.ResolveTarget(0, 10);
            command.ResolveTarget(1, 20);

            CoroutineTestUtil.RunToCompletion(command.Execute(context, new StoryPointer()));

            CollectionAssert.AreEqual(new[] { "Хорошо, спасибо!", "Не очень..." }, choices.Calls[0]);
        }

        [Test]
        public void Execute_WithLocalizationTableAndOnePartialMissingId_FallsBackForThatOptionOnlyAndLogsWarning()
        {
            var choices = new RecordingChoicePresenter { NextSelection = 0 };
            var localization = LocalizationTable.FromJson("{\"opt_1\": \"Хорошо, спасибо!\"}");
            var context = new StoryContext { Choices = choices, Localization = localization };
            var command = new ChoiceCommand(new[] { "Good, thanks!", "Not great..." }, new[] { "opt_1", "opt_2" }, optionCount: 2);
            command.ResolveTarget(0, 10);
            command.ResolveTarget(1, 20);

            LogAssert.Expect(LogType.Warning, "NovelForge: no translation for line id 'opt_2' — falling back to source text.");
            CoroutineTestUtil.RunToCompletion(command.Execute(context, new StoryPointer()));

            CollectionAssert.AreEqual(new[] { "Хорошо, спасибо!", "Not great..." }, choices.Calls[0]);
        }
```

- [ ] **Step 3: Write the new `ScriptCompiler` localization tests**

Create `Tests/Runtime/ScriptCompilerLocalizationTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NovelForge.Runtime.Tests
{
    public class ScriptCompilerLocalizationTests
    {
        [Test]
        public void DialogueLine_WithExplicitIdTag_UsesThatIdForTranslationLookup()
        {
            var script = new ScriptCompiler().Compile("Alice: Hello! @custom_greet\n");
            var dialogue = new RecordingDialoguePresenter();
            var context = new StoryContext { Dialogue = dialogue, Localization = LocalizationTable.FromJson("{\"custom_greet\": \"Привет!\"}") };

            CoroutineTestUtil.RunToCompletion(script.Commands[0].Execute(context, new StoryPointer()));

            Assert.AreEqual("Привет!", dialogue.Calls[0].text);
        }

        [Test]
        public void DialogueLine_WithoutIdTag_GeneratesLabelAndOrdinalId()
        {
            var script = new ScriptCompiler().Compile("label greet\nAlice: Hello!\n");
            var dialogue = new RecordingDialoguePresenter();
            var context = new StoryContext { Dialogue = dialogue, Localization = LocalizationTable.FromJson("{\"greet_1\": \"Привет!\"}") };

            CoroutineTestUtil.RunToCompletion(script.Commands[0].Execute(context, new StoryPointer()));

            Assert.AreEqual("Привет!", dialogue.Calls[0].text);
        }

        [Test]
        public void DialogueLine_BeforeAnyLabel_UsesStartPseudoLabelForAutoId()
        {
            var script = new ScriptCompiler().Compile("Alice: Hello!\n");
            var dialogue = new RecordingDialoguePresenter();
            var context = new StoryContext { Dialogue = dialogue, Localization = LocalizationTable.FromJson("{\"_start_1\": \"Привет!\"}") };

            CoroutineTestUtil.RunToCompletion(script.Commands[0].Execute(context, new StoryPointer()));

            Assert.AreEqual("Привет!", dialogue.Calls[0].text);
        }

        [Test]
        public void Label_ResetsAutoIdOrdinalForFollowingLines()
        {
            var script = new ScriptCompiler().Compile("label first\nAlice: One\nAlice: Two\nlabel second\nAlice: Three\n");
            var dialogue = new RecordingDialoguePresenter();
            var localization = LocalizationTable.FromJson(@"{
                ""first_1"": ""Один"",
                ""first_2"": ""Два"",
                ""second_1"": ""Три""
            }");
            var context = new StoryContext { Dialogue = dialogue, Localization = localization };
            var controller = new PlaybackController(script, context);

            CoroutineTestUtil.RunToCompletion(controller.RunAll());

            CollectionAssert.AreEqual(new[] { "Один", "Два", "Три" }, dialogue.Calls.ConvertAll(c => c.text));
        }

        [Test]
        public void ChoiceOption_ExplicitAndAutoIdsShareOrdinalCounterWithDialogueLines()
        {
            const string source =
                "label pick\n" +
                "choice\n" +
                "  \"Yes\" @yes_opt -> yes_label\n" +
                "  \"No\" -> no_label\n" +
                "label yes_label\n" +
                "return\n" +
                "label no_label\n" +
                "return\n";
            var script = new ScriptCompiler().Compile(source);
            var choices = new RecordingChoicePresenter { NextSelection = 0 };
            var localization = LocalizationTable.FromJson("{\"yes_opt\": \"Да\", \"pick_2\": \"Нет\"}");
            var context = new StoryContext { Choices = choices, Localization = localization };

            CoroutineTestUtil.RunToCompletion(script.Commands[0].Execute(context, new StoryPointer()));

            CollectionAssert.AreEqual(new[] { "Да", "Нет" }, choices.Calls[0]);
        }

        [Test]
        public void DuplicateExplicitId_ThrowsParseException()
        {
            var ex = Assert.Throws<ParseException>(() =>
                new ScriptCompiler().Compile("label dup\nAlice: Hello! @dup_id\nAlice: Hi again! @dup_id\n"));

            StringAssert.Contains("dup_id", ex.Message);
        }

        [Test]
        public void ExplicitIdCollidingWithAutoGeneratedId_ThrowsParseException()
        {
            var ex = Assert.Throws<ParseException>(() =>
                new ScriptCompiler().Compile("label dup2\nAlice: First auto line.\nAlice: Second line! @dup2_1\n"));

            StringAssert.Contains("dup2_1", ex.Message);
        }

        [Test]
        public void FullScript_WithMixedAutoAndExplicitIds_PartialTranslation_ResolvesThroughPlaybackController()
        {
            const string source = @"
label opening
  Alice: Hello there! @greeting
  Alice: How are you?

  choice
    ""Good, thanks!"" -> good_path
    ""Not great..."" @choice_bad -> bad_path

label good_path
  Alice: Glad to hear it!
  jump the_end

label bad_path
  Alice: What happened?
  jump the_end

label the_end
  Alice: The end.
";
            var script = new ScriptCompiler().Compile(source);
            var dialogue = new RecordingDialoguePresenter();
            var choices = new RecordingChoicePresenter { NextSelection = 1 };
            var localization = LocalizationTable.FromJson(@"{
                ""greeting"": ""Привет!"",
                ""opening_2"": ""Как дела?"",
                ""opening_3"": ""Хорошо, спасибо!"",
                ""choice_bad"": ""Не очень..."",
                ""good_path_1"": ""Приятно слышать!"",
                ""the_end_1"": ""Конец.""
            }");
            var context = new StoryContext { Dialogue = dialogue, Choices = choices, Localization = localization };
            var controller = new PlaybackController(script, context);

            LogAssert.Expect(LogType.Warning, "NovelForge: no translation for line id 'bad_path_1' — falling back to source text.");
            CoroutineTestUtil.RunToCompletion(controller.RunAll());

            CollectionAssert.AreEqual(new[] { "Хорошо, спасибо!", "Не очень..." }, choices.Calls[0]);
            CollectionAssert.AreEqual(
                new[] { "Привет!", "Как дела?", "What happened?", "Конец." },
                dialogue.Calls.ConvertAll(c => c.text));
            Assert.IsTrue(controller.IsFinished);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify the new/updated tests fail**

Run the EditMode test command from Global Constraints.
Expected: compile error (constructors don't accept the new arguments yet; `StoryContext.Localization` and `LocalizationTable`-consuming production code don't exist on the command classes yet).

- [ ] **Step 5: Implement `StoryContext.Localization`**

In `Runtime/Commands/StoryContext.cs`, add one property to the class:

```csharp
public LocalizationTable Localization { get; set; }
```

Full file:

```csharp
namespace NovelForge.Runtime
{
    public class StoryContext
    {
        public VariableStore Variables { get; } = new VariableStore();
        public IDialoguePresenter Dialogue { get; set; }
        public IChoicePresenter Choices { get; set; }
        public IAudioPresenter Audio { get; set; }
        public IBackgroundPresenter Backgrounds { get; set; }
        public ITimingPresenter Timing { get; set; }
        public LocalizationTable Localization { get; set; }
    }
}
```

- [ ] **Step 6: Implement `SayLineCommand`'s id-aware constructor and resolution**

Replace `Runtime/Commands/Builtin/SayLineCommand.cs` with:

```csharp
using System.Collections;
using UnityEngine;

namespace NovelForge.Runtime
{
    public class SayLineCommand : Command
    {
        private readonly string _characterId;
        private readonly string _text;
        private readonly string _emotion;
        private readonly string _position;
        private readonly string _lineId;

        public SayLineCommand(string characterId, string text, string emotion, string position, string lineId)
        {
            _characterId = characterId;
            _text = text;
            _emotion = emotion;
            _position = position;
            _lineId = lineId;
        }

        public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
        {
            if (context.Dialogue == null)
            {
                Debug.LogError("NovelForge: no IDialoguePresenter wired — skipping dialogue line.");
                yield break;
            }
            yield return context.Dialogue.ShowLine(_characterId, ResolveText(context), _emotion, _position);
        }

        private string ResolveText(StoryContext context)
        {
            if (context.Localization == null)
                return _text;
            if (context.Localization.TryGetText(_lineId, out string translated))
                return translated;
            Debug.LogWarning($"NovelForge: no translation for line id '{_lineId}' — falling back to source text.");
            return _text;
        }
    }
}
```

- [ ] **Step 7: Implement `ChoiceCommand`'s id-aware constructor and resolution**

Replace `Runtime/Commands/Builtin/ChoiceCommand.cs` with:

```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NovelForge.Runtime
{
    public class ChoiceCommand : Command
    {
        private readonly IReadOnlyList<string> _optionTexts;
        private readonly IReadOnlyList<string> _optionIds;
        private readonly int[] _targetIndices;

        public ChoiceCommand(IReadOnlyList<string> optionTexts, IReadOnlyList<string> optionIds, int optionCount)
        {
            _optionTexts = optionTexts;
            _optionIds = optionIds;
            _targetIndices = new int[optionCount];
        }

        internal void ResolveTarget(int optionIndex, int targetIndex) => _targetIndices[optionIndex] = targetIndex;

        public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
        {
            if (context.Choices == null)
            {
                Debug.LogError("NovelForge: no IChoicePresenter wired — defaulting to option 0.");
                pointer.Current = _targetIndices[0];
                yield break;
            }

            int selected = -1;
            yield return context.Choices.PresentChoices(ResolveTexts(context), i => selected = i);

            if (selected < 0 || selected >= _targetIndices.Length)
            {
                Debug.LogError($"NovelForge: choice presenter returned invalid selection {selected} — defaulting to option 0.");
                selected = 0;
            }

            pointer.Current = _targetIndices[selected];
        }

        private IReadOnlyList<string> ResolveTexts(StoryContext context)
        {
            if (context.Localization == null)
                return _optionTexts;

            var resolved = new string[_optionTexts.Count];
            for (int i = 0; i < _optionTexts.Count; i++)
            {
                if (context.Localization.TryGetText(_optionIds[i], out string translated))
                {
                    resolved[i] = translated;
                }
                else
                {
                    Debug.LogWarning($"NovelForge: no translation for line id '{_optionIds[i]}' — falling back to source text.");
                    resolved[i] = _optionTexts[i];
                }
            }
            return resolved;
        }
    }
}
```

- [ ] **Step 8: Implement `ScriptCompiler` DSL support**

Replace `Runtime/Parsing/ScriptCompiler.cs` with:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace NovelForge.Runtime
{
    public class ScriptCompiler
    {
        private static readonly Regex DialogueLine = new(@"^([A-Za-z_][A-Za-z0-9_]*):\s+(.+)$", RegexOptions.Compiled);
        private static readonly Regex SetLine = new(@"^set\s+([A-Za-z_][A-Za-z0-9_]*)\s*(=|\+=|-=)\s*(.+)$", RegexOptions.Compiled);
        private static readonly Regex IfLine = new(@"^if\s+([A-Za-z_][A-Za-z0-9_]*)\s*(==|!=|>=|<=|>|<)\s*(.+)$", RegexOptions.Compiled);
        private static readonly Regex ChoiceOptionLine = new(@"^""([^""]*)""(?:\s*@([A-Za-z_][A-Za-z0-9_]*))?\s*->\s*([A-Za-z_][A-Za-z0-9_]*)$", RegexOptions.Compiled);

        private readonly CommandRegistry _registry;

        private enum BlockKind { If, Else }

        private struct PendingBlock
        {
            public BlockKind Kind;
            public ConditionalJumpCommand IfCommand;
            public JumpCommand ElseJumpCommand;
        }

        public ScriptCompiler(CommandRegistry registry = null)
        {
            _registry = registry ?? CommandRegistry.CreateDefault();
        }

        public NovelScript Compile(string source)
        {
            var commands = new List<Command>();
            var labels = new Dictionary<string, int>();
            // (commandIndex, targetLabel, choiceOptionIndex-or-null, sourceLineNumber)
            var pendingLabelRefs = new List<(int commandIndex, string labelName, int? choiceOption, int lineNumber)>();
            var blockStack = new Stack<PendingBlock>();
            string pendingComment = null;

            // Localization: every dialogue line and choice option gets a stable id, either
            // explicit (@tag) or auto-generated as "<nearest label>_<ordinal>". usedIds
            // enforces global uniqueness across the whole script (the translation table on
            // disk is one flat dictionary); currentLabel/lineIdOrdinal track auto-id state.
            var usedIds = new HashSet<string>();
            string currentLabel = "_start";
            int lineIdOrdinal = 0;

            string[] lines = source.Replace("\r\n", "\n").Split('\n');

            for (int lineNumber = 1; lineNumber <= lines.Length; lineNumber++)
            {
                string line = lines[lineNumber - 1].Trim();

                if (line.Length == 0)
                    continue;

                if (line.StartsWith("//"))
                {
                    pendingComment = line.Substring(2).Trim();
                    continue;
                }

                if (line.StartsWith("label ", StringComparison.Ordinal))
                {
                    string name = line.Substring("label ".Length).Trim();
                    if (labels.ContainsKey(name))
                        throw new ParseException(lineNumber, $"Duplicate label '{name}'.");
                    labels[name] = commands.Count;
                    currentLabel = name;
                    lineIdOrdinal = 0;
                    pendingComment = null;
                    continue;
                }

                if (line == "return")
                {
                    commands.Add(Attach(new ReturnCommand(), ref pendingComment));
                    continue;
                }

                if (line.StartsWith("jump ", StringComparison.Ordinal))
                {
                    string target = line.Substring("jump ".Length).Trim();
                    pendingLabelRefs.Add((commands.Count, target, null, lineNumber));
                    commands.Add(Attach(new JumpCommand(-1), ref pendingComment));
                    continue;
                }

                if (line.StartsWith("gosub ", StringComparison.Ordinal))
                {
                    string target = line.Substring("gosub ".Length).Trim();
                    pendingLabelRefs.Add((commands.Count, target, null, lineNumber));
                    commands.Add(Attach(new GosubCommand(-1), ref pendingComment));
                    continue;
                }

                var setMatch = SetLine.Match(line);
                if (setMatch.Success)
                {
                    string varName = setMatch.Groups[1].Value;
                    var op = setMatch.Groups[2].Value switch
                    {
                        "=" => VariableOperator.Assign,
                        "+=" => VariableOperator.Add,
                        "-=" => VariableOperator.Subtract,
                        _ => throw new ParseException(lineNumber, $"Unknown assignment operator '{setMatch.Groups[2].Value}'."),
                    };
                    object value = ParseLiteral(setMatch.Groups[3].Value.Trim(), lineNumber);
                    commands.Add(Attach(new SetVariableCommand(varName, op, value), ref pendingComment));
                    continue;
                }

                var ifMatch = IfLine.Match(line);
                if (ifMatch.Success)
                {
                    string varName = ifMatch.Groups[1].Value;
                    var op = ParseComparisonOperator(ifMatch.Groups[2].Value, lineNumber);
                    object value = ParseLiteral(ifMatch.Groups[3].Value.Trim(), lineNumber);
                    var cmd = new ConditionalJumpCommand(varName, op, value, -1);
                    blockStack.Push(new PendingBlock { Kind = BlockKind.If, IfCommand = cmd });
                    commands.Add(Attach(cmd, ref pendingComment));
                    continue;
                }

                if (line == "choice")
                {
                    var options = new List<(string text, string id, string labelName)>();
                    int lookahead = lineNumber;
                    while (lookahead < lines.Length)
                    {
                        string nextLine = lines[lookahead].Trim();
                        if (nextLine.Length == 0 || nextLine.StartsWith("//"))
                        {
                            lookahead++;
                            continue;
                        }
                        var optionMatch = ChoiceOptionLine.Match(nextLine);
                        if (!optionMatch.Success)
                            break;
                        string explicitOptionId = optionMatch.Groups[2].Success ? optionMatch.Groups[2].Value : null;
                        options.Add((optionMatch.Groups[1].Value, explicitOptionId, optionMatch.Groups[3].Value));
                        lookahead++;
                    }
                    if (options.Count == 0)
                        throw new ParseException(lineNumber, "'choice' has no options.");

                    var optionIds = new string[options.Count];
                    for (int i = 0; i < options.Count; i++)
                        optionIds[i] = AllocateId(options[i].id, ref lineIdOrdinal, currentLabel, usedIds, lineNumber);

                    var choiceCommand = new ChoiceCommand(options.ConvertAll(o => o.text), optionIds, options.Count);
                    for (int i = 0; i < options.Count; i++)
                        pendingLabelRefs.Add((commands.Count, options[i].labelName, i, lineNumber));
                    commands.Add(Attach(choiceCommand, ref pendingComment));

                    // lines[lineNumber .. lookahead-1] (0-indexed) were option lines already consumed;
                    // jump the 1-based cursor to lookahead so the next loop iteration (which does
                    // lineNumber++) resumes at lines[lookahead], the first unconsumed line.
                    lineNumber = lookahead;
                    continue;
                }

                if (line == "else")
                {
                    if (blockStack.Count == 0 || blockStack.Peek().Kind != BlockKind.If)
                        throw new ParseException(lineNumber, "'else' without a matching 'if'.");
                    var pending = blockStack.Pop();
                    var jumpToEndif = new JumpCommand(-1);
                    commands.Add(jumpToEndif);
                    pending.IfCommand.FalseTargetIndex = commands.Count;
                    blockStack.Push(new PendingBlock { Kind = BlockKind.Else, ElseJumpCommand = jumpToEndif });
                    continue;
                }

                if (line == "endif")
                {
                    if (blockStack.Count == 0)
                        throw new ParseException(lineNumber, "'endif' without a matching 'if'.");
                    var pending = blockStack.Pop();
                    if (pending.Kind == BlockKind.If)
                        pending.IfCommand.FalseTargetIndex = commands.Count;
                    else
                        pending.ElseJumpCommand.TargetIndex = commands.Count;
                    continue;
                }

                var dialogueMatch = DialogueLine.Match(line);
                if (dialogueMatch.Success)
                {
                    string characterId = dialogueMatch.Groups[1].Value;
                    (string text, string emotion, string position, string explicitId) = ParseDialogueRest(dialogueMatch.Groups[2].Value);
                    string lineId = AllocateId(explicitId, ref lineIdOrdinal, currentLabel, usedIds, lineNumber);
                    commands.Add(Attach(new SayLineCommand(characterId, text, emotion, position, lineId), ref pendingComment));
                    continue;
                }

                // Generic content command: "<name> <args...>" — bg/music/sfx/wait/cg and any
                // custom commands registered on the CommandRegistry passed to this compiler.
                int spaceIndex = line.IndexOf(' ');
                string commandName = spaceIndex < 0 ? line : line.Substring(0, spaceIndex);
                string rawArgs = spaceIndex < 0 ? string.Empty : line.Substring(spaceIndex + 1);
                Command generic;
                try
                {
                    if (!_registry.TryCreate(commandName, rawArgs, out generic))
                        throw new ParseException(lineNumber, $"Unknown command '{commandName}'.");
                }
                catch (Exception e) when (e is not ParseException)
                {
                    // A registered factory (e.g. "wait"'s float.Parse) can throw its own
                    // exception type on bad arguments — re-raise as a located ParseException
                    // so every parse failure, built-in or custom, carries a line number.
                    throw new ParseException(lineNumber, $"Command '{commandName}' rejected arguments '{rawArgs}': {e.Message}");
                }
                commands.Add(Attach(generic, ref pendingComment));
            }

            if (blockStack.Count > 0)
                throw new ParseException(lines.Length, "Unclosed 'if' block — missing 'endif'.");

            foreach (var (commandIndex, labelName, choiceOption, lineNumber) in pendingLabelRefs)
            {
                if (!labels.TryGetValue(labelName, out int targetIndex))
                    throw new ParseException(lineNumber, $"Undefined label '{labelName}'.");

                switch (commands[commandIndex])
                {
                    case JumpCommand jump:
                        jump.TargetIndex = targetIndex;
                        break;
                    case GosubCommand gosub:
                        gosub.TargetIndex = targetIndex;
                        break;
                    case ChoiceCommand choice:
                        choice.ResolveTarget(choiceOption.Value, targetIndex);
                        break;
                }
            }

            return new NovelScript(commands, labels);
        }

        private static Command Attach(Command command, ref string pendingComment)
        {
            command.SourceComment = pendingComment;
            pendingComment = null;
            return command;
        }

        private static string AllocateId(string explicitId, ref int ordinal, string currentLabel, HashSet<string> usedIds, int lineNumber)
        {
            ordinal++;
            string id = explicitId ?? $"{currentLabel}_{ordinal}";
            if (!usedIds.Add(id))
                throw new ParseException(lineNumber, $"Duplicate localization id '{id}'.");
            return id;
        }

        private static (string text, string emotion, string position, string id) ParseDialogueRest(string rest)
        {
            string emotion = null;
            string position = null;
            string id = null;
            string text = rest;

            var emotionMatch = Regex.Match(text, @"#(\w+)");
            if (emotionMatch.Success)
            {
                emotion = emotionMatch.Groups[1].Value;
                text = text.Remove(emotionMatch.Index, emotionMatch.Length).TrimEnd();
            }

            var idMatch = Regex.Match(text, @"@(\w+)");
            if (idMatch.Success)
            {
                id = idMatch.Groups[1].Value;
                text = text.Remove(idMatch.Index, idMatch.Length).TrimEnd();
            }

            // Known limitation: dialogue that legitimately ends in the literal word
            // "left"/"right"/"center" will be misread as a position tag. Acceptable
            // for this DSL's scope — rephrase the line if that ever comes up.
            foreach (string candidate in new[] { "left", "right", "center" })
            {
                var posMatch = Regex.Match(text, $@"\b{candidate}\b$");
                if (posMatch.Success)
                {
                    position = candidate;
                    text = text.Remove(posMatch.Index).TrimEnd();
                    break;
                }
            }

            return (text.Trim(), emotion, position, id);
        }

        private static ComparisonOperator ParseComparisonOperator(string token, int lineNumber) => token switch
        {
            "==" => ComparisonOperator.Equal,
            "!=" => ComparisonOperator.NotEqual,
            ">" => ComparisonOperator.GreaterThan,
            ">=" => ComparisonOperator.GreaterOrEqual,
            "<" => ComparisonOperator.LessThan,
            "<=" => ComparisonOperator.LessOrEqual,
            _ => throw new ParseException(lineNumber, $"Unknown comparison operator '{token}'."),
        };

        private static object ParseLiteral(string token, int lineNumber)
        {
            if (token == "true") return true;
            if (token == "false") return false;
            if (token.StartsWith("\"") && token.EndsWith("\"") && token.Length >= 2)
                return token.Substring(1, token.Length - 2);
            if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
                return i;
            if (float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                return f;
            throw new ParseException(lineNumber, $"Could not parse value '{token}' — expected true/false, a number, or a \"quoted string\".");
        }
    }
}
```

- [ ] **Step 9: Run tests to verify everything passes**

Run the EditMode test command from Global Constraints.
Expected: `failed="0"`, 12 new tests passing (2 in `ContentCommandsTests.cs` + 2 in `ChoiceCommandTests.cs` + 8 in `ScriptCompilerLocalizationTests.cs`), running total from 151: **163**. Also confirm the pre-existing `ScriptCompilerCoreTests.cs` and `ScriptCompilerChoiceAndIntegrationTests.cs` tests are still all green, unmodified.

- [ ] **Step 10: Commit**

```bash
git add Runtime/Commands/StoryContext.cs Runtime/Commands/Builtin/SayLineCommand.cs Runtime/Commands/Builtin/ChoiceCommand.cs Runtime/Parsing/ScriptCompiler.cs Tests/Runtime/ContentCommandsTests.cs Tests/Runtime/ChoiceCommandTests.cs Tests/Runtime/ScriptCompilerLocalizationTests.cs
git commit -m "$(cat <<'EOF'
Add id-aware localization to SayLineCommand/ChoiceCommand and DSL @id tag support

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```
