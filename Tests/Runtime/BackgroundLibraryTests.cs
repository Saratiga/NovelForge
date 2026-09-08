using NUnit.Framework;
using UnityEngine;

namespace NovelForge.Runtime.Tests
{
    public class BackgroundLibraryTests
    {
        private static Sprite CreateSprite(string name)
        {
            var texture = new Texture2D(4, 4);
            return Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.zero);
        }

        [Test]
        public void TryGetBackgroundSprite_ReturnsTrueAndSprite_WhenIdRegistered()
        {
            var sprite = CreateSprite("park_day");
            var library = ScriptableObject.CreateInstance<BackgroundLibrary>();
            library.backgrounds = new[] { new BackgroundLibrary.Entry { id = "park_day", sprite = sprite } };

            bool found = library.TryGetBackgroundSprite("park_day", out var result);

            Assert.IsTrue(found);
            Assert.AreEqual(sprite, result);
        }

        [Test]
        public void TryGetBackgroundSprite_ReturnsFalse_WhenIdMissing()
        {
            var library = ScriptableObject.CreateInstance<BackgroundLibrary>();

            bool found = library.TryGetBackgroundSprite("park_day", out var result);

            Assert.IsFalse(found);
            Assert.IsNull(result);
        }

        [Test]
        public void TryGetCgSprite_ReturnsTrueAndSprite_WhenIdRegistered()
        {
            var sprite = CreateSprite("intro_cg");
            var library = ScriptableObject.CreateInstance<BackgroundLibrary>();
            library.cgs = new[] { new BackgroundLibrary.Entry { id = "intro_cg", sprite = sprite } };

            bool found = library.TryGetCgSprite("intro_cg", out var result);

            Assert.IsTrue(found);
            Assert.AreEqual(sprite, result);
        }

        [Test]
        public void TryGetCgSprite_ReturnsFalse_WhenIdMissing()
        {
            var library = ScriptableObject.CreateInstance<BackgroundLibrary>();

            bool found = library.TryGetCgSprite("intro_cg", out var result);

            Assert.IsFalse(found);
            Assert.IsNull(result);
        }

        [Test]
        public void BackgroundAndCg_UseIndependentIdSpaces()
        {
            var bgSprite = CreateSprite("shared_id_bg");
            var cgSprite = CreateSprite("shared_id_cg");
            var library = ScriptableObject.CreateInstance<BackgroundLibrary>();
            library.backgrounds = new[] { new BackgroundLibrary.Entry { id = "shared", sprite = bgSprite } };
            library.cgs = new[] { new BackgroundLibrary.Entry { id = "shared", sprite = cgSprite } };

            library.TryGetBackgroundSprite("shared", out var bgResult);
            library.TryGetCgSprite("shared", out var cgResult);

            Assert.AreEqual(bgSprite, bgResult);
            Assert.AreEqual(cgSprite, cgResult);
        }
    }
}
