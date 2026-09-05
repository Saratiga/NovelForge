using System.Collections;

namespace NovelForge.Runtime
{
    public class WaitCommand : Command
    {
        private readonly float _seconds;

        public WaitCommand(float seconds) => _seconds = seconds;

        public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
        {
            yield return context.Timing.Wait(_seconds);
        }
    }
}
