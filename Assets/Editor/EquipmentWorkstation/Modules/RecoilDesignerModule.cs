using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using DIG.Weapons.Data;

namespace DIG.Editor.EquipmentWorkstation.Modules
{
    /// <summary>
    /// EPIC 15.5 EW-03: Recoil Pattern Designer module.
    /// Visual pattern editor with 2D grid, preview animation, and import/export.
    /// </summary>
    public class RecoilDesignerModule : IEquipmentModule
    {
        private RecoilPatternAsset _selectedPattern;
        private Vector2 _scrollPosition;
        
        // Grid settings
        private Rect _gridRect;
        private const float GridSize = 300f;
        private const float GridPadding = 20f;
        private float _gridScale = 5f; // Degrees visible
        
        // Pattern data
        private List<Vector2> _patternPoints = new List<Vector2>();
        private int _selectedPointIndex = -1;
        private bool _isDragging = false;
        
        // Pattern settings
        private string _patternName = "NewPattern";
        private PatternOverflowMode _overflowMode = PatternOverflowMode.RepeatLast;
        private float _visualKickStrength = 0.5f;
        private float _visualKickRecovery = 8f;
        private float _recoveryDelay = 0.2f;
        private float _recoveryTimePerStep = 0.1f;
        private float _randomnessScale = 0.2f;
        private bool _firstShotAccurate = true;
        
        // Preview
        private bool _isPreviewPlaying = false;
        private int _previewIndex = 0;
        private double _lastPreviewTime;
        private float _previewFireRate = 600f; // RPM
        private Vector2 _previewAccumulated = Vector2.zero;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Recoil Pattern Designer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Design recoil patterns visually. Drag points on the grid to define per-shot recoil offsets.",
                MessageType.Info);
            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();

            // Left panel: Grid
            EditorGUILayout.BeginVertical(GUILayout.Width(GridSize + GridPadding * 2));
            DrawPatternGrid();
            DrawGridControls();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Right panel: Settings
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            DrawPatternAssetSection();
            EditorGUILayout.Space(10);
            DrawPatternSettings();
            EditorGUILayout.Space(10);
            DrawPointList();
            EditorGUILayout.Space(10);
            DrawPreviewSection();
            EditorGUILayout.Space(10);
            DrawPresetButtons();
            EditorGUILayout.Space(10);
            DrawActions();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndHorizontal();

