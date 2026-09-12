using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NovelForge.Runtime;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace NovelForge.Editor
{
    public class NovelBranchGraphWindow : EditorWindow
    {
        private string _assetPath;
        private string _text = string.Empty;
        private GraphDocument _document;
        private BranchGraphView _graphView;

        [MenuItem("NovelForge/Branch Graph")]
        public static void ShowWindowFromMenu()
        {
            string path = Selection.activeObject is NovelScriptAsset
                ? AssetDatabase.GetAssetPath(Selection.activeObject)
                : EditorUtility.OpenFilePanel("Open .nfscript", Application.dataPath, "nfscript");
            if (string.IsNullOrEmpty(path))
                return;
            Open(path);
        }

        [MenuItem("Assets/Open in Branch Graph", true)]
        public static bool ValidateOpenFromAssetsMenu() => Selection.activeObject is NovelScriptAsset;

        [MenuItem("Assets/Open in Branch Graph")]
        public static void OpenFromAssetsMenu()
        {
            Open(AssetDatabase.GetAssetPath(Selection.activeObject));
        }

        private static void Open(string assetPath)
        {
            // OpenFilePanel returns an absolute OS path; normalize to the "Assets/..."
            // project-relative form File.ReadAllText/WriteAllText and AssetDatabase both
            // accept when the Editor's working directory is the project root (it always is) —
            // the same convention NovelScriptEditorWindow already uses.
            if (assetPath.StartsWith(Application.dataPath, StringComparison.Ordinal))
                assetPath = "Assets" + assetPath.Substring(Application.dataPath.Length);

            var window = GetWindow<NovelBranchGraphWindow>("Branch Graph");
            window.Load(assetPath);
            window.Show();
        }

        private void Load(string assetPath)
        {
            _assetPath = assetPath;
            _text = File.ReadAllText(assetPath);
            _document = GraphDocumentParser.Parse(_text);
            titleContent = new GUIContent(Path.GetFileName(assetPath));
            RebuildGraphView();
        }

        private void RebuildGraphView()
        {
            if (_graphView != null)
                rootVisualElement.Remove(_graphView);
            _graphView = new BranchGraphView();
            _graphView.StretchToParentSize();
            rootVisualElement.Add(_graphView);
            _graphView.Populate(_document);
        }
    }

    internal class BranchGraphView : GraphView
    {
        public BranchGraphView()
        {
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            Insert(0, new GridBackground());
        }

        public void Populate(GraphDocument document)
        {
            DeleteElements(graphElements.ToList());

            var nodeViews = new List<BranchGraphNodeView>();
            for (int i = 0; i < document.Nodes.Count; i++)
            {
                bool hasFallThrough = document.GetFallThroughTarget(i) != null;
                var nodeView = new BranchGraphNodeView(i, document.Nodes[i], hasFallThrough);
                nodeView.SetPosition(new Rect(i * 260, 0, 220, 150));
                AddElement(nodeView);
                nodeViews.Add(nodeView);
            }

            var unreachable = new HashSet<string>(document.GetUnreachableLabels());
            for (int i = 0; i < nodeViews.Count; i++)
            {
                string labelName = document.Nodes[i].LabelName;
                if (labelName != null && unreachable.Contains(labelName))
                    nodeViews[i].MarkUnreachable();

                ConnectPort(nodeViews, document, nodeViews[i].TrailingPort, document.Nodes[i].TrailingJumpTarget, isDashed: false);
                for (int c = 0; c < nodeViews[i].ChoicePorts.Count; c++)
                    ConnectPort(nodeViews, document, nodeViews[i].ChoicePorts[c].Port, document.Nodes[i].Choices[c].TargetLabel, isDashed: false);

                string fallThrough = document.GetFallThroughTarget(i);
                if (fallThrough != null)
                    ConnectPort(nodeViews, document, nodeViews[i].FallThroughPort, fallThrough, isDashed: true);
            }
        }

        private void ConnectPort(List<BranchGraphNodeView> nodeViews, GraphDocument document, Port sourcePort, string targetLabel, bool isDashed)
        {
            if (sourcePort == null || targetLabel == null)
                return;

            int targetIndex = -1;
            for (int i = 0; i < document.Nodes.Count; i++)
            {
                if (document.Nodes[i].LabelName == targetLabel)
                {
                    targetIndex = i;
                    break;
                }
            }

            if (targetIndex < 0)
            {
                sourcePort.AddToClassList("broken-reference");
                return;
            }

            var edge = new Edge { output = sourcePort, input = nodeViews[targetIndex].InputPort };
            sourcePort.Connect(edge);
            nodeViews[targetIndex].InputPort.Connect(edge);
            if (isDashed)
                edge.AddToClassList("fall-through-edge");
            AddElement(edge);
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatible = new List<Port>();
            foreach (Port port in ports.ToList())
            {
                if (port.direction != startPort.direction && port.node != startPort.node)
                    compatible.Add(port);
            }
            return compatible;
        }
    }

    internal class BranchGraphNodeView : Node
    {
        public int NodeIndex { get; }
        public TextField BodyField { get; }
        public Port InputPort { get; }
        public Port TrailingPort { get; }
        public Port FallThroughPort { get; }
        public List<(Port Port, TextField TextField)> ChoicePorts { get; } = new();

        public BranchGraphNodeView(int nodeIndex, GraphNode node, bool hasFallThrough)
        {
            NodeIndex = nodeIndex;
            title = node.LabelName ?? "(no label)";

            InputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            InputPort.portName = string.Empty;
            inputContainer.Add(InputPort);

            BodyField = new TextField { multiline = true, value = node.Body };
            BodyField.style.minWidth = 200;
            BodyField.style.minHeight = 60;
            mainContainer.Add(BodyField);

            foreach (ChoiceOption choice in node.Choices)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                var textField = new TextField { value = choice.Text };
                textField.style.flexGrow = 1;
                var port = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
                port.portName = "->";
                row.Add(textField);
                row.Add(port);
                mainContainer.Add(row);
                ChoicePorts.Add((port, textField));
            }

            if (node.TrailingJumpTarget != null)
            {
                TrailingPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
                TrailingPort.portName = node.TrailingIsGosub ? "gosub" : "jump";
                outputContainer.Add(TrailingPort);
            }

            if (hasFallThrough)
            {
                FallThroughPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
                FallThroughPort.portName = "(fall-through)";
                outputContainer.Add(FallThroughPort);
            }

            RefreshExpandedState();
            RefreshPorts();
        }

        public void MarkUnreachable()
        {
            titleContainer.AddToClassList("unreachable-node");
        }
    }
}
