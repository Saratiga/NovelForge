# NovelForge Core: Scaffolding + Parser + VM — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up the NovelForge UPM package skeleton and build the core of the engine — a DSL parser/compiler and the flat-command VM that runs it — fully proven by EditMode tests, with no rendering/UI yet.

**Architecture:** DSL text is compiled by `ScriptCompiler` into a `NovelScript` (a flat `List<Command>` plus a label→index table). `PlaybackController` walks that list with an index and a call stack, executing each `Command` and auto-advancing unless the command moved the pointer itself (jump/gosub/return/choice/conditional). Content-facing commands (dialogue, audio, backgrounds, timing, choices) talk to small presenter interfaces (`IDialoguePresenter`, `IAudioPresenter`, etc.) instead of touching Unity objects directly, so this whole phase is testable with recording test doubles — real Unity-backed presenters arrive in later phases.

**Tech Stack:** Unity 6000.6.0f1, C# (Unity default API compatibility, .NET Standard 2.1), Unity Test Framework (NUnit) EditMode tests, UPM local package.

**Spec:** [docs/superpowers/specs/2026-09-05-novelforge-design.md](../specs/2026-09-05-novelforge-design.md)

## Global Constraints

- Unity Editor version: **6000.6.0f1**, installed via Unity Hub at `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe` (adjust only if Hub installed it elsewhere).
- Package id: `com.novelforge.core`, display name `NovelForge`, package root `G:\ClaudeProjects\NovelForge`.
- The companion `TestProject~` (used only to run EditMode tests against the package locally) is named with a trailing `~` deliberately: Unity's Package Manager and AssetDatabase ignore any folder ending in `~` when scanning a package's content. Since `TestProject~` lives *inside* the very package it references (`com.novelforge.core` resolves to `file:../..`, i.e. the package root two levels above `TestProject~/Packages/`), naming it without the `~` makes Unity treat the whole nested Unity project — its own `Library/`, `Temp/`, `Packages/`, csproj/sln artifacts — as package content, which causes an endless recompile/domain-reload loop. This was discovered during implementation of Task 1; do not rename it back.
- This phase is Runtime-only: no `NovelForge.UI` or `NovelForge.Editor` asmdefs yet — those arrive in later phases.
- Every command's execution touches Unity/game state only through a presenter interface — never directly. This is what keeps the VM unit-testable without Play Mode.
- Git commit messages end with: `Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>`
- Every `git add` of a specific new `.cs` file must also stage that file's Unity-generated `.meta` companion (`Foo.cs` + `Foo.cs.meta`) — Unity creates one alongside each asset the first time it refreshes, and a fresh clone without it gets a new, different GUID for that script. Tasks 2-5 missed this (their `git add` lines named bare file paths); a backfill commit added the missing `.meta` files after Task 5's review caught it. Tasks 6-10's `git add` lines below already include the `.meta` paths — keep that pattern for any task not listed here too. A `git add` of a whole folder (e.g. `git add Runtime/Parsing`) already recurses into any `.meta` files Unity created under it, so no special-casing is needed there.
- All EditMode tests run via:
  ```powershell
  & "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe" -batchmode -projectPath "G:\ClaudeProjects\NovelForge\TestProject~" -runTests -testPlatform EditMode -testResults "G:\ClaudeProjects\NovelForge\TestProject~\TestResults.xml" -logFile "G:\ClaudeProjects\NovelForge\TestProject~\Logs\RunTests.log"
  ```
  Deliberately no `-quit`: `-runTests` quits the Editor itself once tests finish, and pairing it with an explicit `-quit` races ahead of test execution in Unity 6000.6.0f1 — Unity exits cleanly right after its startup asset refresh, before the Test Runner ever starts, producing no `TestResults.xml` and no error (discovered during Task 1). `-logFile` points at a real file rather than `-logFile -`, since stdout capture of `-logFile -` was unreliable for diagnosing this. After each run, read `TestProject~\TestResults.xml` and confirm the `<test-run>` root element's `failed` attribute is `"0"` and `passed` matches the expected count for that step; if it's missing entirely, check `TestProject~\Logs\RunTests.log` for where the run actually stopped. If Unity prompts for license activation on first run, open the Editor once interactively (Unity Hub → NovelForge TestProject~) to sign in / activate a free Personal license before relying on batchmode.

---

### Task 1: Package scaffolding + TestProject~ + smoke test

**Files:**
- Create: `package.json`
- Create: `.gitignore`
- Create: `Runtime/NovelForge.Runtime.asmdef`
- Create: `Runtime/AssemblyInfo.cs`
- Create: `Tests/Runtime/NovelForge.Runtime.Tests.asmdef`
- Create: `Tests/Runtime/SmokeTests.cs`
- Create: `TestProject~/` (bootstrapped by Unity, then `TestProject~/Packages/manifest.json` edited)

**Interfaces:**
- Produces: an assembly `NovelForge.Runtime` (empty besides `AssemblyInfo.cs`) and `NovelForge.Runtime.Tests` (referencing it), and a working Unity Test Runner CLI pipeline every later task's steps depend on.

- [ ] **Step 1: Write `package.json`**

```json
{
  "name": "com.novelforge.core",
  "version": "0.1.0",
  "displayName": "NovelForge",
  "description": "Personal Unity toolkit for building visual novels: DSL-scripted dialogue, branching, save/load, localization, and a read/write branch-graph editor.",
  "unity": "6000.6",
  "author": {
    "name": "vova1"
  }
}
```

- [ ] **Step 2: Write `.gitignore`**

```
[Ll]ibrary/
[Tt]emp/
[Oo]bj/
[Bb]uild/
[Bb]uilds/
[Ll]ogs/
[Uu]serSettings/
[Mm]emoryCaptures/
*.csproj
*.sln
*.pidb
*.suo
*.user
*.userprefs
*.unityproj
*.tmp
TestResults.xml
```

- [ ] **Step 3: Write `Runtime/NovelForge.Runtime.asmdef`**

```json
{
    "name": "NovelForge.Runtime",
    "rootNamespace": "NovelForge.Runtime",
    "references": [],
    "includePlatforms": [],
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

- [ ] **Step 4: Write `Runtime/AssemblyInfo.cs`**

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("NovelForge.Runtime.Tests")]
```

This lets the test assembly patch `internal set` properties (jump/gosub/choice targets) that `ScriptCompiler` resolves after parsing, without making them part of the package's public API.

- [ ] **Step 5: Write `Tests/Runtime/NovelForge.Runtime.Tests.asmdef`**

```json
{
    "name": "NovelForge.Runtime.Tests",
    "rootNamespace": "NovelForge.Runtime.Tests",
    "references": [
        "NovelForge.Runtime"
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

- [ ] **Step 6: Write `Tests/Runtime/SmokeTests.cs`**

```csharp
using NUnit.Framework;

namespace NovelForge.Runtime.Tests
{
    public class SmokeTests
    {
        [Test]
        public void PackageAndTestProjectAreWiredCorrectly()
        {
            Assert.Pass();
        }
    }
}
```

- [ ] **Step 7: Bootstrap the TestProject~**

Run:
```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe" -batchmode -createProject "G:\ClaudeProjects\NovelForge\TestProject~" -quit -logFile -
```
Expected: exits cleanly, `TestProject~\Assets`, `TestProject~\Packages\manifest.json` and `TestProject~\ProjectSettings` now exist.

- [ ] **Step 8: Wire the local package into the TestProject~**

Open `TestProject~/Packages/manifest.json` and ensure the `dependencies` object contains (add alongside whatever Unity auto-generated; keep the existing `com.unity.modules.*` entries):

```json
"com.novelforge.core": "file:../..",
"com.unity.test-framework": "1.4.5",
"com.unity.ugui": "2.0.0"
```

and add a top-level `testables` array so Test Runner discovers our package's tests:

```json
"testables": [
  "com.novelforge.core"
]
```

If Package Manager later reports a different resolvable version for `com.unity.test-framework` or `com.unity.ugui`, accept its suggested version — the pin above is a starting point, not a hard requirement.

- [ ] **Step 9: Run the EditMode smoke test**

Run (no `-quit` — see Global Constraints for why):
```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe" -batchmode -projectPath "G:\ClaudeProjects\NovelForge\TestProject~" -runTests -testPlatform EditMode -testResults "G:\ClaudeProjects\NovelForge\TestProject~\TestResults.xml" -logFile "G:\ClaudeProjects\NovelForge\TestProject~\Logs\RunTests.log"
```
Expected: `TestProject~\TestResults.xml` exists, its `<test-run>` root has `total="1" passed="1" failed="0"`. If it instead reports zero tests found, check `TestProject~\Logs\RunTests.log` for where the run actually stopped before opening the Editor UI to investigate further.

- [ ] **Step 10: Commit**

```bash
git add package.json .gitignore Runtime Tests TestProject~/Packages/manifest.json TestProject~/ProjectSettings
git commit -m "$(cat <<'EOF'
Scaffold NovelForge package and EditMode test pipeline

