# Script Editor Window Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give `.nfscript` files a custom Unity Editor window (opened via double-click) with syntax highlighting, a live parse-error panel, and Tab-triggered autocomplete over commands, characters, and labels — instead of opening in an external text editor.

**Architecture:** Three independent, fully unit-testable pure-logic pieces — `CommandRegistry.RegisteredNames` (a point addition to already-merged `Runtime/Parsing/CommandRegistry.cs`), `Editor/DslSyntaxHighlighter.cs` (plain text → rich-text-colored string), `Editor/DslAutocompleteProvider.cs` (source + cursor position → ranked candidate list) — plus one thin, untested `EditorWindow` GUI shell (`Editor/NovelScriptEditorWindow.cs`) that wires them together via `OnGUI`. Syntax highlighting uses the standard Unity IMGUI overlay technique: a read-only rich-text `Label` rendered behind a transparent-text `TextArea` that handles real input, both sharing one `GUIStyle` so glyph positions align exactly.

**Tech Stack:** C#, Unity 6000.6, IMGUI (`UnityEditor`/`UnityEngine` — no UI Toolkit, matching this project's existing Editor code style), NUnit (Unity Test Framework, EditMode).

**Spec:** [docs/superpowers/specs/2026-09-10-editor-script-window-design.md](../specs/2026-09-10-editor-script-window-design.md). Out of scope for this plan (per the spec): inline squiggly-underline errors (replaced with an error panel), the branch graph editor (separate future phase), line-number gutter, multi-file tabs, find/replace, undo beyond what IMGUI's `TextArea` provides natively.

## Global Constraints

