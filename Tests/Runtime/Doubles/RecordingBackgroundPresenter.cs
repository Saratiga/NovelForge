using System.Collections;
using System.Collections.Generic;

namespace NovelForge.Runtime.Tests
{
    public class RecordingBackgroundPresenter : IBackgroundPresenter
    {
        public readonly List<string> BackgroundCalls = new();
        public readonly List<string> CgCalls = new();

        public IEnumerator ShowBackground(string backgroundId)
        {
            BackgroundCalls.Add(backgroundId);
            yield break;
        }

        public IEnumerator ShowCg(string cgId)
        {
            CgCalls.Add(cgId);
            yield break;
        }
    }
}
