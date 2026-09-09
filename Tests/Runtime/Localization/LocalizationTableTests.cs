using NUnit.Framework;

namespace NovelForge.Runtime.Tests
{
    public class LocalizationTableTests
    {
        [Test]
        public void FromJson_ThenTryGetText_ReturnsMatchingEntry()
        {
            var table = LocalizationTable.FromJson("{\"greet_1\": \"Привет!\"}");

            Assert.IsTrue(table.TryGetText("greet_1", out string text));
            Assert.AreEqual("Привет!", text);
        }

        [Test]
        public void TryGetText_MissingId_ReturnsFalse()
        {
            var table = LocalizationTable.FromJson("{\"greet_1\": \"Привет!\"}");

            Assert.IsFalse(table.TryGetText("missing_id", out string text));
            Assert.IsNull(text);
        }

        [Test]
        public void FromJson_Null_ReturnsEmptyTable()
        {
            var table = LocalizationTable.FromJson(null);

            Assert.IsFalse(table.TryGetText("anything", out _));
        }

        [Test]
        public void FromJson_EmptyString_ReturnsEmptyTable()
        {
            var table = LocalizationTable.FromJson(string.Empty);

            Assert.IsFalse(table.TryGetText("anything", out _));
        }
    }
}