- Unity Editor version: **6000.6.0f1**, at `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe`. Package id `com.novelforge.core`, package root `G:\ClaudeProjects\NovelForge`.
- No new package dependency.
- Task 1 touches already-merged `Runtime/Parsing/CommandRegistry.cs` — additive only (one new read-only property), every existing test must keep passing unmodified.
- Tasks 2-3 are new files in the existing `NovelForge.Editor`/`NovelForge.Editor.Tests` assemblies (created in the prior "ScriptedImporter" plan) — no asmdef changes needed.
- Task 4 (`NovelScriptEditorWindow`) has **no automated test** — IMGUI `EditorWindow`/`OnGUI` code has no established way to drive a real repaint cycle from an EditMode test in this project (or generally in Unity Editor testing). This is a deliberate, spec-approved choice, matching this project's existing precedent of verifying visual/interactive components manually rather than automating them. Task 4's own step list ends with a manual QA walkthrough instead of `LogAssert`/`Assert` code — do not treat the absence of a test file for this task as a gap.
- Task 4 reads and sets the IMGUI `TextArea`'s live cursor position via `typeof(EditorGUI).GetField("s_RecycledEditor", ...)` reflection — there is no public API for this in IMGUI. This is a long-standing, widely-used community pattern, but the exact private field name is **not guaranteed to be confirmed against this specific Unity version** in advance. The code is written so this is safe either way: `GetField` returns `null` if the field doesn't exist (no exception), and every caller falls back to a documented, non-crashing default (`_text.Length`, i.e. "act as if the cursor were at the end of the text") rather than throwing. If manual QA (Task 4's last step) shows the caret-follow behavior doesn't work precisely, that is an acceptable, already-documented limitation — do not treat it as a bug to chase further; the feature still functions (autocomplete still inserts text, "go to line" still exists), just possibly less precisely positioned.
- `.meta` files must be genuinely Unity-generated, never hand-authored, under any circumstances. This project has hit hand-fabricated `.meta` files (patterned/cyclic GUIDs, a fake `MonoImporter:` block no real `.cs.meta` file in this repo has) multiple times, requiring rejected fix rounds each time. Let Unity generate every `.meta` file by actually running the EditMode test suite (which imports the project); never type one by hand.
- Git commit messages end with: `Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>`.
- Every `git add` must be of the whole containing folder, never individual bare file paths — Unity generates `.meta` companions on import, and folder-add recurses into them automatically. This plan creates no new folders (`Editor/`, `Tests/Editor/`, `Runtime/Parsing/`, `Tests/Runtime/` all already exist), so the "new folder's sibling .meta" gotcha that has repeatedly bitten this project should not apply — but check `git status` after every commit regardless.
- Use the working directory's own `TestProject~`, addressed with absolute paths for every argument to the Unity test command:
  ```powershell
  & "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe" -batchmode -projectPath "<worktree-root>\TestProject~" -runTests -testPlatform EditMode -testResults "<worktree-root>\TestProject~\TestResults.xml" -logFile "<worktree-root>\TestProject~\Logs\RunTests.log"
  ```
  Deliberately no `-quit`. After launching, **poll for completion synchronously within your own tool calls** (wait ~20-30 seconds, check for a fresh `TestResults.xml`, repeat up to ~90s total) — never end your turn assuming something else will resume you; nothing will. If you see "Couldn't set project path", check `TestProject~\Assets` exists (create it with `New-Item -ItemType Directory -Force -Path "<worktree-root>\TestProject~\Assets"` if not — a known one-time-per-worktree gotcha). If a run fails with an `ILPPTrigger: Can't find file \\.\pipe\...` error, that is a known transient Unity toolchain glitch unrelated to your code — simply retry the same command once.
- Do not hand-author `.meta` files. Unity generates them automatically on import (during the test run).
- Baseline before this plan: **166 passing tests** (confirmed on current `master`). Each task states the expected running total after it.

---

### Task 1: `CommandRegistry.RegisteredNames`

**Files:**
- Modify: `Runtime/Parsing/CommandRegistry.cs`
- Create: `Tests/Runtime/CommandRegistryTests.cs`

**Interfaces:**
- Produces: `NovelForge.Runtime.CommandRegistry.RegisteredNames` (`public IReadOnlyCollection<string> RegisteredNames`). Task 4 calls `CommandRegistry.CreateDefault().RegisteredNames` to get the command autocomplete candidate list.

- [ ] **Step 1: Write the failing tests**

Create `Tests/Runtime/CommandRegistryTests.cs`:

```csharp
using NUnit.Framework;

namespace NovelForge.Runtime.Tests
{
    public class CommandRegistryTests
    {
        [Test]
        public void CreateDefault_RegisteredNames_ContainsAllBuiltinCommands()
        {
            var registry = CommandRegistry.CreateDefault();

            CollectionAssert.AreEquivalent(new[] { "bg", "cg", "music", "sfx", "wait" }, registry.RegisteredNames);
        }

        [Test]
        public void RegisteredNames_ReflectsCustomRegistrations()
        {
            var registry = new CommandRegistry();
            registry.Register("custom", args => new ShowBackgroundCommand(args));

            CollectionAssert.AreEquivalent(new[] { "custom" }, registry.RegisteredNames);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the EditMode test command from Global Constraints.
Expected: compile error (`CommandRegistry.RegisteredNames` does not exist yet).

- [ ] **Step 3: Add `RegisteredNames`**

In `Runtime/Parsing/CommandRegistry.cs`, add this property to the class, directly after the `Register` method:

```csharp
        public IReadOnlyCollection<string> RegisteredNames => _factories.Keys;
```

Full file after the change:

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

        public IReadOnlyCollection<string> RegisteredNames => _factories.Keys;

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

- [ ] **Step 4: Run tests to verify they pass**

Run the EditMode test command from Global Constraints.
Expected: `failed="0"`, 2 new tests passing (running total from 166: **168**). Confirm every pre-existing `ScriptCompiler*Tests.cs` file is still green unmodified (they use `CommandRegistry` indirectly and must be unaffected by this additive change).

- [ ] **Step 5: Commit**

```bash
git add Runtime/Parsing/CommandRegistry.cs Tests/Runtime/CommandRegistryTests.cs
git commit -m "$(cat <<'EOF'
Add CommandRegistry.RegisteredNames for autocomplete command listing

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: `DslSyntaxHighlighter`

**Files:**
- Create: `Editor/DslSyntaxHighlighter.cs`
- Create: `Tests/Editor/DslSyntaxHighlighterTests.cs`

**Interfaces:**
- Produces: `NovelForge.Editor.DslSyntaxHighlighter.ToRichText(string source)` → `string`. Task 4 calls this to render the highlighted overlay `Label`.

This task is fully independent of Task 1 and Task 3 — no shared files, no interface dependency.

- [ ] **Step 1: Write the failing tests**

Create `Tests/Editor/DslSyntaxHighlighterTests.cs`:

```csharp
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace NovelForge.Editor.Tests
{
    public class DslSyntaxHighlighterTests
    {
        private static void AssertColored(string richText, string expectedToken)
        {
            var pattern = $@"<color=#[0-9A-Fa-f]{{6}}>{Regex.Escape(expectedToken)}</color>";
            StringAssert.IsMatch(pattern, richText);
        }

        [Test]
        public void CommentLine_IsColoredAsWhole()
        {
            string result = DslSyntaxHighlighter.ToRichText("// a note");

            AssertColored(result, "// a note");
        }

        [Test]
        public void LabelLine_ColorsOnlyTheKeyword()
        {
            string result = DslSyntaxHighlighter.ToRichText("label start");

            AssertColored(result, "label");
            StringAssert.Contains(" start", result);
        }

        [Test]
        public void DialogueLine_ColorsCharacterIdEmotionIdTagAndPosition()
        {
            string result = DslSyntaxHighlighter.ToRichText("Alice: Привет! #happy @greet left");

            AssertColored(result, "Alice");
            AssertColored(result, "#happy");
            AssertColored(result, "@greet");
            AssertColored(result, "left");
        }

        [Test]
        public void ChoiceOptionLine_ColorsQuotedTextExplicitIdAndTargetLabel()
        {
            string result = DslSyntaxHighlighter.ToRichText("\"Yes\" @yes_opt -> yes_label");

            AssertColored(result, "\"Yes\"");
            AssertColored(result, "@yes_opt");
            AssertColored(result, "yes_label");
        }

        [Test]
        public void GenericCommandLine_ColorsFirstWord()
        {
            string result = DslSyntaxHighlighter.ToRichText("bg park_day");

            AssertColored(result, "bg");
            StringAssert.Contains(" park_day", result);
        }

        [Test]
        public void UnrecognizedWordLine_StillColorsFirstWord()
        {
            string result = DslSyntaxHighlighter.ToRichText("frobnicate foo");

            AssertColored(result, "frobnicate");
        }

        [Test]
        public void EmptyOrNullSource_ReturnsEmptyStringWithoutThrowing()
        {
            Assert.AreEqual(string.Empty, DslSyntaxHighlighter.ToRichText(null));
            Assert.AreEqual(string.Empty, DslSyntaxHighlighter.ToRichText(string.Empty));
        }

        [Test]
        public void TextContainingAngleBracketsAndAmpersand_IsEscaped()
        {
            string result = DslSyntaxHighlighter.ToRichText("Alice: 1 < 2 & 3 > 0");

            StringAssert.Contains("1 &lt; 2 &amp; 3 &gt; 0", result);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the EditMode test command from Global Constraints.
Expected: compile error (`DslSyntaxHighlighter` does not exist yet).

- [ ] **Step 3: Implement `DslSyntaxHighlighter`**

Create `Editor/DslSyntaxHighlighter.cs`:

```csharp
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;

namespace NovelForge.Editor
{
    public static class DslSyntaxHighlighter
    {
        private static readonly Regex DialogueLine = new(@"^([A-Za-z_][A-Za-z0-9_]*):\s+(.+)$", RegexOptions.Compiled);
        private static readonly Regex ChoiceOptionLine = new(@"^(""[^""]*"")(?:\s*(@[A-Za-z_][A-Za-z0-9_]*))?\s*->\s*([A-Za-z_][A-Za-z0-9_]*)$", RegexOptions.Compiled);
        private static readonly Regex EmotionTag = new(@"#\w+", RegexOptions.Compiled);
        private static readonly Regex IdTag = new(@"(?<=^|\s)@[A-Za-z_][A-Za-z0-9_]*\b", RegexOptions.Compiled);
        private static readonly Regex PositionKeyword = new(@"\b(left|right|center)\b$", RegexOptions.Compiled);

        private static readonly HashSet<string> Keywords = new()
        {
            "label", "jump", "gosub", "set", "if", "return", "else", "endif", "choice",
        };

        public static string ToRichText(string source)
        {
            if (string.IsNullOrEmpty(source))
                return source ?? string.Empty;

            string[] lines = source.Replace("\r\n", "\n").Split('\n');
            var result = new StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0) result.Append('\n');
                result.Append(HighlightLine(lines[i]));
            }
            return result.ToString();
        }

        private static string HighlightLine(string line)
        {
            string trimmed = line.TrimStart();
            int indent = line.Length - trimmed.Length;
            var spans = new List<(int start, int length, string color)>();

            if (trimmed.StartsWith("//"))
            {
                spans.Add((indent, trimmed.Length, CommentColor));
                return Render(line, spans);
            }

            string firstWord = FirstWord(trimmed);
            if (Keywords.Contains(firstWord))
                spans.Add((indent, firstWord.Length, KeywordColor));

            var choiceMatch = ChoiceOptionLine.Match(trimmed);
            if (choiceMatch.Success)
            {
                var quoteGroup = choiceMatch.Groups[1];
                spans.Add((indent + quoteGroup.Index, quoteGroup.Length, StringLiteralColor));

                var idGroup = choiceMatch.Groups[2];
                if (idGroup.Success)
                    spans.Add((indent + idGroup.Index, idGroup.Length, TagColor));

                var labelGroup = choiceMatch.Groups[3];
                spans.Add((indent + labelGroup.Index, labelGroup.Length, LabelColor));

                return Render(line, spans);
            }

            var dialogueMatch = DialogueLine.Match(trimmed);
            if (dialogueMatch.Success)
            {
                var idGroup = dialogueMatch.Groups[1];
                spans.Add((indent + idGroup.Index, idGroup.Length, CharacterIdColor));

                Group restGroup = dialogueMatch.Groups[2];
                string rest = restGroup.Value;
                int restOffset = indent + restGroup.Index;

                var emotionMatch = EmotionTag.Match(rest);
                if (emotionMatch.Success)
                    spans.Add((restOffset + emotionMatch.Index, emotionMatch.Length, TagColor));

                var idTagMatch = IdTag.Match(rest);
                if (idTagMatch.Success)
                    spans.Add((restOffset + idTagMatch.Index, idTagMatch.Length, TagColor));

                var posMatch = PositionKeyword.Match(rest);
                if (posMatch.Success)
                    spans.Add((restOffset + posMatch.Index, posMatch.Length, KeywordColor));

                return Render(line, spans);
            }

            if (spans.Count == 0 && firstWord.Length > 0)
            {
                // Generic content command (bg/cg/music/sfx/wait/custom, or an unrecognized word) —
                // colored so it reads visually distinct even before the DSL is fully valid.
                spans.Add((indent, firstWord.Length, CommandColor));
            }

            return Render(line, spans);
        }

        private static string Render(string line, List<(int start, int length, string color)> spans)
        {
            if (spans.Count == 0)
                return Escape(line);

            spans.Sort((a, b) => a.start.CompareTo(b.start));

            var sb = new StringBuilder();
            int cursor = 0;
            foreach (var span in spans)
            {
                if (span.start < cursor)
                    continue;
                sb.Append(Escape(line.Substring(cursor, span.start - cursor)));
                sb.Append("<color=").Append(span.color).Append('>');
                sb.Append(Escape(line.Substring(span.start, span.length)));
                sb.Append("</color>");
                cursor = span.start + span.length;
            }
            sb.Append(Escape(line.Substring(cursor)));
            return sb.ToString();
        }

        private static string Escape(string text) =>
            text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        private static string FirstWord(string trimmed)
        {
            int spaceIndex = trimmed.IndexOf(' ');
            return spaceIndex < 0 ? trimmed : trimmed.Substring(0, spaceIndex);
        }

        private static string KeywordColor => EditorGUIUtility.isProSkin ? "#569CD6" : "#0000FF";
        private static string CommentColor => EditorGUIUtility.isProSkin ? "#6A9955" : "#008000";
        private static string CharacterIdColor => EditorGUIUtility.isProSkin ? "#4EC9B0" : "#267F99";
        private static string CommandColor => EditorGUIUtility.isProSkin ? "#DCDCAA" : "#795E26";
        private static string TagColor => EditorGUIUtility.isProSkin ? "#C586C0" : "#AF00DB";
        private static string StringLiteralColor => EditorGUIUtility.isProSkin ? "#CE9178" : "#A31515";
        private static string LabelColor => EditorGUIUtility.isProSkin ? "#D7BA7D" : "#800000";
    }
}
```

Note why the tests check for `<color=#......>` structurally (via regex) rather than asserting an exact hex value: the color depends on `EditorGUIUtility.isProSkin`, which reflects whichever Editor theme happens to be active when the test runs — not something a test controls. Asserting structure (a span wraps the right token) rather than a specific theme's hex code keeps the tests deterministic regardless of the runner's theme.

