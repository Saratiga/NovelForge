using System.Collections.Generic;
using Newtonsoft.Json;

namespace NovelForge.Runtime
{
    public class LocalizationTable
    {
        private readonly Dictionary<string, string> _entries;

        private LocalizationTable(Dictionary<string, string> entries)
        {
            _entries = entries;
        }

        public bool TryGetText(string lineId, out string text) => _entries.TryGetValue(lineId, out text);

        public static LocalizationTable FromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                return new LocalizationTable(new Dictionary<string, string>());

            var entries = JsonConvert.DeserializeObject<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
            return new LocalizationTable(entries);
        }
    }
}
