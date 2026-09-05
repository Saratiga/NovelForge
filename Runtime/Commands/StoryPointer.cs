using System.Collections.Generic;

namespace NovelForge.Runtime
{
    public class StoryPointer : IStoryPointer
    {
        private readonly Stack<int> _callStack = new();

        public int Current { get; set; }

        public void Push(int returnIndex) => _callStack.Push(returnIndex);

        public bool TryPop(out int returnIndex) => _callStack.TryPop(out returnIndex);
    }
}
