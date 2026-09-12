using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NovelForge.Runtime.Tests
{
    public class JsonSaveStorageTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "NovelForgeSaveTests_" + Guid.NewGuid());
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }

        private JsonSaveStorage CreateStorage() => new JsonSaveStorage(_directory);

        [Test]
        public void Save_ThenLoad_ReturnsSuccessWithEquivalentData()
        {
            var storage = CreateStorage();
            var data = new SaveData
            {
                ScriptId = "chapter1",
                PointerIndex = 5,
                CallStack = new[] { 10, 20 },
                Variables = new Dictionary<string, object> { ["relationship"] = 3 },
            };

            storage.Save("slot1", data);
            var result = storage.Load("slot1");

            Assert.AreEqual(SaveLoadStatus.Success, result.Status);
            Assert.AreEqual("chapter1", result.Data.ScriptId);
            Assert.AreEqual(5, result.Data.PointerIndex);
            CollectionAssert.AreEqual(new[] { 10, 20 }, result.Data.CallStack);
            Assert.AreEqual(3, Convert.ToInt32(result.Data.Variables["relationship"]));
        }

        [Test]
        public void Save_SetsCurrentSchemaVersionRegardlessOfCallerValue()
        {
            var storage = CreateStorage();
            var data = new SaveData { SchemaVersion = 999, ScriptId = "chapter1" };

            storage.Save("slot1", data);
            var result = storage.Load("slot1");

            Assert.AreEqual(JsonSaveStorage.CurrentSchemaVersion, result.Data.SchemaVersion);
        }

        [Test]
        public void Load_MissingSlot_ReturnsNotFound()
        {
            var storage = CreateStorage();

            var result = storage.Load("does-not-exist");

            Assert.AreEqual(SaveLoadStatus.NotFound, result.Status);
        }

        [Test]
        public void Load_WrongSchemaVersion_ReturnsIncompatibleWithFoundVersion()
        {
            var storage = CreateStorage();
            Directory.CreateDirectory(_directory);
            File.WriteAllText(Path.Combine(_directory, "slot1.json"), "{\"SchemaVersion\":42,\"ScriptId\":\"x\"}");

            var result = storage.Load("slot1");

            Assert.AreEqual(SaveLoadStatus.Incompatible, result.Status);
            Assert.AreEqual(42, result.FoundSchemaVersion);
        }

        [Test]
        public void Load_CorruptJson_LogsErrorAndReturnsIncompatible()
        {
            var storage = CreateStorage();
            Directory.CreateDirectory(_directory);
            File.WriteAllText(Path.Combine(_directory, "slot1.json"), "{not valid json");

            // The exact exception text is Newtonsoft's own message, not ours — only the
            // fixed prefix this code controls is asserted, hence Regex rather than the
            // exact-string LogAssert.Expect used everywhere else in this project.
            LogAssert.Expect(LogType.Error, new Regex("^NovelForge: save slot 'slot1' is corrupt"));
            var result = storage.Load("slot1");

            Assert.AreEqual(SaveLoadStatus.Incompatible, result.Status);
            Assert.AreEqual(-1, result.FoundSchemaVersion);
        }

        [Test]
        public void Load_EmptyFile_LogsErrorAndReturnsIncompatible()
        {
            var storage = CreateStorage();
            Directory.CreateDirectory(_directory);
            File.WriteAllText(Path.Combine(_directory, "slot1.json"), "");

            LogAssert.Expect(LogType.Error, "NovelForge: save slot 'slot1' is empty or malformed.");
            var result = storage.Load("slot1");

            Assert.AreEqual(SaveLoadStatus.Incompatible, result.Status);
            Assert.AreEqual(-1, result.FoundSchemaVersion);
        }

        [Test]
        public void SlotExists_AfterSave_ReturnsTrue()
        {
            var storage = CreateStorage();
            storage.Save("slot1", new SaveData { ScriptId = "chapter1" });

            Assert.IsTrue(storage.SlotExists("slot1"));
        }

        [Test]
        public void SlotExists_NeverSaved_ReturnsFalse()
        {
            var storage = CreateStorage();

            Assert.IsFalse(storage.SlotExists("does-not-exist"));
        }
    }
}