- [ ] **Step 4: Run tests to verify they pass**

Run the EditMode test command from Global Constraints.
Expected: `failed="0"`, 8 new tests passing (running total from 168: **176**).

- [ ] **Step 5: Commit**

```bash
git add Editor/DslSyntaxHighlighter.cs Tests/Editor/DslSyntaxHighlighterTests.cs
git commit -m "$(cat <<'EOF'
Add DslSyntaxHighlighter: rich-text syntax coloring for the DSL

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: `DslAutocompleteProvider`

**Files:**
- Create: `Editor/DslAutocompleteProvider.cs`
- Create: `Tests/Editor/DslAutocompleteProviderTests.cs`

**Interfaces:**
- Produces: `NovelForge.Editor.DslAutocompleteProvider.GetCandidates(string source, int cursorPosition, IEnumerable<string> commands, IEnumerable<string> characters)` → `IReadOnlyList<string>`. Task 4 calls this with `CommandRegistry.CreateDefault().RegisteredNames` and a project-wide `CharacterDefinition.Id` scan as the two injected sources.

This task is fully independent of Task 1 and Task 2 — it takes `commands`/`characters` as plain `IEnumerable<string>` parameters, with no compile-time reference to `CommandRegistry` or `CharacterDefinition` at all, keeping it testable with plain arrays.

- [ ] **Step 1: Write the failing tests**

Create `Tests/Editor/DslAutocompleteProviderTests.cs`:

```csharp
using NUnit.Framework;

