using System.Collections;

namespace NovelForge.Runtime
{
    public class ShowBackgroundCommand : Command
    {
        private readonly string _backgroundId;

        public ShowBackgroundCommand(string backgroundId) => _backgroundId = backgroundId;

        public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
        {
            yield return context.Backgrounds.ShowBackground(_backgroundId);
        }
    }
}
