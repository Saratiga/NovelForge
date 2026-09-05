using NUnit.Framework;

namespace NovelForge.Runtime.Tests
{
    public class VariableStoreTests
    {
        [Test]
        public void MissingVariable_ReturnsTypeDefaults()
        {
            var store = new VariableStore();

            Assert.AreEqual(0, store.GetInt("missing"));
            Assert.AreEqual(0f, store.GetFloat("missing"));
            Assert.AreEqual(false, store.GetBool("missing"));
            Assert.AreEqual(string.Empty, store.GetString("missing"));
        }

        [Test]
        public void Set_ThenGet_RoundTripsEachType()
        {
            var store = new VariableStore();

            store.Set("relationship", 3);
            store.Set("ratio", 1.5f);
            store.Set("metAlice", true);
            store.Set("playerName", "Kai");

            Assert.AreEqual(3, store.GetInt("relationship"));
            Assert.AreEqual(1.5f, store.GetFloat("ratio"));
            Assert.IsTrue(store.GetBool("metAlice"));
            Assert.AreEqual("Kai", store.GetString("playerName"));
        }

        [Test]
        public void TryGet_ReflectsWhetherVariableWasSet()
        {
            var store = new VariableStore();
            store.Set("x", 1);

            Assert.IsTrue(store.TryGet("x", out _));
            Assert.IsFalse(store.TryGet("y", out _));
        }
    }
}
