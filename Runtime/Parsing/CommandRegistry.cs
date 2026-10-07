using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using UnityEngine;

namespace NovelForge.Runtime
{
    public class CommandRegistry
    {
        internal static int ScanCountForTesting;

        private static readonly Lazy<IReadOnlyList<(string name, Type type)>> AttributedTypes = new(ScanAssemblies);

        private readonly Dictionary<string, Func<string, Command>> _factories = new();

        public void Register(string commandName, Func<string, Command> factory) => _factories[commandName] = factory;

        public IReadOnlyCollection<string> RegisteredNames => _factories.Keys;

        public bool IsRegistered(string commandName) => _factories.ContainsKey(commandName);

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
            RegisterTypes(registry, FindAttributedTypes());
            return registry;
        }

        internal static IReadOnlyList<(string name, Type type)> FindAttributedTypes() => AttributedTypes.Value;

        private static IReadOnlyList<(string name, Type type)> ScanAssemblies()
        {
            ScanCountForTesting++;
            Assembly runtimeAssembly = typeof(Command).Assembly;
            string runtimeName = runtimeAssembly.GetName().Name;
            var found = new List<(string name, Type type)>();

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly != runtimeAssembly && !ReferencesAssembly(assembly, runtimeName))
                    continue;

                foreach (Type type in LoadableTypes(assembly))
                {
                    var attribute = type.GetCustomAttribute<NovelCommandAttribute>();
                    if (attribute != null)
                        found.Add((attribute.Name, type));
                }
            }

            found.Sort((a, b) => string.CompareOrdinal(a.type.FullName, b.type.FullName));
            return found;
        }

        private static bool ReferencesAssembly(Assembly assembly, string referencedName)
        {
            foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
            {
                if (reference.Name == referencedName)
                    return true;
            }
            return false;
        }

        private static IEnumerable<Type> LoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return Array.FindAll(e.Types, t => t != null);
            }
        }

        internal static void RegisterTypes(CommandRegistry registry, IEnumerable<(string name, Type type)> candidates)
        {
            foreach (var (name, type) in candidates)
            {
                if (!typeof(Command).IsAssignableFrom(type))
                {
                    Debug.LogError($"NovelForge: [NovelCommand] type '{type.FullName}' does not derive from Command — skipped.");
                    continue;
                }
                if (type.IsAbstract)
                {
                    Debug.LogError($"NovelForge: [NovelCommand] type '{type.FullName}' is abstract — skipped.");
                    continue;
                }
                ConstructorInfo constructor = type.GetConstructor(new[] { typeof(string) });
                if (constructor == null)
                {
                    Debug.LogError($"NovelForge: [NovelCommand] type '{type.FullName}' needs a public constructor (string rawArgs) — skipped.");
                    continue;
                }
                if (registry.IsRegistered(name))
                {
                    Debug.LogError($"NovelForge: [NovelCommand] '{name}' on '{type.FullName}' is already registered — skipped.");
                    continue;
                }

                registry.Register(name, args =>
                {
                    try
                    {
                        return (Command)constructor.Invoke(new object[] { args });
                    }
                    catch (TargetInvocationException e) when (e.InnerException != null)
                    {
                        ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                        throw;
                    }
                });
            }
        }
    }
}
