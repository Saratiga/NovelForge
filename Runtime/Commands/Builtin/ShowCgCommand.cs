using System.Collections;

namespace NovelForge.Runtime
{
    public class ShowCgCommand : Command
    {
        private readonly string _cgId;

        public ShowCgCommand(string cgId) => _cgId = cgId;

        public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
        {
            yield return context.Backgrounds.ShowCg(_cgId);
        }
    }
}
