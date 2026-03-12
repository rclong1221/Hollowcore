#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using DIG.Roguelite.Zones;

namespace DIG.Roguelite.Editor.Modules
{
    /// <summary>
    /// EPIC 23.7: Run Blueprint Visual Editor module.
    /// Horizontal timeline where each column is a zone index. ZoneDefinitionSO nodes are
    /// dragged from a palette onto columns. Supports difficulty overlay, connection lines,
    /// and a run summary strip.
    /// </summary>
    public class RunBlueprintModule : IRunWorkstationModule
    {
        public string TabName => "Run Blueprint";

        private RogueliteDataContext _context;
        private RunConfigSO _runConfig;
        private ZoneSequenceSO _sequence;
        private Vector2 _scrollPos;
        private bool _showDifficultyOverlay = true;
        private bool _showPalette = true;
        private bool _showSummary = true;
        private int _selectedLayer = -1;
        private int _selectedEntry = -1;
        private ZoneDefinitionSO _draggedZone;

        // Layout constants
        private const float ColumnWidth = 140f;
        private const float ColumnSpacing = 20f;
        private const float NodeHeight = 72f;
        private const float NodeSpacing = 8f;
        private const float PaletteWidth = 160f;
        private const float TimelineTop = 30f;
        private const float DiffBarHeight = 16f;

        // Cached palette groups
        private Dictionary<ZoneType, List<ZoneDefinitionSO>> _paletteGroups;
        private double _paletteBuiltTime;

        // Cached GUIStyles
        private static GUIStyle _loopLabelStyle;
        private static GUIStyle _warningIconStyle;
        private static GUIStyle _miniBarStyle;
        private static bool _blueprintStylesInit;

        public void OnEnable() { }

        public void OnDisable()
        {
            CleanupPaletteCache();
        }

        public void SetContext(RogueliteDataContext context)
        {
            _context = context;
            _paletteGroups = null;
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Run Blueprint", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // Config selection
            EditorGUILayout.BeginHorizontal();
            _runConfig = (RunConfigSO)EditorGUILayout.ObjectField(
                "Run Config", _runConfig, typeof(RunConfigSO), false);
            if (_runConfig != null && _runConfig.ZoneSequence != null && _sequence != _runConfig.ZoneSequence)
                _sequence = _runConfig.ZoneSequence;
            EditorGUILayout.EndHorizontal();

            _sequence = (ZoneSequenceSO)EditorGUILayout.ObjectField(
                "Zone Sequence", _sequence, typeof(ZoneSequenceSO), false);

            if (_runConfig == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a RunConfigSO to visualize and edit its zone blueprint.",
                    MessageType.Info);
                return;
            }

            // Toolbar
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            _showDifficultyOverlay = GUILayout.Toggle(_showDifficultyOverlay, "Difficulty", EditorStyles.toolbarButton, GUILayout.Width(70));
            _showPalette = GUILayout.Toggle(_showPalette, "Palette", EditorStyles.toolbarButton, GUILayout.Width(55));
            _showSummary = GUILayout.Toggle(_showSummary, "Summary", EditorStyles.toolbarButton, GUILayout.Width(65));
            GUILayout.FlexibleSpace();
            if (_sequence != null && GUILayout.Button("+Layer", EditorStyles.toolbarButton, GUILayout.Width(55)))
                AddLayer();
            EditorGUILayout.EndHorizontal();

            // Main layout: palette | timeline
            EditorGUILayout.BeginHorizontal();

            if (_showPalette)
                DrawPalette();

            // Timeline
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandWidth(true));
            DrawTimeline();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndHorizontal();

            // Summary strip
            if (_showSummary)
                DrawSummaryStrip();

