#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace DIG.Dialogue.Editor
{
    /// <summary>
    /// EPIC 18.5: Visual graph editor for authoring DialogueTreeSO.
    /// Opens via double-click on DialogueTreeSO or menu DIG > Dialogue > Graph Editor.
    /// NOTE: Undo is supported for the Save operation (Undo.RecordObject on the SO).
    /// Individual graph edits (add/move/delete nodes) do not register Undo steps.
    /// </summary>
    public class DialogueGraphEditorWindow : EditorWindow
    {
        private DialogueTreeSO _tree;
        private DialogueGraphView _graphView;
        private DialogueGraphSerializer _serializer;

        [MenuItem("DIG/Dialogue/Graph Editor")]
        public static void OpenEmpty()
        {
            var window = GetWindow<DialogueGraphEditorWindow>("Dialogue Graph");
            window.minSize = new Vector2(800, 600);
        }

        public static void OpenTree(DialogueTreeSO tree)
        {
            var window = GetWindow<DialogueGraphEditorWindow>("Dialogue Graph");
            window.minSize = new Vector2(800, 600);
            window.LoadTree(tree);
        }

        private void OnEnable()
        {
            // Clear previous UI to prevent toolbar accumulation on domain reload
            rootVisualElement.Clear();

            _serializer = new DialogueGraphSerializer();
            CreateGraphView();
            CreateToolbar();
        }

        private void OnDisable()
        {
            if (_graphView != null)
                rootVisualElement.Remove(_graphView);
        }

        private void CreateGraphView()
        {
            _graphView = new DialogueGraphView(this)
            {
                name = "Dialogue Graph"
            };
            _graphView.StretchToParentSize();
            rootVisualElement.Add(_graphView);
        }

        private void CreateToolbar()
        {
            var toolbar = new Toolbar();

            // Tree selector
            var treeField = new IMGUIContainer(() =>
            {
                EditorGUILayout.BeginHorizontal(GUILayout.Height(20));
                var newTree = (DialogueTreeSO)EditorGUILayout.ObjectField(
                    _tree, typeof(DialogueTreeSO), false, GUILayout.Width(250));
                if (newTree != _tree && newTree != null)
                    LoadTree(newTree);
                EditorGUILayout.EndHorizontal();
            });
            toolbar.Add(treeField);

            // Save button
            var saveButton = new Button(SaveTree) { text = "Save" };
            saveButton.style.width = 60;
            toolbar.Add(saveButton);

            // Auto-layout button
            var layoutButton = new Button(AutoLayout) { text = "Auto-Layout" };
            layoutButton.style.width = 90;
            toolbar.Add(layoutButton);

            // Add node buttons
            var addMenu = new Button(() =>
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Speech Node"), false, () => AddNode(DialogueNodeType.Speech));
                menu.AddItem(new GUIContent("Player Choice Node"), false, () => AddNode(DialogueNodeType.PlayerChoice));
                menu.AddItem(new GUIContent("Condition Node"), false, () => AddNode(DialogueNodeType.Condition));
                menu.AddItem(new GUIContent("Action Node"), false, () => AddNode(DialogueNodeType.Action));
                menu.AddItem(new GUIContent("Random Node"), false, () => AddNode(DialogueNodeType.Random));
                menu.AddItem(new GUIContent("End Node"), false, () => AddNode(DialogueNodeType.End));
                menu.ShowAsContext();
            })
            { text = "+ Add Node" };
            addMenu.style.width = 100;
            toolbar.Add(addMenu);

            // Validate button
            var validateButton = new Button(Validate) { text = "Validate" };
            validateButton.style.width = 70;
            toolbar.Add(validateButton);

            rootVisualElement.Add(toolbar);
            toolbar.BringToFront();
        }

        public void LoadTree(DialogueTreeSO tree)
        {
            _tree = tree;
            _graphView.ClearGraph();

            if (_tree == null) return;

            _serializer.Deserialize(_tree, _graphView);
            titleContent = new GUIContent($"Graph: {_tree.DisplayName ?? _tree.name}");
        }

        private void SaveTree()
        {
            if (_tree == null)
            {
                EditorUtility.DisplayDialog("No Tree", "No DialogueTreeSO loaded.", "OK");
                return;
            }

            Undo.RecordObject(_tree, "Save Dialogue Graph");
            _serializer.Serialize(_graphView, _tree);
            EditorUtility.SetDirty(_tree);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DialogueGraph] Saved {_tree.DisplayName ?? _tree.name}.");
        }

        private void AutoLayout()
        {
            if (_tree == null) return;
            DialogueAutoLayout.Layout(_graphView, _tree.StartNodeId);
        }

        private void AddNode(DialogueNodeType nodeType)
        {
            if (_tree == null) return;

            // Use unified ID generation from the graph view (single authoritative source)
            int newId = _graphView.GetNextNodeId();

            var nodeView = DialogueNodeFactory.CreateNodeView(nodeType, newId);
            nodeView.SetPosition(new Rect(200, 200, 200, 150));
            _graphView.AddElement(nodeView);
        }

        private void Validate()
        {
            if (_tree == null) return;

            // Save first so we validate current state
            SaveTree();
            var results = DialogueValidator.Validate(_tree);

            if (results.Count == 0)
            {
                EditorUtility.DisplayDialog("Validation Passed", "No issues found.", "OK");
                return;
            }

            string msg = $"{results.Count} issue(s) found:\n\n";
            int shown = Math.Min(results.Count, 15);
            for (int i = 0; i < shown; i++)
                msg += $"  [{results[i].Severity}] {results[i].Message}\n";
            if (results.Count > shown)
                msg += $"\n  ...and {results.Count - shown} more.";

            EditorUtility.DisplayDialog("Validation Results", msg, "OK");
        }
    }

    /// <summary>
    /// EPIC 18.5: Custom GraphView for dialogue node editing.
    /// </summary>
    public class DialogueGraphView : GraphView
    {
        private readonly DialogueGraphEditorWindow _window;

        public DialogueGraphView(DialogueGraphEditorWindow window)
        {
            _window = window;

            // Enable standard graph view features
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            // Grid background
            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            // Minimap
            var minimap = new MiniMap { anchored = true };
            minimap.SetPosition(new Rect(10, 30, 200, 140));
            Add(minimap);

            // Style (null-safe — stylesheet may not exist in project)
            var style = Resources.Load<StyleSheet>("DialogueGraphStyle");
            if (style != null)
                styleSheets.Add(style);
        }

        /// <summary>
        /// Returns the next available NodeId by scanning all current graph nodes.
        /// Single authoritative source for ID generation — used by both toolbar
        /// AddNode and context menu AddNodeAtMouse.
        /// </summary>
        public int GetNextNodeId()
        {
            int maxId = 0;
            graphElements.ForEach(element =>
            {
                if (element is DialogueNodeViewBase nodeView && nodeView.NodeId > maxId)
                    maxId = nodeView.NodeId;
            });
            return maxId + 1;
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction("Add Speech Node", _ => AddNodeAtMouse(DialogueNodeType.Speech, evt.localMousePosition));
            evt.menu.AppendAction("Add Choice Node", _ => AddNodeAtMouse(DialogueNodeType.PlayerChoice, evt.localMousePosition));
            evt.menu.AppendAction("Add Condition Node", _ => AddNodeAtMouse(DialogueNodeType.Condition, evt.localMousePosition));
            evt.menu.AppendAction("Add Action Node", _ => AddNodeAtMouse(DialogueNodeType.Action, evt.localMousePosition));
            evt.menu.AppendAction("Add Random Node", _ => AddNodeAtMouse(DialogueNodeType.Random, evt.localMousePosition));
            evt.menu.AppendAction("Add End Node", _ => AddNodeAtMouse(DialogueNodeType.End, evt.localMousePosition));
        }

        private void AddNodeAtMouse(DialogueNodeType type, Vector2 position)
        {
            var node = DialogueNodeFactory.CreateNodeView(type, GetNextNodeId());
            node.SetPosition(new Rect(position, new Vector2(200, 150)));
            AddElement(node);
        }

        public void ClearGraph()
        {
            // Snapshot to list first — graphElements.ForEach(RemoveElement) mutates during iteration
            var elements = new List<GraphElement>();
            graphElements.ForEach(e => elements.Add(e));
            DeleteElements(elements);
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatible = new List<Port>();
            ports.ForEach(port =>
            {
                if (port != startPort && port.node != startPort.node && port.direction != startPort.direction)
                    compatible.Add(port);
            });
            return compatible;
        }
    }

    /// <summary>
    /// Minimal Toolbar element for GraphView editor.
    /// </summary>
    public class Toolbar : VisualElement
    {
        public Toolbar()
        {
            style.flexDirection = FlexDirection.Row;
            style.height = 24;
            style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            style.paddingLeft = 4;
            style.paddingRight = 4;
        }
    }
}
#endif