Empty NovelForge.Runtime assembly, a TestProject~ that references it
as a local UPM package, and one smoke test proving Unity Test Runner
can build and run tests against the package via CLI.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

Note: do not add `TestProject~/Library`, `TestProject~/Temp`, or `TestProject~/Logs` — `.gitignore` excludes them.

---

### Task 2: VariableStore, presenter interfaces, StoryContext, NovelScript

**Files:**
- Create: `Runtime/Commands/VariableStore.cs`
- Create: `Runtime/Commands/Presenters/IDialoguePresenter.cs`
- Create: `Runtime/Commands/Presenters/IChoicePresenter.cs`
- Create: `Runtime/Commands/Presenters/IAudioPresenter.cs`
- Create: `Runtime/Commands/Presenters/IBackgroundPresenter.cs`
- Create: `Runtime/Commands/Presenters/ITimingPresenter.cs`
- Create: `Runtime/Commands/StoryContext.cs`
- Create: `Runtime/Commands/NovelScript.cs`
- Test: `Tests/Runtime/VariableStoreTests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `VariableStore` with `Set(string,object)`, `TryGet(string,out object)`, `GetInt/GetFloat/GetBool/GetString(string)`; presenter interfaces `IDialoguePresenter.ShowLine(string,string,string,string):IEnumerator`, `IChoicePresenter.PresentChoices(IReadOnlyList<string>,Action<int>):IEnumerator`, `IAudioPresenter.PlayMusic/PlaySfx(string):IEnumerator`, `IBackgroundPresenter.ShowBackground/ShowCg(string):IEnumerator`, `ITimingPresenter.Wait(float):IEnumerator`; `StoryContext` with a `Variables` property and settable `Dialogue/Choices/Audio/Backgrounds/Timing` properties; `NovelScript(IReadOnlyList<Command>, IReadOnlyDictionary<string,int>)` — used by every later task.

- [ ] **Step 1: Write the failing test for VariableStore**

```csharp
using NUnit.Framework;

namespace NovelForge.Runtime.Tests
{
    public class VariableStoreTests
    {
        [Test]
        public void MissingVariable_ReturnsTypeDefaults()
        {
            var store = new VariableStore();

            Assert.AreEqual(0, store.GetInt("missing"));
            Assert.AreEqual(0f, store.GetFloat("missing"));
            Assert.AreEqual(false, store.GetBool("missing"));
            Assert.AreEqual(string.Empty, store.GetString("missing"));
        }

        [Test]
        public void Set_ThenGet_RoundTripsEachType()
        {
            var store = new VariableStore();

            store.Set("relationship", 3);
            store.Set("ratio", 1.5f);
            store.Set("metAlice", true);
            store.Set("playerName", "Kai");

            Assert.AreEqual(3, store.GetInt("relationship"));
            Assert.AreEqual(1.5f, store.GetFloat("ratio"));
            Assert.IsTrue(store.GetBool("metAlice"));
            Assert.AreEqual("Kai", store.GetString("playerName"));
        }

        [Test]
        public void TryGet_ReflectsWhetherVariableWasSet()
        {
            var store = new VariableStore();
            store.Set("x", 1);

            Assert.IsTrue(store.TryGet("x", out _));
            Assert.IsFalse(store.TryGet("y", out _));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run the command from Global Constraints. Expected: build fails — `VariableStore` does not exist yet (0 passed besides the earlier smoke test, compile error reported in the log).

- [ ] **Step 3: Write `Runtime/Commands/VariableStore.cs`**

```csharp
using System;
using System.Collections.Generic;

namespace NovelForge.Runtime
{
    public class VariableStore
    {
        private readonly Dictionary<string, object> _values = new();

        public void Set(string name, object value) => _values[name] = value;

        public bool TryGet(string name, out object value) => _values.TryGetValue(name, out value);

        public int GetInt(string name) => _values.TryGetValue(name, out var v) ? Convert.ToInt32(v) : 0;

        public float GetFloat(string name) => _values.TryGetValue(name, out var v) ? Convert.ToSingle(v) : 0f;

        public bool GetBool(string name) => _values.TryGetValue(name, out var v) ? Convert.ToBoolean(v) : false;

        public string GetString(string name) => _values.TryGetValue(name, out var v) ? v.ToString() : string.Empty;
    }
}
```

- [ ] **Step 4: Write the presenter interfaces**

`Runtime/Commands/Presenters/IDialoguePresenter.cs`:
```csharp
using System.Collections;

namespace NovelForge.Runtime
{
    public interface IDialoguePresenter
    {
        IEnumerator ShowLine(string characterId, string text, string emotion, string position);
    }
}
```

`Runtime/Commands/Presenters/IChoicePresenter.cs`:
```csharp
using System;
using System.Collections;
using System.Collections.Generic;

namespace NovelForge.Runtime
{
    public interface IChoicePresenter
    {
        IEnumerator PresentChoices(IReadOnlyList<string> optionTexts, Action<int> onSelected);
    }
}
```

`Runtime/Commands/Presenters/IAudioPresenter.cs`:
```csharp
using System.Collections;

namespace NovelForge.Runtime
{
    public interface IAudioPresenter
    {
        IEnumerator PlayMusic(string trackId);
        IEnumerator PlaySfx(string clipId);
    }
}
```

`Runtime/Commands/Presenters/IBackgroundPresenter.cs`:
```csharp
using System.Collections;

namespace NovelForge.Runtime
{
    public interface IBackgroundPresenter
    {
        IEnumerator ShowBackground(string backgroundId);
        IEnumerator ShowCg(string cgId);
    }
}
```

`Runtime/Commands/Presenters/ITimingPresenter.cs`:
```csharp
using System.Collections;

namespace NovelForge.Runtime
{
    public interface ITimingPresenter
    {
        IEnumerator Wait(float seconds);
    }
}
```

- [ ] **Step 5: Write `Runtime/Commands/StoryContext.cs`**

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
    }
}
```

- [ ] **Step 6: Write `Runtime/Commands/NovelScript.cs`**

```csharp
using System.Collections.Generic;

namespace NovelForge.Runtime
{
    public class NovelScript
    {
        public IReadOnlyList<Command> Commands { get; }
        public IReadOnlyDictionary<string, int> Labels { get; }

