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
            var command = new ChoiceCommand(new[] { "Good, thanks!", "Not great..." }, new[] { "opt_1", "opt_2" }, optionCount: 2);
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
            var command = new ChoiceCommand(new[] { "Only option" }, new[] { "opt_1" }, optionCount: 1);
            command.ResolveTarget(0, 10);
            var pointer = new StoryPointer();

            LogAssert.Expect(LogType.Error, "NovelForge: choice presenter returned invalid selection 99 — defaulting to option 0.");
            CoroutineTestUtil.RunToCompletion(command.Execute(context, pointer));

            Assert.AreEqual(10, pointer.Current);
        }

        [Test]
        public void Execute_NullChoicePresenter_LogsErrorAndDefaultsToOptionZero()
        {
            var context = new StoryContext();
            var command = new ChoiceCommand(new[] { "Only option" }, new[] { "opt_1" }, optionCount: 1);
            command.ResolveTarget(0, 10);
            var pointer = new StoryPointer();

            LogAssert.Expect(LogType.Error, "NovelForge: no IChoicePresenter wired — defaulting to option 0.");
            CoroutineTestUtil.RunToCompletion(command.Execute(context, pointer));

            Assert.AreEqual(10, pointer.Current);
        }

        [Test]
        public void Execute_WithLocalizationTableAndAllIdsMatching_PresentsTranslatedTexts()
        {
            var choices = new RecordingChoicePresenter { NextSelection = 0 };
            var localization = LocalizationTable.FromJson("{\"opt_1\": \"Хорошо, спасибо!\", \"opt_2\": \"Не очень...\"}");
            var context = new StoryContext { Choices = choices, Localization = localization };
            var command = new ChoiceCommand(new[] { "Good, thanks!", "Not great..." }, new[] { "opt_1", "opt_2" }, optionCount: 2);
            command.ResolveTarget(0, 10);
            command.ResolveTarget(1, 20);

            CoroutineTestUtil.RunToCompletion(command.Execute(context, new StoryPointer()));

            CollectionAssert.AreEqual(new[] { "Хорошо, спасибо!", "Не очень..." }, choices.Calls[0]);
        }

        [Test]
        public void Execute_WithLocalizationTableAndOnePartialMissingId_FallsBackForThatOptionOnlyAndLogsWarning()
        {
            var choices = new RecordingChoicePresenter { NextSelection = 0 };
            var localization = LocalizationTable.FromJson("{\"opt_1\": \"Хорошо, спасибо!\"}");
            var context = new StoryContext { Choices = choices, Localization = localization };
            var command = new ChoiceCommand(new[] { "Good, thanks!", "Not great..." }, new[] { "opt_1", "opt_2" }, optionCount: 2);
            command.ResolveTarget(0, 10);
            command.ResolveTarget(1, 20);

            LogAssert.Expect(LogType.Warning, "NovelForge: no translation for line id 'opt_2' — falling back to source text.");
            CoroutineTestUtil.RunToCompletion(command.Execute(context, new StoryPointer()));

            CollectionAssert.AreEqual(new[] { "Хорошо, спасибо!", "Not great..." }, choices.Calls[0]);
        }
    }
}
