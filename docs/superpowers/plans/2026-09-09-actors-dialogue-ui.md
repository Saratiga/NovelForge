# Actors + Dialogue/Choice UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the Actors subsystem (`CharacterDefinition`, `CharacterLibrary`, `ActorView`) and the first real, playable `IDialoguePresenter`/`IChoicePresenter` implementations (`DialoguePresenter`, `DialogueBoxView`, `ChoiceView`) on UGUI + TextMeshPro, in a new `NovelForge.UI` assembly — the first vertical slice of NovelForge that can actually be watched and clicked through.

**Architecture:** `CharacterDefinition`/`CharacterLibrary`/`ActorView` live in the existing `NovelForge.Runtime` assembly (`Runtime/Actors/`), following the exact `Try*`-lookup and `IDeltaTimeSource`-fade patterns already established by `AudioLibrary`/`BackgroundPresenter`. A new `NovelForge.UI` assembly (referencing `NovelForge.Runtime`, `UnityEngine.UI`, `Unity.TextMeshPro`) holds `DialogueBoxView` (typewriter text + click-to-advance, gated by a new injectable `IAdvanceInputSource` abstraction — same testability principle as `IDeltaTimeSource`), `ChoiceView` (a fixed pool of UGUI buttons), and `DialoguePresenter` (a composite `MonoBehaviour` implementing `IDialoguePresenter` that orchestrates `CharacterLibrary` + a configurable array of `ActorView` slots + one `DialogueBoxView`). Every coroutine in this plan is driven manually by the existing `CoroutineTestUtil`/`FakeDeltaTimeSource` pattern — nothing relies on Unity's own `StartCoroutine` scheduler, which does not advance in EditMode tests without a real frame loop.

**Tech Stack:** C#, Unity 6000.6, UGUI, TextMeshPro, NUnit (Unity Test Framework, EditMode).

**Spec:** [docs/superpowers/specs/2026-09-09-actors-dialogue-ui-design.md](../specs/2026-09-09-actors-dialogue-ui-design.md). Out of scope for this plan (per the spec): `SaveLoadView`/Save-Load, localization, editor tools.

## Global Constraints

- Unity Editor version: **6000.6.0f1**, at `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe`. Package id `com.novelforge.core`, package root `G:\ClaudeProjects\NovelForge`.
- **This plan introduces a second assembly, `NovelForge.UI`, and a second test assembly, `NovelForge.UI.Tests`** — the first phase of this project to go beyond `NovelForge.Runtime`. Task 1 bootstraps both. Every later task's UI-side files go under `UI/` (production) and `Tests/UI/` (tests), mirroring the existing `Runtime/`/`Tests/Runtime/` split exactly (namespace `NovelForge.UI` / `NovelForge.UI.Tests`).
- **TextMeshPro dependency is a real risk in this plan** — the exact package version compatible with this Unity install is not confirmed in advance (unlike everything else in this plan, which is proven-working code carried over from prior phases). Task 1 states a best-guess version (`3.0.9`); if Unity's import log reports it as unavailable or incompatible, use the version Unity itself reports as resolvable, and **update this plan file in place** with the working version before continuing — the same practice this project already followed for the `TestProject~` naming and `-quit` issues discovered in Phase 1.
- **Never call `Time.deltaTime` directly.** Every new timed loop (`ActorView`'s fade, `DialogueBoxView`'s typewriter) routes through the existing `IDeltaTimeSource` (`Runtime/Content/IDeltaTimeSource.cs`, already public) — same reason as always: EditMode tests must drive multi-second timing in one or two `MoveNext()` calls via a fake, not real elapsed time.
- **No `MonoBehaviour.StartCoroutine` anywhere in this plan.** `StartCoroutine` requires a real running scene/Play Mode frame loop to advance — it does not progress under `CoroutineTestUtil`'s manual `MoveNext()` draining, which every test in this project (and this plan) relies on. Every multi-step operation is a plain `IEnumerator`, composed via `yield return someOtherEnumerator`, exactly like `PlayMusicCommand`/`SayLineCommand` already do.
- Config fields tests need to set directly must be `[SerializeField] internal`, not `private` (existing `InternalsVisibleTo("NovelForge.Runtime.Tests")` in `Runtime/AssemblyInfo.cs`, and a new `InternalsVisibleTo("NovelForge.UI.Tests")` in `UI/AssemblyInfo.cs`, both created/extended in this plan — see Task 1 and Task 8). Any type read by `NovelForge.UI` production code from `NovelForge.Runtime` (not just tests) must be `public`, not `internal` — this is the first plan where a second assembly consumes Runtime's public surface, so double-check every cross-assembly reference is actually `public`.
- Every `Debug.LogError` message text below is exact and must match verbatim — `LogAssert.Expect(LogType.Error, "...")` does a literal string match.
- Git commit messages end with: `Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>`.
- Every `git add` must be of the whole containing folder (e.g. `git add Runtime/Actors`, `git add UI`), never individual bare `.cs` file paths — Unity generates `.meta` companions on import, and folder-add recurses into them automatically.
- This task runs inside a git worktree. **Use the working directory's own `TestProject~`, addressed with absolute paths for every argument to the Unity test command** — a relative `TestProject~` for `-testResults`/`-logFile` gets silently doubled by Unity into a nonexistent nested path (confirmed in the prior phase). Get the worktree root with `git rev-parse --show-toplevel` and build every path from that:
  ```powershell
  & "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe" -batchmode -projectPath "<worktree-root>\TestProject~" -runTests -testPlatform EditMode -testResults "<worktree-root>\TestProject~\TestResults.xml" -logFile "<worktree-root>\TestProject~\Logs\RunTests.log"
  ```
  Deliberately no `-quit`. After each run, read `TestResults.xml` and confirm the `<test-run>` root element's `failed` attribute is `"0"` and `passed` matches the expected running total; if missing, check `RunTests.log`. The launching process can return before Unity's actual test run finishes (Unity relaunches itself internally) — if the results file isn't there yet, wait and re-check rather than assuming failure.
