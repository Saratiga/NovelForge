using System.Collections;
using UnityEngine;

namespace NovelForge.Runtime
{
    public class PlayMusicCommand : Command
    {
        private readonly string _trackId;

        public PlayMusicCommand(string trackId) => _trackId = trackId;

        public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
        {
            if (context.Audio == null)
            {
                Debug.LogError("NovelForge: no IAudioPresenter wired — skipping music.");
                yield break;
            }
            yield return context.Audio.PlayMusic(_trackId);
        }
    }
}
