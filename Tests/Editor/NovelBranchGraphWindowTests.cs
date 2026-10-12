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
            // The window writes its layout sidecar on close; remove it by hand because the
            // AssetDatabase treats "*.nfgraph.meta" as an orphan .meta, not as a tracked file.
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
