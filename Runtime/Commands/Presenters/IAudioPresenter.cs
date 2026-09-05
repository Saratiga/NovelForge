using System.Collections;

namespace NovelForge.Runtime
{
    public interface IAudioPresenter
    {
        IEnumerator PlayMusic(string trackId);
        IEnumerator PlaySfx(string clipId);
    }
}
