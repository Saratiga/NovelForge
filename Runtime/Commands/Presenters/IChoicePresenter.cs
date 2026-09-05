using System;
using System.Collections;
using System.Collections.Generic;

namespace NovelForge.Runtime
{
    public interface IChoicePresenter
    {
        IEnumerator PresentChoices(IReadOnlyList<string> optionTexts, Action<int> onSelected);
    }
}
