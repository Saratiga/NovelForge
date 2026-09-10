using System.IO;
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
    }
}
