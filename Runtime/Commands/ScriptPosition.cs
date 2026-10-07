namespace NovelForge.Runtime
{
    // Label-relative so a save survives edits elsewhere in the script. Label == null
    // means "before the first label", with Offset counted from command 0.
    public struct ScriptPosition
    {
        public string Label;
        public int Offset;
    }
}
