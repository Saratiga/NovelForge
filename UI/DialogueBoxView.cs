using System.Collections;
using NovelForge.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovelForge.UI
{
    public class DialogueBoxView : MonoBehaviour
    {
        [SerializeField] internal TMP_Text nameText;
        [SerializeField] internal TMP_Text bodyText;
        [SerializeField] internal Button advanceButton;
        [SerializeField] internal float secondsPerCharacter = 0.02f;

        internal IDeltaTimeSource TimeSource = new UnityDeltaTimeSource();
        internal IAdvanceInputSource AdvanceInput;

        internal void Awake()
        {
            if (AdvanceInput == null && advanceButton != null)
                AdvanceInput = new ButtonAdvanceInputSource(advanceButton);
        }

        public IEnumerator ShowText(string speakerName, Color nameColor, string text)
        {
            if (nameText == null || bodyText == null)
            {
                Debug.LogError("NovelForge: DialogueBoxView is missing nameText/bodyText — skipping.");
                yield break;
            }

            nameText.text = speakerName;
            nameText.color = nameColor;

            if (AdvanceInput == null)
            {
                Debug.LogError("NovelForge: DialogueBoxView has no advance input wired — showing full text without waiting.");
                bodyText.text = text;
                yield break;
            }

            if (secondsPerCharacter <= 0f)
            {
                bodyText.text = text;
            }
            else
            {
                bodyText.text = string.Empty;
                float t = 0f;
                int shown = 0;
                while (shown < text.Length)
                {
                    t += TimeSource.DeltaTime;
                    int target = Mathf.Min(text.Length, Mathf.FloorToInt(t / secondsPerCharacter));
                    if (target > shown)
                    {
                        shown = target;
                        bodyText.text = text.Substring(0, shown);
                    }

                    if (shown >= text.Length)
                        break;

                    if (AdvanceInput.ConsumeAdvanceRequest())
                    {
                        bodyText.text = text;
                        break;
                    }

                    yield return null;
                }

                bodyText.text = text;
            }

            while (!AdvanceInput.ConsumeAdvanceRequest())
                yield return null;
        }
    }
}
