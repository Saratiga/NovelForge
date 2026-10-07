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
        public void LoadMode_ScriptMismatch_ShowsDifferentStoryMessage()
        {
            var storage = new JsonSaveStorage(_directory);
            var script = new ScriptCompiler().Compile("label start\nreturn\n");
            var otherContext = new StoryContext();
            new SaveLoadController(new PlaybackController(script, otherContext), otherContext, "other-story", storage).SaveTo("slot1");
            var view = CreateView(out _, "slot1");
            view.ShowLoadMode();
            bool loadSucceeded = false;
            view.OnLoadSucceeded += () => loadSucceeded = true;

            view.slots[0].button.onClick.Invoke();

            Assert.IsFalse(loadSucceeded);
            Assert.AreEqual("This save belongs to a different story.", view.statusText.text);
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
