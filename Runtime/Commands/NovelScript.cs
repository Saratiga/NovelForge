using System.Collections.Generic;

namespace NovelForge.Runtime
{
    public class NovelScript
    {
        public IReadOnlyList<Command> Commands { get; }
        public IReadOnlyDictionary<string, int> Labels { get; }

        public NovelScript(IReadOnlyList<Command> commands, IReadOnlyDictionary<string, int> labels)
        {
            Commands = commands;
            Labels = labels;
        }
    }
}
