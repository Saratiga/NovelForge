using NUnit.Framework;

namespace NovelForge.Editor.Tests
{
    public class DslAutocompleteProviderTests
    {
        [Test]
        public void GetCandidates_FiltersByPartialWordBeforeCursor_PrefixMatchesFirst()
        {
            var commands = new[] { "bg", "background_music", "wait" };

            var result = DslAutocompleteProvider.GetCandidates("b", 1, commands, System.Array.Empty<string>());

            CollectionAssert.AreEqual(new[] { "background_music", "bg" }, result);
        }

        [Test]
        public void GetCandidates_IncludesContainsMatchesAfterPrefixMatches()
        {
            var commands = new[] { "unbg", "bg" };

            var result = DslAutocompleteProvider.GetCandidates("bg", 2, commands, System.Array.Empty<string>());

            CollectionAssert.AreEqual(new[] { "bg", "unbg" }, result);
        }

        [Test]
        public void GetCandidates_ExtractsLabelsFromSourceText_RegardlessOfOtherContent()
        {
            string source = "label greet\njump nowhere_undefined\nlabel farewell\n";

            var result = DslAutocompleteProvider.GetCandidates(source, source.Length, System.Array.Empty<string>(), System.Array.Empty<string>());

            CollectionAssert.AreEqual(new[] { "farewell", "greet" }, result);
        }

        [Test]
        public void GetCandidates_MergesCommandsCharactersAndLabels_DeduplicatingRepeats()
        {
            string source = "label Alice\n";
            var commands = new[] { "bg" };
            var characters = new[] { "Alice", "Bob" };

            var result = DslAutocompleteProvider.GetCandidates(source, 0, commands, characters);

            CollectionAssert.AreEqual(new[] { "Alice", "bg", "Bob" }, result);
        }

        [Test]
        public void GetCandidates_CursorMidWord_ExtractsPartialUpToCursorOnly()
        {
            var commands = new[] { "greet", "growl" };

            var result = DslAutocompleteProvider.GetCandidates("jump gr", 7, commands, System.Array.Empty<string>());

            CollectionAssert.AreEqual(new[] { "greet", "growl" }, result);
        }

        [Test]
        public void GetCandidates_NullOrEmptySource_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => DslAutocompleteProvider.GetCandidates(null, 0, System.Array.Empty<string>(), System.Array.Empty<string>()));
            Assert.DoesNotThrow(() => DslAutocompleteProvider.GetCandidates(string.Empty, 0, null, null));
        }

        [Test]
        public void GetCandidates_WithDefaultRegistryNames_OffersCustomCommand()
        {
            var result = DslAutocompleteProvider.GetCandidates("test_e", 6,
                NovelForge.Runtime.CommandRegistry.CreateDefault().RegisteredNames, System.Array.Empty<string>());

            CollectionAssert.Contains(result, "test_editor_cmd");
        }
    }
}
