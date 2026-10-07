# Save/Load Robustness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Saves reject a different story, survive edits to the script, and bring back background, CG, music and visible actors on load.

**Architecture:** Saved positions become `ScriptPosition` (label + offset) resolved against the current `NovelScript`; `PlaybackSnapshot` stays index-based and internal to playback. A new `SceneState` on `StoryContext` is written by content commands, saved in `SaveData`, and replayed through the presenters by `NovelRunner` before playback resumes.

**Tech Stack:** C#, Unity 6000.6, Newtonsoft.Json (already a dependency), NUnit (EditMode).

**Spec:** [docs/superpowers/specs/2026-10-07-architecture-fixes-design.md](../specs/2026-10-07-architecture-fixes-design.md), section B.

## Global Constraints

- Unity Editor **6000.6.0f1** at `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe`. Package `com.novelforge.core`, root `G:\ClaudeProjects\NovelForge`.
- No new package dependency.
- Test command (absolute paths, no `-quit`; poll for a fresh `TestResults.xml` every ~20–30 s up to ~90 s; if "Couldn't set project path", create `TestProject~\Assets`):
  ```powershell
  & "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe" -batchmode -projectPath "<worktree-root>\TestProject~" -runTests -testPlatform EditMode -testResults "<worktree-root>\TestProject~\TestResults.xml" -logFile "<worktree-root>\TestProject~\Logs\RunTests.log"
  ```
- Baseline: record the passing count on the branch start (229 on `master` `4c7013c`; more if the custom-command plan merged first). Each task says how many tests it adds; zero failures always.
- `.meta` files are Unity-generated only. `git add` whole containing folders; check `git status` before every commit.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- `JsonSaveStorage.CurrentSchemaVersion = 2`. Version 1 files load as `Incompatible` through the existing schema check — no migration.
- Load is all-or-nothing: any status other than `Success` leaves `StoryContext` and `PlaybackController` untouched (existing rule, `SaveLoadController.cs:33-36`).

## Review Focus

- Save taken after playback finished (pointer `int.MaxValue` from an empty-stack `return`) → loads as finished, no exception. Test in Task 1.
- Script edited so the saved label no longer exists → `Incompatible`, state untouched. Test in Task 2.
- Script edited so the label's block got shorter than the saved offset → resumes at label start with a warning. Test in Task 1.
- Two labels at the same command index (`label a` directly followed by `label b`) → save/load round-trips to the same index. Test in Task 1.
- Dialogue line without position but with emotion, then load → actor replays into the slot registered for `null`/empty position, same as the original line. Test in Task 3 (`""` key) and Task 4 (replay passes `null`).

---

### Task 1: `ScriptPosition` and label-relative resolution

**Files:**
- Create: `Runtime/Commands/ScriptPosition.cs`
- Modify: `Runtime/Commands/NovelScript.cs`, `Runtime/Commands/PlaybackController.cs`
- Test: `Tests/Runtime/ScriptPositionTests.cs`

**Interfaces:**
- Produces:
  - `public struct ScriptPosition { public string Label; public int Offset; }` (public fields, Newtonsoft-serializable).
  - `public ScriptPosition NovelScript.ToPosition(int commandIndex)`.
  - `public bool NovelScript.TryResolve(ScriptPosition position, out int commandIndex)`.
  - `public NovelScript PlaybackController.Script { get; }`.

- [ ] **Step 1: Write failing tests** (all compile `const string Src = "Alice: intro\nlabel a\nAlice: a1\nAlice: a2\nlabel b\nlabel c\nAlice: c1\n";` → indices: 0 intro, 1 a1, 2 a2, 3 c1; `a`→1, `b`→3, `c`→3):

```csharp
[Test] public void ToPosition_BeforeFirstLabel_HasNullLabel()        // ToPosition(0) == { null, 0 }
[Test] public void ToPosition_InsideLabel_IsOffsetFromLabel()         // ToPosition(2) == { "a", 1 }
[Test] public void ToPosition_SharedIndex_PicksOrdinalSmallestName()  // ToPosition(3) == { "b", 0 }
[Test] public void ToPosition_PastEnd_IsNullLabelWithCommandCount()   // ToPosition(int.MaxValue) == { null, 4 }
[Test] public void TryResolve_RoundTripsEveryIndex()                  // for i in 0..4: TryResolve(ToPosition(i)) → i
[Test] public void TryResolve_MissingLabel_ReturnsFalse()             // { "zzz", 0 } → false
[Test] public void TryResolve_OffsetPastLabelBlock_ReturnsLabelStartWithWarning()
// LogAssert.Expect(LogType.Warning, new Regex("'a'.*offset 5"));
// TryResolve({ "a", 5 }) → true, index 1
[Test] public void TryResolve_AfterInsertingLineAboveLabel_StillHitsSameLine()
// pos = Compile(Src).ToPosition(2); edited = Compile("Alice: new\n" + Src);
// edited.TryResolve(pos, out i); Assert.AreEqual(3, i); // "a2" moved from 2 to 3
```

