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

            if (library == null)
            {
                Debug.LogError("NovelForge: DialoguePresenter is missing library — showing text without actor.");
            }
            else if (!library.TryGetCharacter(characterId, out var character))
            {
                Debug.LogError($"NovelForge: no character registered for id '{characterId}' — showing text without actor.");
            }
            else
            {
                displayName = character.DisplayName;
                nameColor = character.NameColor;

                if (!string.IsNullOrEmpty(emotion))
                    yield return ShowActorSprite(character, characterId, emotion, position);
            }

            yield return dialogueBox.ShowText(displayName, nameColor, text);
        }

        public IEnumerator ShowActor(string characterId, string emotion, string position)
        {
            if (string.IsNullOrEmpty(emotion))
                yield break;

            if (library == null)
            {
                Debug.LogError("NovelForge: DialoguePresenter is missing library — skipping actor.");
                yield break;
            }

            if (!library.TryGetCharacter(characterId, out var character))
            {
                Debug.LogError($"NovelForge: no character registered for id '{characterId}' — skipping actor.");
                yield break;
            }

            yield return ShowActorSprite(character, characterId, emotion, position);
        }

        private IEnumerator ShowActorSprite(CharacterDefinition character, string characterId, string emotion, string position)
        {
            if (!character.TryGetSprite(emotion, out var sprite))
            {
                Debug.LogError($"NovelForge: character '{characterId}' has no sprite for emotion '{emotion}' — leaving actor unchanged.");
            }
            else if (!TryFindSlot(position, out var slotView))
            {
                Debug.LogError($"NovelForge: no position slot registered for '{position}' — skipping actor display.");
            }
            else if (slotView == null)
            {
                Debug.LogError($"NovelForge: position slot '{position}' has no ActorView assigned — skipping actor display.");
            }
            else
            {
                yield return slotView.ShowSprite(sprite);
            }
        }

        private bool TryFindSlot(string position, out ActorView view)
        {
            foreach (var slot in positionSlots)
            {
                if (slot.position == position)
                {
                    view = slot.view;
                    return true;
                }
            }

            view = null;
            return false;
        }
    }
}
