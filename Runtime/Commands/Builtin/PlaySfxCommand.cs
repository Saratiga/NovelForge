using System.Collections;

namespace NovelForge.Runtime
{
    public class PlaySfxCommand : Command
    {
        private readonly string _clipId;

        public PlaySfxCommand(string clipId) => _clipId = clipId;

        public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
        {
            yield return context.Audio.PlaySfx(_clipId);
        }
    }
}
