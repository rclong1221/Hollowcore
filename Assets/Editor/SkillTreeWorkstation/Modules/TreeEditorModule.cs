using System.Collections.Generic;
using DIG.SkillTree;
using UnityEditor;
using UnityEngine;

namespace DIG.Editor.SkillTreeWorkstation.Modules
{
    /// <summary>
    /// EPIC 17.1: Visual node graph editor for skill trees.
    /// Displays tree nodes as colored rectangles with Bezier prerequisite lines.
    /// Supports drag, zoom, pan, right-click context menu.
    /// </summary>
    public class TreeEditorModule : ISkillTreeWorkstationModule
    {
        private SkillTreeDatabaseSO _database;
        private int _selectedTreeIndex;
        private int _selectedNodeIndex = -1;
        private Vector2 _scrollOffset;
        private float _zoom = 1f;
        private bool _isDragging;
        private bool _isPanning;
        private Vector2 _lastMousePos;

        private const float NodeWidth = 160f;
        private const float NodeHeight = 60f;
        private const float GridSpacing = 20f;

        private static readonly Color ColorLocked   = new(0.35f, 0.35f, 0.35f);
        private static readonly Color ColorPassive   = new(0.2f, 0.5f, 0.8f);
        private static readonly Color ColorActive    = new(0.8f, 0.4f, 0.1f);
        private static readonly Color ColorKeystone  = new(0.7f, 0.15f, 0.7f);
        private static readonly Color ColorGateway   = new(0.2f, 0.7f, 0.3f);
        private static readonly Color ColorSelected  = new(1f, 0.85f, 0.2f);

        public void OnGUI()
        {
            EditorGUILayout.BeginVertical();
            DrawToolbar();

            if (_database == null || _database.Trees == null || _database.Trees.Count == 0)
            {
                EditorGUILayout.HelpBox("No SkillTreeDatabase loaded. Create one at Resources/SkillTreeDatabase.", MessageType.Warning);
                if (GUILayout.Button("Create Database", GUILayout.Width(200)))
                    CreateDatabase();
                EditorGUILayout.EndVertical();
                return;
            }

            DrawCanvas();
            EditorGUILayout.EndVertical();
        }

        public void OnSceneGUI(SceneView sceneView) { }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Database field
            var newDb = (SkillTreeDatabaseSO)EditorGUILayout.ObjectField("Database", _database,
                typeof(SkillTreeDatabaseSO), false, GUILayout.Width(350));
            if (newDb != _database)
            {
                _database = newDb;
                _selectedTreeIndex = 0;
                _selectedNodeIndex = -1;
            }

            if (_database == null)
                _database = Resources.Load<SkillTreeDatabaseSO>("SkillTreeDatabase");

            // Tree selector
            if (_database != null && _database.Trees != null && _database.Trees.Count > 0)
            {
                var treeNames = new string[_database.Trees.Count];
                for (int i = 0; i < _database.Trees.Count; i++)
                    treeNames[i] = _database.Trees[i] != null ? _database.Trees[i].TreeName : $"(null {i})";

                int newIdx = EditorGUILayout.Popup("Tree", _selectedTreeIndex, treeNames, GUILayout.Width(250));
                if (newIdx != _selectedTreeIndex)
                {
                    _selectedTreeIndex = newIdx;
                    _selectedNodeIndex = -1;
                }
            }

            GUILayout.FlexibleSpace();

            // Zoom
            EditorGUILayout.LabelField("Zoom:", GUILayout.Width(40));
            _zoom = EditorGUILayout.Slider(_zoom, 0.25f, 2f, GUILayout.Width(120));

