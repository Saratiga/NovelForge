using System.Collections;

namespace NovelForge.Runtime
{
    public class PlayMusicCommand : Command
    {
        private readonly string _trackId;

        public PlayMusicCommand(string trackId) => _trackId = trackId;

        public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
        {
            yield return context.Audio.PlayMusic(_trackId);
        }
    }
}
