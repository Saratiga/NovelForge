using System;
using System.Collections.Generic;

namespace NovelForge.Runtime
{
    public class VariableStore
    {
        private readonly Dictionary<string, object> _values = new();

        public void Set(string name, object value) => _values[name] = value;

        public bool TryGet(string name, out object value) => _values.TryGetValue(name, out value);

        public int GetInt(string name) => _values.TryGetValue(name, out var v) ? Convert.ToInt32(v) : 0;

        public float GetFloat(string name) => _values.TryGetValue(name, out var v) ? Convert.ToSingle(v) : 0f;

        public bool GetBool(string name) => _values.TryGetValue(name, out var v) ? Convert.ToBoolean(v) : false;

        public string GetString(string name) => _values.TryGetValue(name, out var v) ? v.ToString() : string.Empty;
    }
}
