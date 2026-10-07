using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using UnityEngine;

namespace NovelForge.Runtime
{
    public class CommandRegistry
    {
        internal static int ScanCountForTesting;
        internal static int ValidationCountForTesting;

        private static readonly string[] BuiltinNames = { "bg", "cg", "music", "sfx", "wait" };

        private static readonly HashSet<string> ReservedKeywords = new()
        {
            "label", "jump", "gosub", "set", "if", "return", "else", "endif", "choice",
        };

        private static readonly Regex CommandName = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

        // Validated once per domain so a broken [NovelCommand] logs once, not on every
        // CreateDefault() (the script editor window calls it on every keystroke).
        private static readonly Lazy<IReadOnlyList<(string name, Func<string, Command> factory)>> DiscoveredFactories =
            new(() => BuildFactories(ScanAssemblies(AppDomain.CurrentDomain.GetAssemblies()), BuiltinNames));

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
            foreach (var (name, factory) in DiscoveredFactories.Value)
                registry.Register(name, factory);
            return registry;
        }

        internal static void RegisterTypes(CommandRegistry registry, IEnumerable<(string name, Type type)> candidates)
        {
            foreach (var (name, factory) in BuildFactories(candidates, registry.RegisteredNames))
                registry.Register(name, factory);
        }

        private static List<(string name, Func<string, Command> factory)> BuildFactories(
            IEnumerable<(string name, Type type)> candidates, IEnumerable<string> takenNames)
        {
            ValidationCountForTesting++;
            var taken = new HashSet<string>(takenNames);
            var accepted = new List<(string name, Func<string, Command> factory)>();

            foreach (var (name, type) in candidates)
            {
                if (name == null || !CommandName.IsMatch(name))
                {
                    Debug.LogError($"NovelForge: [NovelCommand] name '{name}' on '{type.FullName}' is not a valid command name (letters, digits and '_', not starting with a digit) — skipped.");
                    continue;
                }
                if (ReservedKeywords.Contains(name))
                {
                    Debug.LogError($"NovelForge: [NovelCommand] '{name}' on '{type.FullName}' is a reserved DSL keyword — skipped.");
                    continue;
                }
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
                if (!taken.Add(name))
                {
                    Debug.LogError($"NovelForge: [NovelCommand] '{name}' on '{type.FullName}' is already registered — skipped.");
                    continue;
                }

                accepted.Add((name, args =>
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
                }));
            }

            return accepted;
        }

        internal static IReadOnlyList<(string name, Type type)> ScanAssemblies(IEnumerable<Assembly> assemblies)
        {
            ScanCountForTesting++;
            Assembly runtimeAssembly = typeof(Command).Assembly;
            string runtimeName = runtimeAssembly.GetName().Name;
            var found = new List<(string name, Type type)>();

            foreach (Assembly assembly in assemblies)
            {
                if (assembly.IsDynamic)
                    continue;

                try
                {
                    if (assembly != runtimeAssembly && !ReferencesAssembly(assembly, runtimeName))
                        continue;

                    foreach (Type type in LoadableTypes(assembly))
                    {
                        NovelCommandAttribute attribute;
                        try
                        {
                            attribute = type.GetCustomAttribute<NovelCommandAttribute>();
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"NovelForge: could not read attributes of '{type.FullName}' while looking for [NovelCommand] — {e.Message}");
                            continue;
                        }
                        if (attribute != null)
                            found.Add((attribute.Name, type));
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"NovelForge: could not scan assembly '{assembly.FullName}' for [NovelCommand] types — {e.Message}");
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
    }
}
