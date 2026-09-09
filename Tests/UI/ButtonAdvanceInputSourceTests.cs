using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace NovelForge.UI.Tests
{
    public class ButtonAdvanceInputSourceTests
    {
        private readonly List<GameObject> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
                Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        private Button CreateButton()
        {
            var go = new GameObject("Button");
            _spawned.Add(go);
            return go.AddComponent<Button>();
        }

        [Test]
        public void ConsumeAdvanceRequest_ReturnsFalse_BeforeAnyClick()
        {
            var source = new ButtonAdvanceInputSource(CreateButton());

            Assert.IsFalse(source.ConsumeAdvanceRequest());
        }

        [Test]
        public void ConsumeAdvanceRequest_ReturnsTrueOnce_AfterClick()
        {
            var button = CreateButton();
            var source = new ButtonAdvanceInputSource(button);

            button.onClick.Invoke();

            Assert.IsTrue(source.ConsumeAdvanceRequest());
            Assert.IsFalse(source.ConsumeAdvanceRequest());
        }
    }
}
