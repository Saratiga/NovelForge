using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace NovelForge.Runtime
{
    public class JsonSaveStorage : ISaveStorage
    {
        public const int CurrentSchemaVersion = 2;

        private readonly string _directory;

        public JsonSaveStorage() : this(Application.persistentDataPath)
        {
        }

        internal JsonSaveStorage(string directory)
        {
            _directory = directory;
        }

        public void Save(string slotId, SaveData data)
        {
            data.SchemaVersion = CurrentSchemaVersion;
            Directory.CreateDirectory(_directory);
            File.WriteAllText(PathFor(slotId), JsonConvert.SerializeObject(data));
        }

        public SaveLoadResult Load(string slotId)
        {
            string path = PathFor(slotId);
            if (!File.Exists(path))
                return new SaveLoadResult { Status = SaveLoadStatus.NotFound };

            SaveData data;
            try
            {
                data = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(path));
            }
            catch (JsonException e)
            {
                Debug.LogError($"NovelForge: save slot '{slotId}' is corrupt — {e.Message}");
                return new SaveLoadResult { Status = SaveLoadStatus.Incompatible, FoundSchemaVersion = -1 };
            }

            if (data == null)
            {
                Debug.LogError($"NovelForge: save slot '{slotId}' is empty or malformed.");
                return new SaveLoadResult { Status = SaveLoadStatus.Incompatible, FoundSchemaVersion = -1 };
            }

            if (data.SchemaVersion != CurrentSchemaVersion)
                return new SaveLoadResult { Status = SaveLoadStatus.Incompatible, FoundSchemaVersion = data.SchemaVersion };

            return new SaveLoadResult { Status = SaveLoadStatus.Success, Data = data };
        }

        public bool SlotExists(string slotId) => File.Exists(PathFor(slotId));

        private string PathFor(string slotId) => Path.Combine(_directory, $"{slotId}.json");
    }
}
