using System.Collections.Generic;
using NUnit.Framework;
using NovelForge.Runtime.Tests;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace NovelForge.UI.Tests
{
    public class DialogueBoxViewTests
    {
        private readonly List<GameObject> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
                Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        private GameObject CreateTracked(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        private DialogueBoxView CreateView(FakeDeltaTimeSource time, FakeAdvanceInputSource input)
        {
            var go = CreateTracked("DialogueBoxView");
            var view = go.AddComponent<DialogueBoxView>();
            view.nameText = CreateTracked("Name").AddComponent<TextMeshProUGUI>();
            view.bodyText = CreateTracked("Body").AddComponent<TextMeshProUGUI>();
            view.secondsPerCharacter = 0.1f;
            view.TimeSource = time;
            view.AdvanceInput = input;
            return view;
        }

        [Test]
        public void ShowText_SetsNameAndColor()
        {
            var input = new FakeAdvanceInputSource { Pending = true };
            var view = CreateView(new FakeDeltaTimeSource { DeltaTime = 1f }, input);

            CoroutineTestUtil.RunToCompletion(view.ShowText("Alice", Color.red, "Hi"));

            Assert.AreEqual("Alice", view.nameText.text);
            Assert.AreEqual(Color.red, view.nameText.color);
        }

        [Test]
        public void ShowText_TypewriterCompletesAndWaitsForSeparateClick()
        {
            var input = new FakeAdvanceInputSource();
            var time = new FakeDeltaTimeSource { DeltaTime = 1f };
            var view = CreateView(time, input);

            var routine = view.ShowText("Alice", Color.white, "Hi");
            bool finishedBeforeClick = !routine.MoveNext();
            Assert.IsFalse(finishedBeforeClick);
            Assert.AreEqual("Hi", view.bodyText.text);

            input.Pending = true;
            bool finishedAfterClick = !routine.MoveNext();
            Assert.IsTrue(finishedAfterClick);
        }

        [Test]
        public void ShowText_PartwayThroughTyping_ShowsPartialText()
        {
            var input = new FakeAdvanceInputSource();
            var time = new FakeDeltaTimeSource { DeltaTime = 0.15f };
            var view = CreateView(time, input);
            view.secondsPerCharacter = 0.1f;

            var routine = view.ShowText("Alice", Color.white, "Hi");
            routine.MoveNext();

            Assert.AreEqual("H", view.bodyText.text);
        }

        [Test]
        public void ShowText_ClickDuringTyping_InstantlyCompletesText_ButDoesNotAdvance()
        {
            var input = new FakeAdvanceInputSource { Pending = true };
            var time = new FakeDeltaTimeSource { DeltaTime = 0.05f };
            var view = CreateView(time, input);
            view.secondsPerCharacter = 0.1f;

            var routine = view.ShowText("Alice", Color.white, "Hi");
            bool finished = !routine.MoveNext();

            Assert.AreEqual("Hi", view.bodyText.text);
            Assert.IsFalse(finished);
        }

        [Test]
        public void ShowText_MissingTextFields_LogsErrorAndDoesNotThrow()
        {
            var go = CreateTracked("DialogueBoxView");
            var view = go.AddComponent<DialogueBoxView>();
            view.TimeSource = new FakeDeltaTimeSource { DeltaTime = 1f };
            view.AdvanceInput = new FakeAdvanceInputSource { Pending = true };

            LogAssert.Expect(LogType.Error, "NovelForge: DialogueBoxView is missing nameText/bodyText — skipping.");
            Assert.DoesNotThrow(() => CoroutineTestUtil.RunToCompletion(view.ShowText("Alice", Color.white, "Hi")));
        }

        [Test]
        public void ShowText_MissingAdvanceInput_LogsErrorAndShowsFullTextWithoutWaiting()
        {
            var go = CreateTracked("DialogueBoxView");
            var view = go.AddComponent<DialogueBoxView>();
            view.nameText = CreateTracked("Name").AddComponent<TextMeshProUGUI>();
            view.bodyText = CreateTracked("Body").AddComponent<TextMeshProUGUI>();
            view.TimeSource = new FakeDeltaTimeSource { DeltaTime = 1f };

            LogAssert.Expect(LogType.Error, "NovelForge: DialogueBoxView has no advance input wired — showing full text without waiting.");
            Assert.DoesNotThrow(() => CoroutineTestUtil.RunToCompletion(view.ShowText("Alice", Color.white, "Hi")));
            Assert.AreEqual("Hi", view.bodyText.text);
        }

        [Test]
        public void ShowText_ZeroSecondsPerCharacter_ShowsFullTextImmediately()
        {
            var input = new FakeAdvanceInputSource { Pending = true };
            var view = CreateView(new FakeDeltaTimeSource { DeltaTime = 1f }, input);
            view.secondsPerCharacter = 0f;

            CoroutineTestUtil.RunToCompletion(view.ShowText("Alice", Color.white, "Hi"));

            Assert.AreEqual("Hi", view.bodyText.text);
        }
    }
}
