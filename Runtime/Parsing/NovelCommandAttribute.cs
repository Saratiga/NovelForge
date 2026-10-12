using System;

namespace NovelForge.Runtime
{
    /// <summary>
    /// Registers a <see cref="Command"/> subclass as a DSL command under <paramref name="name"/>.
    /// The class must not be abstract or generic and needs a public constructor taking the raw
    /// argument string. Command classes are discovered once per domain load; assemblies loaded
    /// later (for example with Assembly.Load at runtime) are not scanned.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class NovelCommandAttribute : Attribute
    {
        public NovelCommandAttribute(string name) => Name = name;

        public string Name { get; }
    }
}
