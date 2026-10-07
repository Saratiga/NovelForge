using System;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NovelForge.Runtime.Tests
{
    public class CommandDiscoveryTests
    {
        public class EchoCommand : Command
        {
            public string Args { get; }

            public EchoCommand(string rawArgs) => Args = rawArgs;

            public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
            {
                yield break;
            }
        }

        public class NoStringCtorCommand : Command
        {
            public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
            {
                yield break;
            }
        }

        public abstract class AbstractCommand : Command
        {
            public AbstractCommand(string rawArgs)
            {
            }
        }

        public class NotACommand
        {
            public NotACommand(string rawArgs)
            {
            }
        }

        public class ThrowingCommand : Command
        {
            public ThrowingCommand(string rawArgs) => throw new FormatException("bad speed");

            public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
            {
                yield break;
            }
        }

        [Test]
        public void RegisterTypes_ValidType_CreatesInstanceWithRawArgs()
        {
            var registry = new CommandRegistry();

            CommandRegistry.RegisterTypes(registry, new[] { ("echo", typeof(EchoCommand)) });

            Assert.IsTrue(registry.TryCreate("echo", "hi there", out var command));
            Assert.AreEqual("hi there", ((EchoCommand)command).Args);
        }

        [Test]
        public void RegisterTypes_TypeWithoutStringCtor_LogsErrorAndSkips()
        {
            var registry = new CommandRegistry();
            LogAssert.Expect(LogType.Error, new Regex("NoStringCtorCommand.*constructor"));

            CommandRegistry.RegisterTypes(registry, new[] { ("nostr", typeof(NoStringCtorCommand)) });

            Assert.IsFalse(registry.IsRegistered("nostr"));
        }

        [Test]
        public void RegisterTypes_AbstractType_LogsErrorAndSkips()
        {
            var registry = new CommandRegistry();
            LogAssert.Expect(LogType.Error, new Regex("AbstractCommand.*abstract"));

            CommandRegistry.RegisterTypes(registry, new[] { ("abs", typeof(AbstractCommand)) });

            Assert.IsFalse(registry.IsRegistered("abs"));
        }

        [Test]
        public void RegisterTypes_NonCommandType_LogsErrorAndSkips()
        {
            var registry = new CommandRegistry();
            LogAssert.Expect(LogType.Error, new Regex("NotACommand.*does not derive from Command"));

            CommandRegistry.RegisterTypes(registry, new[] { ("notcmd", typeof(NotACommand)) });

            Assert.IsFalse(registry.IsRegistered("notcmd"));
        }

        [Test]
        public void RegisterTypes_NameAlreadyRegistered_LogsErrorAndKeepsFirst()
        {
            var registry = CommandRegistry.CreateDefault();
            LogAssert.Expect(LogType.Error, new Regex("'bg'.*already registered"));

            CommandRegistry.RegisterTypes(registry, new[] { ("bg", typeof(EchoCommand)) });

            registry.TryCreate("bg", "room", out var command);
            Assert.IsInstanceOf<ShowBackgroundCommand>(command);
        }

        [Test]
        public void Compile_CustomCtorThrows_ParseExceptionCarriesInnerMessage()
        {
            var registry = new CommandRegistry();
            CommandRegistry.RegisterTypes(registry, new[] { ("boom", typeof(ThrowingCommand)) });

            var exception = Assert.Throws<ParseException>(() => new ScriptCompiler(registry).Compile("label a\nboom x\n"));

            StringAssert.Contains("bad speed", exception.Message);
            Assert.AreEqual(2, exception.LineNumber);
        }

        [Test]
        public void CreateDefault_IncludesAttributedCommandFromLoadedAssembly()
        {
            Assert.IsTrue(CommandRegistry.CreateDefault().TryCreate("test_ping", "pong", out var command));
            Assert.AreEqual("pong", ((TestPingCommand)command).Args);
        }

        [Test]
        public void ScriptCompiler_Default_CompilesAttributedCommand()
        {
            var script = new ScriptCompiler().Compile("label a\ntest_ping hello\n");

            Assert.IsInstanceOf<TestPingCommand>(script.Commands[0]);
        }

        private class FakeAssembly : Assembly
        {
            private readonly Func<Type[]> _getTypes;

            public FakeAssembly(Func<Type[]> getTypes) => _getTypes = getTypes;

            public override string FullName => "FakeAssembly";

            public override bool IsDynamic => false;

            public override Type[] GetTypes() => _getTypes();

            public override AssemblyName[] GetReferencedAssemblies() => new[] { new AssemblyName("NovelForge.Runtime") };
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("two words")]
        [TestCase("9lives")]
        public void RegisterTypes_InvalidName_LogsErrorAndSkips(string name)
        {
            var registry = new CommandRegistry();
            LogAssert.Expect(LogType.Error, new Regex("not a valid command name"));

            CommandRegistry.RegisterTypes(registry, new[] { (name, typeof(EchoCommand)) });

            Assert.AreEqual(0, registry.RegisteredNames.Count);
        }

        [TestCase("label")]
        [TestCase("choice")]
        [TestCase("endif")]
        public void RegisterTypes_ReservedKeyword_LogsErrorAndSkips(string name)
        {
            var registry = new CommandRegistry();
            LogAssert.Expect(LogType.Error, new Regex($"'{name}'.*reserved DSL keyword"));

            CommandRegistry.RegisterTypes(registry, new[] { (name, typeof(EchoCommand)) });

            Assert.IsFalse(registry.IsRegistered(name));
        }

        [Test]
        public void ScanAssemblies_AssemblyThatThrows_IsSkippedWithWarning()
        {
            var broken = new FakeAssembly(() => throw new InvalidOperationException("boom"));
            LogAssert.Expect(LogType.Warning, new Regex("FakeAssembly.*boom"));

            var found = CommandRegistry.ScanAssemblies(new[] { broken, typeof(TestPingCommand).Assembly });

            CollectionAssert.Contains(found, ("test_ping", typeof(TestPingCommand)));
        }

        [Test]
        public void ScanAssemblies_PartialTypeLoad_UsesTypesThatLoaded()
        {
            var partial = new FakeAssembly(() => throw new ReflectionTypeLoadException(
                new[] { typeof(TestPingCommand), null }, new Exception[] { null, new TypeLoadException() }));

            var found = CommandRegistry.ScanAssemblies(new Assembly[] { partial });

            CollectionAssert.AreEqual(new[] { ("test_ping", typeof(TestPingCommand)) }, found);
        }

        [Test]
        public void CreateDefault_RepeatedCalls_ValidateOnce()
        {
            CommandRegistry.CreateDefault();
            int before = CommandRegistry.ValidationCountForTesting;

            for (int i = 0; i < 50; i++)
                CommandRegistry.CreateDefault();

            Assert.AreEqual(before, CommandRegistry.ValidationCountForTesting);
        }

        [Test]
        public void CreateDefault_RepeatedCalls_ScanAssembliesOnce()
        {
            CommandRegistry.CreateDefault();
            int before = CommandRegistry.ScanCountForTesting;

            for (int i = 0; i < 50; i++)
                CommandRegistry.CreateDefault();

            Assert.AreEqual(before, CommandRegistry.ScanCountForTesting);
            Assert.LessOrEqual(before, 1);
        }
    }
}
