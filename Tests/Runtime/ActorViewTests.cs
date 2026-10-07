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
        public void ShowSprite_SameSpriteAlreadyFullyShown_DoesNotRefade()
        {
            var sprite = CreateSprite();
            var view = CreateView(new FakeDeltaTimeSource { DeltaTime = 1f });
            CoroutineTestUtil.RunToCompletion(view.ShowSprite(sprite));
            view.TimeSource = new FakeDeltaTimeSource { DeltaTime = 0.01f };

            var again = view.ShowSprite(sprite);
            again.MoveNext();

            Assert.AreEqual(1f, view.spriteRenderer.color.a, 0.001f);
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
