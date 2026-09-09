using System;
using System.Collections;
using System.Collections.Generic;
using NovelForge.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace NovelForge.UI
{
    public class ChoiceView : MonoBehaviour, IChoicePresenter
    {
        [SerializeField] internal Button[] optionButtons;

        public IEnumerator PresentChoices(IReadOnlyList<string> optionTexts, Action<int> onSelected)
        {
            if (optionButtons == null || optionButtons.Length == 0)
            {
                Debug.LogError("NovelForge: ChoiceView has no optionButtons wired — skipping choice.");
                yield break;
            }

            int count = optionTexts.Count;
            if (count > optionButtons.Length)
            {
                Debug.LogError($"NovelForge: ChoiceView has {optionButtons.Length} button(s) but {count} option(s) were requested — truncating.");
                count = optionButtons.Length;
            }

            int selected = -1;
            for (int i = 0; i < optionButtons.Length; i++)
            {
                if (i < count)
                {
                    int optionIndex = i;
                    var label = optionButtons[i].GetComponentInChildren<TMPro.TMP_Text>();
                    if (label != null)
                        label.text = optionTexts[i];
                    optionButtons[i].gameObject.SetActive(true);
                    optionButtons[i].onClick.AddListener(() => selected = optionIndex);
                }
                else
                {
                    optionButtons[i].gameObject.SetActive(false);
                }
            }

            while (selected < 0)
                yield return null;

            for (int i = 0; i < optionButtons.Length; i++)
            {
                optionButtons[i].onClick.RemoveAllListeners();
                optionButtons[i].gameObject.SetActive(false);
            }

            onSelected(selected);
        }
    }
}
