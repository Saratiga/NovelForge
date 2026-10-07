using System;
using System.Collections;
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
    }
}