        public NovelScript(IReadOnlyList<Command> commands, IReadOnlyDictionary<string, int> labels)
        {
            Commands = commands;
            Labels = labels;
        }
    }
}
```

`Command` does not exist yet — this will not compile until Task 3. That is expected; proceed to Step 7 only after Task 3's `Command` class exists. (If running tests strictly after each task, skip the run here and do it once at the end of Task 3 instead — call this out to whoever executes the plan.)

- [ ] **Step 7: Run tests to verify VariableStoreTests passes**

Run the command from Global Constraints (after Task 3's `Command` class exists, so the assembly compiles). Expected: `total="4" passed="4" failed="0"` (1 smoke test + 3 VariableStore tests — `NovelScript.cs` and `StoryContext.cs` add no tests of their own, they're exercised indirectly starting in Task 3).

- [ ] **Step 8: Commit**

```bash
git add Runtime/Commands Tests/Runtime/VariableStoreTests.cs
git commit -m "$(cat <<'EOF'
Add VariableStore, presenter interfaces, StoryContext, NovelScript

Data model and presenter contracts the VM and commands build on.
Presenters keep command execution decoupled from real Unity
rendering, so the VM stays testable without Play Mode.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: Command base, IStoryPointer, StoryPointer, PlaybackController, coroutine test util, recording doubles

**Files:**
- Create: `Runtime/Commands/Command.cs`
- Create: `Runtime/Commands/IStoryPointer.cs`
- Create: `Runtime/Commands/StoryPointer.cs`
- Create: `Runtime/Commands/PlaybackController.cs`
- Create: `Tests/Runtime/CoroutineTestUtil.cs`
- Create: `Tests/Runtime/Doubles/RecordingDialoguePresenter.cs`
- Create: `Tests/Runtime/Doubles/RecordingChoicePresenter.cs`
- Create: `Tests/Runtime/Doubles/RecordingAudioPresenter.cs`
- Create: `Tests/Runtime/Doubles/RecordingBackgroundPresenter.cs`
- Create: `Tests/Runtime/Doubles/RecordingTimingPresenter.cs`
- Test: `Tests/Runtime/PlaybackControllerTests.cs`

**Interfaces:**
- Consumes: `StoryContext`, `NovelScript` (Task 2).
- Produces: `abstract class Command { string SourceComment; abstract IEnumerator Execute(StoryContext, IStoryPointer); }`; `IStoryPointer { int Current; void Push(int); bool TryPop(out int); }`; public `StoryPointer : IStoryPointer`; `PlaybackController(NovelScript, StoryContext)` with `int CurrentIndex`, `bool IsFinished`, `IEnumerator RunAll()` — every built-in command (Tasks 4-7) implements `Command` against this contract, and every compiler test (Tasks 8-10) drives scripts through `PlaybackController`.

- [ ] **Step 1: Write `Runtime/Commands/Command.cs`**

```csharp
using System.Collections;

namespace NovelForge.Runtime
{
    public abstract class Command
    {
        public string SourceComment { get; set; }

        public abstract IEnumerator Execute(StoryContext context, IStoryPointer pointer);
    }
}
```

- [ ] **Step 2: Write `Runtime/Commands/IStoryPointer.cs`**

```csharp
namespace NovelForge.Runtime
{
    public interface IStoryPointer
    {
        int Current { get; set; }
        void Push(int returnIndex);
        bool TryPop(out int returnIndex);
    }
}
```

- [ ] **Step 3: Write `Runtime/Commands/StoryPointer.cs`**

```csharp
using System.Collections.Generic;

namespace NovelForge.Runtime
{
    public class StoryPointer : IStoryPointer
    {
        private readonly Stack<int> _callStack = new();

        public int Current { get; set; }

        public void Push(int returnIndex) => _callStack.Push(returnIndex);

        public bool TryPop(out int returnIndex) => _callStack.TryPop(out returnIndex);
    }
}
```

- [ ] **Step 4: Write `Tests/Runtime/CoroutineTestUtil.cs`**

```csharp
using System.Collections;

namespace NovelForge.Runtime.Tests
{
    public static class CoroutineTestUtil
    {
        public static void RunToCompletion(IEnumerator routine)
        {
            while (routine.MoveNext())
            {
                if (routine.Current is IEnumerator nested)
                    RunToCompletion(nested);
            }
        }
    }
}
```

This drains nested `IEnumerator` yields synchronously (the same shape Unity's `StartCoroutine` handles at runtime), so EditMode tests can run a full command/VM cycle without Play Mode.

- [ ] **Step 5: Write the recording presenter doubles**

`Tests/Runtime/Doubles/RecordingDialoguePresenter.cs`:
```csharp
using System.Collections;
using System.Collections.Generic;

namespace NovelForge.Runtime.Tests
{
    public class RecordingDialoguePresenter : IDialoguePresenter
    {
        public readonly List<(string characterId, string text, string emotion, string position)> Calls = new();

        public IEnumerator ShowLine(string characterId, string text, string emotion, string position)
        {
            Calls.Add((characterId, text, emotion, position));
            yield break;
        }
    }
}
```

`Tests/Runtime/Doubles/RecordingChoicePresenter.cs`:
```csharp
using System;
using System.Collections;
using System.Collections.Generic;

namespace NovelForge.Runtime.Tests
{
    public class RecordingChoicePresenter : IChoicePresenter
    {
        public readonly List<IReadOnlyList<string>> Calls = new();
        public int NextSelection;

        public IEnumerator PresentChoices(IReadOnlyList<string> optionTexts, Action<int> onSelected)
        {
            Calls.Add(optionTexts);
            onSelected(NextSelection);
            yield break;
        }
    }
}
```

`Tests/Runtime/Doubles/RecordingAudioPresenter.cs`:
```csharp
using System.Collections;
using System.Collections.Generic;

namespace NovelForge.Runtime.Tests
{
    public class RecordingAudioPresenter : IAudioPresenter
    {
        public readonly List<string> MusicCalls = new();
        public readonly List<string> SfxCalls = new();

        public IEnumerator PlayMusic(string trackId)
        {
            MusicCalls.Add(trackId);
            yield break;
        }

        public IEnumerator PlaySfx(string clipId)
        {
            SfxCalls.Add(clipId);
            yield break;
        }
    }
}
```

`Tests/Runtime/Doubles/RecordingBackgroundPresenter.cs`:
```csharp
using System.Collections;
using System.Collections.Generic;

namespace NovelForge.Runtime.Tests
{
    public class RecordingBackgroundPresenter : IBackgroundPresenter
    {
        public readonly List<string> BackgroundCalls = new();
        public readonly List<string> CgCalls = new();

        public IEnumerator ShowBackground(string backgroundId)
        {
            BackgroundCalls.Add(backgroundId);
            yield break;
        }

        public IEnumerator ShowCg(string cgId)
        {
            CgCalls.Add(cgId);
            yield break;
        }
    }
}
```

`Tests/Runtime/Doubles/RecordingTimingPresenter.cs`:
```csharp
using System.Collections;
using System.Collections.Generic;

namespace NovelForge.Runtime.Tests
{
    public class RecordingTimingPresenter : ITimingPresenter
    {
        public readonly List<float> Calls = new();

        public IEnumerator Wait(float seconds)
        {
            Calls.Add(seconds);
            yield break;
        }
    }
}
```

These have no branching logic, so they are not individually TDD'd — they're exercised (and thereby verified) by every test from here on that asserts on `.Calls`.

- [ ] **Step 6: Write the failing test for PlaybackController**

```csharp
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;

namespace NovelForge.Runtime.Tests
{
    public class PlaybackControllerTests
    {
        private class RecordingPointerCommand : Command
        {
            private readonly System.Action<IStoryPointer> _action;
            public RecordingPointerCommand(System.Action<IStoryPointer> action) => _action = action;

            public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
            {
                _action(pointer);
                yield break;
            }
        }

        [Test]
        public void RunAll_AutoIncrementsWhenCommandDoesNotMovePointer()
        {
            var calls = new List<int>();
            var commands = new Command[]
            {
                new RecordingPointerCommand(p => calls.Add(0)),
                new RecordingPointerCommand(p => calls.Add(1)),
                new RecordingPointerCommand(p => calls.Add(2)),
            };
            var script = new NovelScript(commands, new Dictionary<string, int>());
            var controller = new PlaybackController(script, new StoryContext());

            CoroutineTestUtil.RunToCompletion(controller.RunAll());

            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, calls);
            Assert.IsTrue(controller.IsFinished);
        }

        [Test]
        public void RunAll_DoesNotAutoIncrementWhenCommandMovesPointer()
        {
            var calls = new List<int>();
            var commands = new Command[]
            {
                new RecordingPointerCommand(p => { calls.Add(0); p.Current = 2; }),
                new RecordingPointerCommand(p => calls.Add(1)),
                new RecordingPointerCommand(p => calls.Add(2)),
            };
            var script = new NovelScript(commands, new Dictionary<string, int>());
            var controller = new PlaybackController(script, new StoryContext());

            CoroutineTestUtil.RunToCompletion(controller.RunAll());

            CollectionAssert.AreEqual(new[] { 0, 2 }, calls);
        }

        [Test]
        public void PushAndTryPop_SupportGosubReturnStack()
        {
            var commands = new Command[]
            {
                new RecordingPointerCommand(p => { p.Push(2); p.Current = 1; }),
                new RecordingPointerCommand(p => { p.TryPop(out int ret); p.Current = ret; }),
                new RecordingPointerCommand(p => { }),
            };
            var script = new NovelScript(commands, new Dictionary<string, int>());
            var controller = new PlaybackController(script, new StoryContext());

            CoroutineTestUtil.RunToCompletion(controller.RunAll());

            Assert.IsTrue(controller.IsFinished);
        }
    }
}
```

Trace for `PushAndTryPop_SupportGosubReturnStack`: index 0 pushes return-index 2 and jumps to 1 (no auto-increment, pointer moved); index 1 pops (gets 2) and jumps there (no auto-increment); index 2 is a no-op, so its pointer is unchanged and it auto-increments to 3 — `IsFinished` (3 >= `Commands.Count` == 3). Terminates in a single pass. (An earlier version of this test revisited index 2 a second time after the stack was already empty, so `TryPop` silently returned the default `0` and sent the pointer back to 0 — a real infinite loop, only visible once the VM actually executes rather than just compiles. Caught during Task 3's execution; if you're implementing this from an older copy of the plan, use the sequence above.)

- [ ] **Step 7: Run test to verify it fails**

Run the command from Global Constraints. Expected: compile error — `PlaybackController` does not exist yet.

- [ ] **Step 8: Write `Runtime/Commands/PlaybackController.cs`**

```csharp
using System.Collections;

namespace NovelForge.Runtime
{
    public class PlaybackController
    {
        private readonly NovelScript _script;
        private readonly StoryContext _context;
        private readonly StoryPointer _pointer = new();

        public PlaybackController(NovelScript script, StoryContext context)
        {
            _script = script;
            _context = context;
        }

        public int CurrentIndex => _pointer.Current;
        public bool IsFinished => _pointer.Current >= _script.Commands.Count;

        public IEnumerator RunAll()
        {
            while (!IsFinished)
                yield return StepOnce();
        }

        private IEnumerator StepOnce()
        {
            int before = _pointer.Current;
            Command command = _script.Commands[before];
            yield return command.Execute(_context, _pointer);
            if (_pointer.Current == before)
                _pointer.Current = before + 1;
        }
    }
}
```

- [ ] **Step 9: Run tests to verify they pass**

Run the command from Global Constraints. Expected: `total="7" passed="7" failed="0"` (1 smoke + 3 VariableStore + 3 PlaybackController).

- [ ] **Step 10: Commit**

```bash
git add Runtime/Commands/Command.cs Runtime/Commands/IStoryPointer.cs Runtime/Commands/StoryPointer.cs Runtime/Commands/PlaybackController.cs Tests/Runtime/CoroutineTestUtil.cs Tests/Runtime/Doubles Tests/Runtime/PlaybackControllerTests.cs
git commit -m "$(cat <<'EOF'
Add Command base, StoryPointer, PlaybackController, test doubles

Core VM stepping logic (auto-increment unless a command moves the
pointer itself; push/pop for gosub-return), proven with a hand-built
fake command so it's tested independently of any real command or
the parser.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: JumpCommand, GosubCommand, ReturnCommand

**Files:**
- Create: `Runtime/Commands/Builtin/JumpCommand.cs`
- Create: `Runtime/Commands/Builtin/GosubCommand.cs`
- Create: `Runtime/Commands/Builtin/ReturnCommand.cs`
- Test: `Tests/Runtime/ControlFlowCommandsTests.cs`

**Interfaces:**
- Consumes: `Command`, `IStoryPointer`, `StoryPointer` (Task 3).
- Produces: `JumpCommand(int targetIndex)` with `int TargetIndex { get; internal set; }`; `GosubCommand(int targetIndex)` with the same shape; `ReturnCommand()` — the compiler (Tasks 8-10) constructs these with a placeholder target and patches `TargetIndex` via the `internal` setter once labels are resolved.

- [ ] **Step 1: Write the failing tests**

```csharp
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NovelForge.Runtime.Tests
{
    public class ControlFlowCommandsTests
    {
        [Test]
        public void Jump_MovesPointerToTargetIndex()
        {
            var pointer = new StoryPointer { Current = 0 };
            var command = new JumpCommand(5);

            CoroutineTestUtil.RunToCompletion(command.Execute(new StoryContext(), pointer));

            Assert.AreEqual(5, pointer.Current);
        }

        [Test]
        public void Gosub_PushesReturnIndexAndJumps()
        {
            var pointer = new StoryPointer { Current = 3 };
            var command = new GosubCommand(10);

            CoroutineTestUtil.RunToCompletion(command.Execute(new StoryContext(), pointer));

            Assert.AreEqual(10, pointer.Current);
            Assert.IsTrue(pointer.TryPop(out int returnIndex));
            Assert.AreEqual(4, returnIndex);
        }

        [Test]
        public void Return_PopsStackAndJumpsBack()
        {
            var pointer = new StoryPointer();
            pointer.Push(7);
            var command = new ReturnCommand();

            CoroutineTestUtil.RunToCompletion(command.Execute(new StoryContext(), pointer));

            Assert.AreEqual(7, pointer.Current);
        }

        [Test]
        public void Return_WithEmptyStack_LogsErrorAndEndsPlayback()
        {
            var pointer = new StoryPointer();
            var command = new ReturnCommand();

            LogAssert.Expect(LogType.Error, "NovelForge: 'return' with an empty call stack — ending playback.");
            CoroutineTestUtil.RunToCompletion(command.Execute(new StoryContext(), pointer));

            Assert.GreaterOrEqual(pointer.Current, int.MaxValue);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the command from Global Constraints. Expected: compile errors — `JumpCommand`, `GosubCommand`, `ReturnCommand` do not exist yet.

- [ ] **Step 3: Write the three command classes**

`Runtime/Commands/Builtin/JumpCommand.cs`:
```csharp
using System.Collections;

namespace NovelForge.Runtime
{
    public class JumpCommand : Command
    {
        public int TargetIndex { get; internal set; }

        public JumpCommand(int targetIndex) => TargetIndex = targetIndex;

        public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
        {
            pointer.Current = TargetIndex;
            yield break;
        }
    }
}
```

`Runtime/Commands/Builtin/GosubCommand.cs`:
```csharp
using System.Collections;

namespace NovelForge.Runtime
{
    public class GosubCommand : Command
    {
        public int TargetIndex { get; internal set; }

        public GosubCommand(int targetIndex) => TargetIndex = targetIndex;

        public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
        {
            pointer.Push(pointer.Current + 1);
            pointer.Current = TargetIndex;
            yield break;
        }
    }
}
```

`Runtime/Commands/Builtin/ReturnCommand.cs`:
```csharp
using System.Collections;
using UnityEngine;

namespace NovelForge.Runtime
{
    public class ReturnCommand : Command
    {
        public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
        {
            if (pointer.TryPop(out int returnIndex))
            {
                pointer.Current = returnIndex;
            }
            else
            {
                Debug.LogError("NovelForge: 'return' with an empty call stack — ending playback.");
                // Any index >= Commands.Count marks playback finished; MaxValue is a
                // safe sentinel here since ReturnCommand has no reference to the script length.
                pointer.Current = int.MaxValue;
            }
            yield break;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run the command from Global Constraints. Expected: `failed="0"`, 4 more tests passing than the previous run.

- [ ] **Step 5: Commit**

```bash
git add Runtime/Commands/Builtin/JumpCommand.cs Runtime/Commands/Builtin/GosubCommand.cs Runtime/Commands/Builtin/ReturnCommand.cs Tests/Runtime/ControlFlowCommandsTests.cs
git commit -m "$(cat <<'EOF'
Add Jump, Gosub, Return commands

Return with an empty call stack logs an error and ends playback
instead of throwing, consistent with the spec's no-crash-in-runtime
policy.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: SetVariableCommand, ConditionalJumpCommand

**Files:**
- Create: `Runtime/Commands/Builtin/SetVariableCommand.cs`
- Create: `Runtime/Commands/Builtin/ConditionalJumpCommand.cs`
- Test: `Tests/Runtime/VariableAndConditionCommandsTests.cs`

**Interfaces:**
- Consumes: `Command`, `VariableStore` (Tasks 2-3).
- Produces: `enum VariableOperator { Assign, Add, Subtract }`; `SetVariableCommand(string variableName, VariableOperator op, object value)`; `enum ComparisonOperator { Equal, NotEqual, GreaterThan, GreaterOrEqual, LessThan, LessOrEqual }`; `ConditionalJumpCommand(string variableName, ComparisonOperator op, object value, int falseTargetIndex)` with `int FalseTargetIndex { get; internal set; }` — the compiler (Tasks 8-9) parses `set`/`if` lines into these and patches `FalseTargetIndex` when it finds the matching `else`/`endif`.

- [ ] **Step 1: Write the failing tests**

```csharp
using NUnit.Framework;

namespace NovelForge.Runtime.Tests
{
    public class VariableAndConditionCommandsTests
    {
        [Test]
        public void SetVariable_Assign_SetsExactValue()
        {
            var context = new StoryContext();
            var pointer = new StoryPointer();
            var command = new SetVariableCommand("playerName", VariableOperator.Assign, "Kai");

            CoroutineTestUtil.RunToCompletion(command.Execute(context, pointer));

            Assert.AreEqual("Kai", context.Variables.GetString("playerName"));
        }

        [Test]
        public void SetVariable_Add_IncrementsExistingValue()
        {
            var context = new StoryContext();
            context.Variables.Set("relationship", 2);
            var pointer = new StoryPointer();
            var command = new SetVariableCommand("relationship", VariableOperator.Add, 1);

            CoroutineTestUtil.RunToCompletion(command.Execute(context, pointer));

            Assert.AreEqual(3, context.Variables.GetInt("relationship"));
        }

        [Test]
        public void SetVariable_Subtract_DecrementsExistingValue()
        {
            var context = new StoryContext();
            context.Variables.Set("relationship", 2);
            var pointer = new StoryPointer();
            var command = new SetVariableCommand("relationship", VariableOperator.Subtract, 1);

            CoroutineTestUtil.RunToCompletion(command.Execute(context, pointer));

            Assert.AreEqual(1, context.Variables.GetInt("relationship"));
        }

        [Test]
        public void ConditionalJump_NumericTrue_DoesNotJump()
        {
            var context = new StoryContext();
            context.Variables.Set("relationship", 5);
            var pointer = new StoryPointer { Current = 0 };
            var command = new ConditionalJumpCommand("relationship", ComparisonOperator.GreaterOrEqual, 3, falseTargetIndex: 10);

            CoroutineTestUtil.RunToCompletion(command.Execute(context, pointer));

            Assert.AreEqual(0, pointer.Current);
        }

        [Test]
        public void ConditionalJump_NumericFalse_JumpsToFalseTarget()
        {
            var context = new StoryContext();
            context.Variables.Set("relationship", 1);
            var pointer = new StoryPointer { Current = 0 };
            var command = new ConditionalJumpCommand("relationship", ComparisonOperator.GreaterOrEqual, 3, falseTargetIndex: 10);

            CoroutineTestUtil.RunToCompletion(command.Execute(context, pointer));

            Assert.AreEqual(10, pointer.Current);
        }

        [Test]
        public void ConditionalJump_StringEquality_Works()
        {
            var context = new StoryContext();
            context.Variables.Set("route", "good");
            var pointer = new StoryPointer { Current = 0 };
            var command = new ConditionalJumpCommand("route", ComparisonOperator.Equal, "good", falseTargetIndex: 10);

            CoroutineTestUtil.RunToCompletion(command.Execute(context, pointer));

            Assert.AreEqual(0, pointer.Current);
        }

        [Test]
        public void ConditionalJump_BoolEquality_Works()
        {
            var context = new StoryContext();
            context.Variables.Set("metAlice", true);
            var pointer = new StoryPointer { Current = 0 };
            var command = new ConditionalJumpCommand("metAlice", ComparisonOperator.Equal, false, falseTargetIndex: 10);

            CoroutineTestUtil.RunToCompletion(command.Execute(context, pointer));

            Assert.AreEqual(10, pointer.Current);
        }

        [Test]
        public void ConditionalJump_GreaterThanOnString_Throws()
        {
            var context = new StoryContext();
            context.Variables.Set("route", "good");
            var pointer = new StoryPointer { Current = 0 };
            var command = new ConditionalJumpCommand("route", ComparisonOperator.GreaterThan, "good", falseTargetIndex: 10);

            Assert.Throws<System.InvalidOperationException>(() =>
                CoroutineTestUtil.RunToCompletion(command.Execute(context, pointer)));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the command from Global Constraints. Expected: compile errors — the two command types do not exist yet.

- [ ] **Step 3: Write `Runtime/Commands/Builtin/SetVariableCommand.cs`**

```csharp
using System;
using System.Collections;

namespace NovelForge.Runtime
{
    public enum VariableOperator { Assign, Add, Subtract }

    public class SetVariableCommand : Command
    {
        private readonly string _variableName;
        private readonly VariableOperator _op;
        private readonly object _value;

        public SetVariableCommand(string variableName, VariableOperator op, object value)
        {
            _variableName = variableName;
            _op = op;
            _value = value;
        }

        public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
        {
            switch (_op)
            {
                case VariableOperator.Assign:
                    context.Variables.Set(_variableName, _value);
                    break;
                case VariableOperator.Add:
                    // Arithmetic always normalizes to float internally; GetInt() still
                    // works afterwards because Convert.ToInt32 accepts a float source.
                    context.Variables.Set(_variableName, context.Variables.GetFloat(_variableName) + Convert.ToSingle(_value));
                    break;
                case VariableOperator.Subtract:
                    context.Variables.Set(_variableName, context.Variables.GetFloat(_variableName) - Convert.ToSingle(_value));
                    break;
            }
            yield break;
        }
    }
}
```

- [ ] **Step 4: Write `Runtime/Commands/Builtin/ConditionalJumpCommand.cs`**

```csharp
using System;
using System.Collections;

namespace NovelForge.Runtime
{
    public enum ComparisonOperator { Equal, NotEqual, GreaterThan, GreaterOrEqual, LessThan, LessOrEqual }

    public class ConditionalJumpCommand : Command
    {
        private readonly string _variableName;
        private readonly ComparisonOperator _op;
        private readonly object _value;

        public int FalseTargetIndex { get; internal set; }

        public ConditionalJumpCommand(string variableName, ComparisonOperator op, object value, int falseTargetIndex)
        {
            _variableName = variableName;
            _op = op;
            _value = value;
            FalseTargetIndex = falseTargetIndex;
        }

        public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
        {
            if (!Evaluate(context.Variables))
                pointer.Current = FalseTargetIndex;
            yield break;
        }

        private bool Evaluate(VariableStore variables)
        {
            if (_value is string s)
            {
                string current = variables.GetString(_variableName);
                return _op switch
                {
                    ComparisonOperator.Equal => current == s,
                    ComparisonOperator.NotEqual => current != s,
                    _ => throw new InvalidOperationException($"Operator {_op} is not supported for string comparisons."),
                };
            }

            if (_value is bool b)
            {
                bool current = variables.GetBool(_variableName);
                return _op switch
                {
                    ComparisonOperator.Equal => current == b,
                    ComparisonOperator.NotEqual => current != b,
                    _ => throw new InvalidOperationException($"Operator {_op} is not supported for bool comparisons."),
                };
            }

            float currentNumber = variables.GetFloat(_variableName);
            float compareNumber = Convert.ToSingle(_value);
            return _op switch
            {
                ComparisonOperator.Equal => currentNumber == compareNumber,
                ComparisonOperator.NotEqual => currentNumber != compareNumber,
                ComparisonOperator.GreaterThan => currentNumber > compareNumber,
                ComparisonOperator.GreaterOrEqual => currentNumber >= compareNumber,
                ComparisonOperator.LessThan => currentNumber < compareNumber,
                ComparisonOperator.LessOrEqual => currentNumber <= compareNumber,
                _ => throw new InvalidOperationException($"Unhandled operator {_op}."),
            };
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run the command from Global Constraints. Expected: `failed="0"`, 8 more tests passing than the previous run.

- [ ] **Step 6: Commit**

```bash
git add Runtime/Commands/Builtin/SetVariableCommand.cs Runtime/Commands/Builtin/ConditionalJumpCommand.cs Tests/Runtime/VariableAndConditionCommandsTests.cs
git commit -m "$(cat <<'EOF'
Add SetVariable and ConditionalJump commands

if/else/endif compiles to ConditionalJumpCommand; comparisons are
restricted to equality for strings/bools and full ordering for
numbers, matching the spec's deliberately simple condition grammar.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 6: SayLineCommand and the simple presenter-delegating commands

**Files:**
- Create: `Runtime/Commands/Builtin/SayLineCommand.cs`
- Create: `Runtime/Commands/Builtin/PlayMusicCommand.cs`
- Create: `Runtime/Commands/Builtin/PlaySfxCommand.cs`
- Create: `Runtime/Commands/Builtin/ShowBackgroundCommand.cs`
- Create: `Runtime/Commands/Builtin/ShowCgCommand.cs`
- Create: `Runtime/Commands/Builtin/WaitCommand.cs`
- Test: `Tests/Runtime/ContentCommandsTests.cs`

**Interfaces:**
- Consumes: `Command`, presenter interfaces (Task 2).
- Produces: `SayLineCommand(string characterId, string text, string emotion, string position)`, `PlayMusicCommand(string trackId)`, `PlaySfxCommand(string clipId)`, `ShowBackgroundCommand(string backgroundId)`, `ShowCgCommand(string cgId)`, `WaitCommand(float seconds)` — all constructed directly by the compiler's generic-command dispatch (Task 8) or dialogue-line parsing (Task 8).

- [ ] **Step 1: Write the failing tests**

```csharp
using NUnit.Framework;

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
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the command from Global Constraints. Expected: compile errors — none of the six command types exist yet.

- [ ] **Step 3: Write the six command classes**

`Runtime/Commands/Builtin/SayLineCommand.cs`:
```csharp
using System.Collections;

namespace NovelForge.Runtime
{
    public class SayLineCommand : Command
    {
        private readonly string _characterId;
        private readonly string _text;
        private readonly string _emotion;
        private readonly string _position;

        public SayLineCommand(string characterId, string text, string emotion, string position)
        {
            _characterId = characterId;
            _text = text;
            _emotion = emotion;
            _position = position;
        }

        public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
        {
            yield return context.Dialogue.ShowLine(_characterId, _text, _emotion, _position);
        }
    }
}
```

`Runtime/Commands/Builtin/PlayMusicCommand.cs`:
```csharp
using System.Collections;

namespace NovelForge.Runtime
{
    public class PlayMusicCommand : Command
    {
        private readonly string _trackId;

        public PlayMusicCommand(string trackId) => _trackId = trackId;

        public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
        {
            yield return context.Audio.PlayMusic(_trackId);
        }
    }
}
```

`Runtime/Commands/Builtin/PlaySfxCommand.cs`:
```csharp
using System.Collections;

namespace NovelForge.Runtime
{
    public class PlaySfxCommand : Command
    {
        private readonly string _clipId;

        public PlaySfxCommand(string clipId) => _clipId = clipId;

        public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
        {
            yield return context.Audio.PlaySfx(_clipId);
        }
    }
}
```

`Runtime/Commands/Builtin/ShowBackgroundCommand.cs`:
```csharp
using System.Collections;

namespace NovelForge.Runtime
{
    public class ShowBackgroundCommand : Command
    {
        private readonly string _backgroundId;

        public ShowBackgroundCommand(string backgroundId) => _backgroundId = backgroundId;

        public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
        {
            yield return context.Backgrounds.ShowBackground(_backgroundId);
        }
    }
}
```

`Runtime/Commands/Builtin/ShowCgCommand.cs`:
```csharp
using System.Collections;

namespace NovelForge.Runtime
{
    public class ShowCgCommand : Command
    {
        private readonly string _cgId;

        public ShowCgCommand(string cgId) => _cgId = cgId;

        public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
        {
            yield return context.Backgrounds.ShowCg(_cgId);
        }
    }
}
```

`Runtime/Commands/Builtin/WaitCommand.cs`:
```csharp
using System.Collections;

namespace NovelForge.Runtime
{
    public class WaitCommand : Command
    {
        private readonly float _seconds;

        public WaitCommand(float seconds) => _seconds = seconds;

        public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
        {
            yield return context.Timing.Wait(_seconds);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run the command from Global Constraints. Expected: `failed="0"`, 6 more tests passing than the previous run.

- [ ] **Step 5: Commit**

```bash
git add Runtime/Commands/Builtin/SayLineCommand.cs Runtime/Commands/Builtin/SayLineCommand.cs.meta Runtime/Commands/Builtin/PlayMusicCommand.cs Runtime/Commands/Builtin/PlayMusicCommand.cs.meta Runtime/Commands/Builtin/PlaySfxCommand.cs Runtime/Commands/Builtin/PlaySfxCommand.cs.meta Runtime/Commands/Builtin/ShowBackgroundCommand.cs Runtime/Commands/Builtin/ShowBackgroundCommand.cs.meta Runtime/Commands/Builtin/ShowCgCommand.cs Runtime/Commands/Builtin/ShowCgCommand.cs.meta Runtime/Commands/Builtin/WaitCommand.cs Runtime/Commands/Builtin/WaitCommand.cs.meta Tests/Runtime/ContentCommandsTests.cs Tests/Runtime/ContentCommandsTests.cs.meta
git commit -m "$(cat <<'EOF'
Add SayLine and the simple presenter-delegating commands

Each just forwards to its presenter interface; real Actor/Audio/
Background implementations arrive in the content-subsystems phase.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 7: ChoiceCommand

**Files:**
- Create: `Runtime/Commands/Builtin/ChoiceCommand.cs`
- Test: `Tests/Runtime/ChoiceCommandTests.cs`

**Interfaces:**
- Consumes: `Command`, `IChoicePresenter` (Task 2).
- Produces: `ChoiceCommand(IReadOnlyList<string> optionTexts, int optionCount)` with `internal void ResolveTarget(int optionIndex, int targetIndex)` — the compiler (Task 10) constructs one per `choice` block and calls `ResolveTarget` once per option after resolving its label.

- [ ] **Step 1: Write the failing tests**

```csharp
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NovelForge.Runtime.Tests
{
    public class ChoiceCommandTests
    {
        [Test]
        public void Execute_PresentsOptionTextsAndJumpsToSelectedTarget()
        {
            var choices = new RecordingChoicePresenter { NextSelection = 1 };
            var context = new StoryContext { Choices = choices };
            var command = new ChoiceCommand(new[] { "Good, thanks!", "Not great..." }, optionCount: 2);
            command.ResolveTarget(0, 10);
            command.ResolveTarget(1, 20);
            var pointer = new StoryPointer();

            CoroutineTestUtil.RunToCompletion(command.Execute(context, pointer));

            CollectionAssert.AreEqual(new[] { "Good, thanks!", "Not great..." }, choices.Calls[0]);
            Assert.AreEqual(20, pointer.Current);
        }

        [Test]
        public void Execute_InvalidSelection_LogsErrorAndDefaultsToOptionZero()
        {
            var choices = new RecordingChoicePresenter { NextSelection = 99 };
            var context = new StoryContext { Choices = choices };
            var command = new ChoiceCommand(new[] { "Only option" }, optionCount: 1);
            command.ResolveTarget(0, 10);
            var pointer = new StoryPointer();

            LogAssert.Expect(LogType.Error, "NovelForge: choice presenter returned invalid selection 99 — defaulting to option 0.");
            CoroutineTestUtil.RunToCompletion(command.Execute(context, pointer));

            Assert.AreEqual(10, pointer.Current);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the command from Global Constraints. Expected: compile error — `ChoiceCommand` does not exist yet.

- [ ] **Step 3: Write `Runtime/Commands/Builtin/ChoiceCommand.cs`**

```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NovelForge.Runtime
{
    public class ChoiceCommand : Command
    {
        private readonly IReadOnlyList<string> _optionTexts;
        private readonly int[] _targetIndices;

        public ChoiceCommand(IReadOnlyList<string> optionTexts, int optionCount)
        {
            _optionTexts = optionTexts;
            _targetIndices = new int[optionCount];
        }

        internal void ResolveTarget(int optionIndex, int targetIndex) => _targetIndices[optionIndex] = targetIndex;

        public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
        {
            int selected = -1;
            yield return context.Choices.PresentChoices(_optionTexts, i => selected = i);

            if (selected < 0 || selected >= _targetIndices.Length)
            {
                Debug.LogError($"NovelForge: choice presenter returned invalid selection {selected} — defaulting to option 0.");
                selected = 0;
            }

            pointer.Current = _targetIndices[selected];
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run the command from Global Constraints. Expected: `failed="0"`, 2 more tests passing than the previous run.

- [ ] **Step 5: Commit**

```bash
git add Runtime/Commands/Builtin/ChoiceCommand.cs Runtime/Commands/Builtin/ChoiceCommand.cs.meta Tests/Runtime/ChoiceCommandTests.cs Tests/Runtime/ChoiceCommandTests.cs.meta
git commit -m "$(cat <<'EOF'
Add ChoiceCommand

Targets are resolved after construction (ResolveTarget, internal)
since the compiler doesn't know a choice option's label index until
it has scanned the whole script for labels.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 8: ScriptCompiler — labels, control flow, set, dialogue lines, generic commands

**Files:**
- Create: `Runtime/Parsing/ParseException.cs`
- Create: `Runtime/Parsing/CommandRegistry.cs`
- Create: `Runtime/Parsing/ScriptCompiler.cs`
- Test: `Tests/Runtime/ScriptCompilerCoreTests.cs`

**Interfaces:**
- Consumes: every `Command` subtype (Tasks 4-7), `NovelScript` (Task 2).
- Produces: `ParseException(int lineNumber, string message) : Exception`; `CommandRegistry` with `Register(string, Func<string,Command>)`, `TryCreate(string, string, out Command)`, static `CreateDefault()`; `ScriptCompiler(CommandRegistry registry = null)` with `NovelScript Compile(string source)` — handles `label`, `jump`, `gosub`, `return`, `set`, dialogue lines, and generic registry-backed commands. `if`/`else`/`endif` and `choice` are added in Tasks 9-10 by editing this same file.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Collections.Generic;
using NUnit.Framework;

namespace NovelForge.Runtime.Tests
{
    public class ScriptCompilerCoreTests
    {
        private static NovelScript Compile(string source) => new ScriptCompiler().Compile(source);

        [Test]
        public void Label_RecordsCommandIndexWithoutEmittingACommand()
        {
            var script = Compile("label start\njump start\n");

            Assert.AreEqual(1, script.Commands.Count);
            Assert.AreEqual(0, script.Labels["start"]);
        }

        [Test]
        public void Jump_ResolvesForwardLabelReference()
        {
            var script = Compile("jump later\nlabel later\nreturn\n");

            var jump = Assert.IsInstanceOf<JumpCommand>(script.Commands[0]) as JumpCommand;
            Assert.AreEqual(1, jump.TargetIndex);
        }

        [Test]
        public void Jump_ToUndefinedLabel_Throws()
        {
            var ex = Assert.Throws<ParseException>(() => Compile("jump nowhere\n"));
            StringAssert.Contains("nowhere", ex.Message);
        }

        [Test]
        public void Gosub_ResolvesToLabelIndex()
        {
            var script = Compile("gosub common\nlabel common\nreturn\n");

            var gosub = Assert.IsInstanceOf<GosubCommand>(script.Commands[0]) as GosubCommand;
            Assert.AreEqual(1, gosub.TargetIndex);
        }

        [Test]
        public void Set_ParsesAssignAddSubtractAndLiteralTypes()
        {
            var script = Compile("set relationship = 3\nset relationship += 1\nset relationship -= 2\nset metAlice = true\nset playerName = \"Kai\"\n");

            Assert.AreEqual(5, script.Commands.Count);
            Assert.IsInstanceOf<SetVariableCommand>(script.Commands[0]);
        }

        [Test]
        public void DialogueLine_ParsesCharacterTextEmotionAndPosition()
        {
            var script = Compile("Alice: Привет! #happy left\n");

            var say = Assert.IsInstanceOf<SayLineCommand>(script.Commands[0]) as SayLineCommand;
            var context = new StoryContext { Dialogue = new RecordingDialoguePresenter() };
            CoroutineTestUtil.RunToCompletion(say.Execute(context, new StoryPointer()));
            var recorded = ((RecordingDialoguePresenter)context.Dialogue).Calls[0];

            Assert.AreEqual("Alice", recorded.characterId);
            Assert.AreEqual("Привет!", recorded.text);
            Assert.AreEqual("happy", recorded.emotion);
            Assert.AreEqual("left", recorded.position);
        }

        [Test]
        public void DialogueLine_WithoutTags_LeavesEmotionAndPositionNull()
        {
            var script = Compile("Alice: Как дела?\n");

            var say = Assert.IsInstanceOf<SayLineCommand>(script.Commands[0]) as SayLineCommand;
            var context = new StoryContext { Dialogue = new RecordingDialoguePresenter() };
            CoroutineTestUtil.RunToCompletion(say.Execute(context, new StoryPointer()));
            var recorded = ((RecordingDialoguePresenter)context.Dialogue).Calls[0];

            Assert.AreEqual("Как дела?", recorded.text);
            Assert.IsNull(recorded.emotion);
            Assert.IsNull(recorded.position);
        }

        [Test]
        public void GenericCommand_bg_CompilesToShowBackgroundCommand()
        {
            var script = Compile("bg park_day\n");
            Assert.IsInstanceOf<ShowBackgroundCommand>(script.Commands[0]);
        }

        [Test]
        public void GenericCommand_Unknown_Throws()
        {
            var ex = Assert.Throws<ParseException>(() => Compile("frobnicate foo\n"));
            StringAssert.Contains("frobnicate", ex.Message);
        }

        [Test]
        public void CommentLine_AttachesToFollowingCommand()
        {
            var script = Compile("// remember this\njump self\nlabel self\n");
            Assert.AreEqual("remember this", script.Commands[0].SourceComment);
        }

        [Test]
        public void BlankLinesAndComments_AreSkippedBetweenStatements()
        {
            var script = Compile("label a\n\n// a comment\n\njump a\n");
            Assert.AreEqual(1, script.Commands.Count);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the command from Global Constraints. Expected: compile errors — `ScriptCompiler`, `CommandRegistry`, `ParseException` do not exist yet.

- [ ] **Step 3: Write `Runtime/Parsing/ParseException.cs`**

```csharp
using System;

namespace NovelForge.Runtime
{
    public class ParseException : Exception
    {
        public int LineNumber { get; }

        public ParseException(int lineNumber, string message) : base($"Line {lineNumber}: {message}")
        {
            LineNumber = lineNumber;
        }
    }
}
```

- [ ] **Step 4: Write `Runtime/Parsing/CommandRegistry.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;

namespace NovelForge.Runtime
{
    public class CommandRegistry
    {
        private readonly Dictionary<string, Func<string, Command>> _factories = new();

        public void Register(string commandName, Func<string, Command> factory) => _factories[commandName] = factory;

        public bool TryCreate(string commandName, string rawArgs, out Command command)
        {
            if (_factories.TryGetValue(commandName, out var factory))
            {
                command = factory(rawArgs);
                return true;
            }
            command = null;
            return false;
        }

        public static CommandRegistry CreateDefault()
        {
            var registry = new CommandRegistry();
            registry.Register("bg", args => new ShowBackgroundCommand(args.Trim()));
            registry.Register("cg", args => new ShowCgCommand(args.Trim()));
            registry.Register("music", args => new PlayMusicCommand(args.Trim()));
            registry.Register("sfx", args => new PlaySfxCommand(args.Trim()));
            registry.Register("wait", args => new WaitCommand(float.Parse(args.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture)));
            return registry;
        }
    }
}
```

- [ ] **Step 5: Write `Runtime/Parsing/ScriptCompiler.cs`**

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

        private readonly CommandRegistry _registry;

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
            string pendingComment = null;

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

                var dialogueMatch = DialogueLine.Match(line);
                if (dialogueMatch.Success)
                {
                    string characterId = dialogueMatch.Groups[1].Value;
                    (string text, string emotion, string position) = ParseDialogueRest(dialogueMatch.Groups[2].Value);
                    commands.Add(Attach(new SayLineCommand(characterId, text, emotion, position), ref pendingComment));
                    continue;
                }

                // Generic content command: "<name> <args...>" — bg/music/sfx/wait/cg and any
                // custom commands registered on the CommandRegistry passed to this compiler.
                int spaceIndex = line.IndexOf(' ');
                string commandName = spaceIndex < 0 ? line : line.Substring(0, spaceIndex);
                string rawArgs = spaceIndex < 0 ? string.Empty : line.Substring(spaceIndex + 1);
                if (!_registry.TryCreate(commandName, rawArgs, out Command generic))
                    throw new ParseException(lineNumber, $"Unknown command '{commandName}'.");
                commands.Add(Attach(generic, ref pendingComment));
            }

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

        private static (string text, string emotion, string position) ParseDialogueRest(string rest)
        {
            string emotion = null;
            string position = null;
            string text = rest;

            var emotionMatch = Regex.Match(text, @"#(\w+)");
            if (emotionMatch.Success)
            {
                emotion = emotionMatch.Groups[1].Value;
                text = text.Remove(emotionMatch.Index, emotionMatch.Length).TrimEnd();
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

            return (text.Trim(), emotion, position);
        }

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

- [ ] **Step 6: Run tests to verify they pass**

Run the command from Global Constraints. Expected: `failed="0"`, 11 more tests passing than the previous run.

- [ ] **Step 7: Commit**

```bash
git add Runtime/Parsing Tests/Runtime/ScriptCompilerCoreTests.cs Tests/Runtime/ScriptCompilerCoreTests.cs.meta
git commit -m "$(cat <<'EOF'
Add ScriptCompiler: labels, control flow, set, dialogue, generic commands

Two-pass label resolution (collect labels while emitting commands,
then patch jump/gosub targets) so forward references like "jump
chapter2" work regardless of where the label is defined. if/else/
endif and choice blocks are added on top of this in the next two
tasks.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 9: ScriptCompiler — if/else/endif

**Files:**
- Modify: `Runtime/Parsing/ScriptCompiler.cs`
- Test: `Tests/Runtime/ScriptCompilerConditionalsTests.cs`

**Interfaces:**
- Consumes: `ConditionalJumpCommand`, `JumpCommand` (Tasks 4-5).
- Produces: `if <var> <op> <value>` / `else` / `endif` support in `ScriptCompiler.Compile`. No new public types.

- [ ] **Step 1: Write the failing tests**

```csharp
using NUnit.Framework;

namespace NovelForge.Runtime.Tests
{
    public class ScriptCompilerConditionalsTests
    {
        private static NovelScript Compile(string source) => new ScriptCompiler().Compile(source);

        private static void Run(NovelScript script, StoryContext context)
        {
            var controller = new PlaybackController(script, context);
            CoroutineTestUtil.RunToCompletion(controller.RunAll());
        }

        [Test]
        public void IfTrue_RunsBodyAndSkipsElse()
        {
            var dialogue = new RecordingDialoguePresenter();
            var script = Compile(
                "set relationship = 5\n" +
                "if relationship >= 3\n" +
                "Alice: You did great!\n" +
                "else\n" +
                "Alice: That was rough.\n" +
                "endif\n" +
                "Alice: The end.\n");

            Run(script, new StoryContext { Dialogue = dialogue });

            CollectionAssert.AreEqual(new[] { "You did great!", "The end." },
                dialogue.Calls.ConvertAll(c => c.text));
        }

        [Test]
        public void IfFalse_RunsElseBranch()
        {
            var dialogue = new RecordingDialoguePresenter();
            var script = Compile(
                "set relationship = 1\n" +
                "if relationship >= 3\n" +
                "Alice: You did great!\n" +
                "else\n" +
                "Alice: That was rough.\n" +
                "endif\n" +
                "Alice: The end.\n");

            Run(script, new StoryContext { Dialogue = dialogue });

            CollectionAssert.AreEqual(new[] { "That was rough.", "The end." },
                dialogue.Calls.ConvertAll(c => c.text));
        }

        [Test]
        public void IfWithoutElse_FalseSkipsToEndif()
        {
            var dialogue = new RecordingDialoguePresenter();
            var script = Compile(
                "set relationship = 1\n" +
                "if relationship >= 3\n" +
                "Alice: You did great!\n" +
                "endif\n" +
                "Alice: The end.\n");

            Run(script, new StoryContext { Dialogue = dialogue });

            CollectionAssert.AreEqual(new[] { "The end." },
                dialogue.Calls.ConvertAll(c => c.text));
        }

        [Test]
        public void Endif_WithoutMatchingIf_Throws()
        {
            Assert.Throws<ParseException>(() => Compile("endif\n"));
        }

        [Test]
        public void Else_WithoutMatchingIf_Throws()
        {
            Assert.Throws<ParseException>(() => Compile("else\n"));
        }

        [Test]
        public void UnclosedIf_Throws()
        {
            Assert.Throws<ParseException>(() => Compile("if x == 1\nAlice: hi\n"));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the command from Global Constraints. Expected: `if`/`else`/`endif` lines currently fall through to the generic-command branch and throw `ParseException: Unknown command 'if'` — tests fail on that mismatch (or on missing branching behavior).

- [ ] **Step 3: Modify `Runtime/Parsing/ScriptCompiler.cs`**

Add a regex field next to `SetLine`:

```csharp
private static readonly Regex IfLine = new(@"^if\s+([A-Za-z_][A-Za-z0-9_]*)\s*(==|!=|>=|<=|>|<)\s*(.+)$", RegexOptions.Compiled);
```

Add these two private types inside the `ScriptCompiler` class (next to the existing fields):

```csharp
private enum BlockKind { If, Else }

private struct PendingBlock
{
    public BlockKind Kind;
    public ConditionalJumpCommand IfCommand;
    public JumpCommand ElseJumpCommand;
}
```

In `Compile`, declare a stack alongside `pendingLabelRefs`:

```csharp
var blockStack = new Stack<PendingBlock>();
```

Insert this block into the `for` loop's if/else chain, immediately before the dialogue-line check (`var dialogueMatch = DialogueLine.Match(line);`):

```csharp
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
```

After the `for` loop ends (right after its closing `}`, before the `foreach (var (commandIndex, ...` label-resolution loop), add:

```csharp
if (blockStack.Count > 0)
    throw new ParseException(lines.Length, "Unclosed 'if' block — missing 'endif'.");
```

Add this private method next to `ParseLiteral`:

```csharp
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
```

- [ ] **Step 4: Run tests to verify they pass**

Run the command from Global Constraints. Expected: `failed="0"`, 6 more tests passing than the previous run.

- [ ] **Step 5: Commit**

```bash
git add Runtime/Parsing/ScriptCompiler.cs Tests/Runtime/ScriptCompilerConditionalsTests.cs Tests/Runtime/ScriptCompilerConditionalsTests.cs.meta
git commit -m "$(cat <<'EOF'
Add if/else/endif support to ScriptCompiler

Structural backpatching via a compile-time block stack: 'if' emits a
ConditionalJumpCommand with an unresolved false-target, 'else' closes
it and opens its own pending jump-to-endif, 'endif' resolves whichever
is still open. No elif — nest another 'if' inside 'else' instead.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 10: ScriptCompiler — choice blocks + end-to-end integration test

**Files:**
- Modify: `Runtime/Parsing/ScriptCompiler.cs`
- Test: `Tests/Runtime/ScriptCompilerChoiceAndIntegrationTests.cs`

**Interfaces:**
- Consumes: `ChoiceCommand` (Task 7).
- Produces: `choice` block support in `ScriptCompiler.Compile`. No new public types. This is the capstone task: its integration test compiles and runs the design doc's example script end-to-end.

- [ ] **Step 1: Write the failing tests**

```csharp
using NUnit.Framework;

namespace NovelForge.Runtime.Tests
{
    public class ScriptCompilerChoiceAndIntegrationTests
    {
        private static NovelScript Compile(string source) => new ScriptCompiler().Compile(source);

        [Test]
        public void Choice_ParsesOptionsAndResolvesTargets()
        {
            var script = Compile(
                "choice\n" +
                "  \"Good, thanks!\" -> good_response\n" +
                "  \"Not great...\" -> bad_response\n" +
                "label good_response\n" +
                "return\n" +
                "label bad_response\n" +
                "return\n");

            Assert.IsInstanceOf<ChoiceCommand>(script.Commands[0]);
        }

        [Test]
        public void Choice_SelectingSecondOption_JumpsToItsLabel()
        {
            var choices = new RecordingChoicePresenter { NextSelection = 1 };
            var dialogue = new RecordingDialoguePresenter();
            var script = Compile(
                "choice\n" +
                "  \"Good, thanks!\" -> good_response\n" +
                "  \"Not great...\" -> bad_response\n" +
                "label good_response\n" +
                "Alice: Glad to hear it!\n" +
                "return\n" +
                "label bad_response\n" +
                "Alice: What happened?\n" +
                "return\n");
            var context = new StoryContext { Choices = choices, Dialogue = dialogue };
            var controller = new PlaybackController(script, context);

            CoroutineTestUtil.RunToCompletion(controller.RunAll());

            CollectionAssert.AreEqual(new[] { "What happened?" }, dialogue.Calls.ConvertAll(c => c.text));
        }

        [Test]
        public void Choice_WithNoOptionLines_Throws()
        {
            Assert.Throws<ParseException>(() => Compile("choice\njump nowhere\n"));
        }

        [Test]
        public void FullExampleScript_CompilesAndRunsToCompletion()
        {
            const string source = @"
label start
  bg park_day
  music theme_calm

  Alice: Привет! #happy left
  Alice: Как дела?

  choice
    ""Хорошо, спасибо!"" -> good_response
    ""Не очень..."" -> bad_response

label good_response
  Alice: Приятно слышать!
  jump chapter2

label bad_response
  set relationship -= 1
  Alice: Что случилось?
  jump chapter2

label chapter2
  gosub common_reaction
  Alice: Вот и конец главы.
  jump the_end

label common_reaction
  Alice: (кивает) #neutral
  return

label the_end
  return
";
            var script = new ScriptCompiler().Compile(source);
            var dialogue = new RecordingDialoguePresenter();
            var choices = new RecordingChoicePresenter { NextSelection = 1 };
            var audio = new RecordingAudioPresenter();
            var backgrounds = new RecordingBackgroundPresenter();
            var context = new StoryContext
            {
                Dialogue = dialogue,
                Choices = choices,
                Audio = audio,
                Backgrounds = backgrounds,
            };
            var controller = new PlaybackController(script, context);

            CoroutineTestUtil.RunToCompletion(controller.RunAll());

            CollectionAssert.AreEqual(new[] { "park_day" }, backgrounds.BackgroundCalls);
            CollectionAssert.AreEqual(new[] { "theme_calm" }, audio.MusicCalls);
            CollectionAssert.AreEqual(
                new[] { "Привет!", "Как дела?", "Что случилось?", "(кивает)", "Вот и конец главы." },
                dialogue.Calls.ConvertAll(c => c.text));
            Assert.AreEqual(-1, context.Variables.GetInt("relationship"));
            Assert.IsTrue(controller.IsFinished);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the command from Global Constraints. Expected: `choice` lines currently fall through to the generic-command branch and throw `ParseException: Unknown command 'choice'` — tests fail on that mismatch.

- [ ] **Step 3: Modify `Runtime/Parsing/ScriptCompiler.cs`**

Add this regex field next to `IfLine`:

```csharp
private static readonly Regex ChoiceOptionLine = new(@"^""([^""]*)""\s*->\s*([A-Za-z_][A-Za-z0-9_]*)$", RegexOptions.Compiled);
```

Insert this block into the `for` loop's if/else chain, immediately before the `if (line == "else")` check added in Task 9 (anywhere among the keyword checks works; this placement keeps all block-structured keywords together):

```csharp
if (line == "choice")
{
    var options = new List<(string text, string labelName)>();
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
        options.Add((optionMatch.Groups[1].Value, optionMatch.Groups[2].Value));
        lookahead++;
    }
    if (options.Count == 0)
        throw new ParseException(lineNumber, "'choice' has no options.");

    var choiceCommand = new ChoiceCommand(options.ConvertAll(o => o.text), options.Count);
    for (int i = 0; i < options.Count; i++)
        pendingLabelRefs.Add((commands.Count, options[i].labelName, i, lineNumber));
    commands.Add(Attach(choiceCommand, ref pendingComment));

    // lines[lineNumber .. lookahead-1] (0-indexed) were option lines already consumed;
    // jump the 1-based cursor to lookahead so the next loop iteration (which does
    // lineNumber++) resumes at lines[lookahead], the first unconsumed line.
    lineNumber = lookahead;
    continue;
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run the command from Global Constraints. Expected: `failed="0"`, 4 more tests passing than the previous run. This is the full Phase 1 test suite passing end to end, including the design-doc example script.

- [ ] **Step 5: Commit**

```bash
git add Runtime/Parsing/ScriptCompiler.cs Tests/Runtime/ScriptCompilerChoiceAndIntegrationTests.cs Tests/Runtime/ScriptCompilerChoiceAndIntegrationTests.cs.meta
git commit -m "$(cat <<'EOF'
Add choice blocks to ScriptCompiler; full example script passes end to end

Completes the Phase 1 core: parser + VM run the design doc's full
example script (labels, dialogue, choice, jump, gosub/return, set,
bg/music) against recording presenters with the expected trace.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

## What's next

This plan ends with a fully tested parser + VM and zero rendering. The next phase (its own plan, written when you're ready to start it) wires real Unity-backed presenters — `ActorView`/`AudioManager`/background handling — into the `IDialoguePresenter`/`IAudioPresenter`/`IBackgroundPresenter`/`ITimingPresenter` interfaces this phase defined, per the spec's Content-subsystems section.
