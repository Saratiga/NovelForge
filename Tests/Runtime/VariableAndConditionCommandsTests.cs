using NUnit.Framework;

namespace NovelForge.Runtime.Tests
{
    public class VariableAndConditionCommandsTests
    {
        [Test]
        public void SetVariable_Assign_SetsExactValue()
        {
            var context = new StoryContext();
            var pointer = new StoryPointer();
            var command = new SetVariableCommand("playerName", VariableOperator.Assign, "Kai");

            CoroutineTestUtil.RunToCompletion(command.Execute(context, pointer));

            Assert.AreEqual("Kai", context.Variables.GetString("playerName"));
        }

        [Test]
        public void SetVariable_Add_IncrementsExistingValue()
        {
            var context = new StoryContext();
            context.Variables.Set("relationship", 2);
            var pointer = new StoryPointer();
            var command = new SetVariableCommand("relationship", VariableOperator.Add, 1);

            CoroutineTestUtil.RunToCompletion(command.Execute(context, pointer));

            Assert.AreEqual(3, context.Variables.GetInt("relationship"));
        }

        [Test]
        public void SetVariable_Subtract_DecrementsExistingValue()
        {
            var context = new StoryContext();
            context.Variables.Set("relationship", 2);
            var pointer = new StoryPointer();
            var command = new SetVariableCommand("relationship", VariableOperator.Subtract, 1);

            CoroutineTestUtil.RunToCompletion(command.Execute(context, pointer));

            Assert.AreEqual(1, context.Variables.GetInt("relationship"));
        }

        [Test]
        public void ConditionalJump_NumericTrue_DoesNotJump()
        {
            var context = new StoryContext();
            context.Variables.Set("relationship", 5);
            var pointer = new StoryPointer { Current = 0 };
            var command = new ConditionalJumpCommand("relationship", ComparisonOperator.GreaterOrEqual, 3, falseTargetIndex: 10);

            CoroutineTestUtil.RunToCompletion(command.Execute(context, pointer));

            Assert.AreEqual(0, pointer.Current);
        }

        [Test]
        public void ConditionalJump_NumericFalse_JumpsToFalseTarget()
        {
            var context = new StoryContext();
            context.Variables.Set("relationship", 1);
            var pointer = new StoryPointer { Current = 0 };
            var command = new ConditionalJumpCommand("relationship", ComparisonOperator.GreaterOrEqual, 3, falseTargetIndex: 10);

            CoroutineTestUtil.RunToCompletion(command.Execute(context, pointer));

            Assert.AreEqual(10, pointer.Current);
        }

        [Test]
        public void ConditionalJump_StringEquality_Works()
        {
            var context = new StoryContext();
            context.Variables.Set("route", "good");
            var pointer = new StoryPointer { Current = 0 };
            var command = new ConditionalJumpCommand("route", ComparisonOperator.Equal, "good", falseTargetIndex: 10);

            CoroutineTestUtil.RunToCompletion(command.Execute(context, pointer));

            Assert.AreEqual(0, pointer.Current);
        }

        [Test]
        public void ConditionalJump_BoolEquality_Works()
        {
            var context = new StoryContext();
            context.Variables.Set("metAlice", true);
            var pointer = new StoryPointer { Current = 0 };
            var command = new ConditionalJumpCommand("metAlice", ComparisonOperator.Equal, false, falseTargetIndex: 10);

            CoroutineTestUtil.RunToCompletion(command.Execute(context, pointer));

            Assert.AreEqual(10, pointer.Current);
        }

        [Test]
        public void ConditionalJump_GreaterThanOnString_Throws()
        {
            var context = new StoryContext();
            context.Variables.Set("route", "good");
            var pointer = new StoryPointer { Current = 0 };
            var command = new ConditionalJumpCommand("route", ComparisonOperator.GreaterThan, "good", falseTargetIndex: 10);

            Assert.Throws<System.InvalidOperationException>(() =>
                CoroutineTestUtil.RunToCompletion(command.Execute(context, pointer)));
        }
    }
}
