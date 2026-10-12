using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NovelForge.Runtime.Tests
{
    public class ResetViewTests
    {
        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _spawned)
                Object.DestroyImmediate(obj);
            _spawned.Clear();
        }

        private GameObject CreateGameObject(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        private static Sprite CreateSprite() => Sprite.Create(new Texture2D(4, 4), new Rect(0, 0, 4, 4), Vector2.zero);

        private static void AssertHidden(SpriteRenderer renderer)
        {
            Assert.IsNull(renderer.sprite);
            Assert.AreEqual(0f, renderer.color.a, 0.001f);
        }

        [Test]
        public void ActorView_ResetView_HidesSprite()
        {
            var go = CreateGameObject("ActorView");
            var view = go.AddComponent<ActorView>();
            view.spriteRenderer = go.AddComponent<SpriteRenderer>();
            view.fadeSeconds = 0.1f;
            view.TimeSource = new FakeDeltaTimeSource { DeltaTime = 1f };
            CoroutineTestUtil.RunToCompletion(view.ShowSprite(CreateSprite()));

            view.ResetView();

            AssertHidden(view.spriteRenderer);
        }

        [Test]
        public void BackgroundPresenter_ResetView_ClearsBackgroundAndCg()
        {
            var library = ScriptableObject.CreateInstance<BackgroundLibrary>();
            _spawned.Add(library);
            library.backgrounds = new[] { new BackgroundLibrary.Entry { id = "room", sprite = CreateSprite() } };
            library.cgs = new[] { new BackgroundLibrary.Entry { id = "sunset", sprite = CreateSprite() } };
            var presenter = CreateGameObject("BackgroundPresenter").AddComponent<BackgroundPresenter>();
            presenter.library = library;
            presenter.backgroundSlotA = CreateGameObject("SlotA").AddComponent<SpriteRenderer>();
            presenter.backgroundSlotB = CreateGameObject("SlotB").AddComponent<SpriteRenderer>();
            presenter.cgSlot = CreateGameObject("CgSlot").AddComponent<SpriteRenderer>();
            presenter.backgroundFadeSeconds = 0.1f;
            presenter.cgFadeSeconds = 0.1f;
            presenter.TimeSource = new FakeDeltaTimeSource { DeltaTime = 1f };
            CoroutineTestUtil.RunToCompletion(presenter.ShowBackground("room"));
            CoroutineTestUtil.RunToCompletion(presenter.ShowCg("sunset"));

            presenter.ResetView();

            AssertHidden(presenter.backgroundSlotA);
            AssertHidden(presenter.backgroundSlotB);
            AssertHidden(presenter.cgSlot);
        }

        [Test]
        public void BackgroundPresenter_ResetView_NextBackgroundStartsFromSlotBAgain()
        {
            var library = ScriptableObject.CreateInstance<BackgroundLibrary>();
            _spawned.Add(library);
            library.backgrounds = new[] { new BackgroundLibrary.Entry { id = "room", sprite = CreateSprite() } };
            var presenter = CreateGameObject("BackgroundPresenter").AddComponent<BackgroundPresenter>();
            presenter.library = library;
            presenter.backgroundSlotA = CreateGameObject("SlotA").AddComponent<SpriteRenderer>();
            presenter.backgroundSlotB = CreateGameObject("SlotB").AddComponent<SpriteRenderer>();
            presenter.backgroundFadeSeconds = 0.1f;
            presenter.TimeSource = new FakeDeltaTimeSource { DeltaTime = 1f };
            CoroutineTestUtil.RunToCompletion(presenter.ShowBackground("room"));
            CoroutineTestUtil.RunToCompletion(presenter.ShowBackground("room"));

            presenter.ResetView();
            CoroutineTestUtil.RunToCompletion(presenter.ShowBackground("room"));

            Assert.IsNotNull(presenter.backgroundSlotB.sprite);
            AssertHidden(presenter.backgroundSlotA);
        }

        [Test]
        public void AudioPresenter_ResetView_StopsMusicAndNextTrackStartsFromSourceBAgain()
        {
            var clip = AudioClip.Create("theme", 1000, 1, 44100, false);
            _spawned.Add(clip);
            var library = ScriptableObject.CreateInstance<AudioLibrary>();
            _spawned.Add(library);
            library.musicTracks = new[] { new AudioLibrary.Entry { id = "theme", clip = clip } };
            var go = CreateGameObject("AudioPresenter");
            var presenter = go.AddComponent<AudioPresenter>();
            presenter.library = library;
            presenter.musicSourceA = go.AddComponent<AudioSource>();
            presenter.musicSourceB = go.AddComponent<AudioSource>();
            presenter.musicCrossfadeSeconds = 0.1f;
            presenter.TimeSource = new FakeDeltaTimeSource { DeltaTime = 1f };
            CoroutineTestUtil.RunToCompletion(presenter.PlayMusic("theme"));
            CoroutineTestUtil.RunToCompletion(presenter.PlayMusic("theme"));

            presenter.ResetView();

            Assert.IsNull(presenter.musicSourceA.clip);
            Assert.IsNull(presenter.musicSourceB.clip);

            CoroutineTestUtil.RunToCompletion(presenter.PlayMusic("theme"));

            Assert.AreEqual(clip, presenter.musicSourceB.clip);
            Assert.IsNull(presenter.musicSourceA.clip);
        }
    }
}
