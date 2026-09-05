using System;
using System.Collections;
using System.Collections.Generic;

namespace NovelForge.Runtime.Tests
{
    public class RecordingChoicePresenter : IChoicePresenter
    {
        public readonly List<IReadOnlyList<string>> Calls = new();
        public int NextSelection;

        public IEnumerator PresentChoices(IReadOnlyList<string> optionTexts, Action<int> onSelected)
        {
            Calls.Add(optionTexts);
            onSelected(NextSelection);
            yield break;
        }
    }
}
