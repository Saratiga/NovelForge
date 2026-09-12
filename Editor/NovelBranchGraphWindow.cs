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
        private string _lastSavedText = string.Empty;
        private bool _isDirty;
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
            _lastSavedText = _text;
            _isDirty = false;
            _document = GraphDocumentParser.Parse(_text);
            UpdateTitle();
            RebuildGraphView();
        }

        private void UpdateTitle()
        {
            string fileName = Path.GetFileName(_assetPath);
            titleContent = new GUIContent(_isDirty ? fileName + " *" : fileName);
        }

        private void OnLostFocus() => SaveIfDirty();

        private void OnDestroy() => SaveIfDirty();

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    SaveIfDirty();
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(_assetPath, EditorStyles.miniLabel, GUILayout.Width(300));
            }
        }

        private void RebuildGraphView()
        {
            if (_graphView != null)
                rootVisualElement.Remove(_graphView);
            _graphView = new BranchGraphView(this);
            _graphView.style.flexGrow = 1;
            rootVisualElement.Add(_graphView);
            _graphView.Populate(_document, LoadLayout());
        }

        // Every structural edit (field defocus, drag-to-reconnect, add/delete node) goes
        // through this single path: patch the node list, round-trip it through the formatter
        // and parser so the text and the in-memory model can never drift apart, then rebuild
        // the view from the freshly reparsed document. This is also what lets a user type a
        // "choice\n..." block directly into a node's Body field and have it promoted into real
        // structured choice ports — the reparse is what recognizes it, not the UI code.
        private void ApplyNodes(List<GraphNode> nodes)
        {
            string newText = GraphDocumentFormatter.Serialize(new GraphDocument(nodes));
            _document = GraphDocumentParser.Parse(newText);
            _text = newText;
            _isDirty = _text != _lastSavedText;
            UpdateTitle();
            RebuildGraphView();
        }

        private void ReplaceNode(int index, GraphNode updated)
        {
            var nodes = new List<GraphNode>(_document.Nodes) { [index] = updated };
            ApplyNodes(nodes);
        }

        internal void OnBodyFieldChanged(int nodeIndex, string newBody)
        {
            GraphNode node = _document.Nodes[nodeIndex];
            ReplaceNode(nodeIndex, new GraphNode(node.LabelName, newBody, node.Choices,
                node.TrailingJumpTarget, node.TrailingIsGosub, node.EndsInReturn, node.LeadingComment));
        }

        internal void OnChoiceTextChanged(int nodeIndex, int choiceIndex, string newText)
        {
            GraphNode node = _document.Nodes[nodeIndex];
            var newChoices = new List<ChoiceOption>(node.Choices);
            ChoiceOption old = newChoices[choiceIndex];
            newChoices[choiceIndex] = new ChoiceOption(newText, old.ExplicitId, old.TargetLabel);
            ReplaceNode(nodeIndex, new GraphNode(node.LabelName, node.Body, newChoices,
                node.TrailingJumpTarget, node.TrailingIsGosub, node.EndsInReturn, node.LeadingComment));
        }

        // Renaming does NOT rewrite other nodes' jump/gosub/choice targets that pointed at the
        // old name — they simply become broken references (GraphDocument.GetBrokenReferences,
        // rendered by BranchGraphView as a warning-tinted dangling port), exactly like editing
        // the label name directly in the text editor would. The author fixes them manually or
        // by dragging a port to the renamed node, same as any other broken reference.
        internal void OnLabelChanged(int nodeIndex, string newLabelName)
        {
            GraphNode node = _document.Nodes[nodeIndex];
            if (newLabelName == node.LabelName)
                return;
            ReplaceNode(nodeIndex, new GraphNode(newLabelName, node.Body, node.Choices,
                node.TrailingJumpTarget, node.TrailingIsGosub, node.EndsInReturn, node.LeadingComment));
        }

        internal void OnPortReconnected(BranchGraphNodeView sourceNode, Port sourcePort, BranchGraphNodeView targetNode)
        {
            if (targetNode.NodeIndex >= _document.Nodes.Count)
                return;
            string newTargetLabel = _document.Nodes[targetNode.NodeIndex].LabelName;
            if (newTargetLabel == null)
                return;

            GraphNode node = _document.Nodes[sourceNode.NodeIndex];
            GraphNode updated;
            if (sourcePort == sourceNode.TrailingPort)
            {
                updated = new GraphNode(node.LabelName, node.Body, node.Choices,
                    newTargetLabel, node.TrailingIsGosub, node.EndsInReturn, node.LeadingComment);
            }
            else if (sourcePort == sourceNode.FallThroughPort)
            {
                // Dragging a fall-through edge converts the implicit fall-through into an
                // explicit jump — there is no other way to "grab" a fall-through edge by design.
                updated = new GraphNode(node.LabelName, node.Body, node.Choices,
                    newTargetLabel, false, node.EndsInReturn, node.LeadingComment);
            }
            else
            {
                int choiceIndex = sourceNode.ChoicePorts.FindIndex(cp => cp.Port == sourcePort);
                if (choiceIndex < 0)
                    return;
                var newChoices = new List<ChoiceOption>(node.Choices);
                ChoiceOption old = newChoices[choiceIndex];
                newChoices[choiceIndex] = new ChoiceOption(old.Text, old.ExplicitId, newTargetLabel);
                updated = new GraphNode(node.LabelName, node.Body, newChoices,
                    node.TrailingJumpTarget, node.TrailingIsGosub, node.EndsInReturn, node.LeadingComment);
            }

            ReplaceNode(sourceNode.NodeIndex, updated);
        }

        public void AddNewLabel()
        {
            var existing = new HashSet<string>();
            foreach (GraphNode n in _document.Nodes)
                if (n.LabelName != null)
                    existing.Add(n.LabelName);

            int i = 1;
            string candidate;
            do
            {
                candidate = "new_label_" + i;
                i++;
            } while (existing.Contains(candidate));

            var nodes = new List<GraphNode>(_document.Nodes)
            {
                new GraphNode(candidate, string.Empty, Array.Empty<ChoiceOption>(), null, false, false, null)
            };
            ApplyNodes(nodes);
        }

        public void DeleteNode(int index)
        {
            var nodes = new List<GraphNode>(_document.Nodes);
            if (index < 0 || index >= nodes.Count)
                return;
            nodes.RemoveAt(index);
            ApplyNodes(nodes);
        }

        private void SaveIfDirty()
        {
            SaveLayout();
            if (!_isDirty)
                return;

            if (File.Exists(_assetPath))
            {
                string onDisk = File.ReadAllText(_assetPath);
                if (onDisk != _lastSavedText)
                {
                    bool overwriteDisk = EditorUtility.DisplayDialog(
                        "File changed on disk",
                        $"'{_assetPath}' changed on disk since this graph was opened, and the graph has unsaved changes.",
                        "Keep graph, overwrite disk",
                        "Reload from disk");
                    if (!overwriteDisk)
                    {
                        Load(_assetPath);
                        return;
                    }
                }
            }

            File.WriteAllText(_assetPath, _text);
            AssetDatabase.ImportAsset(_assetPath, ImportAssetOptions.ForceUpdate);
            _lastSavedText = _text;
            _isDirty = false;
            UpdateTitle();
        }

        private string LayoutFilePath =>
            Path.Combine(Path.GetDirectoryName(_assetPath) ?? string.Empty, Path.GetFileNameWithoutExtension(_assetPath) + ".nfgraph.meta");

        private void SaveLayout()
        {
            if (_graphView == null)
                return;

            var layout = new NodeLayoutFile();
            foreach (BranchGraphNodeView nodeView in _graphView.NodeViews)
            {
                string label = _document.Nodes[nodeView.NodeIndex].LabelName;
                if (label == null)
                    continue;
                Rect pos = nodeView.GetPosition();
                layout.nodes.Add(new NodeLayoutEntry { label = label, x = pos.x, y = pos.y });
            }
            File.WriteAllText(LayoutFilePath, JsonUtility.ToJson(layout, true));
        }

        private Dictionary<string, Vector2> LoadLayout()
        {
            var result = new Dictionary<string, Vector2>();
            if (!File.Exists(LayoutFilePath))
                return result;

            try
            {
                var layout = JsonUtility.FromJson<NodeLayoutFile>(File.ReadAllText(LayoutFilePath));
                if (layout?.nodes != null)
                    foreach (NodeLayoutEntry entry in layout.nodes)
                        result[entry.label] = new Vector2(entry.x, entry.y);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"NovelForge: failed to read layout file '{LayoutFilePath}' — using default layout. {e.Message}");
            }
            return result;
        }

        [Serializable]
        private class NodeLayoutEntry
        {
            public string label;
            public float x;
            public float y;
        }

        [Serializable]
        private class NodeLayoutFile
        {
            public List<NodeLayoutEntry> nodes = new();
        }
    }

    internal class BranchGraphView : GraphView
    {
        private readonly NovelBranchGraphWindow _window;
        private readonly EdgeConnectorListener _edgeConnectorListener;

        public IReadOnlyList<BranchGraphNodeView> NodeViews { get; private set; } = Array.Empty<BranchGraphNodeView>();

        public BranchGraphView(NovelBranchGraphWindow window)
        {
            _window = window;
            _edgeConnectorListener = new EdgeConnectorListener(window);

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            Insert(0, new GridBackground());

            RegisterCallback<ContextualMenuPopulateEvent>(evt =>
            {
                if (evt.target == this)
                    evt.menu.AppendAction("Add Label", _ => _window.AddNewLabel());
            });
        }

        public void Populate(GraphDocument document, Dictionary<string, Vector2> layout)
        {
            DeleteElements(graphElements.ToList());

            var nodeViews = new List<BranchGraphNodeView>();
            for (int i = 0; i < document.Nodes.Count; i++)
            {
                GraphNode node = document.Nodes[i];
                bool hasFallThrough = document.GetFallThroughTarget(i) != null;
                var nodeView = new BranchGraphNodeView(i, node, hasFallThrough, _window);

                Vector2 position = node.LabelName != null && layout.TryGetValue(node.LabelName, out Vector2 saved)
                    ? saved
                    : new Vector2(i * 260, 0);
                nodeView.SetPosition(new Rect(position, new Vector2(220, 150)));

                foreach (Port outputPort in nodeView.OutputPorts)
                    outputPort.AddManipulator(new EdgeConnector<Edge>(_edgeConnectorListener));

                AddElement(nodeView);
                nodeViews.Add(nodeView);
            }
            NodeViews = nodeViews;

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

        private class EdgeConnectorListener : IEdgeConnectorListener
        {
            private readonly NovelBranchGraphWindow _window;

            public EdgeConnectorListener(NovelBranchGraphWindow window)
            {
                _window = window;
            }

            public void OnDrop(GraphView graphView, Edge edge)
            {
                if (edge.output?.node is BranchGraphNodeView sourceNode && edge.input?.node is BranchGraphNodeView targetNode)
                    _window.OnPortReconnected(sourceNode, edge.output, targetNode);
            }

            public void OnDropOutsidePort(Edge edge, Vector2 position)
            {
                // Dropping on empty canvas leaves the model unchanged — no edge is added.
            }
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

        public IEnumerable<Port> OutputPorts
        {
            get
            {
                foreach ((Port port, _) in ChoicePorts)
                    yield return port;
                if (TrailingPort != null)
                    yield return TrailingPort;
                if (FallThroughPort != null)
                    yield return FallThroughPort;
            }
        }

        public BranchGraphNodeView(int nodeIndex, GraphNode node, bool hasFallThrough, NovelBranchGraphWindow window)
        {
            NodeIndex = nodeIndex;
            title = node.LabelName ?? "(no label)";

            InputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            InputPort.portName = string.Empty;
            inputContainer.Add(InputPort);

            // The node's `title` bar (set once above) is NOT editable by default GraphView
            // behavior — renaming the label goes through this ordinary TextField instead,
            // the same edit-on-focus-out pattern as the body and choice fields below, rather
            // than relying on uncertain double-click-title-to-rename GraphView plumbing.
            var labelField = new TextField { value = node.LabelName ?? string.Empty };
            labelField.style.unityFontStyleAndWeight = FontStyle.Bold;
            labelField.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (!string.IsNullOrEmpty(labelField.value))
                    window.OnLabelChanged(NodeIndex, labelField.value);
            });
            mainContainer.Add(labelField);

            BodyField = new TextField { multiline = true, value = node.Body };
            BodyField.style.minWidth = 200;
            BodyField.style.minHeight = 60;
            BodyField.RegisterCallback<FocusOutEvent>(_ => window.OnBodyFieldChanged(NodeIndex, BodyField.value));
            mainContainer.Add(BodyField);

            for (int c = 0; c < node.Choices.Count; c++)
            {
                int choiceIndex = c;
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                var textField = new TextField { value = node.Choices[c].Text };
                textField.style.flexGrow = 1;
                textField.RegisterCallback<FocusOutEvent>(_ => window.OnChoiceTextChanged(NodeIndex, choiceIndex, textField.value));
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

            RegisterCallback<ContextualMenuPopulateEvent>(evt =>
            {
                evt.menu.AppendAction("Delete", _ => window.DeleteNode(NodeIndex));
                evt.StopPropagation();
            });

            RefreshExpandedState();
            RefreshPorts();
        }

        public void MarkUnreachable()
        {
            titleContainer.AddToClassList("unreachable-node");
        }
    }
}