- [ ] **Step 2: Run tests, expect compile failure.**

- [ ] **Step 3: Implement.** In `NovelScript` constructor precompute label starts sorted by (index, name ordinal). `ToPosition`: index ≥ `Commands.Count` → `{ null, Commands.Count }`; else the label with the greatest start ≤ index (ties → ordinal-smallest name), offset = index − start; no such label → `{ null, index }`. `TryResolve`: `Label == null` → `commandIndex = Math.Min(Offset, Commands.Count)`, true. Label missing → false. Block end = smallest label start strictly greater than this label's start, else `Commands.Count`. `start + Offset < end` → that index; `Offset == 0` on an empty block → start; otherwise start plus `Debug.LogWarning($"NovelForge: saved position offset {Offset} is past the end of label '{Label}' — resuming at the label start.")`. `PlaybackController.Script` returns `_script`.

- [ ] **Step 4: Run tests.** Expected: +8 passing, 0 failing.

- [ ] **Step 5: Commit** `Runtime/Commands/` and `Tests/Runtime/`: `feat: label-relative ScriptPosition for saved playback positions`.

---

### Task 2: Save format v2 and `ScriptMismatch`

**Files:**
- Modify: `Runtime/SaveLoad/SaveData.cs`, `Runtime/SaveLoad/SaveLoadController.cs`, `Runtime/SaveLoad/SaveLoadStatus.cs`, `Runtime/SaveLoad/JsonSaveStorage.cs`, `UI/SaveLoadView.cs`
- Test: `Tests/Runtime/SaveLoad/SaveLoadControllerTests.cs`, `Tests/Runtime/SaveLoad/JsonSaveStorageTests.cs`, `Tests/Runtime/SaveLoad/SaveLoadRoundTripTests.cs`, `Tests/UI/SaveLoadViewTests.cs`

**Interfaces:**
- Consumes: Task 1 `ScriptPosition`, `ToPosition`, `TryResolve`, `PlaybackController.Script`.
- Produces: `SaveData { int SchemaVersion; string ScriptId; ScriptPosition Position; ScriptPosition[] CallStack; Dictionary<string, object> Variables; }` (`PointerIndex` removed, `CallStack` type changed). `SaveLoadStatus.ScriptMismatch` (appended last). `JsonSaveStorage.CurrentSchemaVersion == 2`.

- [ ] **Step 1: Update existing tests to the new format.** `JsonSaveStorageTests.cs:37-48`: write/assert `Position = new ScriptPosition { Label = "a", Offset = 5 }` and `CallStack = new[] { new ScriptPosition { Label = "b", Offset = 1 } }`. `SaveLoadRoundTripTests.cs:69,85`: convert via `script.ToPosition(...)` / `script.TryResolve(...)` instead of copying raw indices. Existing `SaveLoadControllerTests` stay as they are and must keep passing.

- [ ] **Step 2: Write failing tests** in `SaveLoadControllerTests`:

```csharp
[Test] public void LoadInto_DifferentScriptId_ReturnsScriptMismatch_AndDoesNotChangeState()
// save with controller("story-a"); load with controller("story-b") on context with score=7
// Assert.AreEqual(SaveLoadStatus.ScriptMismatch, result.Status); score still 7; pointer still 0

[Test] public void LoadInto_SavedLabelRemovedFromScript_ReturnsIncompatible_AndDoesNotChangeState()
// script1 = "label start\nAlice: one\nlabel gone\nAlice: two\n"; advance pointer into "gone" via RestoreSnapshot({PointerIndex=1}); SaveTo
// script2 = "label start\nAlice: one\n"; LoadInto on fresh controller over script2
// Assert Incompatible; variables untouched; pointer 0

[Test] public void LoadInto_ScriptEditedAboveSavedLine_ResumesOnSameLine()
// script1 = "label start\nAlice: one\nAlice: two\n", pointer at 1 ("two"), SaveTo
// script2 = "label intro\nAlice: hi\nlabel start\nAlice: one\nAlice: two\n"; LoadInto
// Assert.AreEqual(2, newPlayback.CurrentIndex)   // "two": was index 1, now index 2

[Test] public void LoadInto_RestoresCallStackThroughLabels()
// script1 = "label main\ngosub sub\nAlice: after\njump end\nlabel sub\nBob: in sub\nreturn\nlabel end\n"
// playback.RestoreSnapshot({ PointerIndex = 3, CallStack = new[] { 1 } }) // at "in sub", return to "after"; SaveTo
// script2 = script1 with "Alice: extra\n" inserted right after "label main\n"; LoadInto on a fresh controller
// RunToCompletion(newPlayback.RunAll()) with RecordingDialoguePresenter
// CollectionAssert.AreEqual(new[] { "in sub", "after" }, dialogue.Calls.Select(c => c.text))
```

