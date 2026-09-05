using System.Collections;
using System.Collections.Generic;

namespace NovelForge.Runtime.Tests
{
    public class RecordingTimingPresenter : ITimingPresenter
    {
        public readonly List<float> Calls = new();

        public IEnumerator Wait(float seconds)
        {
            Calls.Add(seconds);
            yield break;
        }
    }
}
