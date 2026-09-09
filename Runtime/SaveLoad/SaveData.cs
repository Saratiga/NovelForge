using System.Collections.Generic;

namespace NovelForge.Runtime
{
    public class SaveData
    {
        public int SchemaVersion;
        public string ScriptId;
        public int PointerIndex;
        public int[] CallStack;
        public Dictionary<string, object> Variables;
    }
}
