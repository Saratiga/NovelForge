using NUnit.Framework;

namespace NovelForge.Runtime.Tests
{
    public class SceneStateTests
    {
        private static StoryContext Run(string source)
        {
            var context = new StoryContext
            {
                Dialogue = new RecordingDialoguePresenter(),
                Backgrounds = new RecordingBackgroundPresenter(),
                Audio = new RecordingAudioPresenter(),
            };
            var playback = new PlaybackController(new ScriptCompiler().Compile(source), context);
            CoroutineTestUtil.RunToCompletion(playback.RunAll());
            return context;
        }

        [Test]
        public void Bg_SetsBackgroundAndClearsCg()
        {
            SceneState scene = Run("cg sunset\nbg room\n").Scene;

            Assert.AreEqual("room", scene.Background);
            Assert.IsNull(scene.Cg);
        }

        [Test]
        public void Cg_SetsCg()
        {
            Assert.AreEqual("sunset", Run("bg room\ncg sunset\n").Scene.Cg);
        }

        [Test]
        public void Music_SetsMusic()
        {
            Assert.AreEqual("theme", Run("music theme\n").Scene.Music);
        }

        [Test]
        public void SayLine_WithEmotion_SetsActorAtPosition()
        {
            ActorState actor = Run("Alice: hi #happy left\n").Scene.Actors["left"];

            Assert.AreEqual("Alice", actor.CharacterId);
            Assert.AreEqual("happy", actor.Emotion);
        }

        [Test]
        public void SayLine_WithoutPosition_UsesEmptyKey()
        {
            ActorState actor = Run("Alice: hi #happy\n").Scene.Actors[""];

            Assert.AreEqual("Alice", actor.CharacterId);
            Assert.AreEqual("happy", actor.Emotion);
        }

        [Test]
        public void SayLine_WithoutEmotion_LeavesActorsUnchanged()
        {
            CollectionAssert.IsEmpty(Run("Alice: hi left\n").Scene.Actors);
        }

        [Test]
        public void CopyFrom_IsDeep()
        {
            var source = new SceneState { Background = "room" };
            source.Actors["left"] = new ActorState { CharacterId = "Alice", Emotion = "happy" };
            var copy = new SceneState();

            copy.CopyFrom(source);
            source.Actors["left"].Emotion = "sad";
            source.Background = "street";

            Assert.AreEqual("room", copy.Background);
            Assert.AreEqual("happy", copy.Actors["left"].Emotion);
        }

        [Test]
        public void CopyFrom_Null_ClearsAll()
        {
            var scene = new SceneState { Background = "room", Cg = "sunset", Music = "theme" };
            scene.Actors["left"] = new ActorState { CharacterId = "Alice", Emotion = "happy" };

            scene.CopyFrom(null);

            Assert.IsNull(scene.Background);
            Assert.IsNull(scene.Cg);
            Assert.IsNull(scene.Music);
            CollectionAssert.IsEmpty(scene.Actors);
        }
    }
}
