using System;
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
            NovelScript script = _playback.Script;
            PlaybackSnapshot snapshot = _playback.CreateSnapshot();
            var data = new SaveData
            {
                ScriptId = _scriptId,
                Position = script.ToPosition(snapshot.PointerIndex),
                CallStack = Array.ConvertAll(snapshot.CallStack, script.ToPosition),
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

            SaveData data = result.Data;
            if (data.ScriptId != _scriptId)
                return new SaveLoadResult { Status = SaveLoadStatus.ScriptMismatch, Data = data, FoundSchemaVersion = data.SchemaVersion };

            NovelScript script = _playback.Script;
            ScriptPosition[] savedStack = data.CallStack ?? Array.Empty<ScriptPosition>();
            var callStack = new int[savedStack.Length];
            bool resolved = script.TryResolve(data.Position, out int pointerIndex);
            for (int i = 0; resolved && i < savedStack.Length; i++)
                resolved = script.TryResolve(savedStack[i], out callStack[i]);
            if (!resolved)
                return new SaveLoadResult { Status = SaveLoadStatus.Incompatible, FoundSchemaVersion = data.SchemaVersion };

            _context.Variables.Import(data.Variables);
            _playback.RestoreSnapshot(new PlaybackSnapshot { PointerIndex = pointerIndex, CallStack = callStack });
            return result;
        }

        public bool SlotExists(string slotId) => _storage.SlotExists(slotId);
    }
}