            // Handle preview animation
            if (_isPreviewPlaying)
            {
                UpdatePreview();
                EditorUtility.SetDirty(EditorWindow.focusedWindow);
            }
        }

        private void DrawPatternGrid()
        {
            EditorGUILayout.LabelField("Pattern Grid", EditorStyles.boldLabel);
            
            // Reserve rect for grid
            _gridRect = GUILayoutUtility.GetRect(GridSize + GridPadding * 2, GridSize + GridPadding * 2);
            
            // Background
            EditorGUI.DrawRect(_gridRect, new Color(0.15f, 0.15f, 0.15f));
            
            Rect innerRect = new Rect(
                _gridRect.x + GridPadding,
                _gridRect.y + GridPadding,
                GridSize,
                GridSize
            );

            // Grid lines
            Handles.color = new Color(0.3f, 0.3f, 0.3f);
            int gridLines = 10;
            for (int i = 0; i <= gridLines; i++)
            {
                float t = i / (float)gridLines;
                // Vertical
                Handles.DrawLine(
                    new Vector3(innerRect.x + innerRect.width * t, innerRect.y, 0),
                    new Vector3(innerRect.x + innerRect.width * t, innerRect.yMax, 0)
                );
                // Horizontal
                Handles.DrawLine(
                    new Vector3(innerRect.x, innerRect.y + innerRect.height * t, 0),
                    new Vector3(innerRect.xMax, innerRect.y + innerRect.height * t, 0)
                );
            }

            // Center crosshair
            Handles.color = new Color(0.5f, 0.5f, 0.5f);
            Vector2 center = innerRect.center;
            Handles.DrawLine(new Vector3(center.x - 10, center.y, 0), new Vector3(center.x + 10, center.y, 0));
            Handles.DrawLine(new Vector3(center.x, center.y - 10, 0), new Vector3(center.x, center.y + 10, 0));

            // Draw pattern line
            if (_patternPoints.Count > 1)
            {
                Handles.color = new Color(0.2f, 0.6f, 1f, 0.5f);
                Vector2 prevPos = center;
                
                for (int i = 0; i < _patternPoints.Count; i++)
                {
                    Vector2 offset = _patternPoints[i];
                    Vector2 screenPos = PatternToScreen(offset, innerRect);
                    
                    Handles.DrawLine(
                        new Vector3(prevPos.x, prevPos.y, 0),
                        new Vector3(screenPos.x, screenPos.y, 0)
                    );
                    prevPos = screenPos;
                }
            }

            // Draw points
            for (int i = 0; i < _patternPoints.Count; i++)
            {
                Vector2 offset = _patternPoints[i];
                Vector2 screenPos = PatternToScreen(offset, innerRect);
                
                // Point color based on selection and index
                Color pointColor = i == _selectedPointIndex 
                    ? Color.yellow 
                    : Color.Lerp(Color.green, Color.red, i / (float)Mathf.Max(1, _patternPoints.Count - 1));
                
                Rect pointRect = new Rect(screenPos.x - 6, screenPos.y - 6, 12, 12);
                EditorGUI.DrawRect(pointRect, pointColor);
                
                // Label
                GUI.Label(new Rect(screenPos.x + 8, screenPos.y - 8, 30, 16), $"{i + 1}", EditorStyles.miniLabel);
            }

            // Draw preview position
            if (_isPreviewPlaying)
            {
                Vector2 previewScreenPos = PatternToScreen(_previewAccumulated, innerRect);
                Rect previewRect = new Rect(previewScreenPos.x - 8, previewScreenPos.y - 8, 16, 16);
                EditorGUI.DrawRect(previewRect, Color.cyan);
            }

            // Handle mouse input
            HandleGridInput(innerRect);
        }

        private Vector2 PatternToScreen(Vector2 patternOffset, Rect gridRect)
        {
            // Pattern is in degrees, convert to screen position
            // Y is inverted (up in game = down on screen)
            float x = gridRect.center.x + (patternOffset.x / _gridScale) * (gridRect.width / 2);
            float y = gridRect.center.y - (patternOffset.y / _gridScale) * (gridRect.height / 2);
            return new Vector2(x, y);
        }

        private Vector2 ScreenToPattern(Vector2 screenPos, Rect gridRect)
        {
            float x = ((screenPos.x - gridRect.center.x) / (gridRect.width / 2)) * _gridScale;
            float y = -((screenPos.y - gridRect.center.y) / (gridRect.height / 2)) * _gridScale;
            return new Vector2(x, y);
        }

        private void HandleGridInput(Rect gridRect)
        {
            Event e = Event.current;
            Vector2 mousePos = e.mousePosition;

            if (!gridRect.Contains(mousePos)) return;

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                // Check if clicking on existing point
                for (int i = 0; i < _patternPoints.Count; i++)
                {
                    Vector2 screenPos = PatternToScreen(_patternPoints[i], gridRect);
                    if (Vector2.Distance(mousePos, screenPos) < 10)
                    {
                        _selectedPointIndex = i;
                        _isDragging = true;
                        e.Use();
                        return;
                    }
                }

                // Add new point
                Vector2 patternPos = ScreenToPattern(mousePos, gridRect);
                _patternPoints.Add(patternPos);
                _selectedPointIndex = _patternPoints.Count - 1;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _isDragging && _selectedPointIndex >= 0)
            {
                Vector2 patternPos = ScreenToPattern(mousePos, gridRect);
                _patternPoints[_selectedPointIndex] = patternPos;
                e.Use();
            }
            else if (e.type == EventType.MouseUp)
            {
                _isDragging = false;
            }
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Delete && _selectedPointIndex >= 0)
            {
                _patternPoints.RemoveAt(_selectedPointIndex);
                _selectedPointIndex = -1;
                e.Use();
            }
        }

        private void DrawGridControls()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Scale:", GUILayout.Width(40));
            _gridScale = EditorGUILayout.Slider(_gridScale, 1f, 15f);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear All"))
            {
                _patternPoints.Clear();
                _selectedPointIndex = -1;
            }
            if (GUILayout.Button("Mirror X"))
            {
                foreach (var i in System.Linq.Enumerable.Range(0, _patternPoints.Count))
                {
                    _patternPoints[i] = new Vector2(-_patternPoints[i].x, _patternPoints[i].y);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPatternAssetSection()
        {
            EditorGUILayout.LabelField("Pattern Asset", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginChangeCheck();
            _selectedPattern = (RecoilPatternAsset)EditorGUILayout.ObjectField(
                "Load Pattern", _selectedPattern, typeof(RecoilPatternAsset), false);
            if (EditorGUI.EndChangeCheck() && _selectedPattern != null)
            {
                LoadFromAsset();
            }

            _patternName = EditorGUILayout.TextField("Pattern Name", _patternName);

            EditorGUILayout.EndVertical();
        }

        private void DrawPatternSettings()
        {
            EditorGUILayout.LabelField("Pattern Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _overflowMode = (PatternOverflowMode)EditorGUILayout.EnumPopup("Overflow Mode", _overflowMode);
            _firstShotAccurate = EditorGUILayout.Toggle("First Shot Accurate", _firstShotAccurate);
            _randomnessScale = EditorGUILayout.Slider("Randomness", _randomnessScale, 0f, 1f);
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Visual Kick", EditorStyles.miniLabel);
            _visualKickStrength = EditorGUILayout.Slider("Kick Strength", _visualKickStrength, 0f, 2f);
            _visualKickRecovery = EditorGUILayout.Slider("Kick Recovery", _visualKickRecovery, 1f, 20f);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Recovery", EditorStyles.miniLabel);
            _recoveryDelay = EditorGUILayout.Slider("Recovery Delay", _recoveryDelay, 0f, 1f);
            _recoveryTimePerStep = EditorGUILayout.Slider("Time Per Step", _recoveryTimePerStep, 0.05f, 0.5f);

            EditorGUILayout.EndVertical();
        }

        private void DrawPointList()
        {
            EditorGUILayout.LabelField($"Pattern Points ({_patternPoints.Count})", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MaxHeight(150));

            for (int i = 0; i < _patternPoints.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                
                bool isSelected = i == _selectedPointIndex;
                GUI.backgroundColor = isSelected ? Color.yellow : Color.white;
                
                if (GUILayout.Button($"{i + 1}", GUILayout.Width(25)))
                {
                    _selectedPointIndex = i;
                }
                
                GUI.backgroundColor = Color.white;
                
                Vector2 point = _patternPoints[i];
                point.x = EditorGUILayout.FloatField(point.x, GUILayout.Width(50));
                point.y = EditorGUILayout.FloatField(point.y, GUILayout.Width(50));
                _patternPoints[i] = point;
                
                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    _patternPoints.RemoveAt(i);
                    if (_selectedPointIndex >= _patternPoints.Count)
                        _selectedPointIndex = _patternPoints.Count - 1;
                    break;
                }
                
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ Add Point"))
            {
                _patternPoints.Add(Vector2.up * 0.5f);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPreviewSection()
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _previewFireRate = EditorGUILayout.Slider("Fire Rate (RPM)", _previewFireRate, 60, 1200);

            EditorGUILayout.BeginHorizontal();
            
            GUI.backgroundColor = _isPreviewPlaying ? Color.red : Color.green;
            if (GUILayout.Button(_isPreviewPlaying ? "Stop" : "Play", GUILayout.Height(25)))
            {
                if (_isPreviewPlaying)
                {
                    StopPreview();
                }
                else
                {
                    StartPreview();
                }
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Reset", GUILayout.Height(25)))
            {
                StopPreview();
            }

            EditorGUILayout.EndHorizontal();

            if (_isPreviewPlaying)
            {
                EditorGUILayout.LabelField($"Shot: {_previewIndex + 1} / {_patternPoints.Count}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Offset: ({_previewAccumulated.x:F2}, {_previewAccumulated.y:F2})°", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPresetButtons()
        {
            EditorGUILayout.LabelField("Generate Preset", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Assault Rifle")) GeneratePreset("AssaultRifle");
            if (GUILayout.Button("Pistol")) GeneratePreset("Pistol");
            if (GUILayout.Button("SMG")) GeneratePreset("SMG");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("LMG")) GeneratePreset("LMG");
            if (GUILayout.Button("Sniper")) GeneratePreset("Sniper");
            if (GUILayout.Button("Shotgun")) GeneratePreset("Shotgun");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawActions()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("Save as New Asset", GUILayout.Height(30)))
            {
                SaveAsNewAsset();
            }
            GUI.backgroundColor = Color.white;

            EditorGUI.BeginDisabledGroup(_selectedPattern == null);
            if (GUILayout.Button("Update Existing", GUILayout.Height(30)))
            {
                UpdateExistingAsset();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void GeneratePreset(string presetName)
        {
            _patternPoints.Clear();
            _patternName = presetName + "Pattern";

            switch (presetName)
            {
                case "AssaultRifle":
                    // Classic 7-shape pattern
                    _patternPoints.Add(new Vector2(0, 0.8f));
                    _patternPoints.Add(new Vector2(0.1f, 0.7f));
                    _patternPoints.Add(new Vector2(-0.1f, 0.6f));
                    _patternPoints.Add(new Vector2(-0.3f, 0.5f));
                    _patternPoints.Add(new Vector2(-0.2f, 0.4f));
                    _patternPoints.Add(new Vector2(0.1f, 0.3f));
                    _patternPoints.Add(new Vector2(0.4f, 0.3f));
                    _patternPoints.Add(new Vector2(0.3f, 0.2f));
                    _patternPoints.Add(new Vector2(0f, 0.2f));
                    _patternPoints.Add(new Vector2(-0.2f, 0.15f));
                    _patternPoints.Add(new Vector2(-0.3f, 0.1f));
                    _patternPoints.Add(new Vector2(-0.1f, 0.1f));
                    _visualKickStrength = 0.5f;
                    _recoveryDelay = 0.15f;
                    break;

                case "Pistol":
                    _patternPoints.Add(new Vector2(0, 1.2f));
                    _patternPoints.Add(new Vector2(0.2f, 0.8f));
                    _patternPoints.Add(new Vector2(-0.2f, 0.6f));
                    _visualKickStrength = 0.8f;
                    _recoveryDelay = 0.25f;
                    break;

                case "SMG":
                    _patternPoints.Add(new Vector2(0, 0.4f));
                    _patternPoints.Add(new Vector2(0.15f, 0.35f));
                    _patternPoints.Add(new Vector2(-0.1f, 0.3f));
                    _patternPoints.Add(new Vector2(-0.2f, 0.25f));
                    _patternPoints.Add(new Vector2(0.1f, 0.2f));
                    _patternPoints.Add(new Vector2(0.2f, 0.15f));
                    _patternPoints.Add(new Vector2(0f, 0.1f));
                    _patternPoints.Add(new Vector2(-0.15f, 0.1f));
                    _visualKickStrength = 0.3f;
                    _recoveryDelay = 0.1f;
                    break;

                case "LMG":
                    for (int i = 0; i < 20; i++)
                    {
                        float t = i / 19f;
                        float y = Mathf.Lerp(0.6f, 0.15f, t);
                        float x = Mathf.Sin(t * Mathf.PI * 3) * 0.4f;
                        _patternPoints.Add(new Vector2(x, y));
                    }
                    _visualKickStrength = 0.4f;
                    _recoveryDelay = 0.2f;
                    break;

                case "Sniper":
                    _patternPoints.Add(new Vector2(0, 2.5f));
                    _firstShotAccurate = true;
                    _visualKickStrength = 1.5f;
                    _recoveryDelay = 0.8f;
                    break;

                case "Shotgun":
                    _patternPoints.Add(new Vector2(0, 1.8f));
                    _visualKickStrength = 1.2f;
                    _recoveryDelay = 0.4f;
                    break;
            }

            Debug.Log($"[RecoilDesigner] Generated preset: {presetName} ({_patternPoints.Count} points)");
        }

        private void StartPreview()
        {
            _isPreviewPlaying = true;
            _previewIndex = 0;
            _previewAccumulated = Vector2.zero;
            _lastPreviewTime = EditorApplication.timeSinceStartup;
        }

        private void StopPreview()
        {
            _isPreviewPlaying = false;
            _previewIndex = 0;
            _previewAccumulated = Vector2.zero;
        }

        private void UpdatePreview()
        {
            double currentTime = EditorApplication.timeSinceStartup;
            float fireInterval = 60f / _previewFireRate;

            if (currentTime - _lastPreviewTime >= fireInterval)
            {
                _lastPreviewTime = currentTime;

                if (_previewIndex < _patternPoints.Count)
                {
                    _previewAccumulated += _patternPoints[_previewIndex];
                    _previewIndex++;
                }
                else
                {
                    // Reset after pattern completes
                    StopPreview();
                }
            }
        }

        private void LoadFromAsset()
        {
            if (_selectedPattern == null) return;

            _patternPoints.Clear();
            _patternName = _selectedPattern.name;

            if (_selectedPattern.Pattern != null)
            {
                foreach (var p in _selectedPattern.Pattern)
                {
                    _patternPoints.Add(p);
                }
            }

            _overflowMode = _selectedPattern.Overflow;
            _visualKickStrength = _selectedPattern.VisualKickStrength;
            _visualKickRecovery = _selectedPattern.VisualKickRecovery;
            _recoveryDelay = _selectedPattern.RecoveryDelay;
            _recoveryTimePerStep = _selectedPattern.RecoveryTimePerStep;
            _randomnessScale = _selectedPattern.RandomnessScale;
            _firstShotAccurate = _selectedPattern.FirstShotAccurate;

            Debug.Log($"[RecoilDesigner] Loaded pattern: {_patternName}");
        }

        private void SaveAsNewAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Recoil Pattern",
                _patternName,
                "asset",
                "Save recoil pattern asset"
            );

            if (string.IsNullOrEmpty(path)) return;

            var asset = ScriptableObject.CreateInstance<RecoilPatternAsset>();
            ApplyToAsset(asset);

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            _selectedPattern = asset;
            Selection.activeObject = asset;

            Debug.Log($"[RecoilDesigner] Saved new pattern: {path}");
        }

        private void UpdateExistingAsset()
        {
            if (_selectedPattern == null) return;

            Undo.RecordObject(_selectedPattern, "Update Recoil Pattern");
            ApplyToAsset(_selectedPattern);
            EditorUtility.SetDirty(_selectedPattern);
            AssetDatabase.SaveAssets();

            Debug.Log($"[RecoilDesigner] Updated pattern: {_selectedPattern.name}");
        }

        private void ApplyToAsset(RecoilPatternAsset asset)
        {
            asset.Pattern = _patternPoints.ToArray();
            asset.Overflow = _overflowMode;
            asset.VisualKickStrength = _visualKickStrength;
            asset.VisualKickRecovery = _visualKickRecovery;
            asset.RecoveryDelay = _recoveryDelay;
            asset.RecoveryTimePerStep = _recoveryTimePerStep;
            asset.RandomnessScale = _randomnessScale;
            asset.FirstShotAccurate = _firstShotAccurate;
        }
    }
}
