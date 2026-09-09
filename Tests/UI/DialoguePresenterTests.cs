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
                UnityEngine.Object.DestroyImmediate(go);
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
