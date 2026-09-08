using UnityEngine;

namespace NovelForge.Runtime
{
    public class UnityDeltaTimeSource : IDeltaTimeSource
    {
        public float DeltaTime => Time.deltaTime;
    }
}
