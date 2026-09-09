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

        [Test]
        public void Export_ReturnsSnapshotOfAllSetValues()
        {
            var store = new VariableStore();
            store.Set("relationship", 3);
            store.Set("playerName", "Kai");

            var exported = store.Export();

            Assert.AreEqual(3, exported["relationship"]);
            Assert.AreEqual("Kai", exported["playerName"]);
        }

        [Test]
        public void Export_ReturnsIndependentSnapshot_LaterSetsDoNotAffectIt()
        {
            var store = new VariableStore();
            store.Set("relationship", 3);

            var exported = store.Export();
            store.Set("relationship", 99);

            Assert.AreEqual(3, exported["relationship"]);
        }

        [Test]
        public void Import_ReplacesAllExistingValues()
        {
            var store = new VariableStore();
            store.Set("stale", 1);

            store.Import(new System.Collections.Generic.Dictionary<string, object> { ["relationship"] = 3, ["playerName"] = "Kai" });

            Assert.AreEqual(3, store.GetInt("relationship"));
            Assert.AreEqual("Kai", store.GetString("playerName"));
            Assert.IsFalse(store.TryGet("stale", out _));
        }

        [Test]
        public void Import_ThenExport_RoundTripsSetValues()
        {
            var store = new VariableStore();
            store.Import(new System.Collections.Generic.Dictionary<string, object>
            {
                ["relationship"] = 3,
                ["ratio"] = 1.5f,
                ["metAlice"] = true,
                ["playerName"] = "Kai",
            });

            var exported = store.Export();

            Assert.AreEqual(4, exported.Count);
            Assert.AreEqual(3, exported["relationship"]);
        }
    }
}
