# Character Editor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A master-detail `EditorWindow` for browsing and editing `CharacterDefinition` assets — project-wide character list, a detail panel (id/name/color/poses with sprite previews) bound via `SerializedObject`, and a usage-validation block comparing a character's defined poses against the emotions actually used for that character across all `.nfscript` scripts in the project.

**Architecture:** One small additive change to already-merged Runtime code (`SayLineCommand` gets two public read-only properties, `CharacterId`/`Emotion`, over already-existing private fields — editor tooling needs to read them, nothing about command execution changes). One fully unit-testable pure-logic class, `Editor/CharacterUsageValidator.cs` (`CharacterDefinition` + a set of already-compiled `NovelScript`s → two string lists). One thin, untested `EditorWindow` GUI shell, `Editor/CharacterEditorWindow.cs`, that lists project characters, edits the selected one through `SerializedObject`/`SerializedProperty` (not direct field access — `poses`/`id`/`displayName` are `internal`, and `NovelForge.Editor` is not in `Runtime/AssemblyInfo.cs`'s `InternalsVisibleTo` list), and wires in the validator by compiling every `NovelScriptAsset` in the project through the existing `ScriptCompiler`.

**Tech Stack:** C#, Unity 6000.6, IMGUI (`UnityEditor`/`UnityEngine`, matching all existing Editor code in this project — no UI Toolkit), NUnit (Unity Test Framework, EditMode).

**Spec:** [docs/superpowers/specs/2026-09-11-editor-character-editor-design.md](../specs/2026-09-11-editor-character-editor-design.md). Out of scope per the spec: deleting characters from the window (use the Project panel), multi-character batch editing, auto-fixing validation findings, animated pose previews/transitions.

## Global Constraints

- Unity Editor version: **6000.6.0f1**, at `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe`. Package id `com.novelforge.core`, package root `G:\ClaudeProjects\NovelForge`.
- No new package dependency.
- Task 1 touches already-merged `Runtime/Commands/Builtin/SayLineCommand.cs` — additive only (two new read-only properties over existing private fields), every existing test must keep passing unmodified.
- Tasks 2-3 are new files in the existing `NovelForge.Editor`/`NovelForge.Editor.Tests` assemblies — no asmdef changes needed.
- `CharacterDefinition`'s `id`, `displayName`, `poses` fields are `[SerializeField] internal` (`Runtime/Actors/CharacterDefinition.cs`). `Runtime/AssemblyInfo.cs` grants `InternalsVisibleTo` to `NovelForge.Runtime.Tests` and `NovelForge.UI.Tests` only — **not** `NovelForge.Editor` or `NovelForge.Editor.Tests`. Any code in Tasks 2-3 that needs to read or write those fields (including test setup code) MUST go through `SerializedObject`/`SerializedProperty`, never direct field access — direct access is a compile error from these assemblies. `CharacterDefinition.Id`/`.DisplayName`/`.NameColor`/`.TryGetSprite(...)` (all `public`) remain the normal way to *read* already-known values; `SerializedObject` is only needed for enumerating/mutating `poses`, or setting `id`/`displayName` from test code or the editor window.
- Task 3 (`CharacterEditorWindow`) has **no automated test** — IMGUI `EditorWindow`/`OnGUI` code has no established way to drive a real repaint cycle from an EditMode test in this project (same precedent as `NovelScriptEditorWindow`). Task 3 ends with a **live QA pass using computer-use in a running Unity Editor** instead of `LogAssert`/`Assert` code — this is a deliberate, spec-approved step, not optional polish: the previous editor-tools phase merged an `EditorWindow` that passed full code review but rendered nothing at all when actually opened, caught only by this same kind of live check. Do not treat the absence of a test file for Task 3 as a gap, and do not skip its live QA step.
- Git commit messages end with: `Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>`.
- `.meta` files must be genuinely Unity-generated, never hand-authored, under any circumstances. This project has hit hand-fabricated `.meta` files (patterned/cyclic GUIDs, a fake `MonoImporter:` block no real `.cs.meta` file in this repo has) multiple times, requiring rejected fix rounds each time. Let Unity generate every `.meta` file by actually running the EditMode test suite (which imports the project); never type one by hand. Every `git add` must be of the whole containing folder, never individual bare file paths — Unity generates `.meta` companions on import, and folder-add recurses into them automatically. This plan creates no new folders (`Editor/`, `Tests/Editor/`, `Runtime/Commands/Builtin/`, `Tests/Runtime/` all already exist), so check `git status` after every commit regardless.
- Use the working directory's own `TestProject~`, addressed with absolute paths for every argument to the Unity test command:
  ```powershell
  & "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe" -batchmode -projectPath "<worktree-root>\TestProject~" -runTests -testPlatform EditMode -testResults "<worktree-root>\TestProject~\TestResults.xml" -logFile "<worktree-root>\TestProject~\Logs\RunTests.log"
  ```
  Deliberately no `-quit`. After launching, **poll for completion synchronously within your own tool calls** (wait ~20-30 seconds, check for a fresh `TestResults.xml`, repeat up to ~90s total) — never end your turn assuming something else will resume you; nothing will. If you see "Couldn't set project path", check `TestProject~\Assets` exists (create it with `New-Item -ItemType Directory -Force -Path "<worktree-root>\TestProject~\Assets"` if not). If a run fails with an `ILPPTrigger: Can't find file \\.\pipe\...` error, that is a known transient Unity toolchain glitch unrelated to your code — simply retry the same command once.
- Baseline before this plan: **183 passing tests** (confirmed live in Unity's Test Runner on current `master`, after the syntax-highlighter/editor-window fix-up that landed after the Script Editor Window phase). Each task states the expected running total after it. If your own first test run reports a different baseline, trust what you actually see and adjust the running totals you report accordingly — but flag the mismatch in your task report.

---

### Task 1: `SayLineCommand.CharacterId` / `.Emotion`

**Files:**
- Modify: `Runtime/Commands/Builtin/SayLineCommand.cs`
- Modify: `Tests/Runtime/ContentCommandsTests.cs`

**Interfaces:**
- Produces: `NovelForge.Runtime.SayLineCommand.CharacterId` (`public string CharacterId`), `NovelForge.Runtime.SayLineCommand.Emotion` (`public string Emotion`). Task 2's `CharacterUsageValidator.Validate` reads both.

- [ ] **Step 1: Write the failing test**

Open `Tests/Runtime/ContentCommandsTests.cs` and add this test directly after the existing `SayLine_DelegatesToDialoguePresenter` test (inside the same `ContentCommandsTests` class):

```csharp
        [Test]
        public void SayLine_ExposesCharacterIdAndEmotionAsProperties()
        {
            var command = new SayLineCommand("Alice", "Привет!", "happy", "left", "line_1");

            Assert.AreEqual("Alice", command.CharacterId);
            Assert.AreEqual("happy", command.Emotion);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run the EditMode test command from Global Constraints.
Expected: compile error (`SayLineCommand.CharacterId`/`.Emotion` do not exist yet).

- [ ] **Step 3: Add the two properties**

In `Runtime/Commands/Builtin/SayLineCommand.cs`, add these two properties directly after the constructor. Full file after the change:

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

        public string CharacterId => _characterId;
        public string Emotion => _emotion;

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

- [ ] **Step 4: Run tests to verify they pass**

Run the EditMode test command from Global Constraints.
Expected: `failed="0"`, 1 new test passing (running total from 183: **184**). Confirm every pre-existing test in `ContentCommandsTests.cs` and `ScriptCompilerCoreTests.cs` is still green unmodified (both already construct `SayLineCommand` directly and must be unaffected by this additive change).

- [ ] **Step 5: Commit**

```bash
git add Runtime/Commands/Builtin/SayLineCommand.cs Tests/Runtime/ContentCommandsTests.cs
git commit -m "$(cat <<'EOF'
Expose SayLineCommand.CharacterId/.Emotion for editor tooling

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: `CharacterUsageValidator`

**Files:**
- Create: `Editor/CharacterUsageValidator.cs`
- Create: `Tests/Editor/CharacterUsageValidatorTests.cs`

**Interfaces:**
- Consumes: `NovelForge.Runtime.SayLineCommand.CharacterId`/`.Emotion` (Task 1), `NovelForge.Runtime.CharacterDefinition.Id`/`.TryGetSprite(string, out Sprite)` (already-merged), `NovelForge.Runtime.NovelScript.Commands` (already-merged).
- Produces: `NovelForge.Editor.CharacterUsageValidator.Validate(CharacterDefinition character, IEnumerable<NovelScript> scripts)` → `CharacterUsageValidator.Result` (a `readonly struct` with `IReadOnlyList<string> UsedButNotDefined` and `IReadOnlyList<string> DefinedButUnused`, both alphabetically sorted, `StringComparer.Ordinal`). Task 3 calls this with a project-wide-compiled script list and displays both lists.

This task is independent of Task 3 (Task 3 depends on it, not the reverse) and only depends on Task 1 for `SayLineCommand.CharacterId`/`.Emotion`.

- [ ] **Step 1: Write the failing tests**

Create `Tests/Editor/CharacterUsageValidatorTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using NovelForge.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovelForge.Editor.Tests
{
    public class CharacterUsageValidatorTests
    {
        private static Sprite CreateSprite()
        {
            var texture = new Texture2D(4, 4);
            return Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.zero);
        }

        // CharacterDefinition's id/poses fields are `internal` and NovelForge.Editor.Tests has no
        // InternalsVisibleTo grant for them (see Global Constraints) — SerializedObject is the only
        // way to populate them from this assembly, and it's exactly the mechanism the production
        // code (CharacterEditorWindow, and this class's own GetDefinedEmotions) uses too.
        private static CharacterDefinition CreateCharacter(string id, params (string emotion, Sprite sprite)[] poses)
        {
            var character = ScriptableObject.CreateInstance<CharacterDefinition>();
            var serializedObject = new SerializedObject(character);
            serializedObject.FindProperty("id").stringValue = id;

            SerializedProperty posesProperty = serializedObject.FindProperty("poses");
            posesProperty.arraySize = poses.Length;
            for (int i = 0; i < poses.Length; i++)
            {
                SerializedProperty element = posesProperty.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("emotion").stringValue = poses[i].emotion;
                element.FindPropertyRelative("sprite").objectReferenceValue = poses[i].sprite;
            }
            serializedObject.ApplyModifiedProperties();
            return character;
        }

        private static NovelScript CreateScript(params Command[] commands)
        {
            return new NovelScript(new List<Command>(commands), new Dictionary<string, int>());
        }

        [Test]
        public void Validate_EmotionUsedAndDefined_AppearsInNeitherList()
        {
            var character = CreateCharacter("alice", ("happy", CreateSprite()));
            var script = CreateScript(new SayLineCommand("alice", "Hi!", "happy", null, "id1"));

            var result = CharacterUsageValidator.Validate(character, new[] { script });

            CollectionAssert.IsEmpty(result.UsedButNotDefined);
            CollectionAssert.IsEmpty(result.DefinedButUnused);
        }

        [Test]
        public void Validate_EmotionUsedWithoutMatchingPose_AppearsInUsedButNotDefined()
        {
            var character = CreateCharacter("alice");
            var script = CreateScript(new SayLineCommand("alice", "Hi!", "sad", null, "id1"));

            var result = CharacterUsageValidator.Validate(character, new[] { script });

            CollectionAssert.AreEqual(new[] { "sad" }, result.UsedButNotDefined);
            CollectionAssert.IsEmpty(result.DefinedButUnused);
        }

        [Test]
        public void Validate_PoseDefinedButNeverUsed_AppearsInDefinedButUnused()
        {
            var character = CreateCharacter("alice", ("happy", CreateSprite()));
            var script = CreateScript(new SayLineCommand("alice", "Hi!", null, null, "id1"));

            var result = CharacterUsageValidator.Validate(character, new[] { script });

            CollectionAssert.IsEmpty(result.UsedButNotDefined);
            CollectionAssert.AreEqual(new[] { "happy" }, result.DefinedButUnused);
        }

        [Test]
        public void Validate_DialogueLinesBelongToAnotherCharacter_AreIgnored()
        {
            var character = CreateCharacter("alice", ("happy", CreateSprite()), ("sad", CreateSprite()));
            var script = CreateScript(new SayLineCommand("bob", "Hi!", "happy", null, "id1"));

            var result = CharacterUsageValidator.Validate(character, new[] { script });

            CollectionAssert.IsEmpty(result.UsedButNotDefined);
            CollectionAssert.AreEqual(new[] { "happy", "sad" }, result.DefinedButUnused);
        }

        [Test]
        public void Validate_PoseWithUnsetSprite_TreatsUsedEmotionAsNotDefined_ButNotAlsoUnused()
        {
            var character = CreateCharacter("alice", ("happy", null));
            var script = CreateScript(new SayLineCommand("alice", "Hi!", "happy", null, "id1"));

            var result = CharacterUsageValidator.Validate(character, new[] { script });

            CollectionAssert.AreEqual(new[] { "happy" }, result.UsedButNotDefined);
            CollectionAssert.IsEmpty(result.DefinedButUnused);
        }

        [Test]
        public void Validate_NoScripts_ReturnsAllDefinedPosesAsUnused()
        {
            var character = CreateCharacter("alice", ("happy", CreateSprite()));

            var result = CharacterUsageValidator.Validate(character, System.Array.Empty<NovelScript>());

            CollectionAssert.IsEmpty(result.UsedButNotDefined);
            CollectionAssert.AreEqual(new[] { "happy" }, result.DefinedButUnused);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the EditMode test command from Global Constraints.
Expected: compile error (`CharacterUsageValidator` does not exist yet).

- [ ] **Step 3: Implement `CharacterUsageValidator`**

Create `Editor/CharacterUsageValidator.cs`:

```csharp
using System;
using System.Collections.Generic;
using NovelForge.Runtime;
using UnityEditor;

namespace NovelForge.Editor
{
    public static class CharacterUsageValidator
    {
        public readonly struct Result
        {
            public IReadOnlyList<string> UsedButNotDefined { get; }
            public IReadOnlyList<string> DefinedButUnused { get; }

            public Result(IReadOnlyList<string> usedButNotDefined, IReadOnlyList<string> definedButUnused)
            {
                UsedButNotDefined = usedButNotDefined;
                DefinedButUnused = definedButUnused;
            }
        }

        public static Result Validate(CharacterDefinition character, IEnumerable<NovelScript> scripts)
        {
            var usedEmotions = new HashSet<string>();
            foreach (NovelScript script in scripts)
            {
                foreach (Command command in script.Commands)
                {
                    if (command is SayLineCommand say && say.CharacterId == character.Id && say.Emotion != null)
                        usedEmotions.Add(say.Emotion);
                }
            }

            var definedEmotions = new HashSet<string>(GetDefinedEmotions(character));

            var usedButNotDefined = new List<string>();
            foreach (string emotion in usedEmotions)
            {
                if (!character.TryGetSprite(emotion, out _))
                    usedButNotDefined.Add(emotion);
            }

            var definedButUnused = new List<string>();
            foreach (string emotion in definedEmotions)
            {
                if (!usedEmotions.Contains(emotion))
                    definedButUnused.Add(emotion);
            }

            usedButNotDefined.Sort(StringComparer.Ordinal);
            definedButUnused.Sort(StringComparer.Ordinal);

            return new Result(usedButNotDefined, definedButUnused);
        }

        // `poses` is `internal` on CharacterDefinition with no InternalsVisibleTo grant to
        // NovelForge.Editor (see Global Constraints) — SerializedObject is the only way to
        // enumerate it from here, the same mechanism CharacterEditorWindow uses to edit it.
        private static List<string> GetDefinedEmotions(CharacterDefinition character)
        {
            var emotions = new List<string>();
            var serializedObject = new SerializedObject(character);
            SerializedProperty posesProperty = serializedObject.FindProperty("poses");
            for (int i = 0; i < posesProperty.arraySize; i++)
            {
                string emotion = posesProperty.GetArrayElementAtIndex(i).FindPropertyRelative("emotion").stringValue;
                if (!string.IsNullOrEmpty(emotion))
                    emotions.Add(emotion);
            }
            return emotions;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run the EditMode test command from Global Constraints.
Expected: `failed="0"`, 6 new tests passing (running total from 184: **190**).

- [ ] **Step 5: Commit**

```bash
git add Editor/CharacterUsageValidator.cs Tests/Editor/CharacterUsageValidatorTests.cs
git commit -m "$(cat <<'EOF'
Add CharacterUsageValidator: compare defined poses against script usage

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: `CharacterEditorWindow`

**Files:**
- Create: `Editor/CharacterEditorWindow.cs`

**Interfaces:**
- Consumes: `NovelForge.Editor.CharacterUsageValidator.Validate(CharacterDefinition, IEnumerable<NovelScript>)` → `Result` (Task 2), `NovelForge.Runtime.CharacterDefinition` (id/displayName/nameColor/poses via `SerializedObject`, plus public `.Id`/`.DisplayName`), `NovelForge.Runtime.NovelScriptAsset.Source` (already-merged), `NovelForge.Runtime.ScriptCompiler.Compile(string)` / `NovelForge.Runtime.ParseException.Message` (already-merged core; must be fully qualified as `NovelForge.Runtime.ScriptCompiler` — Unity 6000.6 has its own colliding `UnityEditor.ScriptCompiler` type, same gotcha hit in the Script Editor Window phase).
- Produces: nothing consumed by a later task — this is the plan's final task.

No automated test for this task (see Global Constraints) — Step 2 below is a live QA pass instead of a test run.

- [ ] **Step 1: Implement `CharacterEditorWindow`**

Create `Editor/CharacterEditorWindow.cs`:

```csharp
using System;
using System.Collections.Generic;
using NovelForge.Runtime;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace NovelForge.Editor
{
    public class CharacterEditorWindow : EditorWindow
    {
        private CharacterDefinition[] _allCharacters = Array.Empty<CharacterDefinition>();
        private CharacterDefinition _selected;
        private SerializedObject _selectedSerializedObject;
        private CharacterUsageValidator.Result _usageResult;
        private Vector2 _listScrollPosition;
        private Vector2 _detailScrollPosition;

        [MenuItem("NovelForge/Character Editor")]
        public static void ShowWindow()
        {
            GetWindow<CharacterEditorWindow>("Character Editor").Show();
        }

        // Same EntityId-based [OnOpenAsset] shape as NovelScriptEditorWindow's — Unity 6000.5+
        // made the int-based instanceID overloads hard compile errors, EntityId is the replacement.
        [OnOpenAsset(1)]
        public static bool OnOpenAsset(EntityId instanceId, int line)
        {
            if (EditorUtility.EntityIdToObject(instanceId) is not CharacterDefinition character)
                return false;

            var window = GetWindow<CharacterEditorWindow>("Character Editor");
            window.SelectCharacter(character);
            window.Show();
            return true;
        }

        private void OnEnable() => RefreshCharacterList();

        private void OnFocus() => RefreshCharacterList();

        private void RefreshCharacterList()
        {
            var characters = new List<CharacterDefinition>();
            foreach (string guid in AssetDatabase.FindAssets("t:CharacterDefinition"))
            {
                var character = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (character != null)
                    characters.Add(character);
            }
            characters.Sort((a, b) => string.Compare(DisplayLabel(a), DisplayLabel(b), StringComparison.OrdinalIgnoreCase));
            _allCharacters = characters.ToArray();
        }

        private static string DisplayLabel(CharacterDefinition character)
        {
            if (!string.IsNullOrEmpty(character.DisplayName))
                return character.DisplayName;
            if (!string.IsNullOrEmpty(character.Id))
                return character.Id;
            return character.name;
        }

        private void SelectCharacter(CharacterDefinition character)
        {
            _selected = character;
            _selectedSerializedObject = character != null ? new SerializedObject(character) : null;
            RefreshUsage();
            Repaint();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawCharacterList();
                DrawDetailPanel();
            }
        }

        private void DrawCharacterList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(200)))
            {
                _listScrollPosition = EditorGUILayout.BeginScrollView(_listScrollPosition);
                foreach (var character in _allCharacters)
                {
                    if (character == null)
                        continue;
                    string label = character == _selected ? "> " + DisplayLabel(character) : DisplayLabel(character);
                    if (GUILayout.Button(label))
                        SelectCharacter(character);
                }
                EditorGUILayout.EndScrollView();

                if (GUILayout.Button("New Character"))
                    CreateNewCharacter();
            }
        }

        private void CreateNewCharacter()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "New Character", "CharacterDefinition", "asset", "Choose where to save the new character.");
            if (string.IsNullOrEmpty(path))
                return;

            var character = ScriptableObject.CreateInstance<CharacterDefinition>();
            AssetDatabase.CreateAsset(character, path);
            AssetDatabase.SaveAssets();

            RefreshCharacterList();
            SelectCharacter(character);
        }

        private void DrawDetailPanel()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                if (_selected == null)
                {
                    EditorGUILayout.HelpBox("Select a character on the left, or create a new one.", MessageType.Info);
                    return;
                }

                _detailScrollPosition = EditorGUILayout.BeginScrollView(_detailScrollPosition);

                _selectedSerializedObject.Update();

                EditorGUILayout.PropertyField(_selectedSerializedObject.FindProperty("id"));
                EditorGUILayout.PropertyField(_selectedSerializedObject.FindProperty("displayName"));
                EditorGUILayout.PropertyField(_selectedSerializedObject.FindProperty("nameColor"));

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Poses", EditorStyles.boldLabel);
                DrawPoses(_selectedSerializedObject.FindProperty("poses"));

                if (_selectedSerializedObject.ApplyModifiedProperties())
                    RefreshUsage();

                EditorGUILayout.Space();
                DrawUsage();

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawPoses(SerializedProperty posesProperty)
        {
            for (int i = 0; i < posesProperty.arraySize; i++)
            {
                SerializedProperty element = posesProperty.GetArrayElementAtIndex(i);
                SerializedProperty emotionProp = element.FindPropertyRelative("emotion");
                SerializedProperty spriteProp = element.FindPropertyRelative("sprite");

                using (new EditorGUILayout.HorizontalScope())
                {
                    var sprite = spriteProp.objectReferenceValue as Sprite;
                    Rect previewRect = GUILayoutUtility.GetRect(40, 40, GUILayout.Width(40));
                    if (sprite != null)
                        EditorGUI.DrawPreviewTexture(previewRect, sprite.texture);
                    else
                        EditorGUI.DrawRect(previewRect, new Color(0f, 0f, 0f, 0.1f));

                    EditorGUILayout.PropertyField(emotionProp, GUIContent.none, GUILayout.Width(120));
                    EditorGUILayout.PropertyField(spriteProp, GUIContent.none);

                    // Deleting a SerializedProperty array element mid-loop invalidates the
                    // remaining elements this same OnGUI pass — apply the change and abort
                    // immediately via ExitGUI (Unity's documented mechanism for this exact
                    // situation) rather than continuing to iterate a stale array.
                    if (GUILayout.Button("X", GUILayout.Width(24)))
                    {
                        posesProperty.DeleteArrayElementAtIndex(i);
                        _selectedSerializedObject.ApplyModifiedProperties();
                        RefreshUsage();
                        GUIUtility.ExitGUI();
                    }
                }
            }

            if (GUILayout.Button("+ Add Pose"))
            {
                posesProperty.InsertArrayElementAtIndex(posesProperty.arraySize);
                SerializedProperty newElement = posesProperty.GetArrayElementAtIndex(posesProperty.arraySize - 1);
                newElement.FindPropertyRelative("emotion").stringValue = string.Empty;
                newElement.FindPropertyRelative("sprite").objectReferenceValue = null;
            }
        }

        private void RefreshUsage()
        {
            if (_selected == null)
            {
                _usageResult = default;
                return;
            }

            var scripts = new List<NovelScript>();
            foreach (string guid in AssetDatabase.FindAssets("t:NovelScriptAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<NovelScriptAsset>(path);
                if (asset == null)
                    continue;

                try
                {
                    scripts.Add(new NovelForge.Runtime.ScriptCompiler().Compile(asset.Source));
                }
                catch (ParseException e)
                {
                    Debug.LogWarning($"NovelForge: skipping '{path}' for character usage validation — {e.Message}");
                }
            }

            _usageResult = CharacterUsageValidator.Validate(_selected, scripts);
        }

        private void DrawUsage()
        {
            if (_usageResult.UsedButNotDefined == null)
                return;
            if (_usageResult.UsedButNotDefined.Count == 0 && _usageResult.DefinedButUnused.Count == 0)
                return;

            EditorGUILayout.LabelField("Usage", EditorStyles.boldLabel);
            foreach (string emotion in _usageResult.UsedButNotDefined)
                EditorGUILayout.HelpBox($"Used in scripts but not defined: '{emotion}'", MessageType.Warning);
            foreach (string emotion in _usageResult.DefinedButUnused)
                EditorGUILayout.HelpBox($"Defined but never used: '{emotion}'", MessageType.Info);
        }
    }
}
```

- [ ] **Step 2: Run the full EditMode suite to confirm nothing else broke**

Run the EditMode test command from Global Constraints.
Expected: `failed="0"`, `passed="190"` (no new automated tests in this task — same total as after Task 2). This confirms the new file compiles cleanly inside the existing `NovelForge.Editor` assembly and didn't disturb anything.

- [ ] **Step 3: Commit**

```bash
git add Editor/CharacterEditorWindow.cs
git commit -m "$(cat <<'EOF'
Add CharacterEditorWindow: browse/edit characters with usage validation

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step 4: Live QA pass (required — see Global Constraints)**

