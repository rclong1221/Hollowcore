#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace DIG.Roguelite.Editor.Modules
{
    /// <summary>
    /// EPIC 23.7: Data Dependency Graph module.
    /// Visual graph showing how roguelite SOs connect. Supports focus mode,
    /// impact preview, orphan highlighting, and type filtering.
    /// Uses Sugiyama-style layered layout computed once on data change.
    /// </summary>
    public class DependencyGraphModule : IRunWorkstationModule
    {
        public string TabName => "Dependency Graph";

        private RogueliteDataContext _context;
        private SODependencyGraph _graph;
        private Vector2 _scrollPos;
        private Vector2 _canvasOffset;
        private float _zoom = 1f;
        private ScriptableObject _selectedNode;
        private ScriptableObject _focusNode;
        private bool _showImpactPreview;

        // Layout cache
        private Dictionary<ScriptableObject, Rect> _nodePositions = new();
        private double _lastLayoutTime;
        private bool _needsLayout = true;

        // Type filters
        private bool _showRunConfigs = true;
        private bool _showZoneSequences = true;
        private bool _showZoneDefs = true;
        private bool _showEncounterPools = true;
        private bool _showSpawnDirectors = true;
        private bool _showRewardPools = true;
        private bool _showRewardDefs = true;
        private bool _showInteractablePools = true;
        private bool _showModifiers = true;
        private bool _showMeta = true;

        // Layout constants
        private const float NodeWidth = 130f;
        private const float NodeHeight = 36f;
        private const float HSpacing = 170f;
        private const float VSpacing = 50f;
        private const float CanvasWidth = 2000f;
        private const float CanvasHeight = 1500f;

        // Cached GUIStyle for node labels (avoid alloc per node per frame)
        private static GUIStyle _nodeLabelStyle;
        private static bool _nodeLabelStyleInit;

        // Cached impact set — only recomputed when focus node changes
        private HashSet<ScriptableObject> _cachedImpactSet;
        private ScriptableObject _cachedImpactNode;

        public void OnEnable() { }
        public void OnDisable() { }

        public void SetContext(RogueliteDataContext context)
        {
            _context = context;
            _needsLayout = true;
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Data Dependency Graph", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (_context == null)
            {
                EditorGUILayout.HelpBox("Data context not available. Re-open Run Workstation.", MessageType.Warning);
                return;
            }

            // Toolbar
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Rebuild", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                _context.Invalidate();
                _context.EnsureBuilt();
                _graph = _context.GetDependencyGraph();
                _needsLayout = true;
            }
            if (GUILayout.Button("Clear Focus", EditorStyles.toolbarButton, GUILayout.Width(75)))
            {
                _focusNode = null;
                _showImpactPreview = false;
            }
            GUILayout.FlexibleSpace();
            _zoom = EditorGUILayout.Slider("Zoom", _zoom, 0.3f, 2f, GUILayout.Width(200));
            EditorGUILayout.EndHorizontal();

            // Type filters
            DrawTypeFilters();

            // Build graph lazily
            if (_graph == null)
            {
                _graph = _context.GetDependencyGraph();
                _needsLayout = true;
            }

            // Compute layout if needed
            if (_needsLayout)
            {
                ComputeLayout();
                _needsLayout = false;
            }

            // Canvas
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            var canvasRect = GUILayoutUtility.GetRect(CanvasWidth * _zoom, CanvasHeight * _zoom);

            // Draw edges
            DrawEdges(canvasRect);

            // Draw nodes
            DrawNodes(canvasRect);

            EditorGUILayout.EndScrollView();

            // Selected node info
            if (_selectedNode != null)
                DrawSelectedNodeInfo();
        }

        private void DrawTypeFilters()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Show:", GUILayout.Width(35));
            _showRunConfigs = GUILayout.Toggle(_showRunConfigs, "Config", EditorStyles.miniButton, GUILayout.Width(55));
            _showZoneSequences = GUILayout.Toggle(_showZoneSequences, "Seq", EditorStyles.miniButton, GUILayout.Width(35));
            _showZoneDefs = GUILayout.Toggle(_showZoneDefs, "Zones", EditorStyles.miniButton, GUILayout.Width(50));
            _showEncounterPools = GUILayout.Toggle(_showEncounterPools, "Enc", EditorStyles.miniButton, GUILayout.Width(35));
            _showSpawnDirectors = GUILayout.Toggle(_showSpawnDirectors, "Dir", EditorStyles.miniButton, GUILayout.Width(30));
            _showRewardPools = GUILayout.Toggle(_showRewardPools, "RwP", EditorStyles.miniButton, GUILayout.Width(35));
            _showRewardDefs = GUILayout.Toggle(_showRewardDefs, "RwD", EditorStyles.miniButton, GUILayout.Width(35));
            _showInteractablePools = GUILayout.Toggle(_showInteractablePools, "Int", EditorStyles.miniButton, GUILayout.Width(30));
            _showModifiers = GUILayout.Toggle(_showModifiers, "Mod", EditorStyles.miniButton, GUILayout.Width(35));
            _showMeta = GUILayout.Toggle(_showMeta, "Meta", EditorStyles.miniButton, GUILayout.Width(40));
            EditorGUILayout.EndHorizontal();
        }

        private void ComputeLayout()
        {
            _nodePositions.Clear();
            if (_graph == null || _graph.AllNodes.Count == 0) return;

            // Categorize nodes into layers (Sugiyama-style)
            var layers = new List<ScriptableObject>[6];
            for (int i = 0; i < layers.Length; i++)
                layers[i] = new List<ScriptableObject>();

            foreach (var node in _graph.AllNodes)
            {
                int layer = GetNodeLayer(node);
                if (layer >= 0 && layer < layers.Length)
                    layers[layer].Add(node);
            }

            // Assign positions
            float startX = 30f;
            for (int layer = 0; layer < layers.Length; layer++)
            {
                float x = startX + layer * HSpacing;
                for (int i = 0; i < layers[layer].Count; i++)
                {
                    float y = 30f + i * VSpacing;
                    _nodePositions[layers[layer][i]] = new Rect(x, y, NodeWidth, NodeHeight);
                }
            }

            _lastLayoutTime = EditorApplication.timeSinceStartup;
        }

        private static int GetNodeLayer(ScriptableObject so)
        {
            if (so is RunConfigSO) return 0;
            if (so is Zones.ZoneSequenceSO) return 1;
            if (so is Zones.ZoneDefinitionSO) return 2;
            if (so is Zones.EncounterPoolSO || so is Zones.SpawnDirectorConfigSO
                || so is Rewards.RewardPoolSO || so is Zones.InteractablePoolSO) return 3;
            if (so is Rewards.RewardDefinitionSO || so is Rewards.RunEventDefinitionSO) return 4;
            if (so is MetaUnlockTreeSO || so is RunModifierPoolSO || so is AscensionDefinitionSO
                || so is RunModifierDefinitionSO) return 5;
            return 3; // Default
        }

        private bool IsNodeVisible(ScriptableObject so)
        {
            if (so is RunConfigSO) return _showRunConfigs;
            if (so is Zones.ZoneSequenceSO) return _showZoneSequences;
            if (so is Zones.ZoneDefinitionSO) return _showZoneDefs;
            if (so is Zones.EncounterPoolSO) return _showEncounterPools;
            if (so is Zones.SpawnDirectorConfigSO) return _showSpawnDirectors;
            if (so is Rewards.RewardPoolSO) return _showRewardPools;
            if (so is Rewards.RewardDefinitionSO) return _showRewardDefs;
            if (so is Zones.InteractablePoolSO) return _showInteractablePools;
            if (so is RunModifierPoolSO || so is RunModifierDefinitionSO || so is AscensionDefinitionSO) return _showModifiers;
            if (so is MetaUnlockTreeSO) return _showMeta;
            return true;
        }

        private static Color GetNodeColor(ScriptableObject so)
        {
            if (so is RunConfigSO) return Color.white;
            if (so is Zones.ZoneSequenceSO) return Color.cyan;
            if (so is Zones.ZoneDefinitionSO) return new Color(0.4f, 0.5f, 0.9f);
            if (so is Zones.EncounterPoolSO) return new Color(0.9f, 0.3f, 0.3f);
            if (so is Zones.SpawnDirectorConfigSO) return new Color(0.9f, 0.6f, 0.2f);
            if (so is Rewards.RewardPoolSO) return new Color(0.9f, 0.8f, 0.2f);
            if (so is Rewards.RewardDefinitionSO) return new Color(0.9f, 0.9f, 0.4f);
            if (so is Zones.InteractablePoolSO) return new Color(0.3f, 0.8f, 0.3f);
            if (so is RunModifierPoolSO || so is RunModifierDefinitionSO) return new Color(0.7f, 0.3f, 0.9f);
            if (so is AscensionDefinitionSO) return new Color(0.5f, 0.2f, 0.7f);
            if (so is MetaUnlockTreeSO) return new Color(0.3f, 0.8f, 0.8f);
            return Color.gray;
        }

        private void DrawEdges(Rect canvas)
        {
            if (_graph?.References == null) return;

            foreach (var kvp in _graph.References)
            {
                var source = kvp.Key;
                if (!IsNodeVisible(source) || !_nodePositions.TryGetValue(source, out var srcRect)) continue;

                foreach (var target in kvp.Value)
                {
                    if (!IsNodeVisible(target) || !_nodePositions.TryGetValue(target, out var tgtRect)) continue;

                    float alpha = 0.3f;
                    Color edgeColor = new Color(0.5f, 0.5f, 0.5f, alpha);

                    if (_focusNode != null)
                    {
                        if (source == _focusNode)
                            edgeColor = new Color(0.3f, 0.5f, 1f, 0.9f); // outgoing = blue
                        else if (target == _focusNode)
                            edgeColor = new Color(0.3f, 0.8f, 0.3f, 0.9f); // incoming = green
                        else
                            edgeColor = new Color(0.3f, 0.3f, 0.3f, 0.1f); // dim
                    }

                    Vector2 start = new Vector2(
                        canvas.x + (srcRect.xMax) * _zoom,
                        canvas.y + (srcRect.y + srcRect.height * 0.5f) * _zoom);
                    Vector2 end = new Vector2(
                        canvas.x + tgtRect.x * _zoom,
                        canvas.y + (tgtRect.y + tgtRect.height * 0.5f) * _zoom);

                    Handles.color = edgeColor;
                    Handles.DrawLine(start, end);
                }
            }
        }

        private void DrawNodes(Rect canvas)
        {
            // Ensure cached label style
            if (!_nodeLabelStyleInit)
            {
                _nodeLabelStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };
                _nodeLabelStyleInit = true;
            }

            // Cached impact set — only recompute when focus node changes
            HashSet<ScriptableObject> impactedSet = null;
            if (_showImpactPreview && _focusNode != null)
            {
                if (_cachedImpactNode != _focusNode)
                {
                    var impacted = _graph.GetImpactedByDeletion(_focusNode);
                    _cachedImpactSet = new HashSet<ScriptableObject>(impacted);
                    _cachedImpactNode = _focusNode;
                }
                impactedSet = _cachedImpactSet;
            }

            // Update font size once per frame (not per node)
            _nodeLabelStyle.fontSize = Mathf.Max(8, (int)(10 * _zoom));

            foreach (var kvp in _nodePositions)
            {
                var so = kvp.Key;
                if (!IsNodeVisible(so)) continue;

                var nodeRect = new Rect(
                    canvas.x + kvp.Value.x * _zoom,
                    canvas.y + kvp.Value.y * _zoom,
                    NodeWidth * _zoom,
                    NodeHeight * _zoom);

                Color bgColor = GetNodeColor(so) * 0.4f;
                bgColor.a = 1f;

                // Focus dimming
                if (_focusNode != null && so != _focusNode)
                {
                    bool isConnected = false;
                    if (_graph.References.TryGetValue(_focusNode, out var fwdRefs) && fwdRefs.Contains(so))
                        isConnected = true;
                    if (_graph.ReferencedBy.TryGetValue(_focusNode, out var revRefs) && revRefs.Contains(so))
                        isConnected = true;
                    if (!isConnected)
                        bgColor *= 0.3f;
                }

                // Impact highlight
                if (impactedSet != null && impactedSet.Contains(so))
                    bgColor = new Color(0.9f, 0.2f, 0.2f, 0.8f);

                EditorGUI.DrawRect(nodeRect, bgColor);

                // Orphan border
                if (!_graph.ReferencedBy.TryGetValue(so, out var refs) || refs.Count == 0)
                {
                    var borderRect = new Rect(nodeRect.x - 1, nodeRect.y - 1, nodeRect.width + 2, nodeRect.height + 2);
                    Handles.color = new Color(0.9f, 0.8f, 0.2f, 0.8f);
                    Handles.DrawWireDisc(borderRect.center, Vector3.forward, 2f);
                    EditorGUI.DrawRect(new Rect(nodeRect.x, nodeRect.y, nodeRect.width, 2), new Color(0.9f, 0.8f, 0.2f));
                }

                // Selection border
                if (so == _selectedNode)
                    EditorGUI.DrawRect(new Rect(nodeRect.x, nodeRect.y, nodeRect.width, 2), Color.white);

                // Label (cached style — no alloc per node)
                string label = so.name;
                if (label.Length > 16) label = label.Substring(0, 14) + "..";
                EditorGUI.LabelField(nodeRect, label, _nodeLabelStyle);

                // Click handling
                if (Event.current.type == EventType.MouseDown && nodeRect.Contains(Event.current.mousePosition))
                {
                    if (Event.current.clickCount == 2)
                    {
                        EditorGUIUtility.PingObject(so);
                        Selection.activeObject = so;
                    }
                    else if (Event.current.button == 1) // Right-click
                    {
                        _focusNode = so;
                        _showImpactPreview = true;
                        _cachedImpactNode = null; // Force recompute
                    }
                    else
                    {
                        _selectedNode = so;
                        _focusNode = so;
                        _showImpactPreview = false;
                    }
                    Event.current.Use();
                }
            }
        }

        private void DrawSelectedNodeInfo()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"Selected: {_selectedNode.name}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Type: {_selectedNode.GetType().Name}", EditorStyles.miniLabel);

            if (_graph.References.TryGetValue(_selectedNode, out var fwdRefs))
                EditorGUILayout.LabelField($"References: {fwdRefs.Count} SOs", EditorStyles.miniLabel);
            if (_graph.ReferencedBy.TryGetValue(_selectedNode, out var revRefs))
                EditorGUILayout.LabelField($"Referenced by: {revRefs.Count} SOs", EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select in Project", EditorStyles.miniButton))
            {
                EditorGUIUtility.PingObject(_selectedNode);
                Selection.activeObject = _selectedNode;
            }
            if (GUILayout.Button("Show Impact", EditorStyles.miniButton))
            {
                _focusNode = _selectedNode;
                _showImpactPreview = true;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }
    }
}
#endif
