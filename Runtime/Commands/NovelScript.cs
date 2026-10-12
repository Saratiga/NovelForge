using System.Collections.Generic;
using UnityEngine;

namespace NovelForge.Runtime
{
    public class NovelScript
    {
        public const int FinishedOffset = int.MaxValue;

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
                return new ScriptPosition { Label = null, Offset = FinishedOffset };

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
            if (position.Offset < 0)
            {
                commandIndex = -1;
                return false;
            }

            if (position.Label == null)
                return ResolveBeforeFirstLabel(position.Offset, out commandIndex);

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

        // Any offset at or past the end means "finished" — including the FinishedOffset
        // sentinel, so a finished save stays finished after the script grows.
        private bool ResolveBeforeFirstLabel(int offset, out int commandIndex)
        {
            if (offset >= Commands.Count)
            {
                commandIndex = Commands.Count;
                return true;
            }

            int firstLabelStart = _labelsByStart.Count > 0 ? _labelsByStart[0].start : Commands.Count;
            if (offset < firstLabelStart || offset == 0)
            {
                commandIndex = offset;
                return true;
            }

            Debug.LogWarning($"NovelForge: saved position before the first label at offset {offset} is past the start of the first label — resuming at the start of the script.");
            commandIndex = 0;
            return true;
        }
    }
}
