using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NovelForge.Runtime;
using NovelForge.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace NovelForge.PlayMode.Tests
{
    // Plays a scene wired like the GettingStarted sample — runner, dialogue box, actor slot,
    // background and music presenters — through real frames: play, save mid-story, restart,
    // load. Covers what EditMode tests cannot: coroutines actually advancing between frames.
    public class SaveLoadPlayModeTests
    {
        private const string Story =
            "label start\n" +
            "bg room\n" +
            "music theme\n" +
            "Alice: Hi #happy left\n" +
            "Alice: Second line\n";

        private class ManualAdvance : IAdvanceInputSource
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

        private readonly List<Object> _spawned = new();
        private string _saveDirectory;
        private NovelRunner _runner;
        private ManualAdvance _advance;
        private DialogueBoxView _dialogueBox;
        private ActorView _actor;
        private BackgroundPresenter _backgrounds;
        private AudioPresenter _audio;
        private Sprite _roomSprite;
        private Sprite _aliceHappy;
        private AudioClip _theme;

        [SetUp]
        public void SetUp()
        {
            _saveDirectory = Path.Combine(Path.GetTempPath(), "NovelForgePlayModeTests_" + Guid.NewGuid());
            _roomSprite = CreateSprite();
            _aliceHappy = CreateSprite();
            _theme = AudioClip.Create("theme", 44100, 1, 44100, false);
            _spawned.Add(_theme);

            var alice = ScriptableObject.CreateInstance<CharacterDefinition>();
            alice.id = "Alice";
            alice.displayName = "Alice";
            alice.poses = new[] { new CharacterDefinition.Pose { emotion = "happy", sprite = _aliceHappy } };
            var characters = ScriptableObject.CreateInstance<CharacterLibrary>();
            characters.characters = new[] { alice };
            var backgroundLibrary = ScriptableObject.CreateInstance<BackgroundLibrary>();
            backgroundLibrary.backgrounds = new[] { new BackgroundLibrary.Entry { id = "room", sprite = _roomSprite } };
            var audioLibrary = ScriptableObject.CreateInstance<AudioLibrary>();
            audioLibrary.musicTracks = new[] { new AudioLibrary.Entry { id = "theme", clip = _theme } };
            _spawned.AddRange(new Object[] { alice, characters, backgroundLibrary, audioLibrary });

            GameObject root = Track(new GameObject("NovelForgePlayModeScene"));

            _dialogueBox = root.AddComponent<DialogueBoxView>();
            _dialogueBox.nameText = Track(new GameObject("Name")).AddComponent<TextMeshProUGUI>();
            _dialogueBox.bodyText = Track(new GameObject("Body")).AddComponent<TextMeshProUGUI>();
            _dialogueBox.secondsPerCharacter = 0f;
            _advance = new ManualAdvance();
            _dialogueBox.AdvanceInput = _advance;

            GameObject actorObject = Track(new GameObject("ActorLeft"));
            _actor = actorObject.AddComponent<ActorView>();
            _actor.spriteRenderer = actorObject.AddComponent<SpriteRenderer>();
            _actor.fadeSeconds = 0.05f;

            var dialogue = root.AddComponent<DialoguePresenter>();
            dialogue.library = characters;
            dialogue.dialogueBox = _dialogueBox;
            dialogue.positionSlots = new[] { new DialoguePresenter.PositionSlot { position = "left", view = _actor } };

            _backgrounds = root.AddComponent<BackgroundPresenter>();
            _backgrounds.library = backgroundLibrary;
            _backgrounds.backgroundSlotA = Track(new GameObject("BgA")).AddComponent<SpriteRenderer>();
            _backgrounds.backgroundSlotB = Track(new GameObject("BgB")).AddComponent<SpriteRenderer>();
            _backgrounds.cgSlot = Track(new GameObject("Cg")).AddComponent<SpriteRenderer>();
            _backgrounds.backgroundFadeSeconds = 0.05f;

            _audio = root.AddComponent<AudioPresenter>();
            _audio.library = audioLibrary;
            _audio.musicSourceA = Track(new GameObject("MusicA")).AddComponent<AudioSource>();
            _audio.musicSourceB = Track(new GameObject("MusicB")).AddComponent<AudioSource>();
            _audio.musicCrossfadeSeconds = 0.05f;

            _runner = root.AddComponent<NovelRunner>();
            _runner.dialogue = dialogue;
            _runner.choices = root.AddComponent<ChoiceView>();
            _runner.audio = _audio;
            _runner.backgrounds = _backgrounds;
            var asset = ScriptableObject.CreateInstance<NovelScriptAsset>();
            asset.Source = Story;
            _spawned.Add(asset);
            _runner.script = asset;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in _spawned)
            {
                if (obj != null)
                    Object.Destroy(obj);
            }
            _spawned.Clear();
            if (Directory.Exists(_saveDirectory))
                Directory.Delete(_saveDirectory, recursive: true);
        }

        private T Track<T>(T obj) where T : Object
        {
            _spawned.Add(obj);
            return obj;
        }

        private Sprite CreateSprite()
        {
            var texture = Track(new Texture2D(4, 4));
            return Track(Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.zero));
        }

        // Only waits; the caller asserts afterwards. An assertion thrown inside a nested
        // enumerator is not reported reliably by the PlayMode runner and can hang the run.
        private static IEnumerator WaitUntilOrTimeout(Func<bool> condition, float timeoutSeconds = 5f)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        private bool SpriteShown(SpriteRenderer renderer, Sprite sprite) =>
            renderer.sprite == sprite && renderer.color.a >= 0.999f;

        private bool BackgroundShows(Sprite sprite) =>
            SpriteShown(_backgrounds.backgroundSlotA, sprite) || SpriteShown(_backgrounds.backgroundSlotB, sprite);

        private bool MusicPlays(AudioClip clip) =>
            _audio.musicSourceA.clip == clip || _audio.musicSourceB.clip == clip;

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator Play_SaveMidStory_Restart_Load_RestoresScreenAndContinues()
        {
            _runner.Play();
            yield return WaitUntilOrTimeout(() => _dialogueBox.bodyText.text == "Hi");
            Assert.AreEqual("Hi", _dialogueBox.bodyText.text, "first line");
            yield return WaitUntilOrTimeout(() => SpriteShown(_actor.spriteRenderer, _aliceHappy));
            Assert.IsTrue(SpriteShown(_actor.spriteRenderer, _aliceHappy), "actor fade-in");
            Assert.IsTrue(BackgroundShows(_roomSprite), "background shown while playing");
            Assert.IsTrue(MusicPlays(_theme), "music playing while playing");

            var storage = new JsonSaveStorage(_saveDirectory);
            new SaveLoadController(_runner.Playback, _runner.Context, "playmode", storage).SaveTo("slot");

            // Restart mid-story: the old run must stop and the screen must clear.
            _runner.Prepare();
            _advance.Pending = true;
            for (int i = 0; i < 10; i++)
                yield return null;

            Assert.IsTrue(_advance.Pending, "a stopped run must not consume advance input");
            Assert.AreEqual(string.Empty, _dialogueBox.bodyText.text, "dialogue text cleared");
            Assert.IsNull(_actor.spriteRenderer.sprite, "actor cleared");
            Assert.IsFalse(BackgroundShows(_roomSprite), "background cleared");
            Assert.IsFalse(MusicPlays(_theme), "music stopped");

            // Load the save into the freshly prepared run and resume.
            _advance.Pending = false;
            SaveLoadResult result = new SaveLoadController(_runner.Playback, _runner.Context, "playmode", storage).LoadInto("slot");
            Assert.AreEqual(SaveLoadStatus.Success, result.Status);
            _runner.Begin();

            yield return WaitUntilOrTimeout(() => _dialogueBox.bodyText.text == "Hi");
            Assert.AreEqual("Hi", _dialogueBox.bodyText.text, "saved line after load");
            yield return WaitUntilOrTimeout(() => BackgroundShows(_roomSprite));
            Assert.IsTrue(BackgroundShows(_roomSprite), "background replayed after load");
            Assert.IsTrue(MusicPlays(_theme), "music replayed after load");
            Assert.IsTrue(SpriteShown(_actor.spriteRenderer, _aliceHappy), "actor replayed after load");

            bool finished = false;
            _runner.OnFinished += () => finished = true;
            _advance.Pending = true;
            yield return WaitUntilOrTimeout(() => _dialogueBox.bodyText.text == "Second line");
            Assert.AreEqual("Second line", _dialogueBox.bodyText.text, "story continues after load");
            _advance.Pending = true;
            yield return WaitUntilOrTimeout(() => finished);
            Assert.IsTrue(finished, "story finishes");
        }
    }
}
