using System.Collections.Generic;

namespace NovelForge.Runtime
{
    public class SaveLoadController
    {
        private readonly PlaybackController _playback;
        private readonly StoryContext _context;
        private readonly string _scriptId;
        private readonly ISaveStorage _storage;

        public SaveLoadController(PlaybackController playback, StoryContext context, string scriptId, ISaveStorage storage)
        {
            _playback = playback;
            _context = context;
            _scriptId = scriptId;
            _storage = storage;
        }

        public void SaveTo(string slotId)
        {
            PlaybackSnapshot snapshot = _playback.CreateSnapshot();
            var data = new SaveData
            {
                ScriptId = _scriptId,
                PointerIndex = snapshot.PointerIndex,
                CallStack = snapshot.CallStack,
                Variables = new Dictionary<string, object>(_context.Variables.Export()),
            };
            _storage.Save(slotId, data);
        }

        // Any status other than Success leaves the passed-in playback/context completely
        // untouched — a corrupt or missing save must never partially clobber a running
        // session, the same "no exception, no silent partial state" principle used
        // throughout this project's error handling.
        public SaveLoadResult LoadInto(string slotId)
        {
            SaveLoadResult result = _storage.Load(slotId);
            if (result.Status != SaveLoadStatus.Success)
                return result;

            _context.Variables.Import(result.Data.Variables);
            _playback.RestoreSnapshot(new PlaybackSnapshot
            {
                PointerIndex = result.Data.PointerIndex,
                CallStack = result.Data.CallStack,
            });
            return result;
        }

        public bool SlotExists(string slotId) => _storage.SlotExists(slotId);
    }
}
