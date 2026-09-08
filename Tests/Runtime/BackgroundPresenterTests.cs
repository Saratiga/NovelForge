using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NovelForge.Runtime.Tests
{
    public class BackgroundPresenterTests
    {
        private readonly List<GameObject> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
                UnityEngine.Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        private BackgroundPresenter CreatePresenter(BackgroundLibrary library, FakeDeltaTimeSource time)
        {
            var go = new GameObject("BackgroundPresenter");
            _spawned.Add(go);
            var presenter = go.AddComponent<BackgroundPresenter>();
            presenter.library = library;
            presenter.backgroundSlotA = CreateSlot("SlotA");
            presenter.backgroundSlotB = CreateSlot("SlotB");
            presenter.cgSlot = CreateSlot("CgSlot");
            presenter.backgroundFadeSeconds = 0.1f;
            presenter.cgFadeSeconds = 0.1f;
            presenter.TimeSource = time;
            return presenter;
        }

        private SpriteRenderer CreateSlot(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go.AddComponent<SpriteRenderer>();
        }

        private static Sprite CreateSprite(string name)
        {
            var texture = new Texture2D(4, 4);
            return Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.zero);
        }

        private static BackgroundLibrary CreateLibrary()
        {
            var library = ScriptableObject.CreateInstance<BackgroundLibrary>();
            library.backgrounds = Array.Empty<BackgroundLibrary.Entry>();
            library.cgs = Array.Empty<BackgroundLibrary.Entry>();
            return library;
        }

        [Test]
        public void ShowBackground_FirstCall_FadesFromSilenceToSlotB()
        {
            var sprite = CreateSprite("park_day");
            var library = CreateLibrary();
            library.backgrounds = new[] { new BackgroundLibrary.Entry { id = "park_day", sprite = sprite } };
            var presenter = CreatePresenter(library, new FakeDeltaTimeSource { DeltaTime = 1f });

            CoroutineTestUtil.RunToCompletion(presenter.ShowBackground("park_day"));

            Assert.AreEqual(sprite, presenter.backgroundSlotB.sprite);
            Assert.AreEqual(1f, presenter.backgroundSlotB.color.a, 0.001f);
            Assert.AreEqual(0f, presenter.backgroundSlotA.color.a, 0.001f);
        }

        [Test]
        public void ShowBackground_SecondCall_FadesBackToSlotA()
        {
            var library = CreateLibrary();
            var spriteA = CreateSprite("park_day");
            var spriteB = CreateSprite("park_night");
            library.backgrounds = new[]
            {
                new BackgroundLibrary.Entry { id = "park_day", sprite = spriteA },
                new BackgroundLibrary.Entry { id = "park_night", sprite = spriteB },
            };
            var presenter = CreatePresenter(library, new FakeDeltaTimeSource { DeltaTime = 1f });

            CoroutineTestUtil.RunToCompletion(presenter.ShowBackground("park_day"));
            CoroutineTestUtil.RunToCompletion(presenter.ShowBackground("park_night"));

            Assert.AreEqual(spriteB, presenter.backgroundSlotA.sprite);
            Assert.AreEqual(1f, presenter.backgroundSlotA.color.a, 0.001f);
            Assert.AreEqual(0f, presenter.backgroundSlotB.color.a, 0.001f);
        }

        [Test]
        public void ShowBackground_UnknownId_LogsErrorAndDoesNotThrow()
        {
            var presenter = CreatePresenter(CreateLibrary(), new FakeDeltaTimeSource { DeltaTime = 1f });

            LogAssert.Expect(LogType.Error, "NovelForge: no background sprite registered for id 'missing' — skipping.");
            Assert.DoesNotThrow(() => CoroutineTestUtil.RunToCompletion(presenter.ShowBackground("missing")));
        }

        [Test]
        public void ShowBackground_MissingSlots_LogsErrorAndDoesNotThrow()
        {
            var sprite = CreateSprite("park_day");
            var library = CreateLibrary();
            library.backgrounds = new[] { new BackgroundLibrary.Entry { id = "park_day", sprite = sprite } };
            var presenter = CreatePresenter(library, new FakeDeltaTimeSource { DeltaTime = 1f });
            presenter.backgroundSlotB = null;

            LogAssert.Expect(LogType.Error, "NovelForge: BackgroundPresenter is missing backgroundSlotA/backgroundSlotB — skipping background change.");
            Assert.DoesNotThrow(() => CoroutineTestUtil.RunToCompletion(presenter.ShowBackground("park_day")));
        }
    }
}
