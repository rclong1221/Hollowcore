#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DIG.Dialogue.Editor
{
    /// <summary>
    /// EPIC 16.16: Visual node graph editor for dialogue trees.
    /// Colored rects by NodeType, Bezier connections, drag-to-reposition,
    /// right-click context menu, zoom+pan.
    /// </summary>
    public class TreeEditorModule : IDialogueModule
    {
        public event Action<int> OnNodeSelected;

        public DialogueTreeSO SelectedTree { get; private set; }
        private int _selectedNodeIndex = -1;
        private Vector2 _panOffset;
        private float _zoom = 1f;
        private bool _draggingNode;
        private Vector2 _dragStart;

        private static readonly Dictionary<DialogueNodeType, Color> NodeColors = new()
        {
            { DialogueNodeType.Speech, new Color(0.3f, 0.5f, 0.9f) },
            { DialogueNodeType.PlayerChoice, new Color(0.3f, 0.8f, 0.3f) },
            { DialogueNodeType.Condition, new Color(0.9f, 0.8f, 0.2f) },
            { DialogueNodeType.Action, new Color(0.9f, 0.6f, 0.2f) },
            { DialogueNodeType.Random, new Color(0.7f, 0.3f, 0.9f) },
            { DialogueNodeType.Hub, new Color(0.2f, 0.8f, 0.8f) },
            { DialogueNodeType.End, new Color(0.9f, 0.2f, 0.2f) }
        };

        private const float NodeWidth = 160f;
        private const float NodeHeight = 60f;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Dialogue Tree Editor", EditorStyles.boldLabel);

            // Tree selection
            var newTree = (DialogueTreeSO)EditorGUILayout.ObjectField(
                "Dialogue Tree", SelectedTree, typeof(DialogueTreeSO), false);
            if (newTree != SelectedTree)
            {
                SelectedTree = newTree;
                _selectedNodeIndex = -1;
            }

            if (SelectedTree == null)
            {
                EditorGUILayout.HelpBox("Select a DialogueTreeSO to edit.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"TreeId: {SelectedTree.TreeId}  |  Nodes: {SelectedTree.Nodes.Length}  |  Start: {SelectedTree.StartNodeId}",
                EditorStyles.miniLabel);

            // Ensure NodeEditorPositions array matches Nodes
            SyncEditorPositions();

            // Canvas area
            var canvasRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.ExpandWidth(true), GUILayout.Height(500));
            GUI.Box(canvasRect, GUIContent.none, EditorStyles.helpBox);

            // Handle events
            HandleCanvasInput(canvasRect);

            // Draw connections
            DrawConnections(canvasRect);

            // Draw nodes
            DrawNodes(canvasRect);

            // Legend
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            foreach (var kv in NodeColors)
            {
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = kv.Value;
                GUILayout.Box(kv.Key.ToString(), GUILayout.Width(90), GUILayout.Height(18));
                GUI.backgroundColor = prevBg;
            }
            EditorGUILayout.EndHorizontal();
        }

        public void OnSceneGUI(SceneView sceneView) { }

        private void SyncEditorPositions()
        {
            if (SelectedTree.NodeEditorPositions == null ||
                SelectedTree.NodeEditorPositions.Length != SelectedTree.Nodes.Length)
            {
                var positions = new Vector2[SelectedTree.Nodes.Length];
                for (int i = 0; i < positions.Length; i++)
                {
                    if (SelectedTree.NodeEditorPositions != null && i < SelectedTree.NodeEditorPositions.Length)
                        positions[i] = SelectedTree.NodeEditorPositions[i];
                    else
                        positions[i] = new Vector2(100 + (i % 5) * 200, 50 + (i / 5) * 100);
                }
                SelectedTree.NodeEditorPositions = positions;
                EditorUtility.SetDirty(SelectedTree);
            }
        }

        private void HandleCanvasInput(Rect canvasRect)
        {
            var evt = Event.current;
            if (!canvasRect.Contains(evt.mousePosition)) return;

            if (evt.type == EventType.ScrollWheel)
            {
                _zoom = Mathf.Clamp(_zoom - evt.delta.y * 0.05f, 0.3f, 2f);
                evt.Use();
            }

            if (evt.type == EventType.MouseDrag && evt.button == 2)
            {
                _panOffset += evt.delta;
                evt.Use();
            }

            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                Vector2 localMouse = (evt.mousePosition - canvasRect.position - _panOffset) / _zoom;
                for (int i = SelectedTree.Nodes.Length - 1; i >= 0; i--)
                {
                    var nodeRect = new Rect(SelectedTree.NodeEditorPositions[i], new Vector2(NodeWidth, NodeHeight));
                    if (nodeRect.Contains(localMouse))
                    {
                        _selectedNodeIndex = i;
                        _draggingNode = true;
                        _dragStart = localMouse - SelectedTree.NodeEditorPositions[i];
                        OnNodeSelected?.Invoke(i);
                        evt.Use();
                        return;
                    }
                }
                _selectedNodeIndex = -1;
            }

            if (evt.type == EventType.MouseDrag && evt.button == 0 && _draggingNode && _selectedNodeIndex >= 0)
            {
                Vector2 localMouse = (evt.mousePosition - canvasRect.position - _panOffset) / _zoom;
                SelectedTree.NodeEditorPositions[_selectedNodeIndex] = localMouse - _dragStart;
                EditorUtility.SetDirty(SelectedTree);
                evt.Use();
            }

            if (evt.type == EventType.MouseUp && evt.button == 0)
                _draggingNode = false;

            // Right-click context menu
            if (evt.type == EventType.ContextClick && canvasRect.Contains(evt.mousePosition))
            {
                var menu = new GenericMenu();
                Vector2 localPos = (evt.mousePosition - canvasRect.position - _panOffset) / _zoom;

                menu.AddItem(new GUIContent("Add Speech Node"), false, () => AddNode(DialogueNodeType.Speech, localPos));
                menu.AddItem(new GUIContent("Add Choice Node"), false, () => AddNode(DialogueNodeType.PlayerChoice, localPos));
                menu.AddItem(new GUIContent("Add Condition Node"), false, () => AddNode(DialogueNodeType.Condition, localPos));
                menu.AddItem(new GUIContent("Add Action Node"), false, () => AddNode(DialogueNodeType.Action, localPos));
                menu.AddItem(new GUIContent("Add Random Node"), false, () => AddNode(DialogueNodeType.Random, localPos));
                menu.AddItem(new GUIContent("Add Hub Node"), false, () => AddNode(DialogueNodeType.Hub, localPos));
                menu.AddItem(new GUIContent("Add End Node"), false, () => AddNode(DialogueNodeType.End, localPos));

                if (_selectedNodeIndex >= 0)
                {
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Delete Selected"), false, DeleteSelectedNode);
                    menu.AddItem(new GUIContent("Set as Start Node"), false, SetSelectedAsStart);
                }

                menu.ShowAsContext();
                evt.Use();
            }
        }

        private void DrawNodes(Rect canvasRect)
        {
            for (int i = 0; i < SelectedTree.Nodes.Length; i++)
            {
                ref var node = ref SelectedTree.Nodes[i];
                var pos = SelectedTree.NodeEditorPositions[i] * _zoom + _panOffset + canvasRect.position;
                var rect = new Rect(pos, new Vector2(NodeWidth, NodeHeight) * _zoom);

                NodeColors.TryGetValue(node.NodeType, out var color);
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = i == _selectedNodeIndex ? Color.white : color;

                string label = $"[{node.NodeId}] {node.NodeType}";
                if (!string.IsNullOrEmpty(node.Text))
                {
                    string preview = node.Text.Length > 20 ? node.Text.Substring(0, 20) + "..." : node.Text;
                    label += $"\n{preview}";
                }

                GUI.Box(rect, label, EditorStyles.helpBox);
                GUI.backgroundColor = prevBg;

                if (node.NodeId == SelectedTree.StartNodeId)
                {
                    var startRect = new Rect(rect.x, rect.y - 14 * _zoom, rect.width, 14 * _zoom);
                    GUI.Label(startRect, "START", EditorStyles.centeredGreyMiniLabel);
                }
            }
        }

        private void DrawConnections(Rect canvasRect)
        {
            for (int i = 0; i < SelectedTree.Nodes.Length; i++)
            {
                ref var node = ref SelectedTree.Nodes[i];
                var fromPos = SelectedTree.NodeEditorPositions[i] * _zoom + _panOffset + canvasRect.position;
                var fromCenter = fromPos + new Vector2(NodeWidth * _zoom, NodeHeight * _zoom * 0.5f);

                // NextNodeId connection
                if (node.NextNodeId != 0 && node.NodeType != DialogueNodeType.End)
                    DrawConnectionTo(canvasRect, fromCenter, node.NextNodeId, Color.gray);

                // Choice connections
                if (node.Choices != null)
                {
                    for (int c = 0; c < node.Choices.Length; c++)
                        DrawConnectionTo(canvasRect, fromCenter, node.Choices[c].NextNodeId, Color.green);
                }

                // Condition branches
                if (node.NodeType == DialogueNodeType.Condition)
                {
                    DrawConnectionTo(canvasRect, fromCenter, node.TrueNodeId, Color.green);
                    DrawConnectionTo(canvasRect, fromCenter, node.FalseNodeId, Color.red);
                }

                // Random entries
                if (node.RandomEntries != null)
                {
                    for (int r = 0; r < node.RandomEntries.Length; r++)
                        DrawConnectionTo(canvasRect, fromCenter, node.RandomEntries[r].NodeId, new Color(0.7f, 0.3f, 0.9f));
                }
            }
        }

        private void DrawConnectionTo(Rect canvasRect, Vector2 from, int targetNodeId, Color color)
        {
            int targetIndex = SelectedTree.FindNodeIndex(targetNodeId);
            if (targetIndex < 0) return;

            var toPos = SelectedTree.NodeEditorPositions[targetIndex] * _zoom + _panOffset + canvasRect.position;
            var toCenter = toPos + new Vector2(0, NodeHeight * _zoom * 0.5f);

            Handles.DrawBezier(from, toCenter,
                from + Vector2.right * 50f * _zoom,
                toCenter + Vector2.left * 50f * _zoom,
                color, null, 2f);
        }

        private void AddNode(DialogueNodeType type, Vector2 position)
        {
            Undo.RecordObject(SelectedTree, "Add Dialogue Node");

            int maxId = 0;
            for (int i = 0; i < SelectedTree.Nodes.Length; i++)
                maxId = Mathf.Max(maxId, SelectedTree.Nodes[i].NodeId);

            var newNode = new DialogueNode
            {
                NodeId = maxId + 1,
                NodeType = type,
                Text = type == DialogueNodeType.Speech ? "New speech line" : ""
            };

            var nodes = new DialogueNode[SelectedTree.Nodes.Length + 1];
            Array.Copy(SelectedTree.Nodes, nodes, SelectedTree.Nodes.Length);
            nodes[nodes.Length - 1] = newNode;
            SelectedTree.Nodes = nodes;

            var positions = new Vector2[SelectedTree.NodeEditorPositions.Length + 1];
            Array.Copy(SelectedTree.NodeEditorPositions, positions, SelectedTree.NodeEditorPositions.Length);
            positions[positions.Length - 1] = position;
            SelectedTree.NodeEditorPositions = positions;

            EditorUtility.SetDirty(SelectedTree);
        }

        private void DeleteSelectedNode()
        {
            if (_selectedNodeIndex < 0 || _selectedNodeIndex >= SelectedTree.Nodes.Length) return;
            Undo.RecordObject(SelectedTree, "Delete Dialogue Node");

            var nodes = new List<DialogueNode>(SelectedTree.Nodes);
            var positions = new List<Vector2>(SelectedTree.NodeEditorPositions);
            nodes.RemoveAt(_selectedNodeIndex);
            positions.RemoveAt(_selectedNodeIndex);
            SelectedTree.Nodes = nodes.ToArray();
            SelectedTree.NodeEditorPositions = positions.ToArray();
            _selectedNodeIndex = -1;
            EditorUtility.SetDirty(SelectedTree);
        }

        private void SetSelectedAsStart()
        {
            if (_selectedNodeIndex < 0) return;
            Undo.RecordObject(SelectedTree, "Set Start Node");
            SelectedTree.StartNodeId = SelectedTree.Nodes[_selectedNodeIndex].NodeId;
            EditorUtility.SetDirty(SelectedTree);
        }
    }
}
#endif
