# Branch Graph Editor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A `GraphView`-based `EditorWindow` that reads and writes `.nfscript` branching structure visually — one node per `label`-block, edges from `jump`/`gosub`/`choice` targets (plus dashed fall-through edges), drag-to-reconnect, add/delete nodes, with the `.nfscript` text file remaining the single source of truth.

**Architecture:** Two independent, fully unit-testable pure-logic pieces — `Editor/GraphDocumentParser.cs` (text → `GraphDocument` model, plus pure broken-reference/unreachable-label analysis) and `Editor/GraphDocumentFormatter.cs` (`GraphDocument` → canonical text) — followed by two increments of one untested `GraphView` GUI shell, `Editor/NovelBranchGraphWindow.cs`: Task 3 builds read-side rendering (open a script, see an accurate live graph), Task 4 adds write-side interactivity (drag-to-reconnect, add/delete nodes, save, conflict dialog, node-layout persistence).

**Tech Stack:** C#, Unity 6000.6, `UnityEditor.Experimental.GraphView` + UI Toolkit (`TextField`, `Port`, `Edge`) — first use of UI Toolkit in this project's editor tooling (everything else is IMGUI); confirmed built into `UnityEditor.dll` for this Unity version, no new package dependency. NUnit (Unity Test Framework, EditMode) for Tasks 1-2.

**Spec:** [docs/superpowers/specs/2026-09-12-editor-branch-graph-design.md](../specs/2026-09-12-editor-branch-graph-design.md). Out of scope per the spec: multi-select/group operations, minimap/zoom-to-fit polish, undo/redo beyond what `TextField`/`GraphView` give natively, cascading delete of references to a deleted node.

## Global Constraints

- Unity Editor version: **6000.6.0f1**, at `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe`. Package id `com.novelforge.core`, package root `G:\ClaudeProjects\NovelForge`.
- No new package dependency. `GraphView` is `UnityEditor.Experimental.GraphView` (confirmed: this Unity version has not promoted it out of the `Experimental` namespace — do not use a bare `UnityEditor.GraphView` using directive, it does not exist here).
- `NovelForge.Editor.asmdef` may need `"UnityEditor.GraphView"` added under a References/assembly-reference mechanism if `UnityEditor.Experimental.GraphView` types don't resolve by default in Task 3's first build — Editor assemblies normally see all of `UnityEditor` implicitly, so this is expected to just work, but Task 3's Step 2 (build/compile check) is the actual verification; if it doesn't resolve, add `"GUID:...UnityEditor.Graphs..."` is NOT the fix — instead check the Unity console for the exact missing-assembly error and add that literal assembly name to `precompiledReferences`/`references` in `Editor/NovelForge.Editor.asmdef`, whatever the console names.
- Tasks 1-2 are new files in the existing `NovelForge.Editor`/`NovelForge.Editor.Tests` assemblies — no asmdef changes expected for them specifically.
- Tasks 3-4 (`NovelBranchGraphWindow`) have **no automated test** — same established precedent as `NovelScriptEditorWindow` and `CharacterEditorWindow`: IMGUI/UI-Toolkit `EditorWindow` code has no established way to drive a real interaction cycle from an EditMode test in this project. Task 4's own step list ends with a **live QA pass in a running Unity Editor via computer-use** — required before this task (and the whole plan) can be considered complete, per this project's established practice (the Script Editor Window phase merged a completely non-functional IMGUI feature that passed full code review — live QA is what caught it).
- The canonical text format for choice options is exactly what `ScriptCompiler.ChoiceOptionLine` already parses: `^"([^"]*)"(?:\s*@([A-Za-z_][A-Za-z0-9_]*))?\s*->\s*([A-Za-z_][A-Za-z0-9_]*)$` — i.e. `"text" -> target` or `"text" @id -> target`. Anything the formatter writes must stay parseable by the already-merged `ScriptCompiler`, since that's what actually runs scripts at play time.
- `.meta` files must be genuinely Unity-generated, never hand-authored. Every `git add` must be of the whole containing folder, never individual bare file paths — check `git status` after every commit regardless.
- Git commit messages end with: `Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>`.
- Use the working directory's own `TestProject~`, addressed with absolute paths for every argument to the Unity test command:
  ```powershell
  & "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe" -batchmode -projectPath "<worktree-root>\TestProject~" -runTests -testPlatform EditMode -testResults "<worktree-root>\TestProject~\TestResults.xml" -logFile "<worktree-root>\TestProject~\Logs\RunTests.log"
  ```
  Deliberately no `-quit`. After launching, **poll for completion synchronously within your own tool calls** (wait ~20-30 seconds, check for a fresh `TestResults.xml`, repeat up to ~90s total) — never end your turn assuming something else will resume you. If you see "Couldn't set project path", check `TestProject~\Assets` exists (`New-Item -ItemType Directory -Force -Path "<worktree-root>\TestProject~\Assets"` if not) — this is a known, recurring per-worktree gotcha in this project, confirmed to be the actual cause every time it has come up so far (not a path-quoting bug). If a run fails with an `ILPPTrigger: Can't find file \\.\pipe\...` error, that is a known transient Unity toolchain glitch — retry the same command once.
- Baseline before this plan: **190 passing tests** (confirmed on current `master`). Each task states the expected running total after it.
- **`UnityEditor.Experimental.GraphView` API risk:** this plan's Tasks 3-4 code was written from well-established, long-stable `GraphView` usage patterns (the same shapes used across years of Unity's own samples and third-party node-editor tools: `Port.Create<Edge>(Orientation, Direction, Port.Capacity, Type)`, `new Edge { output = ..., input = ... }`, `node.RefreshPorts()`/`RefreshExpandedState()`, `GraphView` manipulators) but was **not compiled against this exact Unity version before being written into this plan** — there is no compiler available at plan-writing time. If the Unity console reports a compile error naming a specific missing/renamed member on a `GraphView`/`Port`/`Edge`/`Node` type, fix that specific line using the Unity API documentation or IntelliSense/autocomplete in the editor (Window > Package Manager docs, or hovering the red-underlined symbol) and continue — this is ordinary first-use-of-a-new-API implementation work, not a plan defect requiring escalation back to the controller. Only escalate if an entire *class* of functionality this plan relies on (e.g. `GraphView` itself not resolving at all) turns out to be unavailable.

---

### Task 1: `GraphDocument` model + `GraphDocumentParser`

**Files:**
- Create: `Editor/GraphDocumentParser.cs`
- Create: `Tests/Editor/GraphDocumentParserTests.cs`

**Interfaces:**
- Produces:
  - `NovelForge.Editor.GraphNode` — `public string LabelName`, `public string Body`, `public IReadOnlyList<ChoiceOption> Choices`, `public string TrailingJumpTarget`, `public bool TrailingIsGosub`, `public bool EndsInReturn`, `public string LeadingComment`.
  - `NovelForge.Editor.ChoiceOption` — readonly struct, `public string Text`, `public string ExplicitId`, `public string TargetLabel`.
  - `NovelForge.Editor.GraphDocument` — `public IReadOnlyList<GraphNode> Nodes`, `public string GetFallThroughTarget(int nodeIndex)` → `string` (label name of the implicit next-node target, or `null`), `public IReadOnlyList<(string SourceLabel, string TargetLabel)> GetBrokenReferences()`, `public IReadOnlyList<string> GetUnreachableLabels()`.
  - `NovelForge.Editor.GraphDocumentParser.Parse(string source)` → `GraphDocument`. Task 2's formatter tests and Task 3's window both construct/consume `GraphDocument`/`GraphNode`/`ChoiceOption` with these exact shapes.

This task is fully independent of Task 2 (Task 2 depends on it, not the reverse).

- [ ] **Step 1: Write the failing tests**

Create `Tests/Editor/GraphDocumentParserTests.cs`:

```csharp
using System.Linq;
using NUnit.Framework;

namespace NovelForge.Editor.Tests
{
    public class GraphDocumentParserTests
    {
        [Test]
        public void Parse_EmptyOrNullSource_ReturnsEmptyDocumentWithoutThrowing()
        {
            Assert.AreEqual(0, GraphDocumentParser.Parse(null).Nodes.Count);
            Assert.AreEqual(0, GraphDocumentParser.Parse(string.Empty).Nodes.Count);
        }

        [Test]
        public void Parse_SingleLabelWithBodyAndJump_ProducesOneNodeWithTrailingJump()
        {
            string source = "label start\nAlice: Hello!\njump ending\n";

            var doc = GraphDocumentParser.Parse(source);

            Assert.AreEqual(1, doc.Nodes.Count);
            var node = doc.Nodes[0];
            Assert.AreEqual("start", node.LabelName);
            Assert.AreEqual("Alice: Hello!", node.Body);
            Assert.AreEqual("ending", node.TrailingJumpTarget);
            Assert.IsFalse(node.TrailingIsGosub);
            Assert.IsFalse(node.EndsInReturn);
            CollectionAssert.IsEmpty(node.Choices);
        }

        [Test]
        public void Parse_TrailingGosub_SetsTrailingIsGosubTrue()
        {
            var doc = GraphDocumentParser.Parse("label a\ngosub helper\n");

            Assert.AreEqual("helper", doc.Nodes[0].TrailingJumpTarget);
            Assert.IsTrue(doc.Nodes[0].TrailingIsGosub);
        }

        [Test]
        public void Parse_TrailingReturn_SetsEndsInReturnTrue()
        {
            var doc = GraphDocumentParser.Parse("label a\nAlice: Bye!\nreturn\n");

            Assert.IsTrue(doc.Nodes[0].EndsInReturn);
            Assert.IsNull(doc.Nodes[0].TrailingJumpTarget);
            Assert.AreEqual("Alice: Bye!", doc.Nodes[0].Body);
        }

        [Test]
        public void Parse_TrailingChoiceBlock_ExtractsAllOptionsWithExplicitIdAndImplicit()
        {
            string source = "label a\nAlice: Pick one.\nchoice\n  \"Yes\" @yes_opt -> good\n  \"No\" -> bad\n";

            var doc = GraphDocumentParser.Parse(source);

            var node = doc.Nodes[0];
            Assert.AreEqual("Alice: Pick one.", node.Body);
            Assert.AreEqual(2, node.Choices.Count);
            Assert.AreEqual("Yes", node.Choices[0].Text);
            Assert.AreEqual("yes_opt", node.Choices[0].ExplicitId);
            Assert.AreEqual("good", node.Choices[0].TargetLabel);
            Assert.AreEqual("No", node.Choices[1].Text);
            Assert.IsNull(node.Choices[1].ExplicitId);
            Assert.AreEqual("bad", node.Choices[1].TargetLabel);
        }

        [Test]
        public void Parse_BlockWithNoTrailingElement_LeavesTrailingFieldsNull_ForFallThrough()
        {
            string source = "label a\nAlice: Hi!\nlabel b\nAlice: Bye!\n";

            var doc = GraphDocumentParser.Parse(source);

            var first = doc.Nodes[0];
            Assert.IsNull(first.TrailingJumpTarget);
            Assert.IsFalse(first.EndsInReturn);
            CollectionAssert.IsEmpty(first.Choices);
            Assert.AreEqual("b", doc.GetFallThroughTarget(0));
        }

        [Test]
        public void Parse_LeadingCommentImmediatelyBeforeLabel_IsCapturedAndJoinedMultiline()
        {
            string source = "// first line\n// second line\nlabel a\nAlice: Hi!\n";

            var doc = GraphDocumentParser.Parse(source);

            Assert.AreEqual("first line\nsecond line", doc.Nodes[0].LeadingComment);
        }

        [Test]
        public void Parse_CommentSeparatedFromLabelByBlankLine_IsNotTreatedAsLeadingComment()
        {
            string source = "label a\nAlice: Hi!\n// orphan, stays in previous block's body\n\nlabel b\nAlice: Bye!\n";

            var doc = GraphDocumentParser.Parse(source);

            Assert.IsNull(doc.Nodes[1].LeadingComment);
            StringAssert.Contains("// orphan", doc.Nodes[0].Body);
        }

        [Test]
        public void Parse_ContentBeforeFirstLabel_BecomesUnlabeledLeadingNode()
        {
            string source = "set intro_seen = true\nlabel start\nAlice: Hi!\n";

            var doc = GraphDocumentParser.Parse(source);

            Assert.AreEqual(2, doc.Nodes.Count);
            Assert.IsNull(doc.Nodes[0].LabelName);
            Assert.AreEqual("set intro_seen = true", doc.Nodes[0].Body);
            Assert.AreEqual("start", doc.Nodes[1].LabelName);
        }

        [Test]
        public void Parse_NoContentBeforeFirstLabel_DoesNotEmitEmptyPreambleNode()
        {
            var doc = GraphDocumentParser.Parse("label start\nAlice: Hi!\n");

            Assert.AreEqual(1, doc.Nodes.Count);
            Assert.AreEqual("start", doc.Nodes[0].LabelName);
        }

        [Test]
        public void GetBrokenReferences_JumpAndChoiceToUndefinedLabel_AreBothReported()
        {
            string source = "label a\njump missing\nlabel b\nchoice\n  \"x\" -> also_missing\n";

            var doc = GraphDocumentParser.Parse(source);
            var broken = doc.GetBrokenReferences();

            Assert.AreEqual(2, broken.Count);
            Assert.IsTrue(broken.Any(r => r.SourceLabel == "a" && r.TargetLabel == "missing"));
            Assert.IsTrue(broken.Any(r => r.SourceLabel == "b" && r.TargetLabel == "also_missing"));
        }

        [Test]
        public void GetBrokenReferences_ValidTargets_ReportsNothing()
        {
            var doc = GraphDocumentParser.Parse("label a\njump b\nlabel b\nreturn\n");

            CollectionAssert.IsEmpty(doc.GetBrokenReferences());
        }

        [Test]
        public void GetUnreachableLabels_NodeWithNoIncomingReference_IsReported_ExceptTheFirstNode()
        {
            string source = "label start\nreturn\nlabel orphan\nreturn\n";

            var doc = GraphDocumentParser.Parse(source);

            CollectionAssert.AreEqual(new[] { "orphan" }, doc.GetUnreachableLabels());
        }

        [Test]
        public void GetUnreachableLabels_FallThroughTargetCountsAsReachable()
        {
            string source = "label start\nAlice: Hi!\nlabel next\nreturn\n";

            var doc = GraphDocumentParser.Parse(source);

            CollectionAssert.IsEmpty(doc.GetUnreachableLabels());
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the EditMode test command from Global Constraints.
Expected: compile error (`GraphDocumentParser`/`GraphDocument`/`GraphNode`/`ChoiceOption` do not exist yet).

- [ ] **Step 3: Implement `GraphDocumentParser`**

Create `Editor/GraphDocumentParser.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NovelForge.Editor
{
    public readonly struct ChoiceOption
    {
        public string Text { get; }
        public string ExplicitId { get; }
        public string TargetLabel { get; }

        public ChoiceOption(string text, string explicitId, string targetLabel)
        {
            Text = text;
            ExplicitId = explicitId;
            TargetLabel = targetLabel;
        }
    }

    public class GraphNode
    {
        public string LabelName { get; }
        public string Body { get; }
        public IReadOnlyList<ChoiceOption> Choices { get; }
        public string TrailingJumpTarget { get; }
        public bool TrailingIsGosub { get; }
        public bool EndsInReturn { get; }
        public string LeadingComment { get; }

        public GraphNode(string labelName, string body, IReadOnlyList<ChoiceOption> choices,
            string trailingJumpTarget, bool trailingIsGosub, bool endsInReturn, string leadingComment)
        {
            LabelName = labelName;
            Body = body;
            Choices = choices;
            TrailingJumpTarget = trailingJumpTarget;
            TrailingIsGosub = trailingIsGosub;
            EndsInReturn = endsInReturn;
            LeadingComment = leadingComment;
        }
    }

    public class GraphDocument
    {
        public IReadOnlyList<GraphNode> Nodes { get; }

        public GraphDocument(IReadOnlyList<GraphNode> nodes)
        {
            Nodes = nodes;
        }

        // The DSL has no explicit "end of block" marker — a block that ends without
        // jump/gosub/choice/return simply falls through into whatever is physically next,
        // exactly like the compiled runtime does (ScriptCompiler never stops appending
        // commands at a label boundary). Surfaced separately from the other trailing fields
        // so the window can render it as a distinct (dashed) edge.
        public string GetFallThroughTarget(int nodeIndex)
        {
            if (nodeIndex < 0 || nodeIndex >= Nodes.Count - 1)
                return null;
            GraphNode node = Nodes[nodeIndex];
            if (node.EndsInReturn || node.TrailingJumpTarget != null || node.Choices.Count > 0)
                return null;
            return Nodes[nodeIndex + 1].LabelName;
        }

        public IReadOnlyList<(string SourceLabel, string TargetLabel)> GetBrokenReferences()
        {
            var defined = new HashSet<string>();
            foreach (GraphNode n in Nodes)
                if (n.LabelName != null)
                    defined.Add(n.LabelName);

            var broken = new List<(string, string)>();
            foreach (GraphNode node in Nodes)
            {
                if (node.TrailingJumpTarget != null && !defined.Contains(node.TrailingJumpTarget))
                    broken.Add((node.LabelName, node.TrailingJumpTarget));
                foreach (ChoiceOption choice in node.Choices)
                    if (!defined.Contains(choice.TargetLabel))
                        broken.Add((node.LabelName, choice.TargetLabel));
            }
            return broken;
        }

        public IReadOnlyList<string> GetUnreachableLabels()
        {
            var referenced = new HashSet<string>();
            for (int i = 0; i < Nodes.Count; i++)
            {
                GraphNode node = Nodes[i];
                if (node.TrailingJumpTarget != null)
                    referenced.Add(node.TrailingJumpTarget);
                foreach (ChoiceOption choice in node.Choices)
                    referenced.Add(choice.TargetLabel);

                string fallThrough = GetFallThroughTarget(i);
                if (fallThrough != null)
                    referenced.Add(fallThrough);
            }

            var unreachable = new List<string>();
            for (int i = 1; i < Nodes.Count; i++) // node 0 is the entry point, always reachable
            {
                if (Nodes[i].LabelName != null && !referenced.Contains(Nodes[i].LabelName))
                    unreachable.Add(Nodes[i].LabelName);
            }
            return unreachable;
        }
    }

    public static class GraphDocumentParser
    {
        private static readonly Regex ChoiceOptionLine = new(@"^""([^""]*)""(?:\s*@([A-Za-z_][A-Za-z0-9_]*))?\s*->\s*([A-Za-z_][A-Za-z0-9_]*)$", RegexOptions.Compiled);

        public static GraphDocument Parse(string source)
        {
            var nodes = new List<GraphNode>();
            if (string.IsNullOrEmpty(source))
                return new GraphDocument(nodes);

            string[] lines = source.Replace("\r\n", "\n").Split('\n');

            var labelStarts = new List<(int LineIndex, string Name, int CommentStart)>();
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();
                if (!trimmed.StartsWith("label ", StringComparison.Ordinal))
                    continue;

                string name = trimmed.Substring("label ".Length).Trim();
                int commentStart = i;
                int j = i - 1;
                while (j >= 0 && lines[j].Trim().StartsWith("//", StringComparison.Ordinal))
                {
                    commentStart = j;
                    j--;
                }
                labelStarts.Add((i, name, commentStart));
            }

            // Content before the first label (rare in practice — every script in this project
            // starts with a label) becomes an unlabeled leading node rather than being dropped
            // or throwing.
            int firstBoundary = labelStarts.Count > 0 ? labelStarts[0].CommentStart : lines.Length;
            if (HasContent(lines, 0, firstBoundary))
                nodes.Add(BuildNode(lines, 0, firstBoundary, null, null));

            for (int k = 0; k < labelStarts.Count; k++)
            {
                (int lineIndex, string name, int commentStart) = labelStarts[k];
                string leadingComment = ExtractComment(lines, commentStart, lineIndex);
                int blockStart = lineIndex + 1;
                int blockEnd = k + 1 < labelStarts.Count ? labelStarts[k + 1].CommentStart : lines.Length;
                nodes.Add(BuildNode(lines, blockStart, blockEnd, name, leadingComment));
            }

            return new GraphDocument(nodes);
        }

        private static bool HasContent(string[] lines, int start, int end)
        {
            for (int i = start; i < end; i++)
            {
                string trimmed = lines[i].Trim();
                if (trimmed.Length > 0 && !trimmed.StartsWith("//", StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static string ExtractComment(string[] lines, int start, int end)
        {
            if (start >= end)
                return null;
            var commentLines = new List<string>();
            for (int i = start; i < end; i++)
                commentLines.Add(lines[i].Trim().Substring(2).Trim());
            return string.Join("\n", commentLines);
        }

        // Finds the block's trailing control-flow element (return / jump / gosub / choice)
        // by scanning backward from the last non-blank, non-comment line — everything before
        // it is Body, preserved verbatim. A block with none of these (or where a choice-looking
        // line has no matching "choice" keyword above it) has no trailing element at all: it's
        // either a fall-through (GraphDocument.GetFallThroughTarget handles that) or malformed
        // input that's simply kept as opaque Body text rather than rejected.
        private static GraphNode BuildNode(string[] lines, int start, int end, string labelName, string leadingComment)
        {
            int lastContentIndex = -1;
            for (int i = end - 1; i >= start; i--)
            {
                string trimmed = lines[i].Trim();
                if (trimmed.Length > 0 && !trimmed.StartsWith("//", StringComparison.Ordinal))
                {
                    lastContentIndex = i;
                    break;
                }
            }

            if (lastContentIndex < 0)
                return new GraphNode(labelName, TrimTrailingBlankLines(lines, start, end), Array.Empty<ChoiceOption>(), null, false, false, leadingComment);

            string lastTrimmed = lines[lastContentIndex].Trim();

            if (lastTrimmed == "return")
                return new GraphNode(labelName, JoinLines(lines, start, lastContentIndex), Array.Empty<ChoiceOption>(), null, false, true, leadingComment);

            if (lastTrimmed.StartsWith("jump ", StringComparison.Ordinal))
                return new GraphNode(labelName, JoinLines(lines, start, lastContentIndex), Array.Empty<ChoiceOption>(), lastTrimmed.Substring("jump ".Length).Trim(), false, false, leadingComment);

            if (lastTrimmed.StartsWith("gosub ", StringComparison.Ordinal))
                return new GraphNode(labelName, JoinLines(lines, start, lastContentIndex), Array.Empty<ChoiceOption>(), lastTrimmed.Substring("gosub ".Length).Trim(), true, false, leadingComment);

            if (ChoiceOptionLine.IsMatch(lastTrimmed))
            {
                var options = new List<ChoiceOption>();
                int i = lastContentIndex;
                int choiceKeywordIndex = -1;
                while (i >= start)
                {
                    string trimmed = lines[i].Trim();
                    if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal))
                    {
                        i--;
                        continue;
                    }
                    if (trimmed == "choice")
                    {
                        choiceKeywordIndex = i;
                        break;
                    }
                    Match match = ChoiceOptionLine.Match(trimmed);
                    if (!match.Success)
                        break;
                    string explicitId = match.Groups[2].Success ? match.Groups[2].Value : null;
                    options.Insert(0, new ChoiceOption(match.Groups[1].Value, explicitId, match.Groups[3].Value));
                    i--;
                }

                if (choiceKeywordIndex >= 0)
                    return new GraphNode(labelName, JoinLines(lines, start, choiceKeywordIndex), options, null, false, false, leadingComment);
            }

            // No trailing control-flow element: a fall-through block (or malformed input we
            // don't try to interpret further). Preserve everything as Body, trimming only
            // fully-blank trailing lines (the canonical blank-line separator before the next
            // label) — a trailing comment that isn't immediately attached to the *next* label
            // (see ExtractComment) still belongs to *this* block's Body, verbatim, so it must
            // NOT be trimmed the way a blank line is.
            return new GraphNode(labelName, TrimTrailingBlankLines(lines, start, end), Array.Empty<ChoiceOption>(), null, false, false, leadingComment);
        }

        private static string TrimTrailingBlankLines(string[] lines, int start, int end)
        {
            int trimmedEnd = end;
            while (trimmedEnd > start && lines[trimmedEnd - 1].Trim().Length == 0)
                trimmedEnd--;
            return JoinLines(lines, start, trimmedEnd);
        }

        private static string JoinLines(string[] lines, int start, int end)
        {
            if (end <= start)
                return string.Empty;
            return string.Join("\n", lines, start, end - start);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run the EditMode test command from Global Constraints.
Expected: `failed="0"`, 14 new tests passing (running total from 190: **204**).

- [ ] **Step 5: Commit**

```bash
git add Editor/GraphDocumentParser.cs Tests/Editor/GraphDocumentParserTests.cs
git commit -m "$(cat <<'EOF'
Add GraphDocumentParser: parse .nfscript into a label-block graph model

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: `GraphDocumentFormatter`

**Files:**
- Create: `Editor/GraphDocumentFormatter.cs`
- Create: `Tests/Editor/GraphDocumentFormatterTests.cs`

**Interfaces:**
- Consumes: `NovelForge.Editor.GraphDocument`/`GraphNode`/`ChoiceOption` (Task 1).
- Produces: `NovelForge.Editor.GraphDocumentFormatter.Serialize(GraphDocument document)` → `string`. Task 4 calls this to write the graph's Save output back to the `.nfscript` file.

This task depends only on Task 1's types (not on Task 1's parser logic at runtime — though its round-trip tests do call `GraphDocumentParser.Parse` to build input documents).

- [ ] **Step 1: Write the failing tests**

Create `Tests/Editor/GraphDocumentFormatterTests.cs`:

```csharp
using NUnit.Framework;

namespace NovelForge.Editor.Tests
{
    public class GraphDocumentFormatterTests
    {
        [Test]
        public void Serialize_EmptyDocument_ReturnsEmptyString()
        {
            var doc = GraphDocumentParser.Parse(string.Empty);

            Assert.AreEqual(string.Empty, GraphDocumentFormatter.Serialize(doc));
        }

        [Test]
        public void Serialize_SingleNodeWithJump_RoundTripsExactly()
        {
            string source = "label start\nAlice: Hello!\njump ending\n";

            var doc = GraphDocumentParser.Parse(source);

            Assert.AreEqual(source, GraphDocumentFormatter.Serialize(doc));
        }

        [Test]
        public void Serialize_NodeWithGosub_RoundTripsExactly()
        {
            string source = "label a\ngosub helper\n";

            var doc = GraphDocumentParser.Parse(source);

            Assert.AreEqual(source, GraphDocumentFormatter.Serialize(doc));
        }

        [Test]
        public void Serialize_NodeWithReturn_RoundTripsExactly()
        {
            string source = "label a\nAlice: Bye!\nreturn\n";

            var doc = GraphDocumentParser.Parse(source);

            Assert.AreEqual(source, GraphDocumentFormatter.Serialize(doc));
        }

        [Test]
        public void Serialize_NodeWithChoiceOptions_RoundTripsExactly()
        {
            string source = "label a\nAlice: Pick one.\nchoice\n  \"Yes\" @yes_opt -> good\n  \"No\" -> bad\n";

            var doc = GraphDocumentParser.Parse(source);

            Assert.AreEqual(source, GraphDocumentFormatter.Serialize(doc));
        }

        [Test]
        public void Serialize_SingleLineLeadingComment_RoundTripsExactly()
        {
            string source = "// a note\nlabel a\nAlice: Hi!\n";

            var doc = GraphDocumentParser.Parse(source);

            Assert.AreEqual(source, GraphDocumentFormatter.Serialize(doc));
        }

        [Test]
        public void Serialize_MultiLineLeadingComment_RoundTripsExactly()
        {
            string source = "// first line\n// second line\nlabel a\nAlice: Hi!\n";

            var doc = GraphDocumentParser.Parse(source);

            Assert.AreEqual(source, GraphDocumentFormatter.Serialize(doc));
        }

        [Test]
        public void Serialize_MultiNodeScriptWithBlankLineSeparators_RoundTripsExactly()
        {
            string source = "label a\nAlice: Hi!\njump b\n\nlabel b\nAlice: Bye!\nreturn\n";

            var doc = GraphDocumentParser.Parse(source);

            Assert.AreEqual(source, GraphDocumentFormatter.Serialize(doc));
        }

        [Test]
        public void Serialize_FallThroughNodeWithBlankLineSeparator_RoundTripsExactly()
        {
            string source = "label a\nAlice: Hi!\n\nlabel b\nAlice: Bye!\nreturn\n";

            var doc = GraphDocumentParser.Parse(source);

            Assert.AreEqual(source, GraphDocumentFormatter.Serialize(doc));
        }

        [Test]
        public void Serialize_OrphanCommentNotAttachedToNextLabel_StaysInPreviousNodesBody()
        {
            string source = "label a\nAlice: Hi!\n// orphan, stays in previous block's body\n\nlabel b\nAlice: Bye!\n";

            var doc = GraphDocumentParser.Parse(source);

            Assert.AreEqual(source, GraphDocumentFormatter.Serialize(doc));
        }

        [Test]
        public void Serialize_ThenReparse_IsIdempotent_ForPreambleWithNoBlankLineBeforeFirstLabel()
        {
            // The formatter always inserts a blank line before every node including the first
            // real label after an unlabeled preamble, so the very first Serialize() of
            // hand-authored text without that blank line won't be byte-identical to the input —
            // but re-parsing its own output must reproduce an equivalent document, proving the
            // formatter's own conventions are stable under repeated save/reload.
            string source = "set intro_seen = true\nlabel start\nAlice: Hi!\n";
            var original = GraphDocumentParser.Parse(source);

            string serialized = GraphDocumentFormatter.Serialize(original);
            var reparsed = GraphDocumentParser.Parse(serialized);

            Assert.AreEqual(original.Nodes.Count, reparsed.Nodes.Count);
            for (int i = 0; i < original.Nodes.Count; i++)
            {
                Assert.AreEqual(original.Nodes[i].LabelName, reparsed.Nodes[i].LabelName);
                Assert.AreEqual(original.Nodes[i].Body, reparsed.Nodes[i].Body);
            }

            string reserialized = GraphDocumentFormatter.Serialize(reparsed);
            Assert.AreEqual(serialized, reserialized);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the EditMode test command from Global Constraints.
Expected: compile error (`GraphDocumentFormatter` does not exist yet).

- [ ] **Step 3: Implement `GraphDocumentFormatter`**

Create `Editor/GraphDocumentFormatter.cs`:

```csharp
using System.Text;

namespace NovelForge.Editor
{
    public static class GraphDocumentFormatter
    {
        public static string Serialize(GraphDocument document)
        {
            var sb = new StringBuilder();
            bool first = true;
            foreach (GraphNode node in document.Nodes)
            {
                if (!first)
                    sb.Append('\n');
                first = false;
                AppendNode(sb, node);
            }
            return sb.ToString();
        }

        private static void AppendNode(StringBuilder sb, GraphNode node)
        {
            if (node.LeadingComment != null)
            {
                foreach (string line in node.LeadingComment.Split('\n'))
                    sb.Append("// ").Append(line).Append('\n');
            }

            if (node.LabelName != null)
                sb.Append("label ").Append(node.LabelName).Append('\n');

            if (!string.IsNullOrEmpty(node.Body))
                sb.Append(node.Body).Append('\n');

            if (node.EndsInReturn)
            {
                sb.Append("return\n");
            }
            else if (node.TrailingJumpTarget != null)
            {
                sb.Append(node.TrailingIsGosub ? "gosub " : "jump ").Append(node.TrailingJumpTarget).Append('\n');
            }
            else if (node.Choices.Count > 0)
            {
                sb.Append("choice\n");
                foreach (ChoiceOption choice in node.Choices)
                {
                    sb.Append("  \"").Append(choice.Text).Append('"');
                    if (choice.ExplicitId != null)
                        sb.Append(" @").Append(choice.ExplicitId);
                    sb.Append(" -> ").Append(choice.TargetLabel).Append('\n');
                }
            }
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run the EditMode test command from Global Constraints.
Expected: `failed="0"`, 11 new tests passing (running total from 204: **215**).

- [ ] **Step 5: Commit**

```bash
git add Editor/GraphDocumentFormatter.cs Tests/Editor/GraphDocumentFormatterTests.cs
git commit -m "Add GraphDocumentFormatter: canonical GraphDocument to .nfscript text

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 3: `NovelBranchGraphWindow` — read-side rendering

**Files:**
- Create: `Editor/NovelBranchGraphWindow.cs`

**Interfaces:**
- Consumes: `NovelForge.Editor.GraphDocumentParser.Parse(string)` (Task 1), `NovelForge.Editor.GraphDocument`/`GraphNode`/`ChoiceOption` including `GetFallThroughTarget(int)`, `GetBrokenReferences()`, `GetUnreachableLabels()` (Task 1). Does not yet consume `GraphDocumentFormatter` (Task 2) — that arrives in Task 4's Save logic.
- Produces: `NovelForge.Editor.NovelBranchGraphWindow`, `NovelForge.Editor.BranchGraphView` (internal `GraphView` subclass), `NovelForge.Editor.BranchGraphNodeView` (internal `Node` subclass) with public fields `NodeIndex`, `BodyField`, `InputPort`, `TrailingPort`, `FallThroughPort`, `ChoicePorts` (`List<(Port Port, TextField TextField)>`). Task 4 modifies this same file to add interactivity — it consumes these exact field names/types to wire drag-to-reconnect and edit-tracking.

No automated test for this task (same established precedent as `NovelScriptEditorWindow`/`CharacterEditorWindow` — see Global Constraints). Its own gate is a clean compile; the mandatory live QA pass happens once, at the end of Task 4, covering both this task's rendering and Task 4's interactivity together (doing it twice would be redundant).

- [ ] **Step 1: Implement `NovelBranchGraphWindow` (read-side)**

Create `Editor/NovelBranchGraphWindow.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NovelForge.Runtime;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace NovelForge.Editor
{
    public class NovelBranchGraphWindow : EditorWindow
    {
        private string _assetPath;
        private string _text = string.Empty;
        private GraphDocument _document;
        private BranchGraphView _graphView;

        [MenuItem("NovelForge/Branch Graph")]
        public static void ShowWindowFromMenu()
        {
            string path = Selection.activeObject is NovelScriptAsset
                ? AssetDatabase.GetAssetPath(Selection.activeObject)
                : EditorUtility.OpenFilePanel("Open .nfscript", Application.dataPath, "nfscript");
            if (string.IsNullOrEmpty(path))
                return;
            Open(path);
        }

        [MenuItem("Assets/Open in Branch Graph", true)]
        public static bool ValidateOpenFromAssetsMenu() => Selection.activeObject is NovelScriptAsset;

        [MenuItem("Assets/Open in Branch Graph")]
        public static void OpenFromAssetsMenu()
        {
            Open(AssetDatabase.GetAssetPath(Selection.activeObject));
        }

        private static void Open(string assetPath)
        {
            // OpenFilePanel returns an absolute OS path; normalize to the "Assets/..."
            // project-relative form File.ReadAllText/WriteAllText and AssetDatabase both
            // accept when the Editor's working directory is the project root (it always is) —
            // the same convention NovelScriptEditorWindow already uses.
            if (assetPath.StartsWith(Application.dataPath, StringComparison.Ordinal))
                assetPath = "Assets" + assetPath.Substring(Application.dataPath.Length);

            var window = GetWindow<NovelBranchGraphWindow>("Branch Graph");
            window.Load(assetPath);
            window.Show();
        }

        private void Load(string assetPath)
        {
            _assetPath = assetPath;
            _text = File.ReadAllText(assetPath);
            _document = GraphDocumentParser.Parse(_text);
            titleContent = new GUIContent(Path.GetFileName(assetPath));
            RebuildGraphView();
        }

        private void RebuildGraphView()
        {
            if (_graphView != null)
                rootVisualElement.Remove(_graphView);
            _graphView = new BranchGraphView();
            _graphView.StretchToParentSize();
            rootVisualElement.Add(_graphView);
            _graphView.Populate(_document);
        }
    }

    internal class BranchGraphView : GraphView
    {
        public BranchGraphView()
        {
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            Insert(0, new GridBackground());
        }

        public void Populate(GraphDocument document)
        {
            DeleteElements(graphElements.ToList());

            var nodeViews = new List<BranchGraphNodeView>();
            for (int i = 0; i < document.Nodes.Count; i++)
            {
                bool hasFallThrough = document.GetFallThroughTarget(i) != null;
                var nodeView = new BranchGraphNodeView(i, document.Nodes[i], hasFallThrough);
                nodeView.SetPosition(new Rect(i * 260, 0, 220, 150));
                AddElement(nodeView);
                nodeViews.Add(nodeView);
            }

            var unreachable = new HashSet<string>(document.GetUnreachableLabels());
            for (int i = 0; i < nodeViews.Count; i++)
            {
                string labelName = document.Nodes[i].LabelName;
                if (labelName != null && unreachable.Contains(labelName))
                    nodeViews[i].MarkUnreachable();

                ConnectPort(nodeViews, document, nodeViews[i].TrailingPort, document.Nodes[i].TrailingJumpTarget, isDashed: false);
                for (int c = 0; c < nodeViews[i].ChoicePorts.Count; c++)
                    ConnectPort(nodeViews, document, nodeViews[i].ChoicePorts[c].Port, document.Nodes[i].Choices[c].TargetLabel, isDashed: false);

                string fallThrough = document.GetFallThroughTarget(i);
                if (fallThrough != null)
                    ConnectPort(nodeViews, document, nodeViews[i].FallThroughPort, fallThrough, isDashed: true);
            }
        }

        private void ConnectPort(List<BranchGraphNodeView> nodeViews, GraphDocument document, Port sourcePort, string targetLabel, bool isDashed)
        {
            if (sourcePort == null || targetLabel == null)
                return;

            int targetIndex = -1;
            for (int i = 0; i < document.Nodes.Count; i++)
            {
                if (document.Nodes[i].LabelName == targetLabel)
                {
                    targetIndex = i;
                    break;
                }
            }

            if (targetIndex < 0)
            {
                sourcePort.AddToClassList("broken-reference");
                return;
            }

            var edge = new Edge { output = sourcePort, input = nodeViews[targetIndex].InputPort };
            sourcePort.Connect(edge);
            nodeViews[targetIndex].InputPort.Connect(edge);
            if (isDashed)
                edge.AddToClassList("fall-through-edge");
            AddElement(edge);
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatible = new List<Port>();
            foreach (Port port in ports.ToList())
            {
                if (port.direction != startPort.direction && port.node != startPort.node)
                    compatible.Add(port);
            }
            return compatible;
        }
    }

    internal class BranchGraphNodeView : Node
    {
        public int NodeIndex { get; }
        public TextField BodyField { get; }
        public Port InputPort { get; }
        public Port TrailingPort { get; }
        public Port FallThroughPort { get; }
        public List<(Port Port, TextField TextField)> ChoicePorts { get; } = new();

        public BranchGraphNodeView(int nodeIndex, GraphNode node, bool hasFallThrough)
        {
            NodeIndex = nodeIndex;
            title = node.LabelName ?? "(no label)";

            InputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            InputPort.portName = string.Empty;
            inputContainer.Add(InputPort);

            BodyField = new TextField { multiline = true, value = node.Body };
            BodyField.style.minWidth = 200;
            BodyField.style.minHeight = 60;
            mainContainer.Add(BodyField);

            foreach (ChoiceOption choice in node.Choices)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                var textField = new TextField { value = choice.Text };
                textField.style.flexGrow = 1;
                var port = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
                port.portName = "->";
                row.Add(textField);
                row.Add(port);
                mainContainer.Add(row);
                ChoicePorts.Add((port, textField));
            }

            if (node.TrailingJumpTarget != null)
            {
                TrailingPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
                TrailingPort.portName = node.TrailingIsGosub ? "gosub" : "jump";
                outputContainer.Add(TrailingPort);
            }

            if (hasFallThrough)
            {
                FallThroughPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
                FallThroughPort.portName = "(fall-through)";
                outputContainer.Add(FallThroughPort);
            }

            RefreshExpandedState();
            RefreshPorts();
        }

        public void MarkUnreachable()
        {
            titleContainer.AddToClassList("unreachable-node");
        }
    }
}
```

- [ ] **Step 2: Run the full EditMode suite to confirm nothing else broke**

Run the EditMode test command from Global Constraints.
Expected: `failed="0"`, `passed="215"` (no new automated tests in this task — same total as after Task 2). This confirms the new file — including its first-ever use of `UnityEditor.Experimental.GraphView` in this project — compiles cleanly. If it does not compile, see the Global Constraints note on GraphView API risk: fix the named member per the actual compiler error, don't treat this as a plan defect.

- [ ] **Step 3: Commit**

```bash
git add Editor/NovelBranchGraphWindow.cs
git commit -m "Add NovelBranchGraphWindow: read-side GraphView rendering of .nfscript branching

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 4: `NovelBranchGraphWindow` — interactivity + persistence

**Files:**
- Modify: `Editor/NovelBranchGraphWindow.cs` (replaces the whole file — every type below supersedes Task 3's version of the same type)

**Interfaces:**
- Consumes: `NovelForge.Editor.GraphDocumentFormatter.Serialize(GraphDocument)` (Task 2), everything from Task 1/Task 3.
- Produces: nothing consumed by a later task — this is the plan's final task.

No automated test for this task (see Global Constraints). Step 3 below is the mandatory live QA pass covering both this task's interactivity and Task 3's rendering together.

- [ ] **Step 1: Replace `Editor/NovelBranchGraphWindow.cs` with the interactive version**

Replace the entire contents of `Editor/NovelBranchGraphWindow.cs` with:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NovelForge.Runtime;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace NovelForge.Editor
{
    public class NovelBranchGraphWindow : EditorWindow
    {
        private string _assetPath;
        private string _text = string.Empty;
        private string _lastSavedText = string.Empty;
        private bool _isDirty;
        private GraphDocument _document;
        private BranchGraphView _graphView;

        [MenuItem("NovelForge/Branch Graph")]
        public static void ShowWindowFromMenu()
        {
            string path = Selection.activeObject is NovelScriptAsset
                ? AssetDatabase.GetAssetPath(Selection.activeObject)
                : EditorUtility.OpenFilePanel("Open .nfscript", Application.dataPath, "nfscript");
            if (string.IsNullOrEmpty(path))
                return;
            Open(path);
        }

        [MenuItem("Assets/Open in Branch Graph", true)]
        public static bool ValidateOpenFromAssetsMenu() => Selection.activeObject is NovelScriptAsset;

        [MenuItem("Assets/Open in Branch Graph")]
        public static void OpenFromAssetsMenu()
        {
            Open(AssetDatabase.GetAssetPath(Selection.activeObject));
        }

        private static void Open(string assetPath)
        {
            if (assetPath.StartsWith(Application.dataPath, StringComparison.Ordinal))
                assetPath = "Assets" + assetPath.Substring(Application.dataPath.Length);

            var window = GetWindow<NovelBranchGraphWindow>("Branch Graph");
            window.Load(assetPath);
            window.Show();
        }

        private void Load(string assetPath)
        {
            _assetPath = assetPath;
            _text = File.ReadAllText(assetPath);
            _lastSavedText = _text;
            _isDirty = false;
            _document = GraphDocumentParser.Parse(_text);
            UpdateTitle();
            RebuildGraphView();
        }

        private void UpdateTitle()
        {
            string fileName = Path.GetFileName(_assetPath);
            titleContent = new GUIContent(_isDirty ? fileName + " *" : fileName);
        }

        private void OnLostFocus() => SaveIfDirty();

        private void OnDestroy() => SaveIfDirty();

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    SaveIfDirty();
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(_assetPath, EditorStyles.miniLabel, GUILayout.Width(300));
            }
        }

        private void RebuildGraphView()
        {
            if (_graphView != null)
                rootVisualElement.Remove(_graphView);
            _graphView = new BranchGraphView(this);
            _graphView.style.flexGrow = 1;
            rootVisualElement.Add(_graphView);
            _graphView.Populate(_document, LoadLayout());
        }

        // Every structural edit (field defocus, drag-to-reconnect, add/delete node) goes
        // through this single path: patch the node list, round-trip it through the formatter
        // and parser so the text and the in-memory model can never drift apart, then rebuild
        // the view from the freshly reparsed document. This is also what lets a user type a
        // "choice\n..." block directly into a node's Body field and have it promoted into real
        // structured choice ports — the reparse is what recognizes it, not the UI code.
        private void ApplyNodes(List<GraphNode> nodes)
        {
            string newText = GraphDocumentFormatter.Serialize(new GraphDocument(nodes));
            _document = GraphDocumentParser.Parse(newText);
            _text = newText;
            _isDirty = _text != _lastSavedText;
            UpdateTitle();
            RebuildGraphView();
        }

        private void ReplaceNode(int index, GraphNode updated)
        {
            var nodes = new List<GraphNode>(_document.Nodes) { [index] = updated };
            ApplyNodes(nodes);
        }

        public void OnBodyFieldChanged(int nodeIndex, string newBody)
        {
            GraphNode node = _document.Nodes[nodeIndex];
            ReplaceNode(nodeIndex, new GraphNode(node.LabelName, newBody, node.Choices,
                node.TrailingJumpTarget, node.TrailingIsGosub, node.EndsInReturn, node.LeadingComment));
        }

        public void OnChoiceTextChanged(int nodeIndex, int choiceIndex, string newText)
        {
            GraphNode node = _document.Nodes[nodeIndex];
            var newChoices = new List<ChoiceOption>(node.Choices);
            ChoiceOption old = newChoices[choiceIndex];
            newChoices[choiceIndex] = new ChoiceOption(newText, old.ExplicitId, old.TargetLabel);
            ReplaceNode(nodeIndex, new GraphNode(node.LabelName, node.Body, newChoices,
                node.TrailingJumpTarget, node.TrailingIsGosub, node.EndsInReturn, node.LeadingComment));
        }

        // Renaming does NOT rewrite other nodes' jump/gosub/choice targets that pointed at the
        // old name — they simply become broken references (GraphDocument.GetBrokenReferences,
        // rendered by BranchGraphView as a warning-tinted dangling port), exactly like editing
        // the label name directly in the text editor would. The author fixes them manually or
        // by dragging a port to the renamed node, same as any other broken reference.
        public void OnLabelChanged(int nodeIndex, string newLabelName)
        {
            GraphNode node = _document.Nodes[nodeIndex];
            if (newLabelName == node.LabelName)
                return;
            ReplaceNode(nodeIndex, new GraphNode(newLabelName, node.Body, node.Choices,
                node.TrailingJumpTarget, node.TrailingIsGosub, node.EndsInReturn, node.LeadingComment));
        }

        public void OnPortReconnected(BranchGraphNodeView sourceNode, Port sourcePort, BranchGraphNodeView targetNode)
        {
            if (targetNode.NodeIndex >= _document.Nodes.Count)
                return;
            string newTargetLabel = _document.Nodes[targetNode.NodeIndex].LabelName;
            if (newTargetLabel == null)
                return;

            GraphNode node = _document.Nodes[sourceNode.NodeIndex];
            GraphNode updated;
            if (sourcePort == sourceNode.TrailingPort)
            {
                updated = new GraphNode(node.LabelName, node.Body, node.Choices,
                    newTargetLabel, node.TrailingIsGosub, node.EndsInReturn, node.LeadingComment);
            }
            else if (sourcePort == sourceNode.FallThroughPort)
            {
                // Dragging a fall-through edge converts the implicit fall-through into an
                // explicit jump — there is no other way to "grab" a fall-through edge by design.
                updated = new GraphNode(node.LabelName, node.Body, node.Choices,
                    newTargetLabel, false, node.EndsInReturn, node.LeadingComment);
            }
            else
            {
                int choiceIndex = sourceNode.ChoicePorts.FindIndex(cp => cp.Port == sourcePort);
                if (choiceIndex < 0)
                    return;
                var newChoices = new List<ChoiceOption>(node.Choices);
                ChoiceOption old = newChoices[choiceIndex];
                newChoices[choiceIndex] = new ChoiceOption(old.Text, old.ExplicitId, newTargetLabel);
                updated = new GraphNode(node.LabelName, node.Body, newChoices,
                    node.TrailingJumpTarget, node.TrailingIsGosub, node.EndsInReturn, node.LeadingComment);
            }

            ReplaceNode(sourceNode.NodeIndex, updated);
        }

        public void AddNewLabel()
        {
            var existing = new HashSet<string>();
            foreach (GraphNode n in _document.Nodes)
                if (n.LabelName != null)
                    existing.Add(n.LabelName);

            int i = 1;
            string candidate;
            do
            {
                candidate = "new_label_" + i;
                i++;
            } while (existing.Contains(candidate));

            var nodes = new List<GraphNode>(_document.Nodes)
            {
                new GraphNode(candidate, string.Empty, Array.Empty<ChoiceOption>(), null, false, false, null)
            };
            ApplyNodes(nodes);
        }

        public void DeleteNode(int index)
        {
            var nodes = new List<GraphNode>(_document.Nodes);
            if (index < 0 || index >= nodes.Count)
                return;
            nodes.RemoveAt(index);
            ApplyNodes(nodes);
        }

        private void SaveIfDirty()
        {
            SaveLayout();
            if (!_isDirty)
                return;

            if (File.Exists(_assetPath))
            {
                string onDisk = File.ReadAllText(_assetPath);
                if (onDisk != _lastSavedText)
                {
                    bool overwriteDisk = EditorUtility.DisplayDialog(
                        "File changed on disk",
                        $"'{_assetPath}' changed on disk since this graph was opened, and the graph has unsaved changes.",
                        "Keep graph, overwrite disk",
                        "Reload from disk");
                    if (!overwriteDisk)
                    {
                        Load(_assetPath);
                        return;
                    }
                }
            }

            File.WriteAllText(_assetPath, _text);
            AssetDatabase.ImportAsset(_assetPath, ImportAssetOptions.ForceUpdate);
            _lastSavedText = _text;
            _isDirty = false;
            UpdateTitle();
        }

        private string LayoutFilePath =>
            Path.Combine(Path.GetDirectoryName(_assetPath) ?? string.Empty, Path.GetFileNameWithoutExtension(_assetPath) + ".nfgraph.meta");

        private void SaveLayout()
        {
            if (_graphView == null)
                return;

            var layout = new NodeLayoutFile();
            foreach (BranchGraphNodeView nodeView in _graphView.NodeViews)
            {
                string label = _document.Nodes[nodeView.NodeIndex].LabelName;
                if (label == null)
                    continue;
                Rect pos = nodeView.GetPosition();
                layout.nodes.Add(new NodeLayoutEntry { label = label, x = pos.x, y = pos.y });
            }
            File.WriteAllText(LayoutFilePath, JsonUtility.ToJson(layout, true));
        }

        private Dictionary<string, Vector2> LoadLayout()
        {
            var result = new Dictionary<string, Vector2>();
            if (!File.Exists(LayoutFilePath))
                return result;

            try
            {
                var layout = JsonUtility.FromJson<NodeLayoutFile>(File.ReadAllText(LayoutFilePath));
                if (layout?.nodes != null)
                    foreach (NodeLayoutEntry entry in layout.nodes)
                        result[entry.label] = new Vector2(entry.x, entry.y);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"NovelForge: failed to read layout file '{LayoutFilePath}' — using default layout. {e.Message}");
            }
            return result;
        }

        [Serializable]
        private class NodeLayoutEntry
        {
            public string label;
            public float x;
            public float y;
        }

        [Serializable]
        private class NodeLayoutFile
        {
            public List<NodeLayoutEntry> nodes = new();
        }
    }

    internal class BranchGraphView : GraphView
    {
        private readonly NovelBranchGraphWindow _window;
        private readonly EdgeConnectorListener _edgeConnectorListener;

        public IReadOnlyList<BranchGraphNodeView> NodeViews { get; private set; } = Array.Empty<BranchGraphNodeView>();

        public BranchGraphView(NovelBranchGraphWindow window)
        {
            _window = window;
            _edgeConnectorListener = new EdgeConnectorListener(window);

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            Insert(0, new GridBackground());

            RegisterCallback<ContextualMenuPopulateEvent>(evt =>
            {
                if (evt.target == this)
                    evt.menu.AppendAction("Add Label", _ => _window.AddNewLabel());
            });
        }

        public void Populate(GraphDocument document, Dictionary<string, Vector2> layout)
        {
            DeleteElements(graphElements.ToList());

            var nodeViews = new List<BranchGraphNodeView>();
            for (int i = 0; i < document.Nodes.Count; i++)
            {
                GraphNode node = document.Nodes[i];
                bool hasFallThrough = document.GetFallThroughTarget(i) != null;
                var nodeView = new BranchGraphNodeView(i, node, hasFallThrough, _window);

                Vector2 position = node.LabelName != null && layout.TryGetValue(node.LabelName, out Vector2 saved)
                    ? saved
                    : new Vector2(i * 260, 0);
                nodeView.SetPosition(new Rect(position, new Vector2(220, 150)));

                foreach (Port outputPort in nodeView.OutputPorts)
                    outputPort.AddManipulator(new EdgeConnector<Edge>(_edgeConnectorListener));

                AddElement(nodeView);
                nodeViews.Add(nodeView);
            }
            NodeViews = nodeViews;

            var unreachable = new HashSet<string>(document.GetUnreachableLabels());
            for (int i = 0; i < nodeViews.Count; i++)
            {
                string labelName = document.Nodes[i].LabelName;
                if (labelName != null && unreachable.Contains(labelName))
                    nodeViews[i].MarkUnreachable();

                ConnectPort(nodeViews, document, nodeViews[i].TrailingPort, document.Nodes[i].TrailingJumpTarget, isDashed: false);
                for (int c = 0; c < nodeViews[i].ChoicePorts.Count; c++)
                    ConnectPort(nodeViews, document, nodeViews[i].ChoicePorts[c].Port, document.Nodes[i].Choices[c].TargetLabel, isDashed: false);

                string fallThrough = document.GetFallThroughTarget(i);
                if (fallThrough != null)
                    ConnectPort(nodeViews, document, nodeViews[i].FallThroughPort, fallThrough, isDashed: true);
            }
        }

        private void ConnectPort(List<BranchGraphNodeView> nodeViews, GraphDocument document, Port sourcePort, string targetLabel, bool isDashed)
        {
            if (sourcePort == null || targetLabel == null)
                return;

            int targetIndex = -1;
            for (int i = 0; i < document.Nodes.Count; i++)
            {
                if (document.Nodes[i].LabelName == targetLabel)
                {
                    targetIndex = i;
                    break;
                }
            }

            if (targetIndex < 0)
            {
                sourcePort.AddToClassList("broken-reference");
                return;
            }

            var edge = new Edge { output = sourcePort, input = nodeViews[targetIndex].InputPort };
            sourcePort.Connect(edge);
            nodeViews[targetIndex].InputPort.Connect(edge);
            if (isDashed)
                edge.AddToClassList("fall-through-edge");
            AddElement(edge);
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatible = new List<Port>();
            foreach (Port port in ports.ToList())
            {
                if (port.direction != startPort.direction && port.node != startPort.node)
                    compatible.Add(port);
            }
            return compatible;
        }

        private class EdgeConnectorListener : IEdgeConnectorListener
        {
            private readonly NovelBranchGraphWindow _window;

            public EdgeConnectorListener(NovelBranchGraphWindow window)
            {
                _window = window;
            }

            public void OnDrop(GraphView graphView, Edge edge)
            {
                if (edge.output?.node is BranchGraphNodeView sourceNode && edge.input?.node is BranchGraphNodeView targetNode)
                    _window.OnPortReconnected(sourceNode, edge.output, targetNode);
            }

            public void OnDropOutsidePort(Edge edge, Vector2 position)
            {
                // Dropping on empty canvas leaves the model unchanged — no edge is added.
            }
        }
    }

    internal class BranchGraphNodeView : Node
    {
        public int NodeIndex { get; }
        public TextField BodyField { get; }
        public Port InputPort { get; }
        public Port TrailingPort { get; }
        public Port FallThroughPort { get; }
        public List<(Port Port, TextField TextField)> ChoicePorts { get; } = new();

        public IEnumerable<Port> OutputPorts
        {
            get
            {
                foreach ((Port port, _) in ChoicePorts)
                    yield return port;
                if (TrailingPort != null)
                    yield return TrailingPort;
                if (FallThroughPort != null)
                    yield return FallThroughPort;
            }
        }

        public BranchGraphNodeView(int nodeIndex, GraphNode node, bool hasFallThrough, NovelBranchGraphWindow window)
        {
            NodeIndex = nodeIndex;
            title = node.LabelName ?? "(no label)";

            InputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            InputPort.portName = string.Empty;
            inputContainer.Add(InputPort);

            // The node's `title` bar (set once above) is NOT editable by default GraphView
            // behavior — renaming the label goes through this ordinary TextField instead,
            // the same edit-on-focus-out pattern as the body and choice fields below, rather
            // than relying on uncertain double-click-title-to-rename GraphView plumbing.
            var labelField = new TextField { value = node.LabelName ?? string.Empty };
            labelField.style.unityFontStyleAndWeight = FontStyle.Bold;
            labelField.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (!string.IsNullOrEmpty(labelField.value))
                    window.OnLabelChanged(NodeIndex, labelField.value);
            });
            mainContainer.Add(labelField);

            BodyField = new TextField { multiline = true, value = node.Body };
            BodyField.style.minWidth = 200;
            BodyField.style.minHeight = 60;
            BodyField.RegisterCallback<FocusOutEvent>(_ => window.OnBodyFieldChanged(NodeIndex, BodyField.value));
            mainContainer.Add(BodyField);

            for (int c = 0; c < node.Choices.Count; c++)
            {
                int choiceIndex = c;
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                var textField = new TextField { value = node.Choices[c].Text };
                textField.style.flexGrow = 1;
                textField.RegisterCallback<FocusOutEvent>(_ => window.OnChoiceTextChanged(NodeIndex, choiceIndex, textField.value));
                var port = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
                port.portName = "->";
                row.Add(textField);
                row.Add(port);
                mainContainer.Add(row);
                ChoicePorts.Add((port, textField));
            }

            if (node.TrailingJumpTarget != null)
            {
                TrailingPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
                TrailingPort.portName = node.TrailingIsGosub ? "gosub" : "jump";
                outputContainer.Add(TrailingPort);
            }

            if (hasFallThrough)
            {
                FallThroughPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
                FallThroughPort.portName = "(fall-through)";
                outputContainer.Add(FallThroughPort);
            }

            RegisterCallback<ContextualMenuPopulateEvent>(evt =>
            {
                evt.menu.AppendAction("Delete", _ => window.DeleteNode(NodeIndex));
                evt.StopPropagation();
            });

            RefreshExpandedState();
            RefreshPorts();
        }

        public void MarkUnreachable()
        {
            titleContainer.AddToClassList("unreachable-node");
        }
    }
}
```

- [ ] **Step 2: Run the full EditMode suite to confirm nothing else broke**

Run the EditMode test command from Global Constraints.
Expected: `failed="0"`, `passed="215"` (no new automated tests in this task — same total as after Task 2). Confirms the rewritten file compiles cleanly.

- [ ] **Step 3: Commit**

```bash
git add Editor/NovelBranchGraphWindow.cs
git commit -m "Add branch graph interactivity: drag-to-reconnect, add/delete nodes, save, layout persistence

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

- [ ] **Step 4: Live QA pass (required — see Global Constraints)**

Open the project in a running Unity Editor (not batch mode) against this worktree's `TestProject~`, then verify by hand (via computer-use, taking screenshots to confirm each visual claim):

1. Create a test `.nfscript` with: a label with dialogue + `jump`, a label with a `choice` block (2 options, one with `@id`), a label with no trailing element followed immediately by another label (fall-through case), a label ending in `return`, and one `jump`/choice target that doesn't exist (to exercise the broken-reference case) and one label nothing points to (unreachable case). Right-click it in the Project window → "Open in Branch Graph". Confirm the window opens and shows one node per label.
2. Confirm solid edges for the explicit `jump`/`choice` targets, a dashed edge for the fall-through pair, a visibly-tinted "broken" port with no edge for the undefined target, and a visibly-tinted node border for the unreachable label.
3. Click into a node's body `TextField`, edit the dialogue text, click elsewhere to defocus. Confirm no error in the Console. Save (toolbar button), then open the `.nfscript` in a plain text editor (or the existing script editor window) and confirm the edited text is there.
4. Type a brand-new `choice` block directly into a node's (previously plain) body field — e.g. append `\nchoice\n  "Test" -> some_label` — and defocus. Confirm the node visually gains a new choice row with a port (the reparse-and-rebuild picking up the newly-typed structural element), matching the design intent that editing body text can promote text into structured ports.
5. Drag from a `jump`/choice port to a different node. Confirm the edge visually reconnects, and after Save, the `.nfscript` text shows the new target label in place of the old one.
6. Drag from the dashed fall-through port to some node. Confirm it becomes a solid edge and, after Save, the text now has an explicit `jump <target>` line where there was none before.
7. Right-click empty canvas → "Add Label". Confirm a new node appears; right-click a node → "Delete". Confirm the node disappears. Save both times and confirm the `.nfscript` text reflects both changes.
7b. Edit a node's label-name field (the bold `TextField` under the title bar, not the static title text itself) to a new name, defocus. Confirm the node's title bar updates to the new name after the rebuild, and confirm any OTHER node's `jump`/choice that used to target the OLD name now shows as a broken (warning-tinted, disconnected) port rather than being silently auto-updated — that's the designed behavior, not a bug.
8. With unsaved graph changes present, edit the same `.nfscript` file's text externally (e.g. via Notepad or the script editor window) and save it there. Then trigger a Save in the graph window (or defocus it). Confirm the "file changed on disk" dialog appears; test both buttons on two separate attempts (one "Keep graph, overwrite disk", one "Reload from disk") and confirm each does what it says.
9. Move a node on the canvas, save, close the window, reopen it (via the Project window context menu again). Confirm the node's position is preserved (read from the `.nfscript.nfgraph.meta`... actually `<name>.nfgraph.meta` sidecar file — confirm that file exists alongside the script and contains the moved position).
10. Check the Console throughout steps 1-9 for unexpected errors (an `ExitGUIException`-style internal control-flow exception, if any ever appears, is not itself an error Unity surfaces — but any *other* exception or error is a real bug to fix before this task is complete).
11. Delete the test `.nfscript` and its `.nfgraph.meta` sidecar when done, so nothing lingers as project clutter.

Report the outcome of each numbered step (pass/fail) with a screenshot-confirmed description of what steps 2, 4, 5, 6, 8, and 9 actually looked like on screen. Any failure is a real bug to fix before this task — and the whole plan — can be marked complete.
