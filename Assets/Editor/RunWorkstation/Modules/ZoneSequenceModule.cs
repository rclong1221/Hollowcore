#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using DIG.Roguelite.Zones;

namespace DIG.Roguelite.Editor.Modules
{
    /// <summary>
    /// Zone Sequence Builder module.
    /// Visualizes ZoneSequenceSO layers, zone definitions, difficulty curve,
    /// and spawn director configurations across a run.
    /// </summary>
    public class ZoneSequenceModule : IRunWorkstationModule
    {
        public string TabName => "Zone Sequence";

        private RunConfigSO _runConfig;
        private ZoneSequenceSO _zoneSequence;
        private UnityEditor.Editor _configEditor;
        private UnityEditor.Editor _sequenceEditor;
        private Vector2 _scrollPos;
        private bool _showDifficultyCurve = true;
        private bool _showZoneOverview = true;
        private bool _showSequenceLayers = true;
        private bool _showValidation = true;

        // Cached SerializedObject — avoids allocation per OnGUI
        private SerializedObject _cachedSerializedConfig;
        private RunConfigSO _cachedSerializedConfigTarget;

        // Cached validation results — recomputed on data change
        private int _cachedMissingPools;
        private int _cachedMissingDirectors;
        private ZoneSequenceSO _cachedValidationTarget;
        private int _cachedValidationLayerCount;

        public void OnEnable() { }
        public void OnDisable()
        {
            if (_configEditor != null)
                Object.DestroyImmediate(_configEditor);
            if (_sequenceEditor != null)
                Object.DestroyImmediate(_sequenceEditor);
            _cachedSerializedConfig = null;
            _cachedSerializedConfigTarget = null;
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Zone Sequence Builder", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _runConfig = (RunConfigSO)EditorGUILayout.ObjectField(
                "Run Config", _runConfig, typeof(RunConfigSO), false);

            if (_runConfig == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a RunConfigSO to visualize its zone sequence.\n" +
                    "Create one via Assets > Create > DIG > Roguelite > Run Configuration.",
                    MessageType.Info);
                return;
            }

            // Auto-sync ZoneSequence from RunConfig
            if (_runConfig.ZoneSequence != null && _zoneSequence != _runConfig.ZoneSequence)
                _zoneSequence = _runConfig.ZoneSequence;

