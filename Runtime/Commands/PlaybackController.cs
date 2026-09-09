using System.Collections;

namespace NovelForge.Runtime
{
    public class PlaybackController
    {
        private readonly NovelScript _script;
        private readonly StoryContext _context;
        private readonly StoryPointer _pointer = new();

        public PlaybackController(NovelScript script, StoryContext context)
        {
            _script = script;
            _context = context;
        }

        public int CurrentIndex => _pointer.Current;
        public bool IsFinished => _pointer.Current >= _script.Commands.Count;

        public PlaybackSnapshot CreateSnapshot() => new PlaybackSnapshot
        {
            PointerIndex = _pointer.Current,
            CallStack = _pointer.ExportCallStack(),
        };

        public void RestoreSnapshot(PlaybackSnapshot snapshot)
        {
            _pointer.Current = snapshot.PointerIndex;
            _pointer.RestoreCallStack(snapshot.CallStack);
        }

        public IEnumerator RunAll()
        {
            while (!IsFinished)
                yield return StepOnce();
        }

        private IEnumerator StepOnce()
        {
            int before = _pointer.Current;
            Command command = _script.Commands[before];
            _pointer.ResetMoved();
            yield return command.Execute(_context, _pointer);
            if (!_pointer.Moved)
                _pointer.Current = before + 1;
        }
    }
}
