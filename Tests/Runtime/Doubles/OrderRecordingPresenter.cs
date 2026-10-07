using System.Collections;
using System.Collections.Generic;

namespace NovelForge.Runtime.Tests
{
    // One shared log across presenter kinds, for asserting cross-presenter call order.
    public class OrderRecordingPresenter : IBackgroundPresenter, IAudioPresenter, IDialoguePresenter
    {
        public readonly List<string> Log = new();

        public IEnumerator ShowBackground(string backgroundId)
        {
            Log.Add($"bg:{backgroundId}");
            yield break;
        }

        public IEnumerator ShowCg(string cgId)
        {
            Log.Add($"cg:{cgId}");
            yield break;
        }

        public IEnumerator PlayMusic(string trackId)
        {
            Log.Add($"music:{trackId}");
            yield break;
        }

        public IEnumerator PlaySfx(string clipId)
        {
            Log.Add($"sfx:{clipId}");
            yield break;
        }

        public IEnumerator ShowLine(string characterId, string text, string emotion, string position)
        {
            Log.Add($"line:{characterId}:{text}");
            yield break;
        }

        public IEnumerator ShowActor(string characterId, string emotion, string position)
        {
            Log.Add($"actor:{characterId}:{emotion}:{position ?? "null"}");
            yield break;
        }
    }
}
