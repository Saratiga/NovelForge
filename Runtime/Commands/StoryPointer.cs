using System.Collections.Generic;

namespace NovelForge.Runtime
{
    public class StoryPointer : IStoryPointer
    {
        private readonly Stack<int> _callStack = new();
        private int _current;

        public int Current
        {
            get => _current;
            set { _current = value; Moved = true; }
        }

        // Tracked explicitly rather than inferred from "did Current change value" —
        // a jump/gosub/choice whose target equals its own index (the common VN "menu
        // hub" pattern: a label immediately followed by a choice that loops back to
        // itself) sets Current to the same value it already held, which value-equality
        // would misread as "the command didn't move the pointer." Internal: only
        // PlaybackController (same assembly) needs this, so IStoryPointer stays
        // untouched — every Command still sees just Current/Push/TryPop.
        internal bool Moved { get; private set; }

        internal void ResetMoved() => Moved = false;

        public void Push(int returnIndex) => _callStack.Push(returnIndex);

        public bool TryPop(out int returnIndex) => _callStack.TryPop(out returnIndex);

        // Same "internal, same-assembly only" reasoning as Moved/ResetMoved above:
        // only PlaybackController's snapshot/restore needs raw access to the call
        // stack's contents, so IStoryPointer stays untouched here too.
        internal int[] ExportCallStack() => _callStack.ToArray();

        internal void RestoreCallStack(IReadOnlyList<int> saved)
        {
            _callStack.Clear();
            for (int i = saved.Count - 1; i >= 0; i--)
                _callStack.Push(saved[i]);
        }
    }
}
