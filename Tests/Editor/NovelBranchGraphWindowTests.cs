using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NovelForge.Editor.Tests
{
    public class NovelBranchGraphWindowTests
    {
        private const string TestFolder = "Assets/Temp_NovelBranchGraphWindowTests";
        private NovelBranchGraphWindow _window;

        [TearDown]
        public void TearDown()
        {
            if (_window != null)
                Object.DestroyImmediate(_window);
            // A legacy "*.nfgraph.meta" sidecar is an orphan .meta to the AssetDatabase, not a
            // tracked file, so it has to be removed by hand before deleting the folder.
            string folderOnDisk = Path.Combine(Application.dataPath, "Temp_NovelBranchGraphWindowTests");
            string layoutSidecar = Path.Combine(folderOnDisk, "graph.nfgraph.meta");
            if (File.Exists(layoutSidecar))
                File.Delete(layoutSidecar);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (AssetDatabase.IsValidFolder(TestFolder))
                AssetDatabase.DeleteAsset(TestFolder);
        }

        private NovelBranchGraphWindow OpenOn(string source)
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Temp_NovelBranchGraphWindowTests"));
            string path = $"{TestFolder}/graph.nfscript";
            File.WriteAllText(path, source);
            _window = ScriptableObject.CreateInstance<NovelBranchGraphWindow>();
            _window.Load(path);
            return _window;
        }

        private static string SidecarPath(string fileName) =>
            Path.Combine(Application.dataPath, "Temp_NovelBranchGraphWindowTests", fileName);

        [Test]
        public void ClosingTheWindow_WritesLayoutAsJsonSidecar_NotAMetaFile()
        {
            OpenOn("label start\nAlice: hi\nreturn\n");

            Object.DestroyImmediate(_window);
            _window = null;

            Assert.IsTrue(File.Exists(SidecarPath("graph.nfgraph.json")));
            Assert.IsFalse(File.Exists(SidecarPath("graph.nfgraph.meta")));
        }

        [Test]
        public void LegacyMetaSidecar_IsMovedToJsonOnLoad()
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Temp_NovelBranchGraphWindowTests"));
            File.WriteAllText(SidecarPath("graph.nfgraph.meta"), "{\"nodes\":[{\"label\":\"start\",\"x\":123.0,\"y\":45.0}]}");

            OpenOn("label start\nAlice: hi\nreturn\n");

            Assert.IsFalse(File.Exists(SidecarPath("graph.nfgraph.meta")));
            Assert.IsTrue(File.Exists(SidecarPath("graph.nfgraph.json")));
            StringAssert.Contains("123", File.ReadAllText(SidecarPath("graph.nfgraph.json")));
        }

        [Test]
        public void BodyFieldLosesFocusUnchanged_DoesNotRebuildOrDirty()
        {
            NovelBranchGraphWindow window = OpenOn("label start\nAlice: hi\nreturn\n");
            int rebuilds = window.RebuildCountForTesting;

            window.OnBodyFieldChanged(0, window.Document.Nodes[0].Body);

            Assert.AreEqual(rebuilds, window.RebuildCountForTesting);
            Assert.IsFalse(window.IsDirty);
        }

        [Test]
        public void ChoiceFieldLosesFocusUnchanged_DoesNotRebuildOrDirty()
        {
            NovelBranchGraphWindow window = OpenOn("label start\nchoice\n\"Go\" -> start\n");
            int rebuilds = window.RebuildCountForTesting;

            window.OnChoiceTextChanged(0, 0, "Go");

            Assert.AreEqual(rebuilds, window.RebuildCountForTesting);
            Assert.IsFalse(window.IsDirty);
        }

        [Test]
        public void BodyFieldChanged_RebuildsAndMarksDirty()
        {
            NovelBranchGraphWindow window = OpenOn("label start\nAlice: hi\nreturn\n");
            int rebuilds = window.RebuildCountForTesting;

            window.OnBodyFieldChanged(0, "Alice: hello");

            Assert.AreEqual(rebuilds + 1, window.RebuildCountForTesting);
            Assert.IsTrue(window.IsDirty);
        }
    }
}
