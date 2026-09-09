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
