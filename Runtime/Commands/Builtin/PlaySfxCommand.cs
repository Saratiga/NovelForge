using System.Collections;
using UnityEngine;

namespace NovelForge.Runtime
{
    public class PlaySfxCommand : Command
    {
        private readonly string _clipId;

        public PlaySfxCommand(string clipId) => _clipId = clipId;

        public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
        {
            if (context.Audio == null)
            {
                Debug.LogError("NovelForge: no IAudioPresenter wired — skipping sfx.");
                yield break;
            }
            yield return context.Audio.PlaySfx(_clipId);
        }
    }
}
