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
