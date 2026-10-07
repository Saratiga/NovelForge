using System.Collections.Generic;

namespace NovelForge.Runtime
{
    public class SaveData
    {
        public int SchemaVersion;
        public string ScriptId;
        public ScriptPosition Position;
        public ScriptPosition[] CallStack;
        public Dictionary<string, object> Variables;
        public SceneState Scene;
    }
}
