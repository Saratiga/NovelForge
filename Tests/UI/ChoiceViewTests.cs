using System.Collections.Generic;
using NUnit.Framework;
using NovelForge.Runtime.Tests;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NovelForge.UI.Tests
{
    public class ChoiceViewTests
    {
        private readonly List<GameObject> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
                Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        private Button CreateButton(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            var button = go.AddComponent<Button>();
            var labelGo = new GameObject("Label");
            _spawned.Add(labelGo);
            labelGo.transform.SetParent(go.transform);
            labelGo.AddComponent<TextMeshProUGUI>();
            return button;
        }

        private ChoiceView CreateView(int buttonCount)
        {
            var go = new GameObject("ChoiceView");
            _spawned.Add(go);
            var view = go.AddComponent<ChoiceView>();
            view.optionButtons = new Button[buttonCount];
            for (int i = 0; i < buttonCount; i++)
                view.optionButtons[i] = CreateButton($"Button{i}");
            return view;
        }

        [Test]
        public void PresentChoices_ShowsButtonsWithLabels_HidesUnused()
        {
            var view = CreateView(3);

            var routine = view.PresentChoices(new[] { "Yes", "No" }, _ => { });
            routine.MoveNext();

            Assert.IsTrue(view.optionButtons[0].gameObject.activeSelf);
            Assert.IsTrue(view.optionButtons[1].gameObject.activeSelf);
            Assert.IsFalse(view.optionButtons[2].gameObject.activeSelf);
            Assert.AreEqual("Yes", view.optionButtons[0].GetComponentInChildren<TMP_Text>().text);
            Assert.AreEqual("No", view.optionButtons[1].GetComponentInChildren<TMP_Text>().text);
        }

        [Test]
        public void PresentChoices_ClickOnButton_CallsOnSelectedWithCorrectIndex_AndHidesAllButtons()
        {
            var view = CreateView(2);
            int? selected = null;

            var routine = view.PresentChoices(new[] { "Yes", "No" }, i => selected = i);
            routine.MoveNext();
            view.optionButtons[1].onClick.Invoke();
            routine.MoveNext();

            Assert.AreEqual(1, selected);
            Assert.IsFalse(view.optionButtons[0].gameObject.activeSelf);
            Assert.IsFalse(view.optionButtons[1].gameObject.activeSelf);
        }

        [Test]
        public void PresentChoices_MoreOptionsThanButtons_LogsErrorAndTruncates()
        {
            var view = CreateView(1);

            LogAssert.Expect(LogType.Error, "NovelForge: ChoiceView has 1 button(s) but 2 option(s) were requested — truncating.");
            var routine = view.PresentChoices(new[] { "Yes", "No" }, _ => { });
            routine.MoveNext();

            Assert.IsTrue(view.optionButtons[0].gameObject.activeSelf);
        }

        [Test]
        public void PresentChoices_NoButtonsWired_LogsErrorAndDoesNotThrow()
        {
            var go = new GameObject("ChoiceView");
            _spawned.Add(go);
            var view = go.AddComponent<ChoiceView>();

            LogAssert.Expect(LogType.Error, "NovelForge: ChoiceView has no optionButtons wired — skipping choice.");
            Assert.DoesNotThrow(() => CoroutineTestUtil.RunToCompletion(view.PresentChoices(new[] { "Yes" }, _ => { })));
        }

        [Test]
        public void PresentChoices_CalledAgainAfterFirstSelection_ShowsNewTextNotStaleText()
        {
            var view = CreateView(2);
            int? firstSelected = null;

            var firstRoutine = view.PresentChoices(new[] { "Yes", "No" }, i => firstSelected = i);
            firstRoutine.MoveNext();
            view.optionButtons[0].onClick.Invoke();
            firstRoutine.MoveNext();

            Assert.AreEqual(0, firstSelected);

            int? secondSelected = null;
            var secondRoutine = view.PresentChoices(new[] { "Maybe", "Never" }, i => secondSelected = i);
            secondRoutine.MoveNext();

            Assert.AreEqual("Maybe", view.optionButtons[0].GetComponentInChildren<TMP_Text>().text);
            Assert.AreEqual("Never", view.optionButtons[1].GetComponentInChildren<TMP_Text>().text);

            view.optionButtons[1].onClick.Invoke();
            secondRoutine.MoveNext();

            Assert.AreEqual(1, secondSelected);
        }

        [Test]
        public void PresentChoices_ButtonWithNoLabelChild_LogsErrorAndDoesNotThrow()
        {
            var go = new GameObject("ChoiceView");
            _spawned.Add(go);
            var view = go.AddComponent<ChoiceView>();
            var buttonGo = new GameObject("ButtonWithoutLabel");
            _spawned.Add(buttonGo);
            view.optionButtons = new[] { buttonGo.AddComponent<Button>() };

            LogAssert.Expect(LogType.Error, "NovelForge: ChoiceView button 0 has no TMP_Text label child — showing blank button.");
            Assert.DoesNotThrow(() =>
            {
                var routine = view.PresentChoices(new[] { "Yes" }, _ => { });
                routine.MoveNext();
            });
        }
    }
}
