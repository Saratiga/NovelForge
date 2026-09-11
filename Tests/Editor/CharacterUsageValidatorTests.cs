using System.Collections.Generic;
using NUnit.Framework;
using NovelForge.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovelForge.Editor.Tests
{
    public class CharacterUsageValidatorTests
    {
        private static Sprite CreateSprite()
        {
            var texture = new Texture2D(4, 4);
            return Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.zero);
        }

        // CharacterDefinition's id/poses fields are `internal` and NovelForge.Editor.Tests has no
        // InternalsVisibleTo grant for them (see Global Constraints) — SerializedObject is the only
        // way to populate them from this assembly, and it's exactly the mechanism the production
        // code (CharacterEditorWindow, and this class's own GetDefinedEmotions) uses too.
        private static CharacterDefinition CreateCharacter(string id, params (string emotion, Sprite sprite)[] poses)
        {
            var character = ScriptableObject.CreateInstance<CharacterDefinition>();
            var serializedObject = new SerializedObject(character);
            serializedObject.FindProperty("id").stringValue = id;

            SerializedProperty posesProperty = serializedObject.FindProperty("poses");
            posesProperty.arraySize = poses.Length;
            for (int i = 0; i < poses.Length; i++)
            {
                SerializedProperty element = posesProperty.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("emotion").stringValue = poses[i].emotion;
                element.FindPropertyRelative("sprite").objectReferenceValue = poses[i].sprite;
            }
            serializedObject.ApplyModifiedProperties();
            return character;
        }

        private static NovelScript CreateScript(params Command[] commands)
        {
            return new NovelScript(new List<Command>(commands), new Dictionary<string, int>());
        }

        [Test]
        public void Validate_EmotionUsedAndDefined_AppearsInNeitherList()
        {
            var character = CreateCharacter("alice", ("happy", CreateSprite()));
            var script = CreateScript(new SayLineCommand("alice", "Hi!", "happy", null, "id1"));

            var result = CharacterUsageValidator.Validate(character, new[] { script });

            CollectionAssert.IsEmpty(result.UsedButNotDefined);
            CollectionAssert.IsEmpty(result.DefinedButUnused);
        }

        [Test]
        public void Validate_EmotionUsedWithoutMatchingPose_AppearsInUsedButNotDefined()
        {
            var character = CreateCharacter("alice");
            var script = CreateScript(new SayLineCommand("alice", "Hi!", "sad", null, "id1"));

            var result = CharacterUsageValidator.Validate(character, new[] { script });

            CollectionAssert.AreEqual(new[] { "sad" }, result.UsedButNotDefined);
            CollectionAssert.IsEmpty(result.DefinedButUnused);
        }

        [Test]
        public void Validate_PoseDefinedButNeverUsed_AppearsInDefinedButUnused()
        {
            var character = CreateCharacter("alice", ("happy", CreateSprite()));
            var script = CreateScript(new SayLineCommand("alice", "Hi!", null, null, "id1"));

            var result = CharacterUsageValidator.Validate(character, new[] { script });

            CollectionAssert.IsEmpty(result.UsedButNotDefined);
            CollectionAssert.AreEqual(new[] { "happy" }, result.DefinedButUnused);
        }

        [Test]
        public void Validate_DialogueLinesBelongToAnotherCharacter_AreIgnored()
        {
            var character = CreateCharacter("alice", ("happy", CreateSprite()), ("sad", CreateSprite()));
            var script = CreateScript(new SayLineCommand("bob", "Hi!", "happy", null, "id1"));

            var result = CharacterUsageValidator.Validate(character, new[] { script });

            CollectionAssert.IsEmpty(result.UsedButNotDefined);
            CollectionAssert.AreEqual(new[] { "happy", "sad" }, result.DefinedButUnused);
        }

        [Test]
        public void Validate_PoseWithUnsetSprite_TreatsUsedEmotionAsNotDefined_ButNotAlsoUnused()
        {
            var character = CreateCharacter("alice", ("happy", null));
            var script = CreateScript(new SayLineCommand("alice", "Hi!", "happy", null, "id1"));

            var result = CharacterUsageValidator.Validate(character, new[] { script });

            CollectionAssert.AreEqual(new[] { "happy" }, result.UsedButNotDefined);
            CollectionAssert.IsEmpty(result.DefinedButUnused);
        }

        [Test]
        public void Validate_NoScripts_ReturnsAllDefinedPosesAsUnused()
        {
            var character = CreateCharacter("alice", ("happy", CreateSprite()));

            var result = CharacterUsageValidator.Validate(character, System.Array.Empty<NovelScript>());

            CollectionAssert.IsEmpty(result.UsedButNotDefined);
            CollectionAssert.AreEqual(new[] { "happy" }, result.DefinedButUnused);
        }
    }
}