namespace NovelForge.Editor.Tests
{
    public class DslAutocompleteProviderTests
    {
        [Test]
        public void GetCandidates_FiltersByPartialWordBeforeCursor_PrefixMatchesFirst()
        {
            var commands = new[] { "bg", "background_music", "wait" };

            var result = DslAutocompleteProvider.GetCandidates("b", 1, commands, System.Array.Empty<string>());

            CollectionAssert.AreEqual(new[] { "background_music", "bg" }, result);
        }

        [Test]
        public void GetCandidates_IncludesContainsMatchesAfterPrefixMatches()
        {
            var commands = new[] { "unbg", "bg" };

            var result = DslAutocompleteProvider.GetCandidates("bg", 2, commands, System.Array.Empty<string>());

            CollectionAssert.AreEqual(new[] { "bg", "unbg" }, result);
        }

        [Test]
        public void GetCandidates_ExtractsLabelsFromSourceText_RegardlessOfOtherContent()
        {
            string source = "label greet\njump nowhere_undefined\nlabel farewell\n";

            var result = DslAutocompleteProvider.GetCandidates(source, source.Length, System.Array.Empty<string>(), System.Array.Empty<string>());

            CollectionAssert.AreEqual(new[] { "farewell", "greet" }, result);
        }

        [Test]
        public void GetCandidates_MergesCommandsCharactersAndLabels_DeduplicatingRepeats()
        {
            string source = "label Alice\n";
            var commands = new[] { "bg" };
            var characters = new[] { "Alice", "Bob" };

            var result = DslAutocompleteProvider.GetCandidates(source, 0, commands, characters);

            CollectionAssert.AreEqual(new[] { "Alice", "bg", "Bob" }, result);
        }

