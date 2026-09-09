using System;
using System.Collections;
using NovelForge.Runtime;
using UnityEngine;

namespace NovelForge.UI
{
    public class DialoguePresenter : MonoBehaviour, IDialoguePresenter
    {
        [Serializable]
        public struct PositionSlot
        {
            public string position;
            public ActorView view;
        }

        [SerializeField] internal CharacterLibrary library;
        [SerializeField] internal DialogueBoxView dialogueBox;
        [SerializeField] internal PositionSlot[] positionSlots = Array.Empty<PositionSlot>();

        public IEnumerator ShowLine(string characterId, string text, string emotion, string position)
        {
            if (dialogueBox == null)
            {
                Debug.LogError("NovelForge: DialoguePresenter is missing dialogueBox — skipping line.");
                yield break;
            }

            string displayName = characterId;
            Color nameColor = Color.white;

            if (library == null || !library.TryGetCharacter(characterId, out var character))
            {
                Debug.LogError($"NovelForge: no character registered for id '{characterId}' — showing text without actor.");
            }
            else
            {
                displayName = character.DisplayName;
                nameColor = character.NameColor;

                if (!character.TryGetSprite(emotion, out var sprite))
                {
                    Debug.LogError($"NovelForge: character '{characterId}' has no sprite for emotion '{emotion}' — leaving actor unchanged.");
                }
                else
                {
                    var slotView = FindSlot(position);
                    if (slotView == null)
                        Debug.LogError($"NovelForge: no position slot registered for '{position}' — skipping actor display.");
                    else
                        yield return slotView.ShowSprite(sprite);
                }
            }

            yield return dialogueBox.ShowText(displayName, nameColor, text);
        }

        private ActorView FindSlot(string position)
        {
            foreach (var slot in positionSlots)
            {
                if (slot.position == position)
                    return slot.view;
            }

            return null;
        }
    }
}