- Do not hand-author `.meta` files. Unity generates them automatically on import (during the test run). If a `.meta` ever looks like it needs manual creation, that's a sign something else is wrong — stop and report it as a concern.
- EditMode tests instantiating real `GameObject`s must destroy them in `[TearDown]` via `Object.DestroyImmediate`, tracked in a list built up during the test — same pattern already used by `AudioPresenterTests`/`BackgroundPresenterTests`.
- `Button.onClick.Invoke()` works directly in EditMode tests without any `Canvas`/`EventSystem`/real click simulation — it's a plain `UnityEvent`, invoking it just runs registered listeners synchronously. Use this to simulate clicks in tests; never attempt real input simulation.

---

### Task 1: Bootstrap `NovelForge.UI` assembly + TextMeshPro

**Files:**
- Create: `UI/NovelForge.UI.asmdef`
- Create: `UI/AssemblyInfo.cs`
- Create: `Tests/UI/NovelForge.UI.Tests.asmdef`
- Create: `Tests/UI/SmokeTests.cs`
- Modify: `package.json`
- Modify: `TestProject~/Packages/manifest.json`

**Interfaces:**
- Produces: the `NovelForge.UI` and `NovelForge.UI.Tests` assemblies, compilable and running one EditMode test that proves both TextMeshPro and the cross-assembly `NovelForge.Runtime` reference work.

- [ ] **Step 1: Add the TextMeshPro package dependency**

In `package.json`, add a `dependencies` object (insert after `"unity": "6000.6",`):

```json
  "dependencies": {
    "com.unity.textmeshpro": "3.0.9"
  },
```

In `TestProject~/Packages/manifest.json`, add the same entry to the `dependencies` object (anywhere inside it, e.g. right after `"com.unity.test-framework": "1.4.5",`):

```json
    "com.unity.textmeshpro": "3.0.9",
```

- [ ] **Step 2: Create the `NovelForge.UI` assembly**

Create `UI/NovelForge.UI.asmdef`:

```json
{
    "name": "NovelForge.UI",
    "rootNamespace": "NovelForge.UI",
    "references": [
        "NovelForge.Runtime",
        "UnityEngine.UI",
        "Unity.TextMeshPro"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

Create `UI/AssemblyInfo.cs`:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("NovelForge.UI.Tests")]
```

- [ ] **Step 3: Create the `NovelForge.UI.Tests` assembly**

Create `Tests/UI/NovelForge.UI.Tests.asmdef`:

```json
{
    "name": "NovelForge.UI.Tests",
    "rootNamespace": "NovelForge.UI.Tests",
    "references": [
        "NovelForge.Runtime",
        "NovelForge.Runtime.Tests",
        "NovelForge.UI",
        "UnityEngine.UI",
        "Unity.TextMeshPro"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 4: Write the smoke test**

Create `Tests/UI/SmokeTests.cs`:

```csharp
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
```

- [ ] **Step 5: Run tests to verify the new assemblies compile and the smoke test passes**

Run the EditMode test command from Global Constraints.

**If this fails with a package resolution error** (Unity cannot find/resolve `com.unity.textmeshpro` at version `3.0.9`): read `TestProject~\Logs\RunTests.log` for the version Unity suggests or the error's exact text, update both `package.json` and `TestProject~/Packages/manifest.json` to a version Unity can resolve, update this plan file's Step 1 to match (so later tasks don't repeat the failed attempt), and re-run.

Expected once resolved: `failed="0"`, 1 new test passing (running total: 93).

- [ ] **Step 6: Commit**

```bash
git add package.json TestProject~/Packages/manifest.json UI Tests/UI
git commit -m "$(cat <<'EOF'
Bootstrap NovelForge.UI assembly with TextMeshPro dependency

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: CharacterDefinition (id, name, color, emotion→sprite)

**Files:**
- Create: `Runtime/Actors/CharacterDefinition.cs`
- Test: `Tests/Runtime/CharacterDefinitionTests.cs`

**Interfaces:**
- Produces: `NovelForge.Runtime.CharacterDefinition : ScriptableObject`, nested `[Serializable] public struct Pose { public string emotion; public Sprite sprite; }`, `[SerializeField] internal string id`, `internal string displayName`, `internal Color nameColor`, `internal Pose[] poses`, public read-only properties `public string Id => id`, `public string DisplayName => displayName`, `public Color NameColor => nameColor`, and `public bool TryGetSprite(string emotion, out Sprite sprite)`. `Id`/`DisplayName`/`NameColor`/`TryGetSprite` are `public` (not just `internal`) because `NovelForge.UI` (Task 8) reads them directly — this is the first Runtime type a different assembly's production code consumes.

- [ ] **Step 1: Write the failing tests**

