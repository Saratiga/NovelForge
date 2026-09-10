using UnityEngine;

namespace NovelForge.Runtime
{
    public class NovelScriptAsset : ScriptableObject
    {
        [TextArea(10, 40)]
        public string Source;
    }
}