            if (GUILayout.Button("Reset View", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                _scrollOffset = Vector2.zero;
                _zoom = 1f;
            }

            if (GUILayout.Button("+ Node", EditorStyles.toolbarButton, GUILayout.Width(60)))
                AddNode();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawCanvas()
        {
            var canvasRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUI.BeginClip(canvasRect);

            // Background
            EditorGUI.DrawRect(new Rect(0, 0, canvasRect.width, canvasRect.height), new Color(0.18f, 0.18f, 0.18f));
            DrawGrid(canvasRect, GridSpacing * _zoom, new Color(0.22f, 0.22f, 0.22f));
            DrawGrid(canvasRect, GridSpacing * 5 * _zoom, new Color(0.25f, 0.25f, 0.25f));

            HandleCanvasInput(canvasRect);

            var tree = GetCurrentTree();
            if (tree != null && tree.Nodes != null)
            {
                // Draw prerequisite lines first (behind nodes)
                for (int i = 0; i < tree.Nodes.Length; i++)
                {
                    var node = tree.Nodes[i];
                    var toPos = NodeCenter(node);

                    if (node.Prerequisites != null)
                    {
                        foreach (int prereqId in node.Prerequisites)
                            DrawPrereqLine(tree, prereqId, toPos);
                    }
                }

                // Draw nodes
                for (int i = 0; i < tree.Nodes.Length; i++)
                    DrawNode(i, tree.Nodes[i]);
            }

            GUI.EndClip();
        }

        private void HandleCanvasInput(Rect canvasRect)
        {
            var e = Event.current;
            if (!canvasRect.Contains(e.mousePosition + canvasRect.position) && e.type != EventType.MouseUp)
                return;

            switch (e.type)
            {
                case EventType.ScrollWheel:
                    _zoom = Mathf.Clamp(_zoom - e.delta.y * 0.05f, 0.25f, 2f);
                    e.Use();
                    break;

                case EventType.MouseDown when e.button == 2: // Middle mouse pan
                    _isPanning = true;
                    _lastMousePos = e.mousePosition;
                    e.Use();
                    break;

                case EventType.MouseDrag when _isPanning:
                    _scrollOffset += e.mousePosition - _lastMousePos;
                    _lastMousePos = e.mousePosition;
                    e.Use();
                    break;

                case EventType.MouseUp when _isPanning:
                    _isPanning = false;
                    e.Use();
                    break;

                case EventType.MouseDown when e.button == 1: // Right-click context menu
                    ShowContextMenu(e.mousePosition);
                    e.Use();
                    break;

                case EventType.MouseDown when e.button == 0: // Left-click select/drag
                    var tree = GetCurrentTree();
                    if (tree?.Nodes != null)
                    {
                        int hit = HitTestNode(tree, e.mousePosition);
                        if (hit >= 0)
                        {
                            _selectedNodeIndex = hit;
                            _isDragging = true;
                            _lastMousePos = e.mousePosition;
                        }
                        else
                        {
                            _selectedNodeIndex = -1;
                        }
                    }
                    e.Use();
                    break;

                case EventType.MouseDrag when _isDragging && _selectedNodeIndex >= 0:
                    var dragTree = GetCurrentTree();
                    if (dragTree?.Nodes != null && _selectedNodeIndex < dragTree.Nodes.Length)
                    {
                        var delta = (e.mousePosition - _lastMousePos) / _zoom;
                        dragTree.Nodes[_selectedNodeIndex].EditorPosition += delta;
                        _lastMousePos = e.mousePosition;
                        EditorUtility.SetDirty(dragTree);
                    }
                    e.Use();
                    break;

                case EventType.MouseUp when _isDragging:
                    _isDragging = false;
                    e.Use();
                    break;
            }
        }

        private void DrawNode(int index, SkillNodeDefinition node)
        {
            var pos = NodeScreenPos(node);
            var rect = new Rect(pos.x, pos.y, NodeWidth * _zoom, NodeHeight * _zoom);

            var color = node.NodeType switch
            {
                SkillNodeType.Passive => ColorPassive,
                SkillNodeType.ActiveAbility => ColorActive,
                SkillNodeType.Keystone => ColorKeystone,
                SkillNodeType.Gateway => ColorGateway,
                _ => ColorLocked
            };

            if (index == _selectedNodeIndex)
            {
                var selRect = new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4);
                EditorGUI.DrawRect(selRect, ColorSelected);
            }

            EditorGUI.DrawRect(rect, color);

            var labelStyle = new GUIStyle(EditorStyles.whiteMiniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Max(8, (int)(11 * _zoom)),
                wordWrap = true
            };

            string label = $"[{node.NodeId}] T{node.Tier}\n{node.NodeType}\nCost: {node.PointCost} | Max: {node.MaxRanks}";
            GUI.Label(rect, label, labelStyle);
        }

