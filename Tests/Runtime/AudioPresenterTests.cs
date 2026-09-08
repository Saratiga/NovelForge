using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NovelForge.Runtime.Tests
{
    public class AudioPresenterTests
    {
        private readonly List<GameObject> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
                UnityEngine.Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        private AudioPresenter CreatePresenter(AudioLibrary library, FakeDeltaTimeSource time)
        {
            var go = new GameObject("AudioPresenter");
            _spawned.Add(go);
            var presenter = go.AddComponent<AudioPresenter>();
            presenter.library = library;
            presenter.musicSourceA = go.AddComponent<AudioSource>();
            presenter.musicSourceB = go.AddComponent<AudioSource>();
            presenter.sfxSource = go.AddComponent<AudioSource>();
            presenter.musicCrossfadeSeconds = 0.1f;
            presenter.TimeSource = time;
            return presenter;
        }

        private static AudioClip CreateClip(string name) => AudioClip.Create(name, 1000, 1, 44100, false);

        private static AudioLibrary CreateLibrary()
        {
            var library = ScriptableObject.CreateInstance<AudioLibrary>();
            library.musicTracks = Array.Empty<AudioLibrary.Entry>();
            library.sfxClips = Array.Empty<AudioLibrary.Entry>();
            return library;
        }

        [Test]
        public void PlayMusic_FirstCall_CrossfadesFromSilenceToTrackB()
        {
            var clip = CreateClip("theme");
            var library = CreateLibrary();
            library.musicTracks = new[] { new AudioLibrary.Entry { id = "theme_calm", clip = clip } };
            var presenter = CreatePresenter(library, new FakeDeltaTimeSource { DeltaTime = 1f });

            CoroutineTestUtil.RunToCompletion(presenter.PlayMusic("theme_calm"));

            Assert.AreEqual(clip, presenter.musicSourceB.clip);
            Assert.AreEqual(1f, presenter.musicSourceB.volume);
            Assert.AreEqual(1f, presenter.musicSourceA.volume);
            Assert.IsFalse(presenter.musicSourceA.isPlaying);
        }

        [Test]
        public void PlayMusic_SecondCall_CrossfadesBackToSourceA()
        {
            var library = CreateLibrary();
            var clipA = CreateClip("track_a");
            var clipB = CreateClip("track_b");
            library.musicTracks = new[]
            {
                new AudioLibrary.Entry { id = "track_a", clip = clipA },
                new AudioLibrary.Entry { id = "track_b", clip = clipB },
            };
            var presenter = CreatePresenter(library, new FakeDeltaTimeSource { DeltaTime = 1f });

            CoroutineTestUtil.RunToCompletion(presenter.PlayMusic("track_a"));
            CoroutineTestUtil.RunToCompletion(presenter.PlayMusic("track_b"));

            Assert.AreEqual(clipB, presenter.musicSourceA.clip);
            Assert.AreEqual(1f, presenter.musicSourceA.volume);
            Assert.AreEqual(1f, presenter.musicSourceB.volume);
            Assert.IsFalse(presenter.musicSourceB.isPlaying);
        }

        [Test]
        public void PlayMusic_UnknownTrackId_LogsErrorAndDoesNotThrow()
        {
            var presenter = CreatePresenter(CreateLibrary(), new FakeDeltaTimeSource { DeltaTime = 1f });

            LogAssert.Expect(LogType.Error, "NovelForge: no music clip registered for id 'missing' — skipping.");
            Assert.DoesNotThrow(() => CoroutineTestUtil.RunToCompletion(presenter.PlayMusic("missing")));
        }

        [Test]
        public void PlayMusic_MissingMusicSources_LogsErrorAndDoesNotThrow()
        {
            var library = CreateLibrary();
            library.musicTracks = new[] { new AudioLibrary.Entry { id = "theme_calm", clip = CreateClip("theme") } };
            var presenter = CreatePresenter(library, new FakeDeltaTimeSource { DeltaTime = 1f });
            presenter.musicSourceB = null;

            LogAssert.Expect(LogType.Error, "NovelForge: AudioPresenter is missing musicSourceA/musicSourceB — skipping music.");
            Assert.DoesNotThrow(() => CoroutineTestUtil.RunToCompletion(presenter.PlayMusic("theme_calm")));
        }

        [Test]
        public void PlaySfx_PlaysRegisteredClip()
        {
            var clip = CreateClip("door");
            var library = CreateLibrary();
            library.sfxClips = new[] { new AudioLibrary.Entry { id = "door_open", clip = clip } };
            var presenter = CreatePresenter(library, new FakeDeltaTimeSource { DeltaTime = 1f });

            CoroutineTestUtil.RunToCompletion(presenter.PlaySfx("door_open"));

            Assert.AreEqual(clip, presenter.sfxSource.clip);
        }

        [Test]
        public void PlaySfx_UnknownClipId_LogsErrorAndDoesNotThrow()
        {
            var presenter = CreatePresenter(CreateLibrary(), new FakeDeltaTimeSource { DeltaTime = 1f });

            LogAssert.Expect(LogType.Error, "NovelForge: no sfx clip registered for id 'missing' — skipping.");
            Assert.DoesNotThrow(() => CoroutineTestUtil.RunToCompletion(presenter.PlaySfx("missing")));
        }

        [Test]
        public void PlaySfx_MissingSfxSource_LogsErrorAndDoesNotThrow()
        {
            var clip = CreateClip("door");
            var library = CreateLibrary();
            library.sfxClips = new[] { new AudioLibrary.Entry { id = "door_open", clip = clip } };
            var presenter = CreatePresenter(library, new FakeDeltaTimeSource { DeltaTime = 1f });
            presenter.sfxSource = null;

            LogAssert.Expect(LogType.Error, "NovelForge: AudioPresenter is missing sfxSource — skipping sfx.");
            Assert.DoesNotThrow(() => CoroutineTestUtil.RunToCompletion(presenter.PlaySfx("door_open")));
        }
    }
}