In `SaveLoadViewTests`, add `LoadMode_ScriptMismatch_ShowsDifferentStoryMessage`: save with one script id, init view with a controller of another id, click slot, assert status text `This save belongs to a different story.`

- [ ] **Step 3: Run tests, expect failures/compile errors.**

- [ ] **Step 4: Implement.** `SaveTo`: `Position = script.ToPosition(snapshot.PointerIndex)`, `CallStack = snapshot.CallStack.Select(script.ToPosition).ToArray()`. `LoadInto`: after storage `Success`, check `result.Data.ScriptId != _scriptId` → return `new SaveLoadResult { Status = ScriptMismatch, Data = result.Data }`. Resolve position and every call-stack entry first; any `false` → `new SaveLoadResult { Status = Incompatible, FoundSchemaVersion = result.Data.SchemaVersion }`. Only then import variables and `RestoreSnapshot`. `SaveLoadView` gets the `ScriptMismatch` case with the exact text above. Bump `CurrentSchemaVersion` to `2`.

- [ ] **Step 5: Run tests.** Expected: +5 passing, 0 failing.

- [ ] **Step 6: Commit** `Runtime/SaveLoad/`, `UI/`, `Tests/Runtime/SaveLoad/`, `Tests/UI/`: `feat: save format v2 with label-relative positions and script id check`.

---

### Task 3: `SceneState` written by commands

**Files:**
- Create: `Runtime/Commands/SceneState.cs`
- Modify: `Runtime/Commands/StoryContext.cs`, `Runtime/Commands/Builtin/ShowBackgroundCommand.cs`, `ShowCgCommand.cs`, `PlayMusicCommand.cs`, `SayLineCommand.cs`
- Test: `Tests/Runtime/SceneStateTests.cs`

**Interfaces:**
- Produces:
  - `public class ActorState { public string CharacterId; public string Emotion; }`
  - `public class SceneState { public string Background; public string Cg; public string Music; public Dictionary<string, ActorState> Actors = new(); public void CopyFrom(SceneState other); }` — `CopyFrom(null)` clears everything; copies actors deeply.
  - `public SceneState StoryContext.Scene { get; } = new SceneState();`

- [ ] **Step 1: Write failing tests** (run commands through `ScriptCompiler` + `PlaybackController` + `CoroutineTestUtil.RunToCompletion`, Recording presenters wired):

```csharp
[Test] public void Bg_SetsBackgroundAndClearsCg()        // "cg sunset\nbg room\n" → Background "room", Cg null
[Test] public void Cg_SetsCg()                            // "bg room\ncg sunset\n" → Cg "sunset"
[Test] public void Music_SetsMusic()                      // "music theme\n" → Music "theme"
[Test] public void SayLine_WithEmotion_SetsActorAtPosition()  // "Alice: hi #happy left\n" → Actors["left"] = { "Alice", "happy" }
[Test] public void SayLine_WithoutPosition_UsesEmptyKey()     // "Alice: hi #happy\n" → Actors[""]
[Test] public void SayLine_WithoutEmotion_LeavesActorsUnchanged()
[Test] public void CopyFrom_IsDeep()                      // mutate source actor after copy; copy unchanged
[Test] public void CopyFrom_Null_ClearsAll()
```

Character id is exactly the identifier before `:` (`"Alice"`).

- [ ] **Step 2: Run tests, expect compile failure.**

- [ ] **Step 3: Implement.** Each command writes the scene **before** calling its presenter and even when the presenter is null (state follows the script, not the wiring). `SayLineCommand` writes only when `_emotion` is non-empty, key `_position ?? ""`.

- [ ] **Step 4: Run tests.** Expected: +8 passing, 0 failing.

