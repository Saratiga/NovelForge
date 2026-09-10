using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NovelForge.Runtime
{
    public class ChoiceCommand : Command
    {
        private readonly IReadOnlyList<string> _optionTexts;
        private readonly IReadOnlyList<string> _optionIds;
        private readonly int[] _targetIndices;

        public ChoiceCommand(IReadOnlyList<string> optionTexts, IReadOnlyList<string> optionIds, int optionCount)
        {
            _optionTexts = optionTexts;
            _optionIds = optionIds;
            _targetIndices = new int[optionCount];
        }

        internal void ResolveTarget(int optionIndex, int targetIndex) => _targetIndices[optionIndex] = targetIndex;

        public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
        {
            if (context.Choices == null)
            {
                Debug.LogError("NovelForge: no IChoicePresenter wired — defaulting to option 0.");
                pointer.Current = _targetIndices[0];
                yield break;
            }

            int selected = -1;
            yield return context.Choices.PresentChoices(ResolveTexts(context), i => selected = i);

            if (selected < 0 || selected >= _targetIndices.Length)
            {
                Debug.LogError($"NovelForge: choice presenter returned invalid selection {selected} — defaulting to option 0.");
                selected = 0;
            }

            pointer.Current = _targetIndices[selected];
        }

        private IReadOnlyList<string> ResolveTexts(StoryContext context)
        {
            if (context.Localization == null)
                return _optionTexts;

            var resolved = new string[_optionTexts.Count];
            for (int i = 0; i < _optionTexts.Count; i++)
            {
                if (context.Localization.TryGetText(_optionIds[i], out string translated))
                {
                    resolved[i] = translated;
                }
                else
                {
                    Debug.LogWarning($"NovelForge: no translation for line id '{_optionIds[i]}' — falling back to source text.");
                    resolved[i] = _optionTexts[i];
                }
            }
            return resolved;
        }
    }
}
