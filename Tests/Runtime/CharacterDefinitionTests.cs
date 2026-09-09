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