        private void DrawPrereqLine(SkillTreeSO tree, int prereqId, Vector2 toPos)
        {
            if (prereqId < 0 || tree.Nodes == null) return;

            for (int i = 0; i < tree.Nodes.Length; i++)
            {
                if (tree.Nodes[i].NodeId == prereqId)
                {
                    var fromPos = NodeCenter(tree.Nodes[i]);
                    var fromScreen = WorldToScreen(fromPos);
                    var toScreen = WorldToScreen(toPos);

                    Handles.BeginGUI();
                    Handles.color = new Color(0.6f, 0.8f, 1f, 0.7f);
                    var tangent = Vector2.up * 40 * _zoom;
                    Handles.DrawBezier(
                        new Vector3(fromScreen.x, fromScreen.y),
                        new Vector3(toScreen.x, toScreen.y),
                        new Vector3(fromScreen.x, fromScreen.y) + (Vector3)tangent,
                        new Vector3(toScreen.x, toScreen.y) - (Vector3)tangent,
                        new Color(0.6f, 0.8f, 1f, 0.7f),
                        null,
                        2f);
                    Handles.EndGUI();
                    break;
                }
            }
        }

        private void DrawGrid(Rect canvasRect, float spacing, Color color)
        {
            if (spacing < 5f) return;
            int xCount = Mathf.CeilToInt(canvasRect.width / spacing);
            int yCount = Mathf.CeilToInt(canvasRect.height / spacing);
            float xOff = _scrollOffset.x % spacing;
            float yOff = _scrollOffset.y % spacing;

            Handles.BeginGUI();
            Handles.color = color;
            for (int i = 0; i <= xCount; i++)
            {
                float x = xOff + i * spacing;
                Handles.DrawLine(new Vector3(x, 0), new Vector3(x, canvasRect.height));
            }
            for (int i = 0; i <= yCount; i++)
            {
                float y = yOff + i * spacing;
                Handles.DrawLine(new Vector3(0, y), new Vector3(canvasRect.width, y));
            }
            Handles.EndGUI();
        }

        private Vector2 NodeCenter(SkillNodeDefinition node)
        {
            return new Vector2(node.EditorPosition.x + NodeWidth * 0.5f, node.EditorPosition.y + NodeHeight * 0.5f);
        }

        private Vector2 NodeScreenPos(SkillNodeDefinition node)
        {
            return new Vector2(node.EditorPosition.x * _zoom + _scrollOffset.x, node.EditorPosition.y * _zoom + _scrollOffset.y);
        }

        private Vector2 WorldToScreen(Vector2 world)
        {
            return new Vector2(world.x * _zoom + _scrollOffset.x, world.y * _zoom + _scrollOffset.y);
        }

        private int HitTestNode(SkillTreeSO tree, Vector2 mousePos)
        {
            if (tree.Nodes == null) return -1;
            for (int i = tree.Nodes.Length - 1; i >= 0; i--)
            {
                var pos = NodeScreenPos(tree.Nodes[i]);
                var rect = new Rect(pos.x, pos.y, NodeWidth * _zoom, NodeHeight * _zoom);
                if (rect.Contains(mousePos))
                    return i;
            }
            return -1;
        }