Create `Tests/Runtime/CharacterDefinitionTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace NovelForge.Runtime.Tests
{
    public class CharacterDefinitionTests
    {
        private static Sprite CreateSprite()
        {
            var texture = new Texture2D(4, 4);
            return Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.zero);
        }

        [Test]
        public void Properties_ReturnValuesSetOnInternalFields()
        {
            var character = ScriptableObject.CreateInstance<CharacterDefinition>();
            character.id = "alice";
            character.displayName = "Alice";
            character.nameColor = Color.red;

            Assert.AreEqual("alice", character.Id);
            Assert.AreEqual("Alice", character.DisplayName);
            Assert.AreEqual(Color.red, character.NameColor);
        }

        [Test]
        public void TryGetSprite_ReturnsTrueAndSprite_WhenEmotionRegistered()
        {
            var sprite = CreateSprite();
            var character = ScriptableObject.CreateInstance<CharacterDefinition>();
            character.poses = new[] { new CharacterDefinition.Pose { emotion = "happy", sprite = sprite } };

            bool found = character.TryGetSprite("happy", out var result);

            Assert.IsTrue(found);
            Assert.AreEqual(sprite, result);
        }

        [Test]
        public void TryGetSprite_ReturnsFalse_WhenEmotionMissing()
        {
            var character = ScriptableObject.CreateInstance<CharacterDefinition>();

            bool found = character.TryGetSprite("happy", out var result);

            Assert.IsFalse(found);
            Assert.IsNull(result);
        }

        [Test]
        public void TryGetSprite_ReturnsFalse_WhenSpriteReferenceIsUnset()
        {
            var character = ScriptableObject.CreateInstance<CharacterDefinition>();
            character.poses = new[] { new CharacterDefinition.Pose { emotion = "happy", sprite = null } };

            bool found = character.TryGetSprite("happy", out var result);

            Assert.IsFalse(found);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the EditMode test command. Expected: compile error, `CharacterDefinition` does not exist.

- [ ] **Step 3: Implement CharacterDefinition**

Create `Runtime/Actors/CharacterDefinition.cs`:

```csharp
using System;
using UnityEngine;

namespace NovelForge.Runtime
{
    [CreateAssetMenu(menuName = "NovelForge/Character Definition", fileName = "CharacterDefinition")]
    public class CharacterDefinition : ScriptableObject
    {
        [Serializable]
        public struct Pose
        {
            public string emotion;
            public Sprite sprite;
        }

        [SerializeField] internal string id;
        [SerializeField] internal string displayName;
        [SerializeField] internal Color nameColor = Color.white;
        [SerializeField] internal Pose[] poses = Array.Empty<Pose>();

        public string Id => id;
        public string DisplayName => displayName;
        public Color NameColor => nameColor;

