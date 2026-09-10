using System;
using System.Collections.Generic;
using System.Globalization;

namespace NovelForge.Runtime
{
    public class CommandRegistry
    {
        private readonly Dictionary<string, Func<string, Command>> _factories = new();

        public void Register(string commandName, Func<string, Command> factory) => _factories[commandName] = factory;

        public IReadOnlyCollection<string> RegisteredNames => _factories.Keys;

        public bool TryCreate(string commandName, string rawArgs, out Command command)
        {
            if (_factories.TryGetValue(commandName, out var factory))
            {
                command = factory(rawArgs);
                return true;
            }
            command = null;
            return false;
        }

        public static CommandRegistry CreateDefault()
        {
            var registry = new CommandRegistry();
            registry.Register("bg", args => new ShowBackgroundCommand(args.Trim()));
            registry.Register("cg", args => new ShowCgCommand(args.Trim()));
            registry.Register("music", args => new PlayMusicCommand(args.Trim()));
            registry.Register("sfx", args => new PlaySfxCommand(args.Trim()));
            registry.Register("wait", args => new WaitCommand(float.Parse(args.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture)));
            return registry;
        }
    }
}
