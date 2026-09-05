using System.Collections;

namespace NovelForge.Runtime
{
    public class GosubCommand : Command
    {
        public int TargetIndex { get; internal set; }

        public GosubCommand(int targetIndex) => TargetIndex = targetIndex;

        public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
        {
            pointer.Push(pointer.Current + 1);
            pointer.Current = TargetIndex;
            yield break;
        }
    }
}
