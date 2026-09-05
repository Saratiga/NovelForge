using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NovelForge.Runtime.Tests
{
    public class ChoiceCommandTests
    {
        [Test]
        public void Execute_PresentsOptionTextsAndJumpsToSelectedTarget()
        {
            var choices = new RecordingChoicePresenter { NextSelection = 1 };
            var context = new StoryContext { Choices = choices };
            var command = new ChoiceCommand(new[] { "Good, thanks!", "Not great..." }, optionCount: 2);
            command.ResolveTarget(0, 10);
            command.ResolveTarget(1, 20);
            var pointer = new StoryPointer();

            CoroutineTestUtil.RunToCompletion(command.Execute(context, pointer));

            CollectionAssert.AreEqual(new[] { "Good, thanks!", "Not great..." }, choices.Calls[0]);
            Assert.AreEqual(20, pointer.Current);
        }

        [Test]
        public void Execute_InvalidSelection_LogsErrorAndDefaultsToOptionZero()
        {
            var choices = new RecordingChoicePresenter { NextSelection = 99 };
            var context = new StoryContext { Choices = choices };
            var command = new ChoiceCommand(new[] { "Only option" }, optionCount: 1);
            command.ResolveTarget(0, 10);
            var pointer = new StoryPointer();

            LogAssert.Expect(LogType.Error, "NovelForge: choice presenter returned invalid selection 99 — defaulting to option 0.");
            CoroutineTestUtil.RunToCompletion(command.Execute(context, pointer));

            Assert.AreEqual(10, pointer.Current);
        }
    }
}
