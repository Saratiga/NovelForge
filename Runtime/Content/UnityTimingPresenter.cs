using System.Collections;
using UnityEngine;

namespace NovelForge.Runtime
{
    public class UnityTimingPresenter : ITimingPresenter
    {
        public IEnumerator Wait(float seconds)
        {
            yield return new WaitForSeconds(seconds);
        }
    }
}
