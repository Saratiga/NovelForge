using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace NovelForge.UI.Tests
{
    public class SmokeTests
    {
        [Test]
        public void UIAssemblyAndTextMeshProAreWiredCorrectly()
        {
            var go = new GameObject("SmokeTest");
            var text = go.AddComponent<TextMeshProUGUI>();

            Assert.IsNotNull(text);

            Object.DestroyImmediate(go);
        }
    }
}
