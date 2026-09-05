namespace NovelForge.Runtime
{
    public interface IStoryPointer
    {
        int Current { get; set; }
        void Push(int returnIndex);
        bool TryPop(out int returnIndex);
    }
}
