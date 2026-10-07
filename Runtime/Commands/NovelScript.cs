using System;
using System.Collections.Generic;
using UnityEngine;

namespace NovelForge.Runtime
{
    public class NovelScript
    {
        private readonly List<(int start, string name)> _labelsByStart = new();

        public IReadOnlyList<Command> Commands { get; }
        public IReadOnlyDictionary<string, int> Labels { get; }

        public NovelScript(IReadOnlyList<Command> commands, IReadOnlyDictionary<string, int> labels)
        {
            Commands = commands;
            Labels = labels;

            foreach (var pair in labels)
                _labelsByStart.Add((pair.Value, pair.Key));
            _labelsByStart.Sort((a, b) => a.start != b.start ? a.start.CompareTo(b.start) : string.CompareOrdinal(a.name, b.name));
        }

        public ScriptPosition ToPosition(int commandIndex)
        {
            if (commandIndex >= Commands.Count)
                return new ScriptPosition { Label = null, Offset = Commands.Count };

            (int start, string name) owner = (-1, null);
            foreach (var entry in _labelsByStart)
            {
                if (entry.start > commandIndex)
                    break;
                if (entry.start > owner.start)
                    owner = entry;
            }

            return owner.name == null
                ? new ScriptPosition { Label = null, Offset = commandIndex }
                : new ScriptPosition { Label = owner.name, Offset = commandIndex - owner.start };
        }

        public bool TryResolve(ScriptPosition position, out int commandIndex)
        {
            if (position.Label == null)
            {
                commandIndex = Math.Min(position.Offset, Commands.Count);
                return true;
            }

            if (!Labels.TryGetValue(position.Label, out int start))
            {
                commandIndex = -1;
                return false;
            }

            int end = Commands.Count;
            foreach (var entry in _labelsByStart)
            {
                if (entry.start > start)
                {
                    end = entry.start;
                    break;
                }
            }

            if (start + position.Offset < end || position.Offset == 0)
            {
                commandIndex = start + position.Offset;
                return true;
            }

            Debug.LogWarning($"NovelForge: saved position in label '{position.Label}' at offset {position.Offset} is past the end of that label — resuming at the label start.");
            commandIndex = start;
            return true;
        }
    }
}
