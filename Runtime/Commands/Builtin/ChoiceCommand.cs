using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NovelForge.Runtime
{
    public class ChoiceCommand : Command
    {
        private readonly IReadOnlyList<string> _optionTexts;
        private readonly int[] _targetIndices;

        public ChoiceCommand(IReadOnlyList<string> optionTexts, int optionCount)
        {
            _optionTexts = optionTexts;
            _targetIndices = new int[optionCount];
        }

        internal void ResolveTarget(int optionIndex, int targetIndex) => _targetIndices[optionIndex] = targetIndex;

        public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
        {
            int selected = -1;
            yield return context.Choices.PresentChoices(_optionTexts, i => selected = i);

            if (selected < 0 || selected >= _targetIndices.Length)
            {
                Debug.LogError($"NovelForge: choice presenter returned invalid selection {selected} — defaulting to option 0.");
                selected = 0;
            }

            pointer.Current = _targetIndices[selected];
        }
    }
}
