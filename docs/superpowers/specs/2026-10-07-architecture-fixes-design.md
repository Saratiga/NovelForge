# Architecture Fixes — Design

Source: architecture review of `master` at `4c7013c` (2026-10-07). Three independent sub-projects, each with its own plan. Order of execution: A, B, C. A and B do not depend on each other; C touches `NovelRunner` after B.

## A. Custom commands reach every compiler

**Problem.** `ScriptCompiler(CommandRegistry registry = null)` is the only extension point for custom DSL commands, but all five call sites build `new ScriptCompiler()` / `CommandRegistry.CreateDefault()`: `UI/NovelRunner.cs:36`, `Editor/NovelScriptImporter.cs:17`, `Editor/NovelScriptEditorWindow.cs:167` and `:198`, `Editor/CharacterEditorWindow.cs:222`. A script using a custom command fails import with `Unknown command` and cannot be played through `NovelRunner`.

**Decision.** Attribute-based discovery inside `CommandRegistry.CreateDefault()`. No call site changes.

- `[NovelCommand("name")]` on a `Command` subclass registers it under `name`.
- Valid type: derives from `Command`, not abstract, has a public constructor `(string rawArgs)`. Invalid type: `Debug.LogError` naming the type, type skipped.
- Built-ins (`bg`, `cg`, `music`, `sfx`, `wait`) register first. A discovered name equal to an already registered name: `Debug.LogError`, the earlier registration wins. Discovered types register in `Type.FullName` ordinal order, so the result is deterministic.
- Scan: every loaded assembly that is `NovelForge.Runtime` or references it. Scan result cached in a static for the domain lifetime (Editor domain reload resets it).
- Exceptions from a custom constructor reach `ScriptCompiler` unwrapped (not as `TargetInvocationException`), so the existing `Command '<name>' rejected arguments` message shows the real cause.

## B. Save/Load survives script edits and restores the screen

**Problems.**
1. `SaveData.ScriptId` is written but never checked on load.
2. `SaveData.PointerIndex` and `CallStack` are raw indices into the compiled command list. Any edit to the script shifts them.
3. Background, CG, music and visible actors are not saved. After load the screen is empty until the next `bg`/`music` command.

**Decisions.**
- New status `SaveLoadStatus.ScriptMismatch`: saved `ScriptId` differs from the controller's. `SaveLoadView` text: `This save belongs to a different story.`
- Positions are stored as `ScriptPosition { string Label; int Offset; }`: nearest label at or before the command, plus offset from that label. `Label == null` means "before the first label", offset from index 0.
  - Resolve: label missing → load fails with `Incompatible`. Offset past the end of the label's block → resume at label start, `Debug.LogWarning`. Finished playback (index ≥ command count) saves as `{ Label = null, Offset = Commands.Count }` and restores as finished.
  - Call stack entries use the same type.
- New `SceneState` in `StoryContext.Scene`: `Background`, `Cg`, `Music`, `Actors` (position → `{ CharacterId, Emotion }`, null position stored as `""`). Commands update it as they execute: `bg` sets `Background` and clears `Cg` (the presenter fades the CG out on a background change), `cg` sets `Cg`, `music` sets `Music`, a dialogue line with an emotion sets `Actors[position]`.
- `SceneState.Replay(StoryContext)` re-applies the state through the presenters. `NovelRunner` runs it before `RunAll()`. A fresh game has an empty scene, so replay does nothing.
- Actor replay needs a way to show an actor without a line: new `IDialoguePresenter.ShowActor(characterId, emotion, position)`. `DialoguePresenter.ShowLine` reuses it.
- Load is still all-or-nothing: every position resolves before any state changes.
- `JsonSaveStorage.CurrentSchemaVersion` becomes `2`. Version 1 saves load as `Incompatible`. Accepted: package is `0.1.0`, no shipped games.

## C. Cleanup

- `DslGrammar` (Runtime/Parsing): one home for the DSL regexes and keywords now copied across `ScriptCompiler`, `GraphDocumentParser`, `DslSyntaxHighlighter`, `DslAutocompleteProvider`. Pure refactor; existing tests are the safety net.
- `GraphDocumentParser.BuildNode` (cyclomatic 21): fold the three copies of the "body + trailing extra" logic and merge the `jump`/`gosub` branches. Target cyclomatic ≤ 12.
- `NovelRunner` stops implementing `ITimingPresenter`; `UnityTimingPresenter` takes that role, replaceable through `NovelRunner.Timing`.

## Out of scope

- Restoring an in-progress choice menu or half-typed line (the pointer re-runs the current command; that is enough).
- Save slot deletion, overwrite confirmation, migration of v1 saves.
- Removing stale `.claude/worktrees` (housekeeping, owner's call).
- Moving save/load into `NovelRunner`: `USAGE.md` documents that `NovelRunner` deliberately knows nothing about save/load and screen flow; that decision stands.