- [ ] **Step 5: Commit** `Runtime/Commands/` and `Tests/Runtime/`: `feat: track scene state (background, CG, music, actors) in StoryContext`.

---

### Task 4: Save, restore and replay the scene

**Files:**
- Modify: `Runtime/Commands/Presenters/IDialoguePresenter.cs`, `Runtime/Commands/SceneState.cs`, `Runtime/SaveLoad/SaveData.cs`, `Runtime/SaveLoad/SaveLoadController.cs`, `UI/DialoguePresenter.cs`, `UI/NovelRunner.cs`, `Tests/Runtime/Doubles/RecordingDialoguePresenter.cs`
- Test: `Tests/Runtime/SceneStateTests.cs`, `Tests/Runtime/SaveLoad/SaveLoadControllerTests.cs`, `Tests/UI/DialoguePresenterTests.cs`, `Tests/UI/NovelRunnerTests.cs`

**Interfaces:**
- Consumes: Task 3 `SceneState`, `StoryContext.Scene`; Task 2 `SaveData`.
- Produces:
  - `IEnumerator IDialoguePresenter.ShowActor(string characterId, string emotion, string position)`.
  - `public IEnumerator SceneState.Replay(StoryContext context)`.
  - `SaveData.Scene` (`SceneState`).
  - `RecordingDialoguePresenter.ActorCalls` — `List<(string characterId, string emotion, string position)>`.

- [ ] **Step 1: Write failing tests**

```csharp
// DialoguePresenterTests — reuse the file's existing setup helpers
[Test] public void ShowActor_AllResolve_ShowsSpriteWithoutTouchingDialogueBox()
[Test] public void ShowActor_UnknownCharacter_LogsErrorAndShowsNothing()
// existing ShowLine_* tests keep passing unchanged (ShowLine now delegates actor display to ShowActor)

// SceneStateTests
[Test] public void Replay_CallsPresentersInOrder_BgThenCgThenMusicThenActors()
// scene { Background="room", Cg="sunset", Music="theme", Actors={ ["left"]={"Alice","happy"}, [""]={"Bob","sad"} } }
// recording presenters: backgrounds [ShowBackground room, ShowCg sunset], audio [PlayMusic theme],
// dialogue.ActorCalls contains ("Alice","happy","left") and ("Bob","sad",null); dialogue.Calls empty
[Test] public void Replay_EmptyScene_CallsNothing()
[Test] public void Replay_NullPresenters_SkipsWithoutError()   // context with no presenters, scene filled; no exception, no log

// SaveLoadControllerTests
[Test] public void SaveTo_ThenLoadInto_RestoresSceneState()
[Test] public void LoadInto_Failure_LeavesSceneUntouched()     // ScriptMismatch path; Scene.Background unchanged

// NovelRunnerTests
[Test] public void RunAndNotify_ReplaysSceneBeforeFirstCommand()
// Prepare script "label a\nAlice: hi\n"; runner.Context.Scene.Background = "room";
// replace runner.Context.Backgrounds and .Dialogue with Recording doubles; Pump(runner.RunAndNotify());
// background ShowBackground("room") recorded before the dialogue line
```

- [ ] **Step 2: Run tests, expect compile failure.**

- [ ] **Step 3: Implement.**
  - `DialoguePresenter.ShowActor`: move the actor branch of `ShowLine` (library lookup, sprite, slot) into it; log texts stay byte-identical. `ShowLine` keeps name/color resolution and calls `ShowActor` when `emotion` is non-empty, then `dialogueBox.ShowText`.
  - `RecordingDialoguePresenter.ShowActor` appends to `ActorCalls`.
  - `Replay`: background → CG → music → actors in `Actors` key ordinal order; key `""` passes `position = null`; null presenter → skip silently.
  - `SaveTo` stores a copy of `context.Scene`; `LoadInto` calls `context.Scene.CopyFrom(saved.Scene)` together with the variable import, after all checks pass.
  - `NovelRunner.RunAndNotify`: `yield return Context.Scene.Replay(Context);` before `yield return Playback.RunAll();`.

- [ ] **Step 4: Run tests.** Expected: +8 passing, 0 failing.

- [ ] **Step 5: Manual check in the GettingStarted sample** (open `TestProject~`, import sample, Play): advance past the first `bg` and a line with an actor, save to slot 1, quit to title, load slot 1 → background, music and actor are visible before the next line.

- [ ] **Step 6: Commit** `Runtime/`, `UI/`, `Tests/`: `feat: persist and replay scene state on load`.
