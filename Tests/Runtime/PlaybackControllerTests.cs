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

        [Test]
        public void CreateSnapshot_CapturesPointerAndCallStackTopFirst()
        {
            var commands = new Command[]
            {
                new RecordingPointerCommand(p => { p.Push(10); p.Push(20); }),
            };
            var script = new NovelScript(commands, new Dictionary<string, int>());
            var controller = new PlaybackController(script, new StoryContext());

            CoroutineTestUtil.RunToCompletion(controller.RunAll());
            var snapshot = controller.CreateSnapshot();

            Assert.AreEqual(1, snapshot.PointerIndex);
            CollectionAssert.AreEqual(new[] { 20, 10 }, snapshot.CallStack);
        }

        [Test]
        public void RestoreSnapshot_ThenContinuing_PopsCallStackInOriginalOrder()
        {
            var popped = new List<int>();
            var commands = new Command[]
            {
                new RecordingPointerCommand(p => { }),
                new RecordingPointerCommand(p =>
                {
                    p.TryPop(out int a);
                    popped.Add(a);
                    p.TryPop(out int b);
                    popped.Add(b);
                }),
            };
            var script = new NovelScript(commands, new Dictionary<string, int>());
            var controller = new PlaybackController(script, new StoryContext());
            var snapshot = new PlaybackSnapshot { PointerIndex = 1, CallStack = new[] { 20, 10 } };

            controller.RestoreSnapshot(snapshot);
            CoroutineTestUtil.RunToCompletion(controller.RunAll());

            CollectionAssert.AreEqual(new[] { 20, 10 }, popped);
            Assert.IsTrue(controller.IsFinished);
        }

        [Test]
        public void CreateSnapshot_ThenRestoreSnapshotOnFreshController_ReproducesSameContinuation()
        {
            // Full round trip: run only the "push" step of a script (stopping there by
            // manually draining one command via the same nested-IEnumerator recursion
            // CoroutineTestUtil itself uses — RunAll's `yield return StepOnce()` composition
            // does not auto-drive its own nested enumerator, so a bare MoveNext() alone
            // would only hand back StepOnce()'s enumerator without running it), snapshot
            // it, feed that snapshot into a brand-new controller for the same script, and
            // confirm continuing from there pops the call stack in the original order.
            var popped = new List<int>();
            var commands = new Command[]
            {
                new RecordingPointerCommand(p => { p.Push(10); p.Push(20); }),
                new RecordingPointerCommand(p =>
                {
                    p.TryPop(out int a);
                    popped.Add(a);
                    p.TryPop(out int b);
                    popped.Add(b);
                }),
            };
            var script = new NovelScript(commands, new Dictionary<string, int>());
            var firstController = new PlaybackController(script, new StoryContext());

            IEnumerator routine = firstController.RunAll();
            routine.MoveNext();
            if (routine.Current is IEnumerator nested)
                CoroutineTestUtil.RunToCompletion(nested);

            var snapshot = firstController.CreateSnapshot();
            Assert.AreEqual(1, snapshot.PointerIndex);

            var secondController = new PlaybackController(script, new StoryContext());
            secondController.RestoreSnapshot(snapshot);
            CoroutineTestUtil.RunToCompletion(secondController.RunAll());

            CollectionAssert.AreEqual(new[] { 20, 10 }, popped);
        }

        [Test]
        public void RestoreSnapshot_WithNullCallStack_ClearsStackAndDoesNotThrow()
        {
            var commands = new Command[]
            {
                new RecordingPointerCommand(p => { }),
            };
            var script = new NovelScript(commands, new Dictionary<string, int>());
            var controller = new PlaybackController(script, new StoryContext());
            var snapshot = new PlaybackSnapshot { PointerIndex = 0, CallStack = null };

            Assert.DoesNotThrow(() => controller.RestoreSnapshot(snapshot));
            CoroutineTestUtil.RunToCompletion(controller.RunAll());

            Assert.IsTrue(controller.IsFinished);
        }
    }
}
