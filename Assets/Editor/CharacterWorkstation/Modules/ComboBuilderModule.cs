using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace DIG.Editor.CharacterWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 CW-04: Combo Builder module.
    /// Visual node graph for combo chains, cancel windows, branch conditions.
    /// </summary>
    public class ComboBuilderModule : ICharacterModule
    {
        private Vector2 _scrollPosition;
        private Vector2 _graphScrollPosition;
        private float _graphZoom = 1.0f;
        
        // Combo data
        private string _comboName = "NewCombo";
        private List<ComboNode> _nodes = new List<ComboNode>();
        private List<ComboConnection> _connections = new List<ComboConnection>();
        private int _selectedNodeIndex = -1;
        
        // Drag state
        private bool _isDraggingNode = false;
        private bool _isCreatingConnection = false;
        private int _connectionStartNode = -1;
        private Vector2 _dragOffset;
        
        // Graph settings
        private Rect _graphRect;
        private const float NODE_WIDTH = 150f;
        private const float NODE_HEIGHT = 80f;

        private class ComboNode
        {
            public int Id;
            public string Name;
            public Vector2 Position;
            public AnimationClip Clip;
            public float HitWindowStart = 0.2f;
            public float HitWindowEnd = 0.6f;
            public float CancelWindowStart = 0.7f;
            public float DamageMultiplier = 1.0f;
            public float KnockbackForce = 5f;
            public bool IsStarter = false;
            public bool IsFinisher = false;
            public ComboNodeType Type = ComboNodeType.Normal;
        }

        private class ComboConnection
        {
            public int FromNode;
            public int ToNode;
            public InputCondition Condition;
            public float RequiredTiming = 0f; // 0 = any time in window
        }

        private enum ComboNodeType { Starter, Normal, Branch, Finisher }
        private enum InputCondition { LightAttack, HeavyAttack, Special, DirectionUp, DirectionDown, DirectionForward, Any }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Combo Builder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Build combo chains visually. Connect attack nodes to create branching combo trees.",
                MessageType.Info);
            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            
            // Left panel - node list and properties
            EditorGUILayout.BeginVertical(GUILayout.Width(250));
            DrawNodeList();
            EditorGUILayout.Space(10);
            DrawNodeProperties();
            EditorGUILayout.EndVertical();

            // Right panel - graph view
            EditorGUILayout.BeginVertical();
            DrawGraphToolbar();
            DrawGraphView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawNodeList()
        {
            EditorGUILayout.LabelField("Combo Chain", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Name
            _comboName = EditorGUILayout.TextField("Combo Name", _comboName);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"Nodes ({_nodes.Count})", EditorStyles.miniLabel);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(150));

            for (int i = 0; i < _nodes.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                
                bool selected = i == _selectedNodeIndex;
                Color prevColor = GUI.backgroundColor;
                if (selected) GUI.backgroundColor = Color.cyan;
                
                if (GUILayout.Button(_nodes[i].Name, EditorStyles.miniButton))
                {
                    _selectedNodeIndex = i;
                }
                
                GUI.backgroundColor = prevColor;

                // Type indicator
                string typeLabel = _nodes[i].Type switch
                {
                    ComboNodeType.Starter => "▶",
                    ComboNodeType.Finisher => "★",
                    ComboNodeType.Branch => "◆",
                    _ => "○"
                };
                EditorGUILayout.LabelField(typeLabel, GUILayout.Width(20));

                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    RemoveNode(i);
                    i--;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            // Add node button
            EditorGUILayout.Space(5);
            if (GUILayout.Button("+ Add Attack Node"))
            {
                AddNode();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawNodeProperties()
        {
            EditorGUILayout.LabelField("Node Properties", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (_selectedNodeIndex < 0 || _selectedNodeIndex >= _nodes.Count)
            {
                EditorGUILayout.LabelField("Select a node to edit", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndVertical();
                return;
            }

            var node = _nodes[_selectedNodeIndex];

            node.Name = EditorGUILayout.TextField("Name", node.Name);
            node.Type = (ComboNodeType)EditorGUILayout.EnumPopup("Type", node.Type);
            node.Clip = (AnimationClip)EditorGUILayout.ObjectField("Animation", node.Clip, 
                typeof(AnimationClip), false);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Timing", EditorStyles.miniLabel);
            
            float maxTime = node.Clip != null ? node.Clip.length : 1f;
            
            EditorGUILayout.MinMaxSlider("Hit Window", ref node.HitWindowStart, ref node.HitWindowEnd, 0f, maxTime);
            EditorGUILayout.LabelField($"  {node.HitWindowStart:F2}s - {node.HitWindowEnd:F2}s", EditorStyles.miniLabel);
            
            node.CancelWindowStart = EditorGUILayout.Slider("Cancel Start", node.CancelWindowStart, node.HitWindowEnd, maxTime);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Combat", EditorStyles.miniLabel);
            node.DamageMultiplier = EditorGUILayout.Slider("Damage Mult", node.DamageMultiplier, 0.5f, 3f);
            node.KnockbackForce = EditorGUILayout.Slider("Knockback", node.KnockbackForce, 0f, 20f);

            EditorGUILayout.Space(5);
            
            // Connections from this node
            var outConnections = _connections.Where(c => c.FromNode == node.Id).ToList();
            EditorGUILayout.LabelField($"Branches ({outConnections.Count})", EditorStyles.miniLabel);
            
            foreach (var conn in outConnections)
            {
                var targetNode = _nodes.FirstOrDefault(n => n.Id == conn.ToNode);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"  → {targetNode?.Name ?? "?"}", GUILayout.Width(100));
                conn.Condition = (InputCondition)EditorGUILayout.EnumPopup(conn.Condition, GUILayout.Width(100));
                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    _connections.Remove(conn);
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawGraphToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Auto Layout", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                AutoLayout();
            }

            if (GUILayout.Button("Clear All", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                if (EditorUtility.DisplayDialog("Clear Combo", "Remove all nodes?", "Yes", "No"))
                {
                    _nodes.Clear();
                    _connections.Clear();
                    _selectedNodeIndex = -1;
                }
            }

            GUILayout.FlexibleSpace();

            EditorGUILayout.LabelField("Zoom:", GUILayout.Width(40));
            _graphZoom = EditorGUILayout.Slider(_graphZoom, 0.5f, 2f, GUILayout.Width(100));

            GUILayout.Space(10);

            if (GUILayout.Button("Export", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                ExportCombo();
            }

            if (GUILayout.Button("Import", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                ImportCombo();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawGraphView()
        {
            _graphRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true), GUILayout.MinHeight(300));

            // Background
            EditorGUI.DrawRect(_graphRect, new Color(0.15f, 0.15f, 0.15f));

            // Grid
            DrawGrid(_graphRect);

            // Begin scroll/zoom group
            GUI.BeginGroup(_graphRect);

            // Draw connections first (behind nodes)
            DrawConnections();

            // Draw connection being created
            if (_isCreatingConnection && _connectionStartNode >= 0)
            {
                var startNode = _nodes.FirstOrDefault(n => n.Id == _connectionStartNode);
                if (startNode != null)
                {
                    Vector2 startPos = (startNode.Position + new Vector2(NODE_WIDTH, NODE_HEIGHT / 2)) * _graphZoom;
                    Vector2 endPos = Event.current.mousePosition;
                    DrawBezierConnection(startPos, endPos, Color.yellow);
                }
            }

            // Draw nodes
            for (int i = 0; i < _nodes.Count; i++)
            {
                DrawNode(_nodes[i], i == _selectedNodeIndex);
            }

            GUI.EndGroup();

            // Handle input
            HandleGraphInput();
        }

        private void DrawGrid(Rect rect)
        {
            float gridSize = 20f * _graphZoom;
            
            Handles.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            
            for (float x = rect.x; x < rect.x + rect.width; x += gridSize)
            {
                Handles.DrawLine(new Vector3(x, rect.y), new Vector3(x, rect.y + rect.height));
            }
            
            for (float y = rect.y; y < rect.y + rect.height; y += gridSize)
            {
                Handles.DrawLine(new Vector3(rect.x, y), new Vector3(rect.x + rect.width, y));
            }
        }

        private void DrawNode(ComboNode node, bool selected)
        {
            Rect nodeRect = new Rect(
                node.Position.x * _graphZoom,
                node.Position.y * _graphZoom,
                NODE_WIDTH * _graphZoom,
                NODE_HEIGHT * _graphZoom
            );

            // Node background
            Color bgColor = node.Type switch
            {
                ComboNodeType.Starter => new Color(0.2f, 0.5f, 0.2f),
                ComboNodeType.Finisher => new Color(0.5f, 0.2f, 0.2f),
                ComboNodeType.Branch => new Color(0.2f, 0.2f, 0.5f),
                _ => new Color(0.3f, 0.3f, 0.3f)
            };
            
            if (selected)
            {
                bgColor = Color.Lerp(bgColor, Color.cyan, 0.3f);
            }
            
            EditorGUI.DrawRect(nodeRect, bgColor);

            // Border
            Color borderColor = selected ? Color.cyan : Color.gray;
            DrawRectBorder(nodeRect, borderColor, 2);

            // Node content
            GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                fontSize = Mathf.RoundToInt(11 * _graphZoom)
            };

            GUI.Label(new Rect(nodeRect.x, nodeRect.y + 5 * _graphZoom, nodeRect.width, 20 * _graphZoom), 
                node.Name, labelStyle);

            // Info
            GUIStyle infoStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.gray },
                fontSize = Mathf.RoundToInt(9 * _graphZoom)
            };

            string info = node.Clip != null ? node.Clip.name : "No Clip";
            GUI.Label(new Rect(nodeRect.x, nodeRect.y + 25 * _graphZoom, nodeRect.width, 15 * _graphZoom), 
                info, infoStyle);

            GUI.Label(new Rect(nodeRect.x, nodeRect.y + 40 * _graphZoom, nodeRect.width, 15 * _graphZoom), 
                $"DMG: {node.DamageMultiplier:F1}x", infoStyle);

            // Connection points
            float connSize = 10 * _graphZoom;
            
            // Input (left)
            Rect inputRect = new Rect(nodeRect.x - connSize / 2, nodeRect.center.y - connSize / 2, connSize, connSize);
            EditorGUI.DrawRect(inputRect, Color.green);
            
            // Output (right)
            Rect outputRect = new Rect(nodeRect.xMax - connSize / 2, nodeRect.center.y - connSize / 2, connSize, connSize);
            EditorGUI.DrawRect(outputRect, Color.red);
        }

        private void DrawConnections()
        {
            foreach (var conn in _connections)
            {
                var fromNode = _nodes.FirstOrDefault(n => n.Id == conn.FromNode);
                var toNode = _nodes.FirstOrDefault(n => n.Id == conn.ToNode);

                if (fromNode == null || toNode == null) continue;

                Vector2 start = (fromNode.Position + new Vector2(NODE_WIDTH, NODE_HEIGHT / 2)) * _graphZoom;
                Vector2 end = (toNode.Position + new Vector2(0, NODE_HEIGHT / 2)) * _graphZoom;

                Color lineColor = conn.Condition switch
                {
                    InputCondition.LightAttack => Color.white,
                    InputCondition.HeavyAttack => Color.red,
                    InputCondition.Special => Color.magenta,
                    _ => Color.gray
                };

                DrawBezierConnection(start, end, lineColor);

                // Draw condition label
                Vector2 midPoint = (start + end) / 2;
                GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = lineColor },
                    fontSize = Mathf.RoundToInt(9 * _graphZoom)
                };
                GUI.Label(new Rect(midPoint.x - 30, midPoint.y - 8, 60, 16), 
                    conn.Condition.ToString(), labelStyle);
            }
        }

        private void DrawBezierConnection(Vector2 start, Vector2 end, Color color)
        {
            Vector2 startTangent = start + Vector2.right * 50 * _graphZoom;
            Vector2 endTangent = end + Vector2.left * 50 * _graphZoom;

            Handles.DrawBezier(start, end, startTangent, endTangent, color, null, 3f);
        }

        private void DrawRectBorder(Rect rect, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private void HandleGraphInput()
        {
            Event e = Event.current;
            Vector2 mousePos = e.mousePosition;

            if (!_graphRect.Contains(mousePos + _graphRect.position)) return;

            Vector2 localMousePos = mousePos;

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button == 0)
                    {
                        // Check if clicked on a node
                        int clickedNode = GetNodeAtPosition(localMousePos);
                        if (clickedNode >= 0)
                        {
                            _selectedNodeIndex = clickedNode;
                            _isDraggingNode = true;
                            _dragOffset = _nodes[clickedNode].Position * _graphZoom - localMousePos;
                            e.Use();
                        }
                    }
                    else if (e.button == 1)
                    {
                        // Right click - start connection or context menu
                        int clickedNode = GetNodeAtPosition(localMousePos);
                        if (clickedNode >= 0)
                        {
                            _isCreatingConnection = true;
                            _connectionStartNode = _nodes[clickedNode].Id;
                            e.Use();
                        }
                    }
                    break;

                case EventType.MouseUp:
                    if (_isDraggingNode)
                    {
                        _isDraggingNode = false;
                        e.Use();
                    }
                    if (_isCreatingConnection)
                    {
                        int targetNode = GetNodeAtPosition(localMousePos);
                        if (targetNode >= 0 && _nodes[targetNode].Id != _connectionStartNode)
                        {
                            _connections.Add(new ComboConnection
                            {
                                FromNode = _connectionStartNode,
                                ToNode = _nodes[targetNode].Id,
                                Condition = InputCondition.LightAttack
                            });
                        }
                        _isCreatingConnection = false;
                        _connectionStartNode = -1;
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (_isDraggingNode && _selectedNodeIndex >= 0)
                    {
                        _nodes[_selectedNodeIndex].Position = (localMousePos + _dragOffset) / _graphZoom;
                        e.Use();
                    }
                    break;
            }

            if (_isDraggingNode || _isCreatingConnection)
            {
                EditorWindow.focusedWindow?.Repaint();
            }
        }

        private int GetNodeAtPosition(Vector2 pos)
        {
            for (int i = _nodes.Count - 1; i >= 0; i--)
            {
                Rect nodeRect = new Rect(
                    _nodes[i].Position.x * _graphZoom,
                    _nodes[i].Position.y * _graphZoom,
                    NODE_WIDTH * _graphZoom,
                    NODE_HEIGHT * _graphZoom
                );

                if (nodeRect.Contains(pos))
                {
                    return i;
                }
            }
            return -1;
        }

        private void AddNode()
        {
            int newId = _nodes.Count > 0 ? _nodes.Max(n => n.Id) + 1 : 0;
            
            _nodes.Add(new ComboNode
            {
                Id = newId,
                Name = $"Attack_{newId}",
                Position = new Vector2(50 + (_nodes.Count % 4) * 180, 50 + (_nodes.Count / 4) * 120),
                Type = _nodes.Count == 0 ? ComboNodeType.Starter : ComboNodeType.Normal
            });

            _selectedNodeIndex = _nodes.Count - 1;
        }

        private void RemoveNode(int index)
        {
            int nodeId = _nodes[index].Id;
            _connections.RemoveAll(c => c.FromNode == nodeId || c.ToNode == nodeId);
            _nodes.RemoveAt(index);
            
            if (_selectedNodeIndex >= _nodes.Count)
            {
                _selectedNodeIndex = _nodes.Count - 1;
            }
        }

        private void AutoLayout()
        {
            // Simple tree layout
            var starters = _nodes.Where(n => n.Type == ComboNodeType.Starter).ToList();
            if (starters.Count == 0 && _nodes.Count > 0)
            {
                starters.Add(_nodes[0]);
            }

            float y = 50;
            foreach (var starter in starters)
            {
                LayoutBranch(starter, 50, y, 0);
                y += 150;
            }
        }

        private void LayoutBranch(ComboNode node, float x, float y, int depth)
        {
            node.Position = new Vector2(x + depth * 180, y);

            var connections = _connections.Where(c => c.FromNode == node.Id).ToList();
            float childY = y - (connections.Count - 1) * 50;

            foreach (var conn in connections)
            {
                var childNode = _nodes.FirstOrDefault(n => n.Id == conn.ToNode);
                if (childNode != null)
                {
                    LayoutBranch(childNode, x, childY, depth + 1);
                    childY += 100;
                }
            }
        }

        private void ExportCombo()
        {
            var data = new ComboExportData
            {
                Name = _comboName,
                Nodes = _nodes.Select(n => new ComboNodeExport
                {
                    Id = n.Id,
                    Name = n.Name,
                    ClipName = n.Clip != null ? n.Clip.name : "",
                    HitWindowStart = n.HitWindowStart,
                    HitWindowEnd = n.HitWindowEnd,
                    CancelWindowStart = n.CancelWindowStart,
                    DamageMultiplier = n.DamageMultiplier,
                    KnockbackForce = n.KnockbackForce,
                    Type = n.Type.ToString()
                }).ToList(),
                Connections = _connections.Select(c => new ComboConnectionExport
                {
                    FromNode = c.FromNode,
                    ToNode = c.ToNode,
                    Condition = c.Condition.ToString()
                }).ToList()
            };

            string json = JsonUtility.ToJson(data, true);
            string path = EditorUtility.SaveFilePanel("Export Combo", "", _comboName, "json");
            
            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, json);
                Debug.Log($"[ComboBuilder] Exported combo to {path}");
            }
        }

        private void ImportCombo()
        {
            string path = EditorUtility.OpenFilePanel("Import Combo", "", "json");
            
            if (!string.IsNullOrEmpty(path))
            {
                string json = System.IO.File.ReadAllText(path);
                var data = JsonUtility.FromJson<ComboExportData>(json);

                _comboName = data.Name;
                _nodes.Clear();
                _connections.Clear();

                foreach (var nodeData in data.Nodes)
                {
                    _nodes.Add(new ComboNode
                    {
                        Id = nodeData.Id,
                        Name = nodeData.Name,
                        Position = new Vector2(50 + _nodes.Count * 180, 100),
                        HitWindowStart = nodeData.HitWindowStart,
                        HitWindowEnd = nodeData.HitWindowEnd,
                        CancelWindowStart = nodeData.CancelWindowStart,
                        DamageMultiplier = nodeData.DamageMultiplier,
                        KnockbackForce = nodeData.KnockbackForce,
                        Type = System.Enum.TryParse<ComboNodeType>(nodeData.Type, out var t) ? t : ComboNodeType.Normal
                    });
                }

                foreach (var connData in data.Connections)
                {
                    _connections.Add(new ComboConnection
                    {
                        FromNode = connData.FromNode,
                        ToNode = connData.ToNode,
                        Condition = System.Enum.TryParse<InputCondition>(connData.Condition, out var c) ? c : InputCondition.Any
                    });
                }

                AutoLayout();
                Debug.Log($"[ComboBuilder] Imported combo '{_comboName}' with {_nodes.Count} nodes");
            }
        }

        [System.Serializable]
        private class ComboExportData
        {
            public string Name;
            public List<ComboNodeExport> Nodes;
            public List<ComboConnectionExport> Connections;
        }

        [System.Serializable]
        private class ComboNodeExport
        {
            public int Id;
            public string Name;
            public string ClipName;
            public float HitWindowStart;
            public float HitWindowEnd;
            public float CancelWindowStart;
            public float DamageMultiplier;
            public float KnockbackForce;
            public string Type;
        }

        [System.Serializable]
        private class ComboConnectionExport
        {
            public int FromNode;
            public int ToNode;
            public string Condition;
        }
    }
}
