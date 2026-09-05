using System.Collections;
using System.Collections.Generic;

namespace NovelForge.Runtime.Tests
{
    public class RecordingAudioPresenter : IAudioPresenter
    {
        public readonly List<string> MusicCalls = new();
        public readonly List<string> SfxCalls = new();

        public IEnumerator PlayMusic(string trackId)
        {
            MusicCalls.Add(trackId);
            yield break;
        }

        public IEnumerator PlaySfx(string clipId)
        {
            SfxCalls.Add(clipId);
            yield break;
        }
    }
}
