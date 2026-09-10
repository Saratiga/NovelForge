using UnityEngine;

namespace NovelForge.Editor
{
    public class NovelScriptAsset : ScriptableObject
    {
        [TextArea(10, 40)]
        public string Source;
    }
}
