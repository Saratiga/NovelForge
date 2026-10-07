using System;

namespace NovelForge.Runtime
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class NovelCommandAttribute : Attribute
    {
        public NovelCommandAttribute(string name) => Name = name;

        public string Name { get; }
    }
}
