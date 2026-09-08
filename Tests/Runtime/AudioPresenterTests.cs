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
        public void PlayMusic_MissingLibrary_LogsErrorAndDoesNotThrow()
        {
            var presenter = CreatePresenter(CreateLibrary(), new FakeDeltaTimeSource { DeltaTime = 1f });
            presenter.library = null;

            LogAssert.Expect(LogType.Error, "NovelForge: AudioPresenter is missing library — skipping music.");
            Assert.DoesNotThrow(() => CoroutineTestUtil.RunToCompletion(presenter.PlayMusic("theme_calm")));
        }

        [Test]
        public void PlayMusic_HalfwayThroughFade_ReportsIntermediateVolumes()
        {
            var clip = CreateClip("theme");
            var library = CreateLibrary();
            library.musicTracks = new[] { new AudioLibrary.Entry { id = "theme_calm", clip = clip } };
            var presenter = CreatePresenter(library, new FakeDeltaTimeSource { DeltaTime = 0.05f });

            var routine = presenter.PlayMusic("theme_calm");
            routine.MoveNext();

            Assert.AreEqual(0.5f, presenter.musicSourceB.volume, 0.001f);
            Assert.AreEqual(0.5f, presenter.musicSourceA.volume, 0.001f);
        }

        [Test]
        public void PlayMusic_StaleReentrantCall_DoesNotStompNewerState()
        {
            var library = CreateLibrary();
            var clipA = CreateClip("track_a");
            var clipB = CreateClip("track_b");
            library.musicTracks = new[]
            {
                new AudioLibrary.Entry { id = "track_a", clip = clipA },
                new AudioLibrary.Entry { id = "track_b", clip = clipB },
            };
            var time = new FakeDeltaTimeSource { DeltaTime = 1f };
            var presenter = CreatePresenter(library, time);

            // Call 1 completes, flipping active musicSourceA -> musicSourceB.
            CoroutineTestUtil.RunToCompletion(presenter.PlayMusic("track_a"));

            // Call 2 starts a B -> A fade but only advances partway, then goes stale
            // (superseded by calls 3 and 4 below) without ever completing.
            var staleRoutine = presenter.PlayMusic("track_b");
            time.DeltaTime = 0f;
            staleRoutine.MoveNext();

            // Call 3 (same direction as call 2, since active hasn't flipped yet) completes,
            // flipping active B -> A.
            time.DeltaTime = 1f;
            CoroutineTestUtil.RunToCompletion(presenter.PlayMusic("track_a"));

            // Call 4 is the most recent request: A -> B, and it completes.
            CoroutineTestUtil.RunToCompletion(presenter.PlayMusic("track_b"));

            Assert.IsTrue(presenter.musicSourceB.isPlaying);
            Assert.AreEqual(1f, presenter.musicSourceB.volume, 0.001f);

            // Resuming the stale call 2 must not stop/undo call 4's result.
            staleRoutine.MoveNext();

            Assert.IsTrue(presenter.musicSourceB.isPlaying);
            Assert.AreEqual(1f, presenter.musicSourceB.volume, 0.001f);
        }

        [Test]
        public void PlaySfx_PlaysRegisteredClip()
        {
            var clip = CreateClip("door");
            var library = CreateLibrary();
            library.sfxClips = new[] { new AudioLibrary.Entry { id = "door_open", clip = clip } };
            var presenter = CreatePresenter(library, new FakeDeltaTimeSource { DeltaTime = 1f });

            CoroutineTestUtil.RunToCompletion(presenter.PlaySfx("door_open"));

            Assert.AreEqual(1, presenter.SfxVoicesForTesting.Count);
            Assert.AreEqual(clip, presenter.SfxVoicesForTesting[0].clip);
        }

        [Test]
        public void PlaySfx_UnknownClipId_LogsErrorAndDoesNotThrow()
        {
            var presenter = CreatePresenter(CreateLibrary(), new FakeDeltaTimeSource { DeltaTime = 1f });

            LogAssert.Expect(LogType.Error, "NovelForge: no sfx clip registered for id 'missing' — skipping.");
            Assert.DoesNotThrow(() => CoroutineTestUtil.RunToCompletion(presenter.PlaySfx("missing")));
        }

        [Test]
        public void PlaySfx_MissingLibrary_LogsErrorAndDoesNotThrow()
        {
            var presenter = CreatePresenter(CreateLibrary(), new FakeDeltaTimeSource { DeltaTime = 1f });
            presenter.library = null;

            LogAssert.Expect(LogType.Error, "NovelForge: AudioPresenter is missing library — skipping sfx.");
            Assert.DoesNotThrow(() => CoroutineTestUtil.RunToCompletion(presenter.PlaySfx("door_open")));
        }

        [Test]
        public void PlaySfx_TwoOverlappingCalls_UseDifferentVoices()
        {
            var library = CreateLibrary();
            var clipA = CreateClip("sfx_a");
            var clipB = CreateClip("sfx_b");
            library.sfxClips = new[]
            {
                new AudioLibrary.Entry { id = "sfx_a", clip = clipA },
                new AudioLibrary.Entry { id = "sfx_b", clip = clipB },
            };
            var time = new FakeDeltaTimeSource { DeltaTime = 0f };
            var presenter = CreatePresenter(library, time);

            var routineA = presenter.PlaySfx("sfx_a");
            var routineB = presenter.PlaySfx("sfx_b");
            routineA.MoveNext();
            routineB.MoveNext();

            Assert.AreEqual(2, presenter.SfxVoicesForTesting.Count);
            Assert.AreEqual(clipA, presenter.SfxVoicesForTesting[0].clip);
            Assert.AreEqual(clipB, presenter.SfxVoicesForTesting[1].clip);

            time.DeltaTime = 1f;
            routineA.MoveNext();
            routineB.MoveNext();
        }

        [Test]
        public void PlaySfx_SequentialCalls_ReuseSameVoiceOnceFreed()
        {
            var library = CreateLibrary();
            var clip = CreateClip("sfx_a");
            library.sfxClips = new[] { new AudioLibrary.Entry { id = "sfx_a", clip = clip } };
            var presenter = CreatePresenter(library, new FakeDeltaTimeSource { DeltaTime = 10f });

            CoroutineTestUtil.RunToCompletion(presenter.PlaySfx("sfx_a"));
            CoroutineTestUtil.RunToCompletion(presenter.PlaySfx("sfx_a"));

            Assert.AreEqual(1, presenter.SfxVoicesForTesting.Count);
        }
    }
}
