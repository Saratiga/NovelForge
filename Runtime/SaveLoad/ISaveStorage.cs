namespace NovelForge.Runtime
{
    public interface ISaveStorage
    {
        void Save(string slotId, SaveData data);
        SaveLoadResult Load(string slotId);
        bool SlotExists(string slotId);
    }
}