        [Test]
        public void GetCandidates_CursorMidWord_ExtractsPartialUpToCursorOnly()
        {
            var commands = new[] { "greet", "growl" };

            var result = DslAutocompleteProvider.GetCandidates("jump gr", 7, commands, System.Array.Empty<string>());

            CollectionAssert.AreEqual(new[] { "greet", "growl" }, result);
        }

        [Test]
        public void GetCandidates_NullOrEmptySource_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => DslAutocompleteProvider.GetCandidates(null, 0, System.Array.Empty<string>(), System.Array.Empty<string>()));
            Assert.DoesNotThrow(() => DslAutocompleteProvider.GetCandidates(string.Empty, 0, null, null));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the EditMode test command from Global Constraints.
Expected: compile error (`DslAutocompleteProvider` does not exist yet).

- [ ] **Step 3: Implement `DslAutocompleteProvider`**

Create `Editor/DslAutocompleteProvider.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace NovelForge.Editor
{
    public static class DslAutocompleteProvider
    {
        private static readonly Regex LabelLine = new(@"^\s*label\s+(\S+)\s*$", RegexOptions.Compiled);

        public static IReadOnlyList<string> GetCandidates(string source, int cursorPosition, IEnumerable<string> commands, IEnumerable<string> characters)
        {
            string partial = ExtractPartialWord(source, cursorPosition);

            var allCandidates = new List<string>();
            allCandidates.AddRange(commands ?? Enumerable.Empty<string>());
            allCandidates.AddRange(characters ?? Enumerable.Empty<string>());
            allCandidates.AddRange(ExtractLabels(source));

            var startsWith = new List<string>();
            var contains = new List<string>();
            var seen = new HashSet<string>();

            foreach (string candidate in allCandidates)
            {
                if (string.IsNullOrEmpty(candidate) || !seen.Add(candidate))
                    continue;

                if (candidate.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
                    startsWith.Add(candidate);
                else if (candidate.IndexOf(partial, StringComparison.OrdinalIgnoreCase) >= 0)
                    contains.Add(candidate);
            }

            startsWith.Sort(StringComparer.OrdinalIgnoreCase);
            contains.Sort(StringComparer.OrdinalIgnoreCase);

            startsWith.AddRange(contains);
            return startsWith;
        }

        private static string ExtractPartialWord(string source, int cursorPosition)
        {
            if (string.IsNullOrEmpty(source) || cursorPosition <= 0)
                return string.Empty;

            int clampedCursor = Mathf.Clamp(cursorPosition, 0, source.Length);
            int start = clampedCursor;
            while (start > 0 && IsWordChar(source[start - 1]))
                start--;

            return source.Substring(start, clampedCursor - start);
        }

        private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

        private static IEnumerable<string> ExtractLabels(string source)
        {
            if (string.IsNullOrEmpty(source))
                yield break;

            foreach (string line in source.Replace("\r\n", "\n").Split('\n'))
            {
                var match = LabelLine.Match(line);
                if (match.Success)
                    yield return match.Groups[1].Value;
            }
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run the EditMode test command from Global Constraints.
Expected: `failed="0"`, 6 new tests passing (running total from 176: **182**).

- [ ] **Step 5: Commit**

```bash
git add Editor/DslAutocompleteProvider.cs Tests/Editor/DslAutocompleteProviderTests.cs
git commit -m "$(cat <<'EOF'
Add DslAutocompleteProvider: command/character/label candidate ranking

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: `NovelScriptEditorWindow`

**Files:**
- Create: `Editor/NovelScriptEditorWindow.cs`

**Interfaces:**
- Consumes: `NovelForge.Editor.DslSyntaxHighlighter.ToRichText(string)` (Task 2), `NovelForge.Editor.DslAutocompleteProvider.GetCandidates(string, int, IEnumerable<string>, IEnumerable<string>)` (Task 3), `NovelForge.Runtime.CommandRegistry.CreateDefault().RegisteredNames` (Task 1), `NovelForge.Runtime.NovelScriptAsset` and `NovelForge.Runtime.CharacterDefinition.Id` (already-merged, from the ScriptedImporter plan and the original core phases respectively), `NovelForge.Runtime.ScriptCompiler.Compile(string)` / `NovelForge.Runtime.ParseException.LineNumber`/`.Message` (already-merged core).
- Produces: nothing consumed by a later task — this is the plan's final task.

No automated test for this task (see Global Constraints) — Step 4 below is a manual QA walkthrough instead of a test run.

- [ ] **Step 1: Implement `NovelScriptEditorWindow`**

Create `Editor/NovelScriptEditorWindow.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NovelForge.Runtime;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace NovelForge.Editor
{
    public class NovelScriptEditorWindow : EditorWindow
    {
        private const string TextAreaControlName = "NovelScriptEditorTextArea";
        private static readonly Dictionary<string, NovelScriptEditorWindow> OpenWindows = new();

        private string _assetPath;
        private string _text = string.Empty;
        private string _lastParsedText;
        private bool _isDirty;
        private ParseException _currentError;

        [OnOpenAsset(1)]
        public static bool OnOpenAsset(int instanceId, int line)
        {
            if (EditorUtility.InstanceIDToObject(instanceId) is not NovelScriptAsset)
                return false;

            Open(AssetDatabase.GetAssetPath(instanceId));
            return true;
        }

        private static void Open(string assetPath)
        {
            if (OpenWindows.TryGetValue(assetPath, out var existing) && existing != null)
            {
                existing.Focus();
                return;
            }

            var window = CreateInstance<NovelScriptEditorWindow>();
            window._assetPath = assetPath;
            window._text = File.ReadAllText(assetPath);
            window._lastParsedText = null;
            window._isDirty = false;
            window.UpdateTitle();
            OpenWindows[assetPath] = window;
            window.Show();
        }

        private void UpdateTitle()
        {
            string fileName = Path.GetFileName(_assetPath);
            titleContent = new GUIContent(_isDirty ? fileName + " *" : fileName);
        }

        private void OnLostFocus() => SaveIfDirty();

        private void OnDestroy()
        {
            SaveIfDirty();
            if (_assetPath != null && OpenWindows.TryGetValue(_assetPath, out var registered) && registered == this)
                OpenWindows.Remove(_assetPath);
        }

        private void OnGUI()
        {
            HandleKeyboardShortcuts();
            ReparseIfNeeded();

            EditorGUILayout.LabelField(_assetPath, EditorStyles.miniLabel);

            const float errorPanelHeight = 60f;
            Rect textAreaRect = GUILayoutUtility.GetRect(
                position.width, position.height - errorPanelHeight - 20f,
                GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawHighlightedTextArea(textAreaRect);

            DrawErrorPanel();
        }

        private void HandleKeyboardShortcuts()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown)
                return;

            if (e.keyCode == KeyCode.S && (e.control || e.command))
            {
                SaveIfDirty();
                e.Use();
                return;
            }

            if (e.keyCode == KeyCode.Tab && GUI.GetNameOfFocusedControl() == TextAreaControlName)
            {
                ShowAutocomplete();
                e.Use();
            }
        }

        private void DrawHighlightedTextArea(Rect rect)
        {
            var displayStyle = new GUIStyle(EditorStyles.textArea) { richText = true, wordWrap = true };
            GUI.Label(rect, DslSyntaxHighlighter.ToRichText(_text), displayStyle);

            var editStyle = new GUIStyle(displayStyle);
            editStyle.normal.textColor = Color.clear;
            editStyle.focused.textColor = Color.clear;
            editStyle.active.textColor = Color.clear;

            GUI.SetNextControlName(TextAreaControlName);
            string newText = EditorGUI.TextArea(rect, _text, editStyle);
            if (newText != _text)
            {
                _text = newText;
                _isDirty = true;
                UpdateTitle();
            }
        }

        private void DrawErrorPanel()
        {
            if (_currentError == null)
            {
                EditorGUILayout.HelpBox("No errors.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox($"Line {_currentError.LineNumber}: {_currentError.Message}", MessageType.Error);
            if (GUILayout.Button("Go to line", GUILayout.Width(100)))
                JumpToLine(_currentError.LineNumber);
        }

        private void ReparseIfNeeded()
        {
            if (_text == _lastParsedText)
                return;

            _lastParsedText = _text;
            try
            {
                new ScriptCompiler().Compile(_text);
                _currentError = null;
            }
            catch (ParseException e)
            {
                _currentError = e;
            }
        }

        private void JumpToLine(int lineNumber)
        {
            string[] lines = _text.Replace("\r\n", "\n").Split('\n');
            int targetIndex = Mathf.Clamp(lineNumber - 1, 0, lines.Length - 1);

            int caret = 0;
            for (int i = 0; i < targetIndex; i++)
                caret += lines[i].Length + 1;

            EditorGUI.FocusTextInControl(TextAreaControlName);
            SetCaretPosition(caret);
            Repaint();
        }

        private void ShowAutocomplete()
        {
            int caret = GetCaretPosition();
            IEnumerable<string> commands = CommandRegistry.CreateDefault().RegisteredNames;
            List<string> characters = FindCharacterIds();
            IReadOnlyList<string> candidates = DslAutocompleteProvider.GetCandidates(_text, caret, commands, characters);

            if (candidates.Count == 0)
                return;

            var menu = new GenericMenu();
            foreach (string candidate in candidates)
            {
                string captured = candidate;
                menu.AddItem(new GUIContent(captured), false, () => InsertCandidate(captured, caret));
            }
            menu.ShowAsContext();
        }

        private void InsertCandidate(string candidate, int caret)
        {
            int end = Mathf.Clamp(caret, 0, _text.Length);
            int start = end;
            while (start > 0 && (char.IsLetterOrDigit(_text[start - 1]) || _text[start - 1] == '_'))
                start--;

            _text = _text.Substring(0, start) + candidate + _text.Substring(end);
            _isDirty = true;
            UpdateTitle();
            Repaint();
        }

        private static List<string> FindCharacterIds()
        {
            var ids = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:CharacterDefinition"))
            {
                var def = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (def != null && !string.IsNullOrEmpty(def.Id))
                    ids.Add(def.Id);
            }
            return ids;
        }

        // Reading/writing the live cursor position inside an IMGUI TextArea has no public API — this
        // uses UnityEditor's internal recycled TextEditor via reflection, the long-standing community
        // pattern for this. If the field name differs in this Unity version, GetValue returns null and
        // every caller falls back to a safe default (act as if the cursor were at the end of the text)
        // instead of throwing.
        private int GetCaretPosition()
        {
            if (GUI.GetNameOfFocusedControl() != TextAreaControlName)
                return _text.Length;

            if (TryGetRecycledEditor(out TextEditor editor))
                return Mathf.Clamp(editor.cursorIndex, 0, _text.Length);

            return _text.Length;
        }

        private void SetCaretPosition(int position)
        {
            if (!TryGetRecycledEditor(out TextEditor editor))
                return;

            int clamped = Mathf.Clamp(position, 0, _text.Length);
            editor.cursorIndex = clamped;
            editor.selectIndex = clamped;
        }

        private static bool TryGetRecycledEditor(out TextEditor editor)
        {
            FieldInfo field = typeof(EditorGUI).GetField("s_RecycledEditor", BindingFlags.NonPublic | BindingFlags.Static);
            editor = field?.GetValue(null) as TextEditor;
            return editor != null;
        }

        private void SaveIfDirty()
        {
            if (!_isDirty || string.IsNullOrEmpty(_assetPath))
                return;

            File.WriteAllText(_assetPath, _text);
            AssetDatabase.ImportAsset(_assetPath, ImportAssetOptions.ForceUpdate);
            _isDirty = false;
            UpdateTitle();
        }
    }
}
```

- [ ] **Step 2: Run the full EditMode suite to confirm nothing else broke**

Run the EditMode test command from Global Constraints.
Expected: `failed="0"`, `passed="182"` (no new automated tests in this task — same total as after Task 3). This confirms the new file compiles cleanly inside the existing `NovelForge.Editor` assembly and didn't disturb anything.

- [ ] **Step 3: Commit**

```bash
git add Editor/NovelScriptEditorWindow.cs
git commit -m "$(cat <<'EOF'
Add NovelScriptEditorWindow: syntax-highlighted .nfscript editor with error panel and Tab autocomplete

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step 4: Manual QA walkthrough**

This window has no automated test (see Global Constraints) — verify it by hand in the Unity Editor:

1. Open the project in Unity Editor (not batch mode): launch `Unity.exe` normally against `<worktree-root>\TestProject~` (or open it via Unity Hub).
2. In the Project window, create a new text file with extension `.nfscript` somewhere under `Assets/` (e.g. `Assets/manual_qa.nfscript`) containing:
   ```
   label start
   Alice: Привет! #happy @greet left
   choice
     "Хорошо" @choice_good -> good_path
     "Плохо" -> bad_path

