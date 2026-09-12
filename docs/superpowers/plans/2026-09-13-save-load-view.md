# SaveLoadView Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The last missing "UI-слой" component from the master spec — a UGUI grid of save slots (`SaveLoadView`), backed by a new `SaveLoadController` that orchestrates the already-merged `PlaybackController`/`StoryContext`/`ISaveStorage` pieces (save/load is player-initiated, not driven by the DSL script the way Dialogue/Choice are).

**Architecture:** One small additive extension to the already-merged `ISaveStorage` interface (`SlotExists`), one new pure-logic orchestrator class in `NovelForge.Runtime` (`SaveLoadController`), and one new UGUI `MonoBehaviour` in `NovelForge.UI` (`SaveLoadView`) following the exact same fixed-slot-array pattern already established by `ChoiceView`. All three layers get full automated EditMode tests — this project already has a working pattern for testing UGUI `MonoBehaviour`s without a scene or Play Mode (`Tests/UI/ChoiceViewTests.cs`: programmatic `GameObject`/`Button`/`TMP_Text` construction, no live Unity session needed).

**Tech Stack:** C#, Unity 6000.6, UGUI + TextMeshPro (already-referenced in `NovelForge.UI.asmdef`), NUnit (Unity Test Framework, EditMode).

**Spec:** [docs/superpowers/specs/2026-09-13-save-load-view-design.md](../specs/2026-09-13-save-load-view-design.md). Out of scope per the spec: presenter visual-state restoration on load (already deferred in the earlier save-load phase), slot deletion, overwrite confirmation.

## Global Constraints

- Unity Editor version: **6000.6.0f1**, at `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe`. Package id `com.novelforge.core`, package root `G:\ClaudeProjects\NovelForge`.
- No new package dependency — `UnityEngine.UI`/`Unity.TextMeshPro` are already referenced in `UI/NovelForge.UI.asmdef`.
- `ISaveStorage` is a public interface already merged into `master` with one production implementation (`JsonSaveStorage`) and no other consumers in this repo — adding `SlotExists` to it is a safe, additive interface change for this project (no external implementers to break).
- All three new/changed pieces get real automated EditMode tests — there is no "no automated test, manual QA" task in this plan. `Tests/UI/ChoiceViewTests.cs` is the proof this works for UGUI `MonoBehaviour`s in this project: construct `GameObject`s and add components programmatically, no scene, no Play Mode, `[TearDown]` calls `Object.DestroyImmediate` on everything spawned.
- Git commit messages end with: `Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>`.
- `.meta` files must be genuinely Unity-generated, never hand-authored. Every `git add` must be of the whole containing folder, never individual bare file paths — check `git status` after every commit regardless (this exact project has hit missing-`.meta`-file commits from naming `.cs` files directly instead of adding the whole folder — verify with `git status` before every commit, not just after).
- Use the working directory's own `TestProject~`, addressed with absolute paths for every argument to the Unity test command:
  ```powershell
  & "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe" -batchmode -projectPath "<worktree-root>\TestProject~" -runTests -testPlatform EditMode -testResults "<worktree-root>\TestProject~\TestResults.xml" -logFile "<worktree-root>\TestProject~\Logs\RunTests.log"
  ```
  Deliberately no `-quit`. After launching, **poll for completion synchronously within your own tool calls** (wait ~20-30 seconds, check for a fresh `TestResults.xml`, repeat up to ~90s total) — never end your turn assuming something else will resume you. If you see "Couldn't set project path", check `TestProject~\Assets` exists (`New-Item -ItemType Directory -Force -Path "<worktree-root>\TestProject~\Assets"` if not).
- Baseline before this plan: **216 passing tests** (confirmed on current `master`). Each task states the expected running total after it.

---

### Task 1: `ISaveStorage.SlotExists`

**Files:**
- Modify: `Runtime/SaveLoad/ISaveStorage.cs`
- Modify: `Runtime/SaveLoad/JsonSaveStorage.cs`
- Modify: `Tests/Runtime/SaveLoad/JsonSaveStorageTests.cs`

**Interfaces:**
- Produces: `NovelForge.Runtime.ISaveStorage.SlotExists(string slotId)` → `bool`; `NovelForge.Runtime.JsonSaveStorage.SlotExists(string slotId)` → `bool` (implementation). Task 2's `SaveLoadController.SlotExists` calls through to this.