            _zoneSequence = (ZoneSequenceSO)EditorGUILayout.ObjectField(
                "Zone Sequence", _zoneSequence, typeof(ZoneSequenceSO), false);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            // Sequence layers
            if (_zoneSequence != null)
            {
                _showSequenceLayers = EditorGUILayout.Foldout(_showSequenceLayers, "Sequence Layers", true);
                if (_showSequenceLayers)
                {
                    EditorGUI.indentLevel++;
                    DrawSequenceLayers();
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.Space(8);
            }

            // Zone overview (from RunConfig difficulty curve)
            _showZoneOverview = EditorGUILayout.Foldout(_showZoneOverview, "Zone Overview", true);
            if (_showZoneOverview)
            {
                EditorGUI.indentLevel++;
                DrawZoneOverview();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(8);

            // Difficulty curve
            _showDifficultyCurve = EditorGUILayout.Foldout(_showDifficultyCurve, "Difficulty Curve", true);
            if (_showDifficultyCurve)
            {
                EditorGUI.indentLevel++;
                DrawDifficultyCurve();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(8);

            // Validation
            _showValidation = EditorGUILayout.Foldout(_showValidation, "Validation", true);
            if (_showValidation)
                DrawValidation();

            // Inline sequence editor
            if (_zoneSequence != null)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Sequence Inspector", EditorStyles.boldLabel);
                if (_sequenceEditor == null || _sequenceEditor.target != _zoneSequence)
                {
                    if (_sequenceEditor != null)
                        Object.DestroyImmediate(_sequenceEditor);
                    _sequenceEditor = UnityEditor.Editor.CreateEditor(_zoneSequence);
                }
                _sequenceEditor.OnInspectorGUI();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawSequenceLayers()
        {
            if (_zoneSequence.Layers == null || _zoneSequence.Layers.Count == 0)
            {
                EditorGUILayout.HelpBox("No layers defined in the zone sequence.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"Layers: {_zoneSequence.Layers.Count}  |  " +
                $"Looping: {(_zoneSequence.EnableLooping ? $"Yes (from layer {_zoneSequence.LoopStartIndex}, {_zoneSequence.LoopDifficultyMultiplier:F1}x)" : "No")}",
                EditorStyles.miniLabel);

            for (int i = 0; i < _zoneSequence.Layers.Count; i++)
            {
                var layer = _zoneSequence.Layers[i];
                string layerName = !string.IsNullOrEmpty(layer.LayerName) ? layer.LayerName : $"Layer {i}";
                string modeStr = layer.Mode.ToString();
                int entryCount = layer.Entries?.Count ?? 0;

                EditorGUILayout.BeginHorizontal("box");
                EditorGUILayout.LabelField($"{i}", GUILayout.Width(20));
                EditorGUILayout.LabelField(layerName, GUILayout.Width(100));
                EditorGUILayout.LabelField(modeStr, GUILayout.Width(100));
                EditorGUILayout.LabelField($"{entryCount} entries", GUILayout.Width(70));

                // Show zone types in entries
                if (layer.Entries != null && layer.Entries.Count > 0)
                {
                    string zones = "";
                    for (int j = 0; j < layer.Entries.Count && j < 3; j++)
                    {
                        var entry = layer.Entries[j];
                        if (entry.Zone != null)
                        {
                            if (j > 0) zones += ", ";
                            zones += $"{entry.Zone.Type}";
                        }
                    }
                    if (layer.Entries.Count > 3) zones += "...";
                    EditorGUILayout.LabelField(zones, EditorStyles.miniLabel);
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawZoneOverview()
        {
            int zoneCount = _runConfig.ZoneCount;
            EditorGUILayout.LabelField($"Zones: {zoneCount}", EditorStyles.miniLabel);

            if (zoneCount <= 0)
            {
                EditorGUILayout.HelpBox("Zone count is zero.", MessageType.Warning);
                return;
            }

            for (int i = 0; i < zoneCount; i++)
            {
                float diff = _runConfig.GetDifficultyAtZone(i);
                int currency = _runConfig.RunCurrencyPerZoneClear;

                // Try to get zone definition from sequence
                string zoneName = $"Zone {i}";
                string zoneType = "";
                string clearMode = "";

                if (_zoneSequence != null && _zoneSequence.Layers != null && i < _zoneSequence.Layers.Count)
                {
                    var layer = _zoneSequence.Layers[i];
                    if (layer.Entries != null && layer.Entries.Count > 0 && layer.Entries[0].Zone != null)
                    {
                        var zoneDef = layer.Entries[0].Zone;
                        zoneName = !string.IsNullOrEmpty(zoneDef.DisplayName) ? zoneDef.DisplayName : zoneName;
                        zoneType = zoneDef.Type.ToString();
                        clearMode = zoneDef.ClearMode.ToString();
                    }
                }

                EditorGUILayout.BeginHorizontal("box");
                EditorGUILayout.LabelField($"{i}: {zoneName}", GUILayout.Width(150));
                if (!string.IsNullOrEmpty(zoneType))
                    EditorGUILayout.LabelField(zoneType, GUILayout.Width(80));
                EditorGUILayout.LabelField($"Diff: {diff:F2}x", GUILayout.Width(80));
                EditorGUILayout.LabelField($"+{currency}c", GUILayout.Width(50));
                if (!string.IsNullOrEmpty(clearMode))
                    EditorGUILayout.LabelField(clearMode, EditorStyles.miniLabel, GUILayout.Width(120));
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawDifficultyCurve()
        {
            if (_runConfig.DifficultyPerZone == null)
            {
                EditorGUILayout.HelpBox("No difficulty curve defined.", MessageType.Info);
                return;
            }

            // Cache SerializedObject — only recreate when target changes
            if (_cachedSerializedConfig == null || _cachedSerializedConfigTarget != _runConfig)
            {
                _cachedSerializedConfig = new SerializedObject(_runConfig);
                _cachedSerializedConfigTarget = _runConfig;
            }

            _cachedSerializedConfig.Update();
            var curveProp = _cachedSerializedConfig.FindProperty("DifficultyPerZone");
            if (curveProp != null)
                EditorGUILayout.PropertyField(curveProp, new GUIContent("Difficulty Curve"), GUILayout.Height(80));
            _cachedSerializedConfig.ApplyModifiedProperties();

            EditorGUILayout.Space(4);
            int zoneCount = _runConfig.ZoneCount;
            if (zoneCount <= 0) return;

            float maxDiff = 0f;
            for (int i = 0; i < zoneCount; i++)
                maxDiff = Mathf.Max(maxDiff, _runConfig.GetDifficultyAtZone(i));
            if (maxDiff <= 0f) maxDiff = 1f;

            for (int i = 0; i < zoneCount; i++)
            {
                float diff = _runConfig.GetDifficultyAtZone(i);
                float pct = diff / (maxDiff * 1.1f);

                var rect = EditorGUILayout.GetControlRect(false, 16);
                var labelRect = new Rect(rect.x, rect.y, 50, rect.height);
                var barRect = new Rect(rect.x + 55, rect.y + 1, (rect.width - 120) * pct, rect.height - 2);
                var valueRect = new Rect(rect.xMax - 60, rect.y, 60, rect.height);

                EditorGUI.LabelField(labelRect, $"Zone {i}");
                Color barColor = Color.Lerp(new Color(0.3f, 0.7f, 0.3f), new Color(0.9f, 0.2f, 0.2f), pct);
                EditorGUI.DrawRect(barRect, barColor);
                EditorGUI.LabelField(valueRect, $"{diff:F2}x", EditorStyles.miniLabel);
            }
        }

        private void RefreshValidationCache()
        {
            int layerCount = _zoneSequence?.Layers?.Count ?? 0;

            // Skip if nothing changed
            if (_cachedValidationTarget == _zoneSequence && _cachedValidationLayerCount == layerCount)
                return;

            _cachedValidationTarget = _zoneSequence;
            _cachedValidationLayerCount = layerCount;
            _cachedMissingPools = 0;
            _cachedMissingDirectors = 0;

            if (_zoneSequence?.Layers == null) return;

            for (int i = 0; i < _zoneSequence.Layers.Count; i++)
            {
                var layer = _zoneSequence.Layers[i];
                if (layer.Entries == null) continue;
                for (int j = 0; j < layer.Entries.Count; j++)
                {
                    var zone = layer.Entries[j].Zone;
                    if (zone == null) continue;
                    if (zone.Type == ZoneType.Combat || zone.Type == ZoneType.Elite || zone.Type == ZoneType.Arena)
                    {
                        if (zone.EncounterPool == null) _cachedMissingPools++;
                        if (zone.SpawnDirectorConfig == null) _cachedMissingDirectors++;
                    }
                }
            }
        }

        private void DrawValidation()
        {
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);

            int zoneCount = _runConfig.ZoneCount;

            if (zoneCount == 0)
            {
                EditorGUILayout.HelpBox("Zone count is zero — run has no content.", MessageType.Error);
                return;
            }

            if (zoneCount < 3)
                EditorGUILayout.HelpBox("Fewer than 3 zones — run may feel too short.", MessageType.Warning);

            if (_runConfig.RunCurrencyPerZoneClear <= 0)
                EditorGUILayout.HelpBox("Currency per zone clear is zero — players earn nothing.", MessageType.Warning);

            float lastDiff = _runConfig.GetDifficultyAtZone(zoneCount - 1);
            if (lastDiff < 1f)
                EditorGUILayout.HelpBox($"Final zone difficulty ({lastDiff:F2}x) is below baseline.", MessageType.Info);

            // Zone sequence validation
            if (_zoneSequence == null)
            {
                EditorGUILayout.HelpBox("No ZoneSequenceSO assigned. Zones will use default difficulty curve only.", MessageType.Info);
            }
            else
            {
                int layerCount = _zoneSequence.Layers?.Count ?? 0;
                if (layerCount == 0)
                    EditorGUILayout.HelpBox("Zone sequence has no layers defined.", MessageType.Warning);
                else if (layerCount != zoneCount && !_zoneSequence.EnableLooping)
                    EditorGUILayout.HelpBox($"Zone sequence has {layerCount} layers but RunConfig expects {zoneCount} zones.", MessageType.Warning);

                // Use cached validation results
                RefreshValidationCache();

                if (_cachedMissingPools > 0)
                    EditorGUILayout.HelpBox($"{_cachedMissingPools} combat zone(s) have no EncounterPool assigned.", MessageType.Warning);
                if (_cachedMissingDirectors > 0)
                    EditorGUILayout.HelpBox($"{_cachedMissingDirectors} combat zone(s) have no SpawnDirectorConfig assigned.", MessageType.Warning);
            }

            if (zoneCount >= 3 && _runConfig.RunCurrencyPerZoneClear > 0)
                EditorGUILayout.HelpBox("Configuration looks good.", MessageType.None);
        }
    }
}
#endif
