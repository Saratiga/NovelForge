using System.Collections;

namespace NovelForge.Runtime
{
    public interface ITimingPresenter
    {
        IEnumerator Wait(float seconds);
    }
}
