using System.Collections;
using UnityEngine;

namespace NovelForge.Runtime
{
    public class ReturnCommand : Command
    {
        public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
        {
            if (pointer.TryPop(out int returnIndex))
            {
                pointer.Current = returnIndex;
            }
            else
            {
                Debug.LogError("NovelForge: 'return' with an empty call stack — ending playback.");
                // Any index >= Commands.Count marks playback finished; MaxValue is a
                // safe sentinel here since ReturnCommand has no reference to the script length.
                pointer.Current = int.MaxValue;
            }
            yield break;
        }
    }
}