This task touches already-merged files additively only — every existing test in `JsonSaveStorageTests.cs` must keep passing unmodified.

- [ ] **Step 1: Write the failing tests**

Open `Tests/Runtime/SaveLoad/JsonSaveStorageTests.cs` and add these two tests directly after the existing `Load_EmptyFile_LogsErrorAndReturnsIncompatible` test (inside the same `JsonSaveStorageTests` class, before the closing `}`):

```csharp
        [Test]
        public void SlotExists_AfterSave_ReturnsTrue()
        {
            var storage = CreateStorage();
            storage.Save("slot1", new SaveData { ScriptId = "chapter1" });

            Assert.IsTrue(storage.SlotExists("slot1"));
        }

        [Test]
        public void SlotExists_NeverSaved_ReturnsFalse()
        {
            var storage = CreateStorage();

            Assert.IsFalse(storage.SlotExists("does-not-exist"));
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run the EditMode test command from Global Constraints.
Expected: compile error (`ISaveStorage`/`JsonSaveStorage` have no `SlotExists` member yet).

- [ ] **Step 3: Add `SlotExists` to the interface and implementation**

In `Runtime/SaveLoad/ISaveStorage.cs`, add the new member. Full file after the change:

```csharp
namespace NovelForge.Runtime
{
    public interface ISaveStorage
    {
        void Save(string slotId, SaveData data);
        SaveLoadResult Load(string slotId);
        bool SlotExists(string slotId);
    }
}
```

In `Runtime/SaveLoad/JsonSaveStorage.cs`, add the implementation directly after the existing `Load` method (before the private `PathFor` helper). Full file after the change:

```csharp
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace NovelForge.Runtime
{
    public class JsonSaveStorage : ISaveStorage
    {
        public const int CurrentSchemaVersion = 1;

        private readonly string _directory;

        public JsonSaveStorage() : this(Application.persistentDataPath)
        {
        }

        internal JsonSaveStorage(string directory)
        {
            _directory = directory;
        }

        public void Save(string slotId, SaveData data)
        {
            data.SchemaVersion = CurrentSchemaVersion;
            Directory.CreateDirectory(_directory);
            File.WriteAllText(PathFor(slotId), JsonConvert.SerializeObject(data));
        }

        public SaveLoadResult Load(string slotId)
        {
            string path = PathFor(slotId);
            if (!File.Exists(path))
                return new SaveLoadResult { Status = SaveLoadStatus.NotFound };

            SaveData data;
            try
            {
                data = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(path));
            }
            catch (JsonException e)
            {
                Debug.LogError($"NovelForge: save slot '{slotId}' is corrupt — {e.Message}");
                return new SaveLoadResult { Status = SaveLoadStatus.Incompatible, FoundSchemaVersion = -1 };
            }

            if (data == null)
            {
                Debug.LogError($"NovelForge: save slot '{slotId}' is empty or malformed.");
                return new SaveLoadResult { Status = SaveLoadStatus.Incompatible, FoundSchemaVersion = -1 };
            }

            if (data.SchemaVersion != CurrentSchemaVersion)
                return new SaveLoadResult { Status = SaveLoadStatus.Incompatible, FoundSchemaVersion = data.SchemaVersion };

            return new SaveLoadResult { Status = SaveLoadStatus.Success, Data = data };
        }

        public bool SlotExists(string slotId) => File.Exists(PathFor(slotId));

        private string PathFor(string slotId) => Path.Combine(_directory, $"{slotId}.json");
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run the EditMode test command from Global Constraints.
Expected: `failed="0"`, 2 new tests passing (running total from 216: **218**). Confirm every pre-existing test in `JsonSaveStorageTests.cs` and `SaveLoadRoundTripTests.cs` is still green unmodified.

- [ ] **Step 5: Commit**

```bash
git add Runtime/SaveLoad/ISaveStorage.cs Runtime/SaveLoad/JsonSaveStorage.cs Tests/Runtime/SaveLoad/JsonSaveStorageTests.cs
git commit -m "$(cat <<'EOF'
Add ISaveStorage.SlotExists for save-slot-grid occupancy checks

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: `SaveLoadController`

**Files:**
- Create: `Runtime/SaveLoad/SaveLoadController.cs`
- Create: `Tests/Runtime/SaveLoad/SaveLoadControllerTests.cs`

**Interfaces:**
- Consumes: `NovelForge.Runtime.PlaybackController.CreateSnapshot()`/`.RestoreSnapshot(PlaybackSnapshot)` (already merged), `NovelForge.Runtime.StoryContext.Variables` (`VariableStore.Export()`/`.Import(...)`, already merged), `NovelForge.Runtime.ISaveStorage.Save`/`.Load`/`.SlotExists` (`.SlotExists` from Task 1).
- Produces: `NovelForge.Runtime.SaveLoadController` — constructor `(PlaybackController playback, StoryContext context, string scriptId, ISaveStorage storage)`; `public void SaveTo(string slotId)`; `public SaveLoadResult LoadInto(string slotId)`; `public bool SlotExists(string slotId)`. Task 3's `SaveLoadView` consumes all three public methods with these exact signatures.

This task depends only on Task 1's `SlotExists`. It does not touch `NovelForge.UI` at all.

- [ ] **Step 1: Write the failing tests**

Create `Tests/Runtime/SaveLoad/SaveLoadControllerTests.cs`:

```csharp
using System.IO;
using NUnit.Framework;

namespace NovelForge.Runtime.Tests
{
    public class SaveLoadControllerTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "NovelForgeSaveLoadControllerTests_" + System.Guid.NewGuid());
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }

        private const string Source = "label start\nreturn\n";

        [Test]
        public void SaveTo_ThenLoadInto_OnFreshContext_RestoresVariables()
        {
            var storage = new JsonSaveStorage(_directory);
            var script = new ScriptCompiler().Compile(Source);

            var context = new StoryContext();
            context.Variables.Set("score", 5);
            var playback = new PlaybackController(script, context);
            var controller = new SaveLoadController(playback, context, "test-script", storage);

            controller.SaveTo("slot1");

            var newContext = new StoryContext();
            var newPlayback = new PlaybackController(script, newContext);
            var newController = new SaveLoadController(newPlayback, newContext, "test-script", storage);

            var result = newController.LoadInto("slot1");

            Assert.AreEqual(SaveLoadStatus.Success, result.Status);
            Assert.AreEqual(5, newContext.Variables.GetInt("score"));
        }

        [Test]
        public void SaveTo_ThenLoadInto_RestoresPointerAndCallStackOntoNewPlaybackController()
        {
            var storage = new JsonSaveStorage(_directory);
            var script = new ScriptCompiler().Compile(Source);

            var context = new StoryContext();
            var playback = new PlaybackController(script, context);
            var controller = new SaveLoadController(playback, context, "test-script", storage);

            controller.SaveTo("slot1");
            PlaybackSnapshot originalSnapshot = playback.CreateSnapshot();

            var newContext = new StoryContext();
            var newPlayback = new PlaybackController(script, newContext);
            var newController = new SaveLoadController(newPlayback, newContext, "test-script", storage);
            newController.LoadInto("slot1");

            PlaybackSnapshot restoredSnapshot = newPlayback.CreateSnapshot();
            Assert.AreEqual(originalSnapshot.PointerIndex, restoredSnapshot.PointerIndex);
            CollectionAssert.AreEqual(originalSnapshot.CallStack, restoredSnapshot.CallStack);
        }

        [Test]
        public void LoadInto_MissingSlot_ReturnsNotFound_AndDoesNotChangeContextOrPlayback()
        {
            var storage = new JsonSaveStorage(_directory);
            var script = new ScriptCompiler().Compile(Source);

            var context = new StoryContext();
            context.Variables.Set("score", 7);
            var playback = new PlaybackController(script, context);
            var controller = new SaveLoadController(playback, context, "test-script", storage);

            var result = controller.LoadInto("does-not-exist");

            Assert.AreEqual(SaveLoadStatus.NotFound, result.Status);
            Assert.AreEqual(7, context.Variables.GetInt("score"));
            Assert.AreEqual(0, playback.CreateSnapshot().PointerIndex);
        }

        [Test]
        public void LoadInto_IncompatibleSlot_ReturnsIncompatible_AndDoesNotChangeContextOrPlayback()
        {
            var storage = new JsonSaveStorage(_directory);
            var script = new ScriptCompiler().Compile(Source);
            Directory.CreateDirectory(_directory);
            File.WriteAllText(Path.Combine(_directory, "slot1.json"), "{\"SchemaVersion\":999,\"ScriptId\":\"x\"}");

            var context = new StoryContext();
            context.Variables.Set("score", 9);
            var playback = new PlaybackController(script, context);
            var controller = new SaveLoadController(playback, context, "test-script", storage);

            var result = controller.LoadInto("slot1");

            Assert.AreEqual(SaveLoadStatus.Incompatible, result.Status);
            Assert.AreEqual(9, context.Variables.GetInt("score"));
            Assert.AreEqual(0, playback.CreateSnapshot().PointerIndex);
        }

        [Test]
        public void SlotExists_DelegatesToStorage()
        {
            var storage = new JsonSaveStorage(_directory);
            var script = new ScriptCompiler().Compile(Source);
            var context = new StoryContext();
            var playback = new PlaybackController(script, context);
            var controller = new SaveLoadController(playback, context, "test-script", storage);

            Assert.IsFalse(controller.SlotExists("slot1"));
            controller.SaveTo("slot1");
            Assert.IsTrue(controller.SlotExists("slot1"));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the EditMode test command from Global Constraints.
Expected: compile error (`SaveLoadController` does not exist yet).

- [ ] **Step 3: Implement `SaveLoadController`**

Create `Runtime/SaveLoad/SaveLoadController.cs`:

```csharp
using System.Collections.Generic;

namespace NovelForge.Runtime
{
    public class SaveLoadController
    {
        private readonly PlaybackController _playback;
        private readonly StoryContext _context;
        private readonly string _scriptId;
        private readonly ISaveStorage _storage;

        public SaveLoadController(PlaybackController playback, StoryContext context, string scriptId, ISaveStorage storage)
        {
            _playback = playback;
            _context = context;
            _scriptId = scriptId;
            _storage = storage;
        }

        public void SaveTo(string slotId)
        {
            PlaybackSnapshot snapshot = _playback.CreateSnapshot();
            var data = new SaveData
            {
                ScriptId = _scriptId,
                PointerIndex = snapshot.PointerIndex,
                CallStack = snapshot.CallStack,
                Variables = new Dictionary<string, object>(_context.Variables.Export()),
            };
            _storage.Save(slotId, data);
        }

        // Any status other than Success leaves the passed-in playback/context completely
        // untouched — a corrupt or missing save must never partially clobber a running
        // session, the same "no exception, no silent partial state" principle used
        // throughout this project's error handling.
        public SaveLoadResult LoadInto(string slotId)
        {
            SaveLoadResult result = _storage.Load(slotId);
            if (result.Status != SaveLoadStatus.Success)
                return result;

            _context.Variables.Import(result.Data.Variables);
            _playback.RestoreSnapshot(new PlaybackSnapshot
            {
                PointerIndex = result.Data.PointerIndex,
                CallStack = result.Data.CallStack,
            });
            return result;
        }

        public bool SlotExists(string slotId) => _storage.SlotExists(slotId);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run the EditMode test command from Global Constraints.
Expected: `failed="0"`, 5 new tests passing (running total from 218: **223**).

- [ ] **Step 5: Commit**

```bash
git add Runtime/SaveLoad/SaveLoadController.cs Tests/Runtime/SaveLoad/SaveLoadControllerTests.cs
git commit -m "$(cat <<'EOF'
Add SaveLoadController: orchestrates snapshot/variables/storage for player-initiated save and load

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: `SaveLoadView`

**Files:**
- Create: `UI/SaveLoadView.cs`
- Create: `Tests/UI/SaveLoadViewTests.cs`

**Interfaces:**
- Consumes: `NovelForge.Runtime.SaveLoadController` — constructor and all three public methods (Task 2), `NovelForge.Runtime.SaveLoadResult`/`.Status` and `NovelForge.Runtime.SaveLoadStatus` enum (already merged).
- Produces: `NovelForge.UI.SaveLoadView` — public nested `SlotUI` struct (`string slotId`, `Button button`, `TMP_Text label`), `internal SlotUI[] slots`, `internal TMP_Text statusText`, `public void Initialize(SaveLoadController controller)`, `public void ShowSaveMode()`, `public void ShowLoadMode()`, `public event Action OnSaveCompleted`, `public event Action OnLoadSucceeded`. Nothing consumes this from a later task — this is the plan's final task.

This task has full automated EditMode tests, following `Tests/UI/ChoiceViewTests.cs`'s established pattern exactly (programmatic `GameObject`/`Button`/`TMP_Text` construction, `[TearDown]` cleanup, no scene, no Play Mode).

- [ ] **Step 1: Write the failing tests**

Create `Tests/UI/SaveLoadViewTests.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using NovelForge.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NovelForge.UI.Tests
{
    public class SaveLoadViewTests
    {
        private readonly List<GameObject> _spawned = new();
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "NovelForgeSaveLoadViewTests_" + System.Guid.NewGuid());
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
                Object.DestroyImmediate(go);
            _spawned.Clear();

            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }

        private (Button button, TMP_Text label) CreateSlotWidgets(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            var button = go.AddComponent<Button>();
            var labelGo = new GameObject("Label");
            _spawned.Add(labelGo);
            labelGo.transform.SetParent(go.transform);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            return (button, label);
        }

        private SaveLoadController CreateController()
        {
            var storage = new JsonSaveStorage(_directory);
            var script = new ScriptCompiler().Compile("label start\nreturn\n");
            var context = new StoryContext();
            var playback = new PlaybackController(script, context);
            return new SaveLoadController(playback, context, "test-script", storage);
        }

        private SaveLoadView CreateView(out SaveLoadController controller, params string[] slotIds)
        {
            var go = new GameObject("SaveLoadView");
            _spawned.Add(go);
            var view = go.AddComponent<SaveLoadView>();

            var statusGo = new GameObject("StatusText");
            _spawned.Add(statusGo);
            view.statusText = statusGo.AddComponent<TextMeshProUGUI>();

            view.slots = new SaveLoadView.SlotUI[slotIds.Length];
            for (int i = 0; i < slotIds.Length; i++)
            {
                (Button button, TMP_Text label) = CreateSlotWidgets($"Slot_{slotIds[i]}");
                view.slots[i] = new SaveLoadView.SlotUI { slotId = slotIds[i], button = button, label = label };
            }

            controller = CreateController();
            view.Initialize(controller);
            return view;
        }

        [Test]
        public void ShowLoadMode_OnEmptyStorage_AllSlotsShowEmptyAndAreNotInteractable()
        {
            var view = CreateView(out _, "slot1", "slot2");

            view.ShowLoadMode();

            Assert.AreEqual("Empty", view.slots[0].label.text);
            Assert.AreEqual("Empty", view.slots[1].label.text);
            Assert.IsFalse(view.slots[0].button.interactable);
            Assert.IsFalse(view.slots[1].button.interactable);
        }

        [Test]
        public void ShowSaveMode_AllSlotsAreInteractable_RegardlessOfOccupancy()
        {
            var view = CreateView(out _, "slot1", "slot2");

            view.ShowSaveMode();

            Assert.IsTrue(view.slots[0].button.interactable);
            Assert.IsTrue(view.slots[1].button.interactable);
        }

        [Test]
        public void ClickingSlotInSaveMode_SavesAndRefreshesToOccupied_RaisesOnSaveCompleted()
        {
            var view = CreateView(out SaveLoadController controller, "slot1");
            view.ShowSaveMode();
            bool saveCompleted = false;
            view.OnSaveCompleted += () => saveCompleted = true;

            view.slots[0].button.onClick.Invoke();

            Assert.IsTrue(controller.SlotExists("slot1"));
            Assert.AreEqual("Occupied", view.slots[0].label.text);
            Assert.IsTrue(saveCompleted);
        }

        [Test]
        public void ClickingOccupiedSlotInLoadMode_RaisesOnLoadSucceeded()
        {
            var view = CreateView(out SaveLoadController controller, "slot1");
            controller.SaveTo("slot1");
            view.ShowLoadMode();
            bool loadSucceeded = false;
            view.OnLoadSucceeded += () => loadSucceeded = true;

            view.slots[0].button.onClick.Invoke();

            Assert.IsTrue(loadSucceeded);
        }

        [Test]
        public void ClickingSlotInLoadMode_Incompatible_SetsStatusText_DoesNotRaiseOnLoadSucceeded()
        {
            // A slot whose file exists but has the wrong SchemaVersion: SlotExists is still
            // true (the file is there), so the button stays interactable — this exercises
            // the Incompatible branch specifically, not the disabled-button short-circuit
            // that ShowLoadMode_OnEmptyStorage already covers for a truly empty slot.
            Directory.CreateDirectory(_directory);
            File.WriteAllText(Path.Combine(_directory, "slot1.json"), "{\"SchemaVersion\":999,\"ScriptId\":\"x\"}");
            var view = CreateView(out _, "slot1");
            view.ShowLoadMode();
            bool loadSucceeded = false;
            view.OnLoadSucceeded += () => loadSucceeded = true;

            view.slots[0].button.onClick.Invoke();

            Assert.IsFalse(loadSucceeded);
            StringAssert.Contains("incompatible", view.statusText.text.ToLowerInvariant());
        }

        [Test]
        public void Refresh_MissingSlotWiring_LogsErrorAndSkipsSlot_DoesNotThrow()
        {
            var go = new GameObject("SaveLoadView");
            _spawned.Add(go);
            var view = go.AddComponent<SaveLoadView>();
            view.slots = new[] { new SaveLoadView.SlotUI { slotId = "slot1", button = null, label = null } };
            view.Initialize(CreateController());

            LogAssert.Expect(LogType.Error, "NovelForge: SaveLoadView slot 'slot1' is missing button/label — skipping.");
            Assert.DoesNotThrow(() => view.ShowLoadMode());
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the EditMode test command from Global Constraints.
Expected: compile error (`SaveLoadView` does not exist yet).

- [ ] **Step 3: Implement `SaveLoadView`**

Create `UI/SaveLoadView.cs`:

```csharp
using System;
using NovelForge.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovelForge.UI
{
    public class SaveLoadView : MonoBehaviour
    {
        public struct SlotUI
        {
            public string slotId;
            public Button button;
            public TMP_Text label;
        }

        [SerializeField] internal SlotUI[] slots = Array.Empty<SlotUI>();
        [SerializeField] internal TMP_Text statusText;

        public event Action OnSaveCompleted;
        public event Action OnLoadSucceeded;

        private SaveLoadController _controller;
        private bool _saveMode;

        public void Initialize(SaveLoadController controller)
        {
            _controller = controller;
        }

        public void ShowSaveMode()
        {
            _saveMode = true;
            Refresh();
        }

        public void ShowLoadMode()
        {
            _saveMode = false;
            Refresh();
        }

        private void Refresh()
        {
            if (_controller == null)
            {
                Debug.LogError("NovelForge: SaveLoadView has no controller — call Initialize first.");
                return;
            }

            if (statusText != null)
                statusText.text = string.Empty;

            foreach (SlotUI slot in slots)
            {
                if (slot.button == null || slot.label == null)
                {
                    Debug.LogError($"NovelForge: SaveLoadView slot '{slot.slotId}' is missing button/label — skipping.");
                    continue;
                }

                bool occupied = _controller.SlotExists(slot.slotId);
                slot.label.text = occupied ? "Occupied" : "Empty";
                slot.button.interactable = _saveMode || occupied;

                string slotId = slot.slotId;
                slot.button.onClick.RemoveAllListeners();
                slot.button.onClick.AddListener(() => OnSlotClicked(slotId));
            }
        }

        private void OnSlotClicked(string slotId)
        {
            if (_controller == null)
                return;

            if (_saveMode)
            {
                _controller.SaveTo(slotId);
                Refresh();
                OnSaveCompleted?.Invoke();
            }
            else
            {
                SaveLoadResult result = _controller.LoadInto(slotId);
                switch (result.Status)
                {
                    case SaveLoadStatus.Success:
                        OnLoadSucceeded?.Invoke();
                        break;
                    case SaveLoadStatus.NotFound:
                        SetStatus("This slot is empty.");
                        break;
                    case SaveLoadStatus.Incompatible:
                        SetStatus("This save is from an incompatible version.");
                        break;
                }
            }
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
                statusText.text = message;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run the EditMode test command from Global Constraints.
Expected: `failed="0"`, 6 new tests passing (running total from 223: **229**).

- [ ] **Step 5: Commit**

```bash
git add UI/SaveLoadView.cs Tests/UI/SaveLoadViewTests.cs
git commit -m "$(cat <<'EOF'
Add SaveLoadView: fixed-slot-grid UGUI save/load screen

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```