Open the project in a running Unity Editor (not batch mode) against this worktree's `TestProject~`, then verify by hand (via computer-use, taking screenshots to confirm each visual claim rather than assuming code review is sufficient — this is the step that caught the previous phase's completely non-rendering merged window):

1. Open the window via the `NovelForge > Character Editor` menu item. Confirm the window opens with a left list panel and a right detail panel (placeholder text if nothing selected yet).
2. If no `CharacterDefinition` assets exist yet in the test project, click "New Character", save it as `Assets/qa_character.asset`. Confirm it appears in the left list and is auto-selected (right panel shows its empty fields).
3. Fill in `id` = `alice`, `displayName` = `Alice`, pick a `nameColor`. Confirm typing into these fields visibly updates them (this exact class of "looks right in review, renders nothing live" bug is what caused the previous phase's fix-up — actually watch the text appear on screen, don't assume).
4. Click "+ Add Pose". Confirm a new row appears with an empty emotion field, an empty sprite object field, and a placeholder preview box. Type `happy` into the emotion field. Drag any `Sprite` asset from the test project onto the sprite field (or use the object picker). Confirm a visible thumbnail of that sprite appears in the preview box next to the row — this is the one genuinely new rendering call in this phase (`EditorGUI.DrawPreviewTexture`), verify it actually draws.
5. Click "+ Add Pose" again, leave it empty, then click its "X" button. Confirm the row disappears immediately and the window doesn't throw an exception in the Console (watch for `ExitGUIException`-related stack traces specifically — a bare `GUIUtility.ExitGUI()` is expected internal control flow and Unity does not surface it as an error, but confirm the Console shows no *other* errors).
6. Create (or reuse) a test `.nfscript` asset containing at least one line `alice: Hello! #happy` and one line `alice: Wait #sad` (an emotion the character does NOT have a pose for). Re-select the `alice` character in the list (or click away and back) to force a usage refresh. Confirm the "Usage" section shows `Used in scripts but not defined: 'sad'` as a warning, and does NOT list `happy` there (it's both used and defined).
7. Remove the `happy` pose (leaving `alice` with no poses at all, while the script still uses `#happy`). Re-select the character. Confirm `happy` now also appears under "Used in scripts but not defined".
8. Add back a pose for an emotion the test script never uses (e.g. `angry`, with any sprite). Re-select the character. Confirm it appears under "Defined but never used".
9. Double-click the `alice` character asset directly in the Project window (not via the already-open window). Confirm it focuses the existing `Character Editor` window (does not open a second one) and selects `alice`.
10. Close the window, reopen it via the menu item. Confirm `alice`'s edited fields (id/displayName/color/poses) persisted — `SerializedObject.ApplyModifiedProperties()` should have saved them without any explicit "Save" action needed.
11. Delete `Assets/qa_character.asset` and any test `.nfscript` created solely for this walkthrough when done, so nothing lingers as project clutter.

Report the outcome of each numbered step (pass/fail) in your task report, with a screenshot-confirmed description of what step 3, 4, 6, 7, and 8 actually looked like on screen — not just "should work per the code". Any failure is a real bug to fix before this task can be marked complete; there is no "acceptable partial" in this task the way Task 4 of the Script Editor Window plan had one for its caret-position reflection hack.
