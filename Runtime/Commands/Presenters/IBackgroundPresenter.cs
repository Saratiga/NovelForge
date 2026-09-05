using System.Collections;

namespace NovelForge.Runtime
{
    public interface IBackgroundPresenter
    {
        IEnumerator ShowBackground(string backgroundId);
        IEnumerator ShowCg(string cgId);
    }
}
