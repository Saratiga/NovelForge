# DSL Grammar Consolidation and Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** One source of truth for the DSL's regexes and keywords, a simpler `GraphDocumentParser.BuildNode`, and a replaceable timing presenter in `NovelRunner`.

**Architecture:** New public static `DslGrammar` in `NovelForge.Runtime` (Editor already references Runtime). `ScriptCompiler`, `GraphDocumentParser`, `DslSyntaxHighlighter` and `DslAutocompleteProvider` drop their private copies. Behavior does not change; the existing 229+ tests are the safety net, plus small direct tests for the new helpers.

**Tech Stack:** C#, Unity 6000.6, NUnit (EditMode).

**Spec:** [docs/superpowers/specs/2026-10-07-architecture-fixes-design.md](../specs/2026-10-07-architecture-fixes-design.md), section C.

## Global Constraints

- Unity Editor **6000.6.0f1** at `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe`. Package `com.novelforge.core`, root `G:\ClaudeProjects\NovelForge`.
- No new package dependency.
- Test command (absolute paths, no `-quit`; poll for a fresh `TestResults.xml` every ~20–30 s up to ~90 s; if "Couldn't set project path", create `TestProject~\Assets`):
  ```powershell
  & "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe" -batchmode -projectPath "<worktree-root>\TestProject~" -runTests -testPlatform EditMode -testResults "<worktree-root>\TestProject~\TestResults.xml" -logFile "<worktree-root>\TestProject~\Logs\RunTests.log"
  ```
- Baseline: record the passing count at branch start. No existing test may be edited in Tasks 1–2 (pure refactor); Task 3 edits exactly one assertion (`NovelRunnerTests.cs:63`).
- `.meta` files are Unity-generated only. `git add` whole containing folders; check `git status` before every commit.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Run after the save-load plan if both are in flight: Task 3 touches `UI/NovelRunner.cs`, which that plan also edits.

## Review Focus

- Highlighter spans for choice lines must still include the quotes and the `@` (shared regex groups exclude them). Existing `DslSyntaxHighlighterTests` cover this; do not loosen them.
- `label` with a tab or double space (`label\tfoo`): compiler rejects it today, autocomplete accepts it today. After the change both use `DslGrammar.TryParseLabel`; add the pinning test in Task 1 so the chosen behavior is explicit.
- Dialogue ending in `left`/`right`/`center` still parses as a position (documented limitation, `ScriptCompiler.cs:283-285`) — existing compiler tests cover it.
- Graph round-trip (`GraphDocumentParser` → `GraphDocumentFormatter`) stays byte-identical for every existing fixture. Existing tests cover it.

---

### Task 1: `DslGrammar`

**Files:**
- Create: `Runtime/Parsing/DslGrammar.cs`
- Modify: `Runtime/Parsing/ScriptCompiler.cs`, `Editor/GraphDocumentParser.cs`, `Editor/DslSyntaxHighlighter.cs`, `Editor/DslAutocompleteProvider.cs`
- Test: `Tests/Runtime/DslGrammarTests.cs`

**Interfaces:**
- Produces (`public static class DslGrammar`, namespace `NovelForge.Runtime`):
  - `public const string Identifier = "[A-Za-z_][A-Za-z0-9_]*";`
  - `public static readonly Regex DialogueLine, SetLine, IfLine, ChoiceOptionLine, EmotionTag, IdTag, TrailingPosition;` — patterns copied verbatim from `ScriptCompiler.cs:10-13`, `:266`, `:276`, with `TrailingPosition = \b(left|right|center)\b$`. `ChoiceOptionLine` groups: 1 text (no quotes), 2 id (no `@`), 3 target. `EmotionTag` group 1 = name, `IdTag` group 1 = id. All `RegexOptions.Compiled`.
  - `public static readonly IReadOnlyList<string> Keywords` = `label, jump, gosub, set, if, return, else, endif, choice`.
  - `public static bool TryParseLabel(string trimmedLine, out string name)` — `"label "` prefix, ordinal, name = rest trimmed, non-empty.
  - `public static bool TryParseJump(string trimmedLine, out string target, out bool isGosub)` — `"jump "` / `"gosub "` prefix, target = rest trimmed.

- [ ] **Step 1: Write failing tests**

```csharp
[Test] public void TryParseLabel_Valid()            // "label start" → true, "start"
[Test] public void TryParseLabel_TabSeparated_IsRejected()  // "label\tstart" → false (matches the compiler today)
[Test] public void TryParseLabel_NoName_IsRejected()        // "label " trimmed to "label" → false
[Test] public void TryParseJump_Jump()              // "jump end" → true, "end", isGosub false
[Test] public void TryParseJump_Gosub()             // "gosub sub" → true, "sub", isGosub true
[Test] public void TryParseJump_OtherLine()         // "jumper x" → false
[Test] public void ChoiceOptionLine_Groups()        // "\"Go\" @go_id -> forest" → "Go", "go_id", "forest"
```