   label good_path
   Alice: Приятно слышать!
   return

   label bad_path
   Alice: Что случилось?
   return
   ```
3. Double-click the asset in the Project window. Confirm: a `NovelScriptEditorWindow` opens (not the OS/IDE default text editor), titled with the file name, showing the script text.
4. Confirm syntax highlighting is visibly present: `label`/`choice`/`return` keywords, `Alice` character id, `#happy`/`@greet` tags, `left` position keyword, the quoted choice option text, and the `good_path`/`bad_path` target labels each render in a distinct color from plain text. Confirm the "No errors." info box is shown (script is valid).
5. Break the script (e.g. change `-> good_path` to `-> missing_path`). Confirm the error panel updates immediately (no visible delay) to show `Line 4: Undefined label 'missing_path'.` (or the equivalent line/message for wherever you introduced the break) in place of "No errors."). Click "Go to line" — confirm the text area regains keyboard focus (exact cursor placement may or may not land precisely, per the Global Constraints note on the reflection-based caret API — that's an acceptable, already-documented limitation, not a bug to chase).
6. Fix the script back to valid. Confirm the error panel returns to "No errors.".
7. Position the text cursor right after `Al` on a new blank line and press Tab. Confirm a dropdown/context menu appears offering `Alice` (from the `CharacterDefinition` scan — if no `CharacterDefinition` asset exists in the test project yet, create one via `Assets > Create > NovelForge > Character Definition` with `id` set to `Alice` first, so this step has something to find). Select it — confirm `Al` is replaced with `Alice`.
8. Clear that line, type `b`, press Tab. Confirm the menu offers `bg` (and any other registered commands starting with or containing "b"). Select `bg` — confirm insertion.
9. Clear that line, type `good_`, press Tab. Confirm the menu offers `good_path` (harvested from the `label good_path` line already in the script). Select it.
10. Type any change, then click outside the window (defocus it) without pressing Ctrl+S. Confirm the window title shows a trailing `*` before the click, and that after clicking away and reopening the same asset (double-click it again in the Project window), the change persisted to disk (i.e. `OnLostFocus` saved it) — the reopened window (or `NovelScriptAsset.Source` in the Inspector) reflects the edit.
11. Delete the `Assets/manual_qa.nfscript` test file (and its `CharacterDefinition` if created solely for this walkthrough) when done, so it doesn't linger as project clutter.

Report the outcome of each numbered step (pass/fail/partial) in your task report. A "partial" on step 5's precise cursor jump is acceptable per Global Constraints; a failure on any other step is a real bug to fix before this task can be marked complete.
