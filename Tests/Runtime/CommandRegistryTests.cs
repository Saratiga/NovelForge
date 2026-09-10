using NUnit.Framework;

namespace NovelForge.Runtime.Tests
{
    public class CommandRegistryTests
    {
        [Test]
        public void CreateDefault_RegisteredNames_ContainsAllBuiltinCommands()
        {
            var registry = CommandRegistry.CreateDefault();

            CollectionAssert.AreEquivalent(new[] { "bg", "cg", "music", "sfx", "wait" }, registry.RegisteredNames);
        }

        [Test]
        public void RegisteredNames_ReflectsCustomRegistrations()
        {
            var registry = new CommandRegistry();
            registry.Register("custom", args => new ShowBackgroundCommand(args));

            CollectionAssert.AreEquivalent(new[] { "custom" }, registry.RegisteredNames);
        }
    }
}