- [ ] **Step 2: Run tests, expect compile failure.**

- [ ] **Step 3: Implement `DslGrammar`, then switch the four consumers.** `ScriptCompiler`: delete its four fields and use `DslGrammar.*`; `ParseDialogueRest` uses `EmotionTag`, `IdTag`, `TrailingPosition` instead of inline `Regex.Match` and the candidate loop; `label`/`jump`/`gosub` branches use `TryParseLabel`/`TryParseJump`. `GraphDocumentParser`: delete `ChoiceOptionLine`, use `TryParseLabel`/`TryParseJump`. `DslSyntaxHighlighter`: delete all five regex fields and `Keywords`; for choice lines color `Index - 1, Length + 2` of group 1 (quotes) and `Index - 1, Length + 1` of group 2 (`@`). `DslAutocompleteProvider`: replace `LabelLine` with `TryParseLabel(line.Trim(), out name)`.

- [ ] **Step 4: Verify no stray grammar copies**

Run: `grep -rnE "new Regex\(|Regex\.Match\(" Runtime Editor`
Expected: matches only in `Runtime/Parsing/DslGrammar.cs`.

- [ ] **Step 5: Run tests.** Expected: +7 passing, 0 failing, no existing test edited.

- [ ] **Step 6: Commit** `Runtime/Parsing/`, `Editor/`, `Tests/Runtime/`: `refactor: single DslGrammar shared by compiler, graph parser, highlighter, autocomplete`.

---

### Task 2: Simplify `GraphDocumentParser.BuildNode`

**Files:**
- Modify: `Editor/GraphDocumentParser.cs:189-269`

**Interfaces:**
- Consumes: Task 1 `DslGrammar.TryParseJump`, `DslGrammar.ChoiceOptionLine`.
- Produces: `private static string BodyWithTrailingExtra(string[] lines, int start, int bodyEnd, int lastContentIndex, int end)` = `CombineBodyWithTrailingExtra(JoinLines(lines, start, bodyEnd), TrimTrailingBlankLines(lines, lastContentIndex + 1, end))`.

- [ ] **Step 1: Refactor.** Replace the three inline `extra`/`body` pairs with `BodyWithTrailingExtra`. Merge the `jump` and `gosub` branches into one `if (DslGrammar.TryParseJump(lastTrimmed, out var target, out var isGosub))`. Move the choice-option backward scan into `private static bool TryCollectChoice(string[] lines, int start, int lastContentIndex, out List<ChoiceOption> options, out int choiceKeywordIndex)`. Keep the closing comment block (lines 262-267) unchanged.

- [ ] **Step 2: Run tests.** Expected: same count as after Task 1, 0 failing.

- [ ] **Step 3: Verify complexity.** Re-index with codebase-memory (`index_repository`, repo `G:/ClaudeProjects/NovelForge`), then `query_graph`: `MATCH (f:Method) WHERE f.name = 'BuildNode' RETURN f.cyclomatic`. Expected: ≤ 12 (was 21).

- [ ] **Step 4: Commit** `Editor/`: `refactor: reduce GraphDocumentParser.BuildNode complexity`.

---

### Task 3: `UnityTimingPresenter`

**Files:**
- Create: `Runtime/Content/UnityTimingPresenter.cs`
- Modify: `UI/NovelRunner.cs`
- Test: `Tests/UI/NovelRunnerTests.cs`

**Interfaces:**
- Produces: `public class UnityTimingPresenter : ITimingPresenter` (`Wait` yields `new WaitForSeconds(seconds)`); `public ITimingPresenter NovelRunner.Timing { get; set; } = new UnityTimingPresenter();`. `NovelRunner` no longer implements `ITimingPresenter`.

- [ ] **Step 1: Change and add tests.** `NovelRunnerTests.cs:63` → `Assert.IsInstanceOf<UnityTimingPresenter>(runner.Context.Timing);`. New:

```csharp
[Test] public void Prepare_UsesAssignedTimingPresenter()
// var timing = new RecordingTimingPresenter(); runner.Timing = timing; runner.Prepare();
// Assert.AreSame(timing, runner.Context.Timing);
```

- [ ] **Step 2: Run tests, expect FAIL** (`Timing` missing).

- [ ] **Step 3: Implement.** `Prepare` sets `Timing = Timing`; delete `NovelRunner.Wait` and the interface from the class declaration.

- [ ] **Step 4: Run tests.** Expected: +1 passing, 0 failing.

- [ ] **Step 5: Commit** `Runtime/Content/`, `UI/`, `Tests/UI/`: `refactor: move NovelRunner timing into replaceable UnityTimingPresenter`.
