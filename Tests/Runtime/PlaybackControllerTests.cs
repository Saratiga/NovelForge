using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;

namespace NovelForge.Runtime.Tests
{
    public class PlaybackControllerTests
    {
        private class RecordingPointerCommand : Command
        {
            private readonly System.Action<IStoryPointer> _action;
            public RecordingPointerCommand(System.Action<IStoryPointer> action) => _action = action;

            public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
            {
                _action(pointer);
                yield break;
            }
        }

        [Test]
        public void RunAll_AutoIncrementsWhenCommandDoesNotMovePointer()
        {
            var calls = new List<int>();
            var commands = new Command[]
            {
                new RecordingPointerCommand(p => calls.Add(0)),
                new RecordingPointerCommand(p => calls.Add(1)),
                new RecordingPointerCommand(p => calls.Add(2)),
            };
            var script = new NovelScript(commands, new Dictionary<string, int>());
            var controller = new PlaybackController(script, new StoryContext());

            CoroutineTestUtil.RunToCompletion(controller.RunAll());

            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, calls);
            Assert.IsTrue(controller.IsFinished);
        }

        [Test]
        public void RunAll_DoesNotAutoIncrementWhenCommandMovesPointer()
        {
            var calls = new List<int>();
            var commands = new Command[]
            {
                new RecordingPointerCommand(p => { calls.Add(0); p.Current = 2; }),
                new RecordingPointerCommand(p => calls.Add(1)),
                new RecordingPointerCommand(p => calls.Add(2)),
            };
            var script = new NovelScript(commands, new Dictionary<string, int>());
            var controller = new PlaybackController(script, new StoryContext());

            CoroutineTestUtil.RunToCompletion(controller.RunAll());

            CollectionAssert.AreEqual(new[] { 0, 2 }, calls);
        }

        [Test]
        public void RunAll_SelfTargetingJump_ReExecutesUntilConditionChanges()
        {
            int iterations = 0;
            var commands = new Command[]
            {
                new RecordingPointerCommand(p =>
                {
                    iterations++;
                    if (iterations < 3)
                        p.Current = 0; // self-jump: same index, should NOT auto-advance
                }),
            };
            var script = new NovelScript(commands, new Dictionary<string, int>());
            var controller = new PlaybackController(script, new StoryContext());

            CoroutineTestUtil.RunToCompletion(controller.RunAll());

            Assert.AreEqual(3, iterations);
            Assert.IsTrue(controller.IsFinished);
        }

        [Test]
        public void PushAndTryPop_SupportGosubReturnStack()
        {
            var commands = new Command[]
            {
                new RecordingPointerCommand(p => { p.Push(2); p.Current = 1; }),
                new RecordingPointerCommand(p => { p.TryPop(out int ret); p.Current = ret; }),
                new RecordingPointerCommand(p => { }),
            };
            var script = new NovelScript(commands, new Dictionary<string, int>());
            var controller = new PlaybackController(script, new StoryContext());

            CoroutineTestUtil.RunToCompletion(controller.RunAll());

            Assert.IsTrue(controller.IsFinished);
        }
    }
}
