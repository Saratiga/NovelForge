using System.IO;
using NovelForge.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace NovelForge.Editor.Tests
{
    public class NovelScriptImporterTests
    {
        private const string TestFolder = "Assets/Temp_NovelScriptImporterTests";

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TestFolder))
                AssetDatabase.DeleteAsset(TestFolder);
        }

        private static string WriteScriptFile(string fileName, string source)
        {
            string absoluteFolder = Path.Combine(Application.dataPath, "Temp_NovelScriptImporterTests");
            Directory.CreateDirectory(absoluteFolder);
            File.WriteAllText(Path.Combine(absoluteFolder, fileName), source);
            return $"{TestFolder}/{fileName}";
        }

        [Test]
        public void ValidScript_ImportsCleanlyWithSourcePreserved()
        {
            string assetPath = WriteScriptFile("valid.nfscript", "label start\nAlice: Hello!\n");

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            var asset = AssetDatabase.LoadAssetAtPath<NovelScriptAsset>(assetPath);

            Assert.IsNotNull(asset);
            Assert.AreEqual("label start\nAlice: Hello!\n", asset.Source);
        }

        [Test]
        public void InvalidScript_StillCreatesAssetAndLogsImportError()
        {
            string assetPath = WriteScriptFile("invalid.nfscript", "jump nowhere\n");

            LogAssert.Expect(LogType.Error, "NovelForge: Line 1: Undefined label 'nowhere'.");
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

            var asset = AssetDatabase.LoadAssetAtPath<NovelScriptAsset>(assetPath);

            Assert.IsNotNull(asset);
            Assert.AreEqual("jump nowhere\n", asset.Source);
        }

        [Test]
        public void ChangingTheCommandSet_ReimportsScripts()
        {
            string assetPath = WriteScriptFile("dependency.nfscript", "label start\nunknown_cmd_xyz 1\n");
            LogAssert.Expect(LogType.Error, "NovelForge: Line 2: Unknown command 'unknown_cmd_xyz'.");
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            int importsBefore = NovelScriptImporter.ImportCountForTesting;

            try
            {
                // Simulates a new [NovelCommand] class appearing: the registered command-set hash changes.
                AssetDatabase.RegisterCustomDependency(NovelCommandDependency.Name, Hash128.Compute("changed-" + System.Guid.NewGuid()));
                LogAssert.Expect(LogType.Error, "NovelForge: Line 2: Unknown command 'unknown_cmd_xyz'.");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                Assert.Greater(NovelScriptImporter.ImportCountForTesting, importsBefore);
            }
            finally
            {
                AssetDatabase.RegisterCustomDependency(NovelCommandDependency.Name, NovelCommandDependency.ComputeHash());
                AssetDatabase.DeleteAsset(TestFolder);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }

        [Test]
        public void CommandDependencyHash_ChangesWithTheSetOfNames()
        {
            Assert.AreEqual(NovelCommandDependency.ComputeHash(new[] { "bg", "shake" }), NovelCommandDependency.ComputeHash(new[] { "shake", "bg" }));
            Assert.AreNotEqual(NovelCommandDependency.ComputeHash(new[] { "bg" }), NovelCommandDependency.ComputeHash(new[] { "bg", "shake" }));
        }

        [Test]
        public void ScriptWithAttributedCustomCommand_ImportsWithoutError()
        {
            string assetPath = WriteScriptFile("custom.nfscript", "label start\ntest_editor_cmd 1\n");

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

            LogAssert.NoUnexpectedReceived();
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<NovelScriptAsset>(assetPath));
        }
    }
}
