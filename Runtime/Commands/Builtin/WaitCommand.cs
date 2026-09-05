using System.Collections;
using UnityEngine;

namespace NovelForge.Runtime
{
    public class WaitCommand : Command
    {
        private readonly float _seconds;

        public WaitCommand(float seconds) => _seconds = seconds;

        public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
        {
            if (context.Timing == null)
            {
                Debug.LogError("NovelForge: no ITimingPresenter wired — skipping wait.");
                yield break;
            }
            yield return context.Timing.Wait(_seconds);
        }
    }
}
