# Content Presenters: Audio + Backgrounds Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement concrete, presenter-interface implementations for music/SFX audio and background/CG scene art, so a real game can wire `StoryContext.Audio` and `StoryContext.Backgrounds` to working Unity components instead of test doubles.

**Architecture:** Two independent `MonoBehaviour` presenters (`AudioPresenter : IAudioPresenter`, `BackgroundPresenter : IBackgroundPresenter`), each backed by a `ScriptableObject` id→asset lookup (`AudioLibrary`, `BackgroundLibrary`). Both presenters crossfade/fade using a small injectable `IDeltaTimeSource` abstraction instead of calling `Time.deltaTime` directly, so fades can be driven deterministically in EditMode tests without depending on real elapsed time or Unity's audio/render clock. Both follow the project's established error-handling convention: a missing lookup entry or missing inspector wiring logs `Debug.LogError` with context and gracefully skips (`yield break`) — it never throws.

**Tech Stack:** C#, Unity 6000.6, NUnit (Unity Test Framework, EditMode), no new Unity packages.

**Spec:** [docs/superpowers/specs/2026-09-05-novelforge-design.md](../specs/2026-09-05-novelforge-design.md) — "Контент-подсистемы" section (Audio, Backgrounds/CG). The Actors subsystem (`CharacterDefinition`, `ActorView`, a working `IDialoguePresenter`) is deliberately **out of scope** for this plan: a real dialogue presenter must also render text, which belongs to the "UI-слой (UGUI)" section of the spec and is planned separately. This plan only covers the two content subsystems that stand alone without any UI layer: Audio and Backgrounds/CG.

## Global Constraints

- Unity Editor version: **6000.6.0f1**, installed via Unity Hub at `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe`.
- Package id: `com.novelforge.core`, package root `G:\ClaudeProjects\NovelForge`.
- All new production code goes under a new `Runtime/Content/` folder (sibling to the existing `Runtime/Commands/` and `Runtime/Parsing/`), namespace `NovelForge.Runtime` (same namespace as the rest of Runtime — Unity does not require namespace-per-folder, and the existing codebase does not use one). All new tests go flat under `Tests/Runtime/` (matching the existing flat layout), namespace `NovelForge.Runtime.Tests`. New test doubles go under `Tests/Runtime/Doubles/`, matching the existing `RecordingXPresenter` doubles there.
- No new Unity packages or asmdef references are needed. Every type this plan touches (`MonoBehaviour`, `ScriptableObject`, `AudioSource`, `AudioClip`, `SpriteRenderer`, `Sprite`, `GameObject`, `Time`) lives in Unity's built-in `UnityEngine` core module, already implicitly available to both `NovelForge.Runtime.asmdef` and its Tests asmdef. Backgrounds/CG use `SpriteRenderer`, not `UnityEngine.UI.Image` — this avoids adding a UGUI package/assembly reference for what is simple full-screen art with no interactivity; the UGUI choice from the spec applies to the *interactive* UI layer (buttons, text boxes, choice lists), planned separately.
- **Never call `Time.deltaTime` directly inside presenter fade/wait loops.** Route all elapsed-time reads through the injected `IDeltaTimeSource` (`internal IDeltaTimeSource TimeSource = new UnityDeltaTimeSource();` on each presenter, overridable by tests via `InternalsVisibleTo`). This is what lets EditMode tests drive a multi-second fade to completion in one or two `MoveNext()` calls by supplying a large fake `DeltaTime`, without looping thousands of times or hanging — the same class of bug (an unbounded loop with no real progress in EditMode) caused a genuine infinite-loop test hang earlier in this project; do not reintroduce it.
- Config fields subagents need to set directly from tests (library references, source/slot references, durations, `TimeSource`) must be `[SerializeField] internal`, not `private` — this project's `AssemblyInfo.cs` already declares `[assembly: InternalsVisibleTo("NovelForge.Runtime.Tests")]`, so tests can assign these fields directly without reflection or public API surface bloat. This mirrors the existing `StoryPointer.Moved`/`ResetMoved()` pattern from Phase 1.
- Every `Debug.LogError` message text below is exact and must match verbatim — `LogAssert.Expect(LogType.Error, "...")` does a literal string match.
- Git commit messages end with: `Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>`.
- Every `git add` must be of the whole containing folder (e.g. `git add Runtime/Content`), not individual file paths — Unity generates a `.meta` companion for every new file (and new folder) the first time it refreshes, and `git add` on a folder recurses into those `.meta` files automatically. Naming bare `.cs` files in `git add` was a real mistake earlier in this project that required a backfill commit; do not repeat it.
- This task runs inside a git worktree, not the main checkout. **Use the working directory's own `TestProject~`, addressed with absolute paths for every argument to the Unity test command** — do not use `TestProject~` as a relative path for `-testResults`/`-logFile`: Unity resolves a relative results/log path against the *project* path (not the launch directory) once `-projectPath` takes effect, which silently doubles it into `TestProject~\TestProject~\TestResults.xml`. Confirmed during this plan's baseline run. Get the current worktree root with `git rev-parse --show-toplevel` and build every path from that, e.g.:
  ```powershell
  & "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe" -batchmode -projectPath "<worktree-root>\TestProject~" -runTests -testPlatform EditMode -testResults "<worktree-root>\TestProject~\TestResults.xml" -logFile "<worktree-root>\TestProject~\Logs\RunTests.log"
  ```
  Deliberately no `-quit` (races with `-runTests` in this Unity version). After each run, read `TestResults.xml` and confirm the `<test-run>` root element's `failed` attribute is `"0"` and `passed` matches the expected running total; if the file is missing, check `RunTests.log`. The launching process can return well before Unity's actual test run finishes (Unity relaunches itself internally) — if the results file isn't there yet, wait and re-check rather than assuming failure.