        public bool TryGetSprite(string emotion, out Sprite sprite)
        {
            foreach (var pose in poses)
            {
                if (pose.emotion == emotion && pose.sprite != null)
                {
                    sprite = pose.sprite;
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

Run the EditMode test command. Expected: `failed="0"`, 4 new tests passing (running total: 97).

- [ ] **Step 5: Commit**

```bash
git add Runtime/Actors Tests/Runtime/CharacterDefinitionTests.cs Tests/Runtime/CharacterDefinitionTests.cs.meta
git commit -m "$(cat <<'EOF'
Add CharacterDefinition: id/name/color + emotion-to-sprite lookup

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: CharacterLibrary (id → CharacterDefinition)

**Files:**
- Create: `Runtime/Actors/CharacterLibrary.cs`
- Test: `Tests/Runtime/CharacterLibraryTests.cs`

**Interfaces:**
- Consumes: `NovelForge.Runtime.CharacterDefinition` (Task 2) — `Id` property.
- Produces: `NovelForge.Runtime.CharacterLibrary : ScriptableObject`, `[SerializeField] internal CharacterDefinition[] characters`, `public bool TryGetCharacter(string id, out CharacterDefinition character)`.

- [ ] **Step 1: Write the failing tests**

Create `Tests/Runtime/CharacterLibraryTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace NovelForge.Runtime.Tests
{
    public class CharacterLibraryTests
    {
        private static CharacterDefinition CreateCharacter(string id)
        {
            var character = ScriptableObject.CreateInstance<CharacterDefinition>();
            character.id = id;
            return character;
        }

        [Test]
        public void TryGetCharacter_ReturnsTrueAndCharacter_WhenIdRegistered()
        {
            var alice = CreateCharacter("alice");
            var library = ScriptableObject.CreateInstance<CharacterLibrary>();
            library.characters = new[] { alice };

            bool found = library.TryGetCharacter("alice", out var result);

            Assert.IsTrue(found);
            Assert.AreEqual(alice, result);
        }

        [Test]
        public void TryGetCharacter_ReturnsFalse_WhenIdMissing()
        {
            var library = ScriptableObject.CreateInstance<CharacterLibrary>();
            library.characters = new[] { CreateCharacter("alice") };

            bool found = library.TryGetCharacter("bob", out var result);

            Assert.IsFalse(found);
            Assert.IsNull(result);
        }

        [Test]
        public void TryGetCharacter_SkipsNullEntries()
        {
            var library = ScriptableObject.CreateInstance<CharacterLibrary>();
            library.characters = new CharacterDefinition[] { null, CreateCharacter("alice") };

            bool found = library.TryGetCharacter("alice", out var result);

            Assert.IsTrue(found);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the EditMode test command. Expected: compile error, `CharacterLibrary` does not exist.

- [ ] **Step 3: Implement CharacterLibrary**

Create `Runtime/Actors/CharacterLibrary.cs`:

```csharp
using System;
using UnityEngine;

namespace NovelForge.Runtime
{
    [CreateAssetMenu(menuName = "NovelForge/Character Library", fileName = "CharacterLibrary")]
    public class CharacterLibrary : ScriptableObject
    {
        [SerializeField] internal CharacterDefinition[] characters = Array.Empty<CharacterDefinition>();

        public bool TryGetCharacter(string id, out CharacterDefinition character)
        {
            foreach (var entry in characters)
            {
                if (entry != null && entry.Id == id)
                {
                    character = entry;
                    return true;
                }
            }

            character = null;
            return false;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run the EditMode test command. Expected: `failed="0"`, 3 new tests passing (running total: 100).

- [ ] **Step 5: Commit**

```bash
git add Runtime/Actors Tests/Runtime/CharacterLibraryTests.cs Tests/Runtime/CharacterLibraryTests.cs.meta
git commit -m "$(cat <<'EOF'
Add CharacterLibrary: id-to-CharacterDefinition lookup

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: ActorView (sprite display with fade-in)

**Files:**
- Create: `Runtime/Actors/ActorView.cs`
- Test: `Tests/Runtime/ActorViewTests.cs`

**Interfaces:**
- Consumes: `NovelForge.Runtime.IDeltaTimeSource`/`UnityDeltaTimeSource` (existing, `Runtime/Content/`). `NovelForge.Runtime.Tests.FakeDeltaTimeSource` (existing, `Tests/Runtime/Doubles/`).
- Produces: `NovelForge.Runtime.ActorView : MonoBehaviour` (public — `DialoguePresenter` in `NovelForge.UI`, Task 8, references this type directly), `[SerializeField] internal SpriteRenderer spriteRenderer`, `internal float fadeSeconds`, `internal IDeltaTimeSource TimeSource`, `public IEnumerator ShowSprite(Sprite sprite)`.

- [ ] **Step 1: Write the failing tests**

Create `Tests/Runtime/ActorViewTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NovelForge.Runtime.Tests
{
    public class ActorViewTests
    {
        private readonly List<GameObject> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
                Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        private ActorView CreateView(FakeDeltaTimeSource time)
        {
            var go = new GameObject("ActorView");
            _spawned.Add(go);
            var view = go.AddComponent<ActorView>();
            view.spriteRenderer = go.AddComponent<SpriteRenderer>();
            view.fadeSeconds = 0.1f;
            view.TimeSource = time;
            return view;
        }

        private static Sprite CreateSprite()
        {
            var texture = new Texture2D(4, 4);
            return Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.zero);
        }

        [Test]
        public void ShowSprite_FadesInToFullAlpha()
        {
            var sprite = CreateSprite();
            var view = CreateView(new FakeDeltaTimeSource { DeltaTime = 1f });

            CoroutineTestUtil.RunToCompletion(view.ShowSprite(sprite));

            Assert.AreEqual(sprite, view.spriteRenderer.sprite);
            Assert.AreEqual(1f, view.spriteRenderer.color.a, 0.001f);
        }

        [Test]
        public void ShowSprite_HalfwayThroughFade_ReportsIntermediateAlpha()
        {
            var sprite = CreateSprite();
            var time = new FakeDeltaTimeSource { DeltaTime = 0.05f };
            var view = CreateView(time);

            var routine = view.ShowSprite(sprite);
            routine.MoveNext();

            Assert.AreEqual(0.5f, view.spriteRenderer.color.a, 0.001f);
        }

        [Test]
        public void ShowSprite_MissingSpriteRenderer_LogsErrorAndDoesNotThrow()
        {
            var go = new GameObject("ActorView");
            _spawned.Add(go);
            var view = go.AddComponent<ActorView>();
            view.TimeSource = new FakeDeltaTimeSource { DeltaTime = 1f };

            LogAssert.Expect(LogType.Error, "NovelForge: ActorView is missing spriteRenderer — skipping.");
            Assert.DoesNotThrow(() => CoroutineTestUtil.RunToCompletion(view.ShowSprite(CreateSprite())));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the EditMode test command. Expected: compile error, `ActorView` does not exist.

- [ ] **Step 3: Implement ActorView**

Create `Runtime/Actors/ActorView.cs`:

```csharp
using System.Collections;
using UnityEngine;

namespace NovelForge.Runtime
{
    public class ActorView : MonoBehaviour
    {
        [SerializeField] internal SpriteRenderer spriteRenderer;
        [SerializeField] internal float fadeSeconds = 0.3f;

        internal IDeltaTimeSource TimeSource = new UnityDeltaTimeSource();

        public IEnumerator ShowSprite(Sprite sprite)
        {
            if (spriteRenderer == null)
            {
                Debug.LogError("NovelForge: ActorView is missing spriteRenderer — skipping.");
                yield break;
            }

            spriteRenderer.sprite = sprite;
            SetAlpha(0f);

            float t = 0f;
            while (t < fadeSeconds)
            {
                t += TimeSource.DeltaTime;
                SetAlpha(Mathf.Clamp01(t / fadeSeconds));
                yield return null;
            }

            SetAlpha(1f);
        }

        private void SetAlpha(float alpha)
        {
            var color = spriteRenderer.color;
            color.a = alpha;
            spriteRenderer.color = color;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run the EditMode test command. Expected: `failed="0"`, 3 new tests passing (running total: 103).

- [ ] **Step 5: Commit**

```bash
git add Runtime/Actors Tests/Runtime/ActorViewTests.cs Tests/Runtime/ActorViewTests.cs.meta
git commit -m "$(cat <<'EOF'
Add ActorView: single-slot sprite display with fade-in

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: IAdvanceInputSource + ButtonAdvanceInputSource + FakeAdvanceInputSource

**Files:**
- Create: `UI/IAdvanceInputSource.cs`
- Create: `UI/ButtonAdvanceInputSource.cs`
- Create: `Tests/UI/Doubles/FakeAdvanceInputSource.cs`
- Test: `Tests/UI/ButtonAdvanceInputSourceTests.cs`

**Interfaces:**
- Produces: `NovelForge.UI.IAdvanceInputSource` (`bool ConsumeAdvanceRequest()`), `NovelForge.UI.ButtonAdvanceInputSource : IAdvanceInputSource` (constructor `ButtonAdvanceInputSource(Button button)`), `NovelForge.UI.Tests.FakeAdvanceInputSource : IAdvanceInputSource` with a public settable `bool Pending` field, for use by Task 6, Task 7, and Task 8.

- [ ] **Step 1: Write the failing tests**

Create `Tests/UI/Doubles/FakeAdvanceInputSource.cs`:

```csharp
namespace NovelForge.UI.Tests
{
    public class FakeAdvanceInputSource : IAdvanceInputSource
    {
        public bool Pending;

        public bool ConsumeAdvanceRequest()
        {
            if (!Pending)
                return false;

            Pending = false;
            return true;
        }
    }
}
```

Create `Tests/UI/ButtonAdvanceInputSourceTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run the EditMode test command. Expected: compile error, `IAdvanceInputSource`/`ButtonAdvanceInputSource` do not exist.

- [ ] **Step 3: Implement IAdvanceInputSource and ButtonAdvanceInputSource**

Create `UI/IAdvanceInputSource.cs`:

```csharp
namespace NovelForge.UI
{
    public interface IAdvanceInputSource
    {
        bool ConsumeAdvanceRequest();
    }
}
```

Create `UI/ButtonAdvanceInputSource.cs`:

```csharp
using UnityEngine.UI;

namespace NovelForge.UI
{
    public class ButtonAdvanceInputSource : IAdvanceInputSource
    {
        private bool _pending;

        public ButtonAdvanceInputSource(Button button)
        {
            button.onClick.AddListener(() => _pending = true);
        }

        public bool ConsumeAdvanceRequest()
        {
            if (!_pending)
                return false;

            _pending = false;
            return true;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run the EditMode test command. Expected: `failed="0"`, 2 new tests passing (running total: 105).

- [ ] **Step 5: Commit**

```bash
git add UI Tests/UI
git commit -m "$(cat <<'EOF'
Add IAdvanceInputSource: injectable click-to-advance, wired to a real UGUI Button

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 6: DialogueBoxView (typewriter text + click-to-advance)

**Files:**
- Create: `UI/DialogueBoxView.cs`
- Test: `Tests/UI/DialogueBoxViewTests.cs`

**Interfaces:**
- Consumes: `NovelForge.Runtime.IDeltaTimeSource`/`UnityDeltaTimeSource` (existing). `NovelForge.Runtime.Tests.FakeDeltaTimeSource` (existing). `NovelForge.UI.IAdvanceInputSource`/`ButtonAdvanceInputSource` (Task 5). `NovelForge.UI.Tests.FakeAdvanceInputSource` (Task 5).
- Produces: `NovelForge.UI.DialogueBoxView : MonoBehaviour`, `[SerializeField] internal TMP_Text nameText`, `internal TMP_Text bodyText`, `internal Button advanceButton`, `internal float secondsPerCharacter`, `internal IDeltaTimeSource TimeSource`, `internal IAdvanceInputSource AdvanceInput`, `public IEnumerator ShowText(string speakerName, Color nameColor, string text)`.

- [ ] **Step 1: Write the failing tests**

Create `Tests/UI/DialogueBoxViewTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run the EditMode test command. Expected: compile error, `DialogueBoxView` does not exist.

- [ ] **Step 3: Implement DialogueBoxView**

Create `UI/DialogueBoxView.cs`:

```csharp
using System.Collections;
using NovelForge.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovelForge.UI
{
    public class DialogueBoxView : MonoBehaviour
    {
        [SerializeField] internal TMP_Text nameText;
        [SerializeField] internal TMP_Text bodyText;
        [SerializeField] internal Button advanceButton;
        [SerializeField] internal float secondsPerCharacter = 0.02f;

        internal IDeltaTimeSource TimeSource = new UnityDeltaTimeSource();
        internal IAdvanceInputSource AdvanceInput;

        private void Awake()
        {
            if (AdvanceInput == null && advanceButton != null)
                AdvanceInput = new ButtonAdvanceInputSource(advanceButton);
        }

        public IEnumerator ShowText(string speakerName, Color nameColor, string text)
        {
            if (nameText == null || bodyText == null)
            {
                Debug.LogError("NovelForge: DialogueBoxView is missing nameText/bodyText — skipping.");
                yield break;
            }

            nameText.text = speakerName;
            nameText.color = nameColor;

            if (AdvanceInput == null)
            {
                Debug.LogError("NovelForge: DialogueBoxView has no advance input wired — showing full text without waiting.");
                bodyText.text = text;
                yield break;
            }

            if (secondsPerCharacter <= 0f)
            {
                bodyText.text = text;
            }
            else
            {
                bodyText.text = string.Empty;
                float t = 0f;
                int shown = 0;
                while (shown < text.Length)
                {
                    t += TimeSource.DeltaTime;
                    int target = Mathf.Min(text.Length, Mathf.FloorToInt(t / secondsPerCharacter));
                    if (target > shown)
                    {
                        shown = target;
                        bodyText.text = text.Substring(0, shown);
                    }

                    if (shown >= text.Length)
                        break;

                    if (AdvanceInput.ConsumeAdvanceRequest())
                    {
                        bodyText.text = text;
                        break;
                    }

                    yield return null;
                }

                bodyText.text = text;
            }

            while (!AdvanceInput.ConsumeAdvanceRequest())
                yield return null;
        }
    }
}
```

Note the `if (shown >= text.Length) break;` check placed *before* the `AdvanceInput.ConsumeAdvanceRequest()` check inside the typewriter loop: without it, the same frame that naturally finishes typing would also consume a pending click meant for the *next* phase (waiting to advance to the next line), causing the final wait-loop below to wait forever for a click that already got consumed. This was caught by hand-tracing `ShowText_SetsNameAndColor` and `ShowText_TypewriterCompletesAndWaitsForSeparateClick` against a naive version of this loop during planning — keep the ordering exactly as shown.

- [ ] **Step 4: Run tests to verify they pass**

Run the EditMode test command. Expected: `failed="0"`, 7 new tests passing (running total: 112).

- [ ] **Step 5: Commit**

```bash
git add UI Tests/UI/DialogueBoxViewTests.cs Tests/UI/DialogueBoxViewTests.cs.meta
git commit -m "$(cat <<'EOF'
Add DialogueBoxView: typewriter text with click-to-skip and click-to-advance

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 7: ChoiceView (fixed button pool, implements IChoicePresenter)

**Files:**
- Create: `UI/ChoiceView.cs`
- Test: `Tests/UI/ChoiceViewTests.cs`

**Interfaces:**
- Consumes: `NovelForge.Runtime.IChoicePresenter` (existing, `Runtime/Commands/Presenters/IChoicePresenter.cs`) — `IEnumerator PresentChoices(IReadOnlyList<string> optionTexts, Action<int> onSelected)`.
- Produces: `NovelForge.UI.ChoiceView : MonoBehaviour, IChoicePresenter`, `[SerializeField] internal Button[] optionButtons`.

- [ ] **Step 1: Write the failing tests**

Create `Tests/UI/ChoiceViewTests.cs`:

```csharp
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
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the EditMode test command. Expected: compile error, `ChoiceView` does not exist.

- [ ] **Step 3: Implement ChoiceView**

Create `UI/ChoiceView.cs`:

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using NovelForge.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace NovelForge.UI
{
    public class ChoiceView : MonoBehaviour, IChoicePresenter
    {
        [SerializeField] internal Button[] optionButtons;

        public IEnumerator PresentChoices(IReadOnlyList<string> optionTexts, Action<int> onSelected)
        {
            if (optionButtons == null || optionButtons.Length == 0)
            {
                Debug.LogError("NovelForge: ChoiceView has no optionButtons wired — skipping choice.");
                yield break;
            }

            int count = optionTexts.Count;
            if (count > optionButtons.Length)
            {
                Debug.LogError($"NovelForge: ChoiceView has {optionButtons.Length} button(s) but {count} option(s) were requested — truncating.");
                count = optionButtons.Length;
            }

            int selected = -1;
            for (int i = 0; i < optionButtons.Length; i++)
            {
                if (i < count)
                {
                    int optionIndex = i;
                    var label = optionButtons[i].GetComponentInChildren<TMPro.TMP_Text>();
                    if (label != null)
                        label.text = optionTexts[i];
                    optionButtons[i].gameObject.SetActive(true);
                    optionButtons[i].onClick.AddListener(() => selected = optionIndex);
                }
                else
                {
                    optionButtons[i].gameObject.SetActive(false);
                }
            }

            while (selected < 0)
                yield return null;

            for (int i = 0; i < optionButtons.Length; i++)
            {
                optionButtons[i].onClick.RemoveAllListeners();
                optionButtons[i].gameObject.SetActive(false);
            }

            onSelected(selected);
        }
    }
}
```

If `ChoiceView.PresentChoices` bails early (no buttons wired), it never calls `onSelected` — this is intentional and safe: `ChoiceCommand.Execute` (`Runtime/Commands/Builtin/ChoiceCommand.cs`, existing, unmodified) already initializes `selected = -1` before calling `PresentChoices`, and already has its own fallback (`LogError` + default to option 0) for exactly the case where `onSelected` was never called. No duplicate fallback needed here.

- [ ] **Step 4: Run tests to verify they pass**

Run the EditMode test command. Expected: `failed="0"`, 4 new tests passing (running total: 116).

- [ ] **Step 5: Commit**

```bash
git add UI Tests/UI/ChoiceViewTests.cs Tests/UI/ChoiceViewTests.cs.meta
git commit -m "$(cat <<'EOF'
Add ChoiceView: fixed button-pool implementation of IChoicePresenter

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 8: DialoguePresenter (composite IDialoguePresenter)

**Files:**
- Create: `UI/DialoguePresenter.cs`
- Test: `Tests/UI/DialoguePresenterTests.cs`
- Modify: `Runtime/AssemblyInfo.cs`

**Interfaces:**
- Consumes: `NovelForge.Runtime.IDialoguePresenter` (existing) — `IEnumerator ShowLine(string characterId, string text, string emotion, string position)`. `NovelForge.Runtime.CharacterLibrary`/`CharacterDefinition` (Tasks 2-3). `NovelForge.Runtime.ActorView` (Task 4). `NovelForge.UI.DialogueBoxView` (Task 6).
- Produces: `NovelForge.UI.DialoguePresenter : MonoBehaviour, IDialoguePresenter`, nested `[Serializable] public struct PositionSlot { public string position; public ActorView view; }`, `[SerializeField] internal CharacterLibrary library`, `internal DialogueBoxView dialogueBox`, `internal PositionSlot[] positionSlots`.

This task's tests need to set `internal` fields on `CharacterDefinition`, `CharacterLibrary`, and `ActorView` — all three are `NovelForge.Runtime` types, but this task's tests live in `NovelForge.UI.Tests`. `Runtime/AssemblyInfo.cs` currently only grants `InternalsVisibleTo("NovelForge.Runtime.Tests")`; this task extends it with a second grant.

- [ ] **Step 1: Extend Runtime's InternalsVisibleTo**

Modify `Runtime/AssemblyInfo.cs` — it currently reads:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("NovelForge.Runtime.Tests")]
```

Change it to:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("NovelForge.Runtime.Tests")]
[assembly: InternalsVisibleTo("NovelForge.UI.Tests")]
```

- [ ] **Step 2: Write the failing tests**

Create `Tests/UI/DialoguePresenterTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using NUnit.Framework;
using NovelForge.Runtime;
using NovelForge.Runtime.Tests;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace NovelForge.UI.Tests
{
    public class DialoguePresenterTests
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

        private DialogueBoxView CreateDialogueBox()
        {
            var go = CreateTracked("DialogueBoxView");
            var view = go.AddComponent<DialogueBoxView>();
            view.nameText = CreateTracked("Name").AddComponent<TextMeshProUGUI>();
            view.bodyText = CreateTracked("Body").AddComponent<TextMeshProUGUI>();
            view.secondsPerCharacter = 0.1f;
            view.TimeSource = new FakeDeltaTimeSource { DeltaTime = 1f };
            view.AdvanceInput = new FakeAdvanceInputSource { Pending = true };
            return view;
        }

        private ActorView CreateActorView()
        {
            var go = CreateTracked("ActorView");
            var view = go.AddComponent<ActorView>();
            view.spriteRenderer = go.AddComponent<SpriteRenderer>();
            view.fadeSeconds = 0.1f;
            view.TimeSource = new FakeDeltaTimeSource { DeltaTime = 1f };
            return view;
        }

        private static Sprite CreateSprite()
        {
            var texture = new Texture2D(4, 4);
            return Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.zero);
        }

        private static CharacterLibrary CreateLibrary()
        {
            var alice = ScriptableObject.CreateInstance<CharacterDefinition>();
            alice.id = "alice";
            alice.displayName = "Alice";
            alice.nameColor = Color.red;
            alice.poses = new[] { new CharacterDefinition.Pose { emotion = "happy", sprite = CreateSprite() } };

            var library = ScriptableObject.CreateInstance<CharacterLibrary>();
            library.characters = new[] { alice };
            return library;
        }

        private DialoguePresenter CreatePresenter(CharacterLibrary library, DialogueBoxView dialogueBox, ActorView leftSlot)
        {
            var go = CreateTracked("DialoguePresenter");
            var presenter = go.AddComponent<DialoguePresenter>();
            presenter.library = library;
            presenter.dialogueBox = dialogueBox;
            presenter.positionSlots = leftSlot == null
                ? Array.Empty<DialoguePresenter.PositionSlot>()
                : new[] { new DialoguePresenter.PositionSlot { position = "left", view = leftSlot } };
            return presenter;
        }

        [Test]
        public void ShowLine_AllResolve_ShowsActorSpriteThenText()
        {
            var library = CreateLibrary();
            var dialogueBox = CreateDialogueBox();
            var actorView = CreateActorView();
            var presenter = CreatePresenter(library, dialogueBox, actorView);

            CoroutineTestUtil.RunToCompletion(presenter.ShowLine("alice", "Hello!", "happy", "left"));

            Assert.IsNotNull(actorView.spriteRenderer.sprite);
            Assert.AreEqual(1f, actorView.spriteRenderer.color.a, 0.001f);
            Assert.AreEqual("Alice", dialogueBox.nameText.text);
            Assert.AreEqual(Color.red, dialogueBox.nameText.color);
            Assert.AreEqual("Hello!", dialogueBox.bodyText.text);
        }

        [Test]
        public void ShowLine_UnknownCharacterId_LogsErrorAndUsesIdAsFallbackName()
        {
            var library = CreateLibrary();
            var dialogueBox = CreateDialogueBox();
            var presenter = CreatePresenter(library, dialogueBox, null);

            LogAssert.Expect(LogType.Error, "NovelForge: no character registered for id 'bob' — showing text without actor.");
            CoroutineTestUtil.RunToCompletion(presenter.ShowLine("bob", "Hi.", "happy", "left"));

            Assert.AreEqual("bob", dialogueBox.nameText.text);
            Assert.AreEqual("Hi.", dialogueBox.bodyText.text);
        }

        [Test]
        public void ShowLine_UnknownEmotion_LogsErrorAndShowsCorrectName()
        {
            var library = CreateLibrary();
            var dialogueBox = CreateDialogueBox();
            var actorView = CreateActorView();
            var presenter = CreatePresenter(library, dialogueBox, actorView);

            LogAssert.Expect(LogType.Error, "NovelForge: character 'alice' has no sprite for emotion 'angry' — leaving actor unchanged.");
            CoroutineTestUtil.RunToCompletion(presenter.ShowLine("alice", "Hi.", "angry", "left"));

            Assert.AreEqual("Alice", dialogueBox.nameText.text);
            Assert.IsNull(actorView.spriteRenderer.sprite);
        }

        [Test]
        public void ShowLine_UnknownPosition_LogsErrorAndSkipsActor()
        {
            var library = CreateLibrary();
            var dialogueBox = CreateDialogueBox();
            var actorView = CreateActorView();
            var presenter = CreatePresenter(library, dialogueBox, actorView);

            LogAssert.Expect(LogType.Error, "NovelForge: no position slot registered for 'center' — skipping actor display.");
            CoroutineTestUtil.RunToCompletion(presenter.ShowLine("alice", "Hi.", "happy", "center"));

            Assert.AreEqual("Alice", dialogueBox.nameText.text);
            Assert.IsNull(actorView.spriteRenderer.sprite);
        }

        [Test]
        public void ShowLine_MissingDialogueBox_LogsErrorAndDoesNotThrow()
        {
            var presenter = CreatePresenter(CreateLibrary(), null, null);

            LogAssert.Expect(LogType.Error, "NovelForge: DialoguePresenter is missing dialogueBox — skipping line.");
            Assert.DoesNotThrow(() => CoroutineTestUtil.RunToCompletion(presenter.ShowLine("alice", "Hi.", "happy", "left")));
        }
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run the EditMode test command. Expected: compile error, `DialoguePresenter` does not exist (and, until Step 1's `AssemblyInfo.cs` change, the `internal` field assignments in `CreateLibrary`/`CreateActorView` would also fail to compile — Step 1 must land first).

- [ ] **Step 4: Implement DialoguePresenter**

Create `UI/DialoguePresenter.cs`:

```csharp
using System;
using System.Collections;
using NovelForge.Runtime;
using UnityEngine;

namespace NovelForge.UI
{
    public class DialoguePresenter : MonoBehaviour, IDialoguePresenter
    {
        [Serializable]
        public struct PositionSlot
        {
            public string position;
            public ActorView view;
        }

        [SerializeField] internal CharacterLibrary library;
        [SerializeField] internal DialogueBoxView dialogueBox;
        [SerializeField] internal PositionSlot[] positionSlots = Array.Empty<PositionSlot>();

        public IEnumerator ShowLine(string characterId, string text, string emotion, string position)
        {
            if (dialogueBox == null)
            {
                Debug.LogError("NovelForge: DialoguePresenter is missing dialogueBox — skipping line.");
                yield break;
            }

            string displayName = characterId;
            Color nameColor = Color.white;

            if (library == null || !library.TryGetCharacter(characterId, out var character))
            {
                Debug.LogError($"NovelForge: no character registered for id '{characterId}' — showing text without actor.");
            }
            else
            {
                displayName = character.DisplayName;
                nameColor = character.NameColor;

                if (!character.TryGetSprite(emotion, out var sprite))
                {
                    Debug.LogError($"NovelForge: character '{characterId}' has no sprite for emotion '{emotion}' — leaving actor unchanged.");
                }
                else
                {
                    var slotView = FindSlot(position);
                    if (slotView == null)
                        Debug.LogError($"NovelForge: no position slot registered for '{position}' — skipping actor display.");
                    else
                        yield return slotView.ShowSprite(sprite);
                }
            }

            yield return dialogueBox.ShowText(displayName, nameColor, text);
        }

        private ActorView FindSlot(string position)
        {
            foreach (var slot in positionSlots)
            {
                if (slot.position == position)
                    return slot.view;
            }

            return null;
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run the EditMode test command. Expected: `failed="0"`, 5 new tests passing (running total: 121).

- [ ] **Step 6: Commit**

```bash
git add Runtime/AssemblyInfo.cs UI Tests/UI/DialoguePresenterTests.cs Tests/UI/DialoguePresenterTests.cs.meta
git commit -m "$(cat <<'EOF'
Add DialoguePresenter: composite IDialoguePresenter wiring CharacterLibrary, ActorView slots, and DialogueBoxView

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

## Out of scope (future plans)

- **`SaveLoadView`** / Save-Load subsystem (`VariableStore` persistence already exists; `SaveData`/`ISaveStorage` do not).
- **Localization** (`LocalizationTable`, `LocaleManager`).
- **Editor tools** (script editor window with syntax highlighting/autocomplete, the read/write branch-graph editor, custom `ScriptedImporter`).
- A `Samples~` demo scene wiring `DialoguePresenter`/`ChoiceView`/`AudioPresenter`/`BackgroundPresenter` together into an actual playable `.unity` scene — this plan finally makes that scene meaningful to build (there's something to watch and click through now), but assembling and hand-verifying it is manual work explicitly deferred, per the spec's testing philosophy, same as the previous two phases.