            // Handle keyboard
            HandleKeyboard();
        }

        // ==================== Palette ====================

        private void EnsurePaletteCache()
        {
            if (_context == null) return;
            _context.EnsureBuilt();

            if (_paletteGroups != null && _paletteBuiltTime == _context.BuildTimestamp) return;

            _paletteGroups = new Dictionary<ZoneType, List<ZoneDefinitionSO>>();
            foreach (var z in _context.ZoneDefinitions)
            {
                if (z == null) continue;
                if (!_paletteGroups.TryGetValue(z.Type, out var list))
                {
                    list = new List<ZoneDefinitionSO>();
                    _paletteGroups[z.Type] = list;
                }
                list.Add(z);
            }
            _paletteBuiltTime = _context.BuildTimestamp;
        }

        private void CleanupPaletteCache()
        {
            _paletteGroups = null;
        }

        private void DrawPalette()
        {
            EnsurePaletteCache();

            EditorGUILayout.BeginVertical("box", GUILayout.Width(PaletteWidth), GUILayout.ExpandHeight(true));
            EditorGUILayout.LabelField("Zone Palette", EditorStyles.boldLabel);

            if (_paletteGroups == null || _paletteGroups.Count == 0)
            {
                EditorGUILayout.LabelField("No zones found.", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                return;
            }

            foreach (var kvp in _paletteGroups)
            {
                Color typeColor = GetZoneTypeColor(kvp.Key);
                var headerRect = EditorGUILayout.GetControlRect(false, 18);
                EditorGUI.DrawRect(new Rect(headerRect.x, headerRect.y, 4, headerRect.height), typeColor);
                EditorGUI.LabelField(new Rect(headerRect.x + 8, headerRect.y, headerRect.width - 8, headerRect.height),
                    kvp.Key.ToString(), EditorStyles.miniLabel);

                foreach (var zone in kvp.Value)
                {
                    var tileRect = EditorGUILayout.GetControlRect(false, 24);
                    EditorGUI.DrawRect(tileRect, typeColor * 0.3f);
                    string label = !string.IsNullOrEmpty(zone.DisplayName) ? zone.DisplayName : zone.name;
                    if (label.Length > 18) label = label.Substring(0, 16) + "..";
                    EditorGUI.LabelField(tileRect, $" {label}", EditorStyles.miniLabel);

                    // Drag start
                    if (Event.current.type == EventType.MouseDrag && tileRect.Contains(Event.current.mousePosition))
                    {
                        _draggedZone = zone;
                        DragAndDrop.PrepareStartDrag();
                        DragAndDrop.objectReferences = new Object[] { zone };
                        DragAndDrop.StartDrag(zone.name);
                        Event.current.Use();
                    }
                }
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.EndVertical();
        }

        // ==================== Timeline ====================

        private void DrawTimeline()
        {
            if (_sequence == null || _sequence.Layers == null)
            {
                EditorGUILayout.HelpBox("No ZoneSequence assigned or it has no layers.", MessageType.Info);
                return;
            }

            int layerCount = _sequence.Layers.Count;
            float totalWidth = layerCount * (ColumnWidth + ColumnSpacing) + 40f;
            int maxEntries = 1;
            for (int i = 0; i < layerCount; i++)
            {
                int ec = _sequence.Layers[i].Entries?.Count ?? 0;
                if (ec > maxEntries) maxEntries = ec;
            }
            float totalHeight = TimelineTop + maxEntries * (NodeHeight + NodeSpacing) + 60f;
            if (_showDifficultyOverlay) totalHeight += DiffBarHeight + 8f;

            var areaRect = GUILayoutUtility.GetRect(totalWidth, totalHeight);

            // Column headers
            for (int col = 0; col < layerCount; col++)
            {
                float x = areaRect.x + 10f + col * (ColumnWidth + ColumnSpacing);
                var headerRect = new Rect(x, areaRect.y, ColumnWidth, 20);
                var layer = _sequence.Layers[col];
                string headerText = !string.IsNullOrEmpty(layer.LayerName) ? layer.LayerName : $"Zone {col}";
                string modeText = layer.Mode != ZoneSelectionMode.Fixed ? $" ({layer.Mode})" : "";
                EditorGUI.LabelField(headerRect, $"{headerText}{modeText}", EditorStyles.miniLabel);

                // Difficulty overlay
                if (_showDifficultyOverlay && _runConfig != null)
                {
                    float diff = _runConfig.GetDifficultyAtZone(col);
                    float maxDiff = _runConfig.GetDifficultyAtZone(layerCount - 1);
                    if (maxDiff <= 0f) maxDiff = 1f;
                    float pct = Mathf.Clamp01(diff / (maxDiff * 1.2f));

                    var barRect = new Rect(x, areaRect.y + 20, ColumnWidth, DiffBarHeight);
                    Color barColor = Color.Lerp(new Color(0.2f, 0.6f, 0.2f), new Color(0.8f, 0.2f, 0.2f), pct);
                    EditorGUI.DrawRect(barRect, barColor * 0.6f);
                    EditorGUI.LabelField(barRect, $" {diff:F1}x", EditorStyles.miniLabel);
                }

                // Zone nodes
                float nodeY = areaRect.y + TimelineTop + (_showDifficultyOverlay ? DiffBarHeight + 8f : 0f);
                if (layer.Entries != null)
                {
                    float totalWeight = 0f;
                    foreach (var entry in layer.Entries)
                        totalWeight += entry.Weight;
                    if (totalWeight <= 0f) totalWeight = 1f;

                    for (int e = 0; e < layer.Entries.Count; e++)
                    {
                        var entry = layer.Entries[e];
                        var nodeRect = new Rect(x, nodeY, ColumnWidth, NodeHeight);
                        DrawZoneNode(nodeRect, entry, col, e, layer.Mode, totalWeight);
                        nodeY += NodeHeight + NodeSpacing;
                    }
                }

                // Drop target
                var dropRect = new Rect(x, nodeY, ColumnWidth, 24);
                EditorGUI.DrawRect(dropRect, new Color(0.3f, 0.3f, 0.3f, 0.2f));
                EditorGUI.LabelField(dropRect, "+ Drop zone here", EditorStyles.centeredGreyMiniLabel);

                if (Event.current.type == EventType.DragUpdated && dropRect.Contains(Event.current.mousePosition))
                {
                    if (DragAndDrop.objectReferences.Length > 0 && DragAndDrop.objectReferences[0] is ZoneDefinitionSO)
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                }
                else if (Event.current.type == EventType.DragPerform && dropRect.Contains(Event.current.mousePosition))
                {
                    DragAndDrop.AcceptDrag();
                    var droppedZone = DragAndDrop.objectReferences[0] as ZoneDefinitionSO;
                    if (droppedZone != null)
                        AddZoneToLayer(col, droppedZone);
                    Event.current.Use();
                }
            }

            // Looping indicator
            if (_sequence.EnableLooping)
            {
                float loopX = areaRect.x + 10f + layerCount * (ColumnWidth + ColumnSpacing);
                var loopRect = new Rect(loopX, areaRect.y + TimelineTop, 80, NodeHeight);
                EditorGUI.DrawRect(loopRect, new Color(0.5f, 0.3f, 0.7f, 0.3f));
                EnsureBlueprintStyles();
                EditorGUI.LabelField(loopRect, $"Loop\n-> Layer {_sequence.LoopStartIndex}\n{_sequence.LoopDifficultyMultiplier:F1}x",
                    _loopLabelStyle);
            }
        }

        private void DrawZoneNode(Rect rect, ZoneSequenceEntry entry, int layerIndex, int entryIndex,
            ZoneSelectionMode mode, float totalWeight)
        {
            bool isSelected = _selectedLayer == layerIndex && _selectedEntry == entryIndex;
            var zone = entry.Zone;

            // Background
            Color bgColor = zone != null ? GetZoneTypeColor(zone.Type) * 0.35f : new Color(0.4f, 0.4f, 0.4f, 0.3f);
            bgColor.a = 1f;
            EditorGUI.DrawRect(rect, bgColor);

            // Header bar
            if (zone != null)
            {
                var headerBar = new Rect(rect.x, rect.y, rect.width, 4);
                EditorGUI.DrawRect(headerBar, GetZoneTypeColor(zone.Type));
            }

            // Selection border
            if (isSelected)
            {
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), Color.white);
                EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), Color.white);
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), Color.white);
                EditorGUI.DrawRect(new Rect(rect.xMax - 1, rect.y, 1, rect.height), Color.white);
            }

            // Zone name
            string name = zone != null
                ? (!string.IsNullOrEmpty(zone.DisplayName) ? zone.DisplayName : zone.name)
                : "(Empty)";
            if (name.Length > 16) name = name.Substring(0, 14) + "..";
            EditorGUI.LabelField(new Rect(rect.x + 4, rect.y + 6, rect.width - 8, 16), name, EditorStyles.miniLabel);

            // Zone type
            if (zone != null)
            {
                EditorGUI.LabelField(new Rect(rect.x + 4, rect.y + 22, rect.width - 8, 14),
                    zone.Type.ToString(), EditorStyles.miniLabel);
            }

            // Difficulty badge
            if (zone != null)
            {
                string diffBadge = $"{zone.DifficultyMultiplier:F1}x";
                EditorGUI.LabelField(new Rect(rect.x + 4, rect.y + 38, 50, 14), diffBadge, EditorStyles.miniLabel);
            }

            // Weight label (for weighted/choice modes)
            if (mode != ZoneSelectionMode.Fixed && totalWeight > 0)
            {
                float pct = entry.Weight / totalWeight;
                EditorGUI.LabelField(new Rect(rect.x + 4, rect.y + 52, 50, 14), $"{pct:P0}", EditorStyles.miniLabel);
            }

            // Missing pool warning
            if (zone != null && zone.EncounterPool == null &&
                (zone.Type == ZoneType.Combat || zone.Type == ZoneType.Elite || zone.Type == ZoneType.Arena || zone.Type == ZoneType.Boss))
            {
                EnsureBlueprintStyles();
                EditorGUI.LabelField(new Rect(rect.xMax - 20, rect.y + 38, 18, 14), "!", _warningIconStyle);
            }

            // Click handling
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                _selectedLayer = layerIndex;
                _selectedEntry = entryIndex;
                if (zone != null && Event.current.clickCount == 2)
                {
                    EditorGUIUtility.PingObject(zone);
                    Selection.activeObject = zone;
                }
                Event.current.Use();
            }
        }

        // ==================== Summary Strip ====================

        private void DrawSummaryStrip()
        {
            if (_sequence == null || _sequence.Layers == null) return;

            EditorGUILayout.BeginHorizontal("box");

            int totalZones = _sequence.Layers.Count;
            int combatCount = 0, eliteCount = 0, bossCount = 0, shopCount = 0, otherCount = 0;
            int totalCurrency = 0;

            for (int i = 0; i < _sequence.Layers.Count; i++)
            {
                var layer = _sequence.Layers[i];
                if (layer.Entries != null && layer.Entries.Count > 0 && layer.Entries[0].Zone != null)
                {
                    switch (layer.Entries[0].Zone.Type)
                    {
                        case ZoneType.Combat: combatCount++; break;
                        case ZoneType.Elite: eliteCount++; break;
                        case ZoneType.Boss: bossCount++; break;
                        case ZoneType.Shop: shopCount++; break;
                        default: otherCount++; break;
                    }
                }
                if (_runConfig != null)
                    totalCurrency += _runConfig.RunCurrencyPerZoneClear;
            }

            EditorGUILayout.LabelField($"Zones: {totalZones}", EditorStyles.miniLabel, GUILayout.Width(60));
            DrawMiniBar("C", combatCount, new Color(0.8f, 0.3f, 0.3f));
            DrawMiniBar("E", eliteCount, new Color(0.9f, 0.6f, 0.2f));
            DrawMiniBar("B", bossCount, new Color(0.6f, 0.1f, 0.1f));
            DrawMiniBar("$", shopCount, new Color(0.9f, 0.8f, 0.2f));
            if (otherCount > 0) DrawMiniBar("O", otherCount, new Color(0.5f, 0.5f, 0.5f));
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"Est. Currency: {totalCurrency}", EditorStyles.miniLabel, GUILayout.Width(110));
            if (_sequence.EnableLooping)
                EditorGUILayout.LabelField($"Loops from {_sequence.LoopStartIndex}", EditorStyles.miniLabel, GUILayout.Width(90));

            EditorGUILayout.EndHorizontal();
        }

        private static void EnsureBlueprintStyles()
        {
            if (_blueprintStylesInit) return;
            _loopLabelStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
            _warningIconStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = new Color(0.9f, 0.3f, 0.3f) },
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter
            };
            _miniBarStyle = new GUIStyle(EditorStyles.miniLabel);
            _blueprintStylesInit = true;
        }

        private static void DrawMiniBar(string label, int count, Color color)
        {
            if (count <= 0) return;
            EnsureBlueprintStyles();
            _miniBarStyle.normal.textColor = color;
            GUILayout.Label($"{label}:{count}", _miniBarStyle, GUILayout.Width(30));
        }

        // ==================== Actions ====================

        private void AddLayer()
        {
            if (_sequence == null) return;
            Undo.RecordObject(_sequence, "Add Layer");
            _sequence.Layers.Add(new ZoneSequenceLayer
            {
                LayerName = $"Zone {_sequence.Layers.Count}",
                Mode = ZoneSelectionMode.Fixed,
                Entries = new List<ZoneSequenceEntry>()
            });
            EditorUtility.SetDirty(_sequence);
        }

        private void AddZoneToLayer(int layerIndex, ZoneDefinitionSO zone)
        {
            if (_sequence == null || layerIndex < 0 || layerIndex >= _sequence.Layers.Count) return;
            Undo.RecordObject(_sequence, "Add Zone to Layer");
            var layer = _sequence.Layers[layerIndex];
            layer.Entries.Add(new ZoneSequenceEntry { Zone = zone, Weight = 1f });
            EditorUtility.SetDirty(_sequence);
        }

        private void RemoveSelectedEntry()
        {
            if (_sequence == null || _selectedLayer < 0 || _selectedEntry < 0) return;
            if (_selectedLayer >= _sequence.Layers.Count) return;
            var layer = _sequence.Layers[_selectedLayer];
            if (layer.Entries == null || _selectedEntry >= layer.Entries.Count) return;

            Undo.RecordObject(_sequence, "Remove Zone Entry");
            layer.Entries.RemoveAt(_selectedEntry);
            _selectedEntry = -1;
            EditorUtility.SetDirty(_sequence);
        }

        private void HandleKeyboard()
        {
            if (Event.current.type != EventType.KeyDown) return;

            switch (Event.current.keyCode)
            {
                case KeyCode.Delete:
                case KeyCode.Backspace:
                    RemoveSelectedEntry();
                    Event.current.Use();
                    break;

                case KeyCode.D when Event.current.control:
                    if (_selectedLayer >= 0 && _selectedEntry >= 0 && _sequence != null)
                    {
                        var layer = _sequence.Layers[_selectedLayer];
                        if (layer.Entries != null && _selectedEntry < layer.Entries.Count)
                        {
                            Undo.RecordObject(_sequence, "Duplicate Zone Entry");
                            layer.Entries.Add(layer.Entries[_selectedEntry]);
                            EditorUtility.SetDirty(_sequence);
                        }
                    }
                    Event.current.Use();
                    break;

                case KeyCode.Space:
                    _showDifficultyOverlay = !_showDifficultyOverlay;
                    Event.current.Use();
                    break;
            }
        }

        // ==================== Helpers ====================

        private static Color GetZoneTypeColor(ZoneType type)
        {
            return type switch
            {
                ZoneType.Combat => new Color(0.8f, 0.3f, 0.3f),
                ZoneType.Elite => new Color(0.9f, 0.6f, 0.2f),
                ZoneType.Boss => new Color(0.6f, 0.1f, 0.1f),
                ZoneType.Shop => new Color(0.9f, 0.8f, 0.2f),
                ZoneType.Event => new Color(0.7f, 0.3f, 0.8f),
                ZoneType.Rest => new Color(0.3f, 0.7f, 0.3f),
                ZoneType.Treasure => new Color(0.9f, 0.7f, 0.2f),
                ZoneType.Exploration => new Color(0.3f, 0.7f, 0.7f),
                ZoneType.Arena => new Color(0.8f, 0.4f, 0.2f),
                ZoneType.Secret => new Color(0.5f, 0.5f, 0.7f),
                _ => Color.gray
            };
        }
    }
}
#endif