- Visual correctness (does the crossfade actually look smooth, is the sprite the right art) is **not** automated — per the spec's Testing section, visual things are verified manually through a demo scene in `Samples~`, which is out of scope for this plan. Tests in this plan verify *logic*: correct clip/sprite selected, correct source/slot ends at correct volume/alpha, correct error on missing wiring or missing id, correct pooling/reuse behavior.
- EditMode tests in this plan instantiate real `GameObject`s (something no earlier test in this project has done — Phase 1's tests were pure C# against `RecordingXPresenter` doubles). Every test class that creates `GameObject`s must destroy them in a `[TearDown]` via `Object.DestroyImmediate`, tracked in a list built up during the test, to avoid leaking objects across tests in the same Editor session.

---

### Task 1: AudioLibrary (id → AudioClip lookup)

**Files:**
- Create: `Runtime/Content/AudioLibrary.cs`
- Test: `Tests/Runtime/AudioLibraryTests.cs`

**Interfaces:**
- Produces: `NovelForge.Runtime.AudioLibrary : ScriptableObject`, nested `[Serializable] public struct Entry { public string id; public AudioClip clip; }`, fields `[SerializeField] internal Entry[] musicTracks`, `[SerializeField] internal Entry[] sfxClips`, methods `public bool TryGetMusicClip(string id, out AudioClip clip)`, `public bool TryGetSfxClip(string id, out AudioClip clip)`.

- [ ] **Step 1: Write the failing tests**

Create `Tests/Runtime/AudioLibraryTests.cs`:

```csharp
using System;
using NUnit.Framework;
using UnityEngine;

namespace NovelForge.Runtime.Tests
{
    public class AudioLibraryTests
    {
        private static AudioClip CreateClip(string name) => AudioClip.Create(name, 1000, 1, 44100, false);

        [Test]
        public void TryGetMusicClip_ReturnsTrueAndClip_WhenIdRegistered()
        {
            var clip = CreateClip("theme");
            var library = ScriptableObject.CreateInstance<AudioLibrary>();
            library.musicTracks = new[] { new AudioLibrary.Entry { id = "theme_calm", clip = clip } };

            bool found = library.TryGetMusicClip("theme_calm", out var result);

            Assert.IsTrue(found);
            Assert.AreEqual(clip, result);
        }

        [Test]
        public void TryGetMusicClip_ReturnsFalse_WhenIdMissing()
        {
            var library = ScriptableObject.CreateInstance<AudioLibrary>();
            library.musicTracks = new[] { new AudioLibrary.Entry { id = "theme_calm", clip = CreateClip("theme") } };

            bool found = library.TryGetMusicClip("theme_sad", out var result);

            Assert.IsFalse(found);
            Assert.IsNull(result);
        }

        [Test]
        public void TryGetMusicClip_ReturnsFalse_WhenClipReferenceIsUnset()
        {
            var library = ScriptableObject.CreateInstance<AudioLibrary>();
            library.musicTracks = new[] { new AudioLibrary.Entry { id = "theme_calm", clip = null } };

            bool found = library.TryGetMusicClip("theme_calm", out var result);

            Assert.IsFalse(found);
        }

        [Test]
        public void TryGetSfxClip_ReturnsTrueAndClip_WhenIdRegistered()
        {
            var clip = CreateClip("door");
            var library = ScriptableObject.CreateInstance<AudioLibrary>();
            library.sfxClips = new[] { new AudioLibrary.Entry { id = "door_open", clip = clip } };

            bool found = library.TryGetSfxClip("door_open", out var result);

            Assert.IsTrue(found);
            Assert.AreEqual(clip, result);
        }

        [Test]
        public void TryGetSfxClip_ReturnsFalse_WhenIdMissing()
        {
            var library = ScriptableObject.CreateInstance<AudioLibrary>();

            bool found = library.TryGetSfxClip("door_open", out var result);

            Assert.IsFalse(found);
            Assert.IsNull(result);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the EditMode test command from Global Constraints. Expected: compile error, `AudioLibrary` does not exist.

- [ ] **Step 3: Implement AudioLibrary**

Create `Runtime/Content/AudioLibrary.cs`:

```csharp
using System;
using UnityEngine;

namespace NovelForge.Runtime
{
    [CreateAssetMenu(menuName = "NovelForge/Audio Library", fileName = "AudioLibrary")]
    public class AudioLibrary : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string id;
            public AudioClip clip;
        }

        [SerializeField] internal Entry[] musicTracks = Array.Empty<Entry>();
        [SerializeField] internal Entry[] sfxClips = Array.Empty<Entry>();

        public bool TryGetMusicClip(string id, out AudioClip clip) => TryGet(musicTracks, id, out clip);

        public bool TryGetSfxClip(string id, out AudioClip clip) => TryGet(sfxClips, id, out clip);

        private static bool TryGet(Entry[] entries, string id, out AudioClip clip)
        {
            foreach (var entry in entries)
            {
                if (entry.id == id && entry.clip != null)
                {
                    clip = entry.clip;
                    return true;
                }
            }

            clip = null;
            return false;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run the EditMode test command. Expected: `failed="0"`, 5 new tests passing (running total: 63).

- [ ] **Step 5: Commit**

```bash
git add Runtime/Content Tests/Runtime/AudioLibraryTests.cs Tests/Runtime/AudioLibraryTests.cs.meta
git commit -m "$(cat <<'EOF'
Add AudioLibrary: id-to-AudioClip lookup for music tracks and sfx clips

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: IDeltaTimeSource + AudioPresenter (music crossfade, single-voice SFX)

**Files:**
- Create: `Runtime/Content/IDeltaTimeSource.cs`
- Create: `Runtime/Content/UnityDeltaTimeSource.cs`
- Create: `Runtime/Content/AudioPresenter.cs`
- Create: `Tests/Runtime/Doubles/FakeDeltaTimeSource.cs`
- Test: `Tests/Runtime/AudioPresenterTests.cs`

**Interfaces:**
- Consumes: `NovelForge.Runtime.AudioLibrary` (Task 1) — `TryGetMusicClip`, `TryGetSfxClip`. `NovelForge.Runtime.IAudioPresenter` (existing, `Runtime/Commands/Presenters/IAudioPresenter.cs`) — `IEnumerator PlayMusic(string trackId)`, `IEnumerator PlaySfx(string clipId)`.
- Produces: `NovelForge.Runtime.IDeltaTimeSource` (`float DeltaTime { get; }`), `NovelForge.Runtime.UnityDeltaTimeSource : IDeltaTimeSource`, `NovelForge.Runtime.AudioPresenter : MonoBehaviour, IAudioPresenter` with internal fields `library` (`AudioLibrary`), `musicSourceA`/`musicSourceB`/`sfxSource` (`AudioSource`), `musicCrossfadeSeconds` (`float`), `TimeSource` (`IDeltaTimeSource`). `NovelForge.Runtime.Tests.FakeDeltaTimeSource : IDeltaTimeSource` with a settable `DeltaTime` property, for use by this task and Task 3.

- [ ] **Step 1: Write the failing tests**

Create `Tests/Runtime/Doubles/FakeDeltaTimeSource.cs`:

```csharp
namespace NovelForge.Runtime.Tests
{
    public class FakeDeltaTimeSource : IDeltaTimeSource
    {
        public float DeltaTime { get; set; }
    }
}
```

Create `Tests/Runtime/AudioPresenterTests.cs`:

```csharp
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
                Object.DestroyImmediate(go);
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run the EditMode test command. Expected: compile error, `IDeltaTimeSource`/`AudioPresenter`/`FakeDeltaTimeSource` do not exist.

- [ ] **Step 3: Implement IDeltaTimeSource, UnityDeltaTimeSource, AudioPresenter**

Create `Runtime/Content/IDeltaTimeSource.cs`:

```csharp
namespace NovelForge.Runtime
{
    public interface IDeltaTimeSource
    {
        float DeltaTime { get; }
    }
}
```

Create `Runtime/Content/UnityDeltaTimeSource.cs`:

```csharp
using UnityEngine;

namespace NovelForge.Runtime
{
    public class UnityDeltaTimeSource : IDeltaTimeSource
    {
        public float DeltaTime => Time.deltaTime;
    }
}
```

Create `Runtime/Content/AudioPresenter.cs`:

```csharp
using System.Collections;
using UnityEngine;

namespace NovelForge.Runtime
{
    public class AudioPresenter : MonoBehaviour, IAudioPresenter
    {
        [SerializeField] internal AudioLibrary library;
        [SerializeField] internal AudioSource musicSourceA;
        [SerializeField] internal AudioSource musicSourceB;
        [SerializeField] internal AudioSource sfxSource;
        [SerializeField] internal float musicCrossfadeSeconds = 1f;

        internal IDeltaTimeSource TimeSource = new UnityDeltaTimeSource();

        private AudioSource _activeMusicSource;

        public IEnumerator PlayMusic(string trackId)
        {
            if (library == null || !library.TryGetMusicClip(trackId, out var clip))
            {
                Debug.LogError($"NovelForge: no music clip registered for id '{trackId}' — skipping.");
                yield break;
            }

            if (musicSourceA == null || musicSourceB == null)
            {
                Debug.LogError("NovelForge: AudioPresenter is missing musicSourceA/musicSourceB — skipping music.");
                yield break;
            }

            _activeMusicSource ??= musicSourceA;
            var from = _activeMusicSource;
            var to = _activeMusicSource == musicSourceA ? musicSourceB : musicSourceA;
            to.clip = clip;
            to.volume = 0f;
            to.Play();

            float t = 0f;
            while (t < musicCrossfadeSeconds)
            {
                t += TimeSource.DeltaTime;
                float ratio = Mathf.Clamp01(t / musicCrossfadeSeconds);
                to.volume = ratio;
                from.volume = 1f - ratio;
                yield return null;
            }

            to.volume = 1f;
            from.Stop();
            from.volume = 1f;
            _activeMusicSource = to;
        }

        public IEnumerator PlaySfx(string clipId)
        {
            if (library == null || !library.TryGetSfxClip(clipId, out var clip))
            {
                Debug.LogError($"NovelForge: no sfx clip registered for id '{clipId}' — skipping.");
                yield break;
            }

            if (sfxSource == null)
            {
                Debug.LogError("NovelForge: AudioPresenter is missing sfxSource — skipping sfx.");
                yield break;
            }

            sfxSource.clip = clip;
            sfxSource.Play();

            float elapsed = 0f;
            while (elapsed < clip.length)
            {
                elapsed += TimeSource.DeltaTime;
                yield return null;
            }
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run the EditMode test command. Expected: `failed="0"`, 7 new tests passing (running total: 70).

- [ ] **Step 5: Commit**

```bash
git add Runtime/Content Tests/Runtime/AudioPresenterTests.cs Tests/Runtime/AudioPresenterTests.cs.meta Tests/Runtime/Doubles/FakeDeltaTimeSource.cs Tests/Runtime/Doubles/FakeDeltaTimeSource.cs.meta
git commit -m "$(cat <<'EOF'
Add AudioPresenter: music crossfade and single-voice sfx playback

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: AudioPresenter — concurrent SFX voice pooling

**Files:**
- Modify: `Runtime/Content/AudioPresenter.cs`
- Modify: `Tests/Runtime/AudioPresenterTests.cs`

**Interfaces:**
- Consumes: Task 2's `AudioPresenter`, `FakeDeltaTimeSource`.
- Produces: `AudioPresenter.SfxVoicesForTesting` (`internal IReadOnlyList<AudioSource>`) — the pool of dynamically-created SFX `AudioSource`s, for test inspection. The `sfxSource` field from Task 2 is **removed**: SFX voices are now created and pooled automatically, so there is nothing left to wire manually or to be "missing".

This task changes `PlaySfx` from a single dedicated `AudioSource` (blocks a second overlapping call until the first finishes) to a small self-growing pool, so multiple one-shot sounds can play at once — matching the spec's "несколько одновременных одноразовых звуков" requirement.

- [ ] **Step 1: Write the failing tests**

In `Tests/Runtime/AudioPresenterTests.cs`:

1. In `CreatePresenter`, delete the line `presenter.sfxSource = go.AddComponent<AudioSource>();` (the field no longer exists).
2. Replace the `PlaySfx_PlaysRegisteredClip` test body's assertion — it now checks the pooled voice instead of a fixed field:

```csharp
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
```

3. Delete the `PlaySfx_MissingSfxSource_LogsErrorAndDoesNotThrow` test entirely — there is no longer a `sfxSource` field that can be missing.
4. Add two new tests, after `PlaySfx_UnknownClipId_LogsErrorAndDoesNotThrow`:

```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run the EditMode test command. Expected: compile error (`SfxVoicesForTesting` does not exist / `sfxSource` still referenced) or, once the stale reference is removed, the two new tests fail against the old single-voice implementation.

- [ ] **Step 3: Replace PlaySfx with a pooled implementation**

In `Runtime/Content/AudioPresenter.cs`:

1. Delete the field `[SerializeField] internal AudioSource sfxSource;`.
2. Add near the top of the class, alongside the other fields:

```csharp
        [SerializeField] internal Transform sfxVoiceParent;

        private readonly System.Collections.Generic.List<AudioSource> _sfxPool = new();
        private readonly System.Collections.Generic.HashSet<AudioSource> _busySfxSources = new();

        internal System.Collections.Generic.IReadOnlyList<AudioSource> SfxVoicesForTesting => _sfxPool;
```

3. Replace the whole `PlaySfx` method with:

```csharp
        public IEnumerator PlaySfx(string clipId)
        {
            if (library == null || !library.TryGetSfxClip(clipId, out var clip))
            {
                Debug.LogError($"NovelForge: no sfx clip registered for id '{clipId}' — skipping.");
                yield break;
            }

            var source = AcquireSfxVoice();
            source.clip = clip;
            source.Play();

            float elapsed = 0f;
            while (elapsed < clip.length)
            {
                elapsed += TimeSource.DeltaTime;
                yield return null;
            }

            source.Stop();
            _busySfxSources.Remove(source);
        }

        private AudioSource AcquireSfxVoice()
        {
            foreach (var source in _sfxPool)
            {
                if (!_busySfxSources.Contains(source))
                {
                    _busySfxSources.Add(source);
                    return source;
                }
            }

            var parent = sfxVoiceParent != null ? sfxVoiceParent : transform;
            var voiceObject = new GameObject($"SfxVoice_{_sfxPool.Count}");
            voiceObject.transform.SetParent(parent);
            var voice = voiceObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            _sfxPool.Add(voice);
            _busySfxSources.Add(voice);
            return voice;
        }
```

- [ ] **Step 4: Run tests to verify they pass**

Run the EditMode test command. Expected: `failed="0"`. Net change from Task 2: -1 test removed, +2 tests added (running total: 71).

- [ ] **Step 5: Commit**

```bash
git add Runtime/Content Tests/Runtime/AudioPresenterTests.cs
git commit -m "$(cat <<'EOF'
AudioPresenter: pool sfx voices so overlapping one-shots don't cut each other off

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: BackgroundLibrary (id → Sprite lookup)

**Files:**
- Create: `Runtime/Content/BackgroundLibrary.cs`
- Test: `Tests/Runtime/BackgroundLibraryTests.cs`

**Interfaces:**
- Produces: `NovelForge.Runtime.BackgroundLibrary : ScriptableObject`, nested `[Serializable] public struct Entry { public string id; public Sprite sprite; }`, fields `[SerializeField] internal Entry[] backgrounds`, `[SerializeField] internal Entry[] cgs`, methods `public bool TryGetBackgroundSprite(string id, out Sprite sprite)`, `public bool TryGetCgSprite(string id, out Sprite sprite)`.

This mirrors Task 1's `AudioLibrary` exactly, with two independent id spaces (`backgrounds` for `bg`, `cgs` for `cg`) since a background id and a CG id may reasonably collide (e.g. both wanting `"intro"`).

- [ ] **Step 1: Write the failing tests**

Create `Tests/Runtime/BackgroundLibraryTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run the EditMode test command. Expected: compile error, `BackgroundLibrary` does not exist.

- [ ] **Step 3: Implement BackgroundLibrary**

Create `Runtime/Content/BackgroundLibrary.cs`:

```csharp
using System;
using UnityEngine;

namespace NovelForge.Runtime
{
    [CreateAssetMenu(menuName = "NovelForge/Background Library", fileName = "BackgroundLibrary")]
    public class BackgroundLibrary : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string id;
            public Sprite sprite;
        }

        [SerializeField] internal Entry[] backgrounds = Array.Empty<Entry>();
        [SerializeField] internal Entry[] cgs = Array.Empty<Entry>();

        public bool TryGetBackgroundSprite(string id, out Sprite sprite) => TryGet(backgrounds, id, out sprite);

        public bool TryGetCgSprite(string id, out Sprite sprite) => TryGet(cgs, id, out sprite);

        private static bool TryGet(Entry[] entries, string id, out Sprite sprite)
        {
            foreach (var entry in entries)
            {
                if (entry.id == id && entry.sprite != null)
                {
                    sprite = entry.sprite;
                    return true;
                }
            }

            sprite = null;
            return false;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run the EditMode test command. Expected: `failed="0"`, 5 new tests passing (running total: 76).

- [ ] **Step 5: Commit**

```bash
git add Runtime/Content Tests/Runtime/BackgroundLibraryTests.cs Tests/Runtime/BackgroundLibraryTests.cs.meta
git commit -m "$(cat <<'EOF'
Add BackgroundLibrary: id-to-Sprite lookup for backgrounds and CGs

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: BackgroundPresenter — background fade

**Files:**
- Create: `Runtime/Content/BackgroundPresenter.cs`
- Test: `Tests/Runtime/BackgroundPresenterTests.cs`

**Interfaces:**
- Consumes: `NovelForge.Runtime.BackgroundLibrary` (Task 4). `NovelForge.Runtime.IBackgroundPresenter` (existing, `Runtime/Commands/Presenters/IBackgroundPresenter.cs`) — `IEnumerator ShowBackground(string backgroundId)`, `IEnumerator ShowCg(string cgId)`. `NovelForge.Runtime.IDeltaTimeSource` (Task 2). `NovelForge.Runtime.Tests.FakeDeltaTimeSource` (Task 2).
- Produces: `NovelForge.Runtime.BackgroundPresenter : MonoBehaviour, IBackgroundPresenter` with internal fields `library` (`BackgroundLibrary`), `backgroundSlotA`/`backgroundSlotB`/`cgSlot` (`SpriteRenderer`), `backgroundFadeSeconds`/`cgFadeSeconds` (`float`), `TimeSource` (`IDeltaTimeSource`). This task implements `ShowBackground` fully; `ShowCg` is implemented in Task 6 and must exist as a method for the class to compile against `IBackgroundPresenter` — implement it here as `public IEnumerator ShowCg(string cgId) { yield break; }` and replace it fully in Task 6.

- [ ] **Step 1: Write the failing tests**

Create `Tests/Runtime/BackgroundPresenterTests.cs`:

```csharp
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
                Object.DestroyImmediate(go);
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run the EditMode test command. Expected: compile error, `BackgroundPresenter` does not exist.

- [ ] **Step 3: Implement BackgroundPresenter (ShowBackground; ShowCg stubbed)**

Create `Runtime/Content/BackgroundPresenter.cs`:

```csharp
using System.Collections;
using UnityEngine;

namespace NovelForge.Runtime
{
    public class BackgroundPresenter : MonoBehaviour, IBackgroundPresenter
    {
        [SerializeField] internal BackgroundLibrary library;
        [SerializeField] internal SpriteRenderer backgroundSlotA;
        [SerializeField] internal SpriteRenderer backgroundSlotB;
        [SerializeField] internal SpriteRenderer cgSlot;
        [SerializeField] internal float backgroundFadeSeconds = 0.5f;
        [SerializeField] internal float cgFadeSeconds = 0.3f;

        internal IDeltaTimeSource TimeSource = new UnityDeltaTimeSource();

        private SpriteRenderer _activeBackgroundSlot;

        public IEnumerator ShowBackground(string backgroundId)
        {
            if (library == null || !library.TryGetBackgroundSprite(backgroundId, out var sprite))
            {
                Debug.LogError($"NovelForge: no background sprite registered for id '{backgroundId}' — skipping.");
                yield break;
            }

            if (backgroundSlotA == null || backgroundSlotB == null)
            {
                Debug.LogError("NovelForge: BackgroundPresenter is missing backgroundSlotA/backgroundSlotB — skipping background change.");
                yield break;
            }

            _activeBackgroundSlot ??= backgroundSlotA;
            var from = _activeBackgroundSlot;
            var to = _activeBackgroundSlot == backgroundSlotA ? backgroundSlotB : backgroundSlotA;
            to.sprite = sprite;
            SetAlpha(to, 0f);

            float t = 0f;
            while (t < backgroundFadeSeconds)
            {
                t += TimeSource.DeltaTime;
                float ratio = Mathf.Clamp01(t / backgroundFadeSeconds);
                SetAlpha(to, ratio);
                SetAlpha(from, 1f - ratio);
                yield return null;
            }

            SetAlpha(to, 1f);
            SetAlpha(from, 0f);
            _activeBackgroundSlot = to;
        }

        public IEnumerator ShowCg(string cgId)
        {
            yield break;
        }

        private static void SetAlpha(SpriteRenderer renderer, float alpha)
        {
            var color = renderer.color;
            color.a = alpha;
            renderer.color = color;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run the EditMode test command. Expected: `failed="0"`, 4 new tests passing (running total: 80).

- [ ] **Step 5: Commit**

```bash
git add Runtime/Content Tests/Runtime/BackgroundPresenterTests.cs Tests/Runtime/BackgroundPresenterTests.cs.meta
git commit -m "$(cat <<'EOF'
Add BackgroundPresenter: crossfading background display (ShowCg stubbed for Task 6)

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 6: BackgroundPresenter — CG overlay fade-in

**Files:**
- Modify: `Runtime/Content/BackgroundPresenter.cs`
- Modify: `Tests/Runtime/BackgroundPresenterTests.cs`

**Interfaces:**
- Consumes: Task 5's `BackgroundPresenter` (including the shared `private static void SetAlpha(SpriteRenderer, float)` helper).
- Produces: The completed `IBackgroundPresenter.ShowCg` implementation — fades `cgSlot`'s sprite in from alpha 0 to 1 over `cgFadeSeconds`. There is no "hide CG" in this plan: `IBackgroundPresenter` (from Phase 1) declares only `ShowCg`, no dismiss method, so once a CG is shown it stays until the next `ShowBackground`/`ShowCg` call changes what's on screen — matching the existing interface exactly, not extending it.

- [ ] **Step 1: Write the failing tests**

Append to `Tests/Runtime/BackgroundPresenterTests.cs`, inside the class, after the last existing test:

```csharp
        [Test]
        public void ShowCg_FadesInOnCgSlot()
        {
            var sprite = CreateSprite("intro_cg");
            var library = CreateLibrary();
            library.cgs = new[] { new BackgroundLibrary.Entry { id = "intro_cg", sprite = sprite } };
            var presenter = CreatePresenter(library, new FakeDeltaTimeSource { DeltaTime = 1f });

            CoroutineTestUtil.RunToCompletion(presenter.ShowCg("intro_cg"));

            Assert.AreEqual(sprite, presenter.cgSlot.sprite);
            Assert.AreEqual(1f, presenter.cgSlot.color.a, 0.001f);
        }

        [Test]
        public void ShowCg_UnknownId_LogsErrorAndDoesNotThrow()
        {
            var presenter = CreatePresenter(CreateLibrary(), new FakeDeltaTimeSource { DeltaTime = 1f });

            LogAssert.Expect(LogType.Error, "NovelForge: no CG sprite registered for id 'missing' — skipping.");
            Assert.DoesNotThrow(() => CoroutineTestUtil.RunToCompletion(presenter.ShowCg("missing")));
        }

        [Test]
        public void ShowCg_MissingSlot_LogsErrorAndDoesNotThrow()
        {
            var sprite = CreateSprite("intro_cg");
            var library = CreateLibrary();
            library.cgs = new[] { new BackgroundLibrary.Entry { id = "intro_cg", sprite = sprite } };
            var presenter = CreatePresenter(library, new FakeDeltaTimeSource { DeltaTime = 1f });
            presenter.cgSlot = null;

            LogAssert.Expect(LogType.Error, "NovelForge: BackgroundPresenter is missing cgSlot — skipping CG.");
            Assert.DoesNotThrow(() => CoroutineTestUtil.RunToCompletion(presenter.ShowCg("intro_cg")));
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run the EditMode test command. Expected: the three new tests fail — `ShowCg_FadesInOnCgSlot` and `ShowCg_MissingSlot_LogsErrorAndDoesNotThrow` because the stub does nothing and never logs, `ShowCg_UnknownId_LogsErrorAndDoesNotThrow` because the stub never logs either (LogAssert.Expect with no matching log fails the test).

- [ ] **Step 3: Implement ShowCg**

In `Runtime/Content/BackgroundPresenter.cs`, replace the stub:

```csharp
        public IEnumerator ShowCg(string cgId)
        {
            yield break;
        }
```

with:

```csharp
        public IEnumerator ShowCg(string cgId)
        {
            if (library == null || !library.TryGetCgSprite(cgId, out var sprite))
            {
                Debug.LogError($"NovelForge: no CG sprite registered for id '{cgId}' — skipping.");
                yield break;
            }

            if (cgSlot == null)
            {
                Debug.LogError("NovelForge: BackgroundPresenter is missing cgSlot — skipping CG.");
                yield break;
            }

            cgSlot.sprite = sprite;
            SetAlpha(cgSlot, 0f);

            float t = 0f;
            while (t < cgFadeSeconds)
            {
                t += TimeSource.DeltaTime;
                SetAlpha(cgSlot, Mathf.Clamp01(t / cgFadeSeconds));
                yield return null;
            }

            SetAlpha(cgSlot, 1f);
        }
```

- [ ] **Step 4: Run tests to verify they pass**

Run the EditMode test command. Expected: `failed="0"`, 3 new tests passing (running total: 83).

- [ ] **Step 5: Commit**

```bash
git add Runtime/Content Tests/Runtime/BackgroundPresenterTests.cs
git commit -m "$(cat <<'EOF'
BackgroundPresenter: implement ShowCg as a fade-in overlay

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

## Out of scope (future plans)

- **Actors** (`CharacterDefinition`, `ActorView`, a working `IDialoguePresenter`) — deferred alongside the UGUI dialogue box, since a real `IDialoguePresenter.ShowLine` must render text and this plan deliberately stays UI-free.
- **UI-слой (UGUI)** — `DialogueBoxView`, `ChoiceView`, `SaveLoadView`.
- **Save/Load**, **Localization**, **editor tools** (script editor window, read/write branch-graph editor) — separate spec sections, separate plans.
- A `Samples~` demo scene wiring `AudioPresenter`/`BackgroundPresenter` into an actual playable `.unity` scene for manual visual verification, per the spec's testing philosophy — worth doing once there's also a dialogue/UI presenter to show alongside the audio and background, so the demo is actually watchable end to end rather than silent fades against a blank screen.
