using System.Collections;

namespace NovelForge.Runtime
{
    public class JumpCommand : Command
    {
        public int TargetIndex { get; internal set; }

        public JumpCommand(int targetIndex) => TargetIndex = targetIndex;

        public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
        {
            pointer.Current = TargetIndex;
            yield break;
        }
    }
}