        private void ShowContextMenu(Vector2 mousePos)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Add Passive Node"), false, () => AddNodeAt(mousePos, SkillNodeType.Passive));
            menu.AddItem(new GUIContent("Add Active Ability Node"), false, () => AddNodeAt(mousePos, SkillNodeType.ActiveAbility));
            menu.AddItem(new GUIContent("Add Keystone Node"), false, () => AddNodeAt(mousePos, SkillNodeType.Keystone));
            menu.AddItem(new GUIContent("Add Gateway Node"), false, () => AddNodeAt(mousePos, SkillNodeType.Gateway));
            menu.AddSeparator("");

            if (_selectedNodeIndex >= 0)
                menu.AddItem(new GUIContent("Delete Selected Node"), false, DeleteSelectedNode);
            else
                menu.AddDisabledItem(new GUIContent("Delete Selected Node"));

            menu.ShowAsContext();
        }

        private void AddNode()
        {
            AddNodeAt(new Vector2(200, 200), SkillNodeType.Passive);
        }

        private void AddNodeAt(Vector2 screenPos, SkillNodeType nodeType)
        {
            var tree = GetCurrentTree();
            if (tree == null) return;

            Undo.RecordObject(tree, "Add Skill Node");

            int nextId = 0;
            if (tree.Nodes != null)
            {
                foreach (var n in tree.Nodes)
                    if (n.NodeId >= nextId) nextId = n.NodeId + 1;
            }

            var worldPos = (screenPos - _scrollOffset) / _zoom;

            var newNode = new SkillNodeDefinition
            {
                NodeId = nextId,
                Tier = 0,
                PointCost = 1,
                MaxRanks = 1,
                TierPointsRequired = 0,
                NodeType = nodeType,
                EditorPosition = worldPos
            };

            var list = tree.Nodes != null ? new List<SkillNodeDefinition>(tree.Nodes) : new List<SkillNodeDefinition>();
            list.Add(newNode);
            tree.Nodes = list.ToArray();

            _selectedNodeIndex = list.Count - 1;
            EditorUtility.SetDirty(tree);
        }

        private void DeleteSelectedNode()
        {
            var tree = GetCurrentTree();
            if (tree?.Nodes == null || _selectedNodeIndex < 0 || _selectedNodeIndex >= tree.Nodes.Length)
                return;

            Undo.RecordObject(tree, "Delete Skill Node");

            var list = new List<SkillNodeDefinition>(tree.Nodes);
            int removedId = list[_selectedNodeIndex].NodeId;
            list.RemoveAt(_selectedNodeIndex);

            // Clear any references to deleted node in prerequisites
            for (int i = 0; i < list.Count; i++)
            {
                var n = list[i];
                if (n.Prerequisites != null)
                {
                    var cleaned = new List<int>();
                    foreach (int prereq in n.Prerequisites)
                    {
                        if (prereq != removedId)
                            cleaned.Add(prereq);
                    }
                    n.Prerequisites = cleaned.Count > 0 ? cleaned.ToArray() : null;
                    list[i] = n;
                }
            }

            tree.Nodes = list.ToArray();
            _selectedNodeIndex = -1;
            EditorUtility.SetDirty(tree);
        }

        private SkillTreeSO GetCurrentTree()
        {
            if (_database == null || _database.Trees == null || _database.Trees.Count == 0)
                return null;
            if (_selectedTreeIndex < 0 || _selectedTreeIndex >= _database.Trees.Count)
                return null;
            return _database.Trees[_selectedTreeIndex];
        }

        private void CreateDatabase()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            var db = ScriptableObject.CreateInstance<SkillTreeDatabaseSO>();
            AssetDatabase.CreateAsset(db, "Assets/Resources/SkillTreeDatabase.asset");
            AssetDatabase.SaveAssets();
            _database = db;
            Debug.Log("[SkillTreeWorkstation] Created SkillTreeDatabase at Assets/Resources/SkillTreeDatabase.asset");
        }
    }
}
