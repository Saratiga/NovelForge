using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NovelForge.Runtime.Tests
{
    public class ControlFlowCommandsTests
    {
        [Test]
        public void Jump_MovesPointerToTargetIndex()
        {
            var pointer = new StoryPointer { Current = 0 };
            var command = new JumpCommand(5);

            CoroutineTestUtil.RunToCompletion(command.Execute(new StoryContext(), pointer));

            Assert.AreEqual(5, pointer.Current);
        }

        [Test]
        public void Gosub_PushesReturnIndexAndJumps()
        {
            var pointer = new StoryPointer { Current = 3 };
            var command = new GosubCommand(10);

            CoroutineTestUtil.RunToCompletion(command.Execute(new StoryContext(), pointer));

            Assert.AreEqual(10, pointer.Current);
            Assert.IsTrue(pointer.TryPop(out int returnIndex));
            Assert.AreEqual(4, returnIndex);
        }

        [Test]
        public void Return_PopsStackAndJumpsBack()
        {
            var pointer = new StoryPointer();
            pointer.Push(7);
            var command = new ReturnCommand();

            CoroutineTestUtil.RunToCompletion(command.Execute(new StoryContext(), pointer));

            Assert.AreEqual(7, pointer.Current);
        }

        [Test]
        public void Return_WithEmptyStack_LogsErrorAndEndsPlayback()
        {
            var pointer = new StoryPointer();
            var command = new ReturnCommand();

            LogAssert.Expect(LogType.Error, "NovelForge: 'return' with an empty call stack — ending playback.");
            CoroutineTestUtil.RunToCompletion(command.Execute(new StoryContext(), pointer));

            Assert.GreaterOrEqual(pointer.Current, int.MaxValue);
        }
    }
}
