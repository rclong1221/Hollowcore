using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.Linq;

namespace DIG.Editor.AnimationWorkstation
{
    public class AnimationCopierModule : IWorkstationModule
    {
        private AnimatorController _sourceController;
        private AnimatorController _targetController;
        
        private bool _copyParameters = true;
        private bool _copyTransitions = true;

        // Preview Data
        // Key: LayerName, Value: List of states in that layer
        private Dictionary<string, List<StatePreview>> _scanResults = new Dictionary<string, List<StatePreview>>();
        private Vector2 _scrollPos;
        private bool _hasScanned = false;

        // Cached GUIStyles to avoid allocation every frame
        private GUIStyle _boxStyle;
        private GUIStyle _existsLabelStyle;
        private GUIStyle _newLabelStyle;
        private bool _stylesInitialized = false;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("State Copier", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Copy states and transitions from Source to Target.\nMatches layers by Name.", MessageType.Info);

            DrawSettings();

            if (_sourceController == null || _targetController == null) return;

            EditorGUILayout.Space();
            DrawActions();
            
            EditorGUILayout.Space();
            DrawPreview();
        }

        private void DrawSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            _sourceController = (AnimatorController)EditorGUILayout.ObjectField("Source", _sourceController, typeof(AnimatorController), false);
            _targetController = (AnimatorController)EditorGUILayout.ObjectField("Target", _targetController, typeof(AnimatorController), false);
            EditorGUILayout.EndHorizontal();

            if (_sourceController != null && _targetController != null)
            {
                EditorGUILayout.BeginHorizontal();
                _copyParameters = EditorGUILayout.Toggle("Copy Parameters", _copyParameters);
                _copyTransitions = EditorGUILayout.Toggle("Copy Transitions", _copyTransitions);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawActions()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Scan All Layers", GUILayout.Height(30)))
            {
                ScanAllLayers();
            }

            // Count total new items
            int totalNew = _scanResults.Values.Sum(list => list.Count(x => !x.ExistsInTarget));
            
            EditorGUI.BeginDisabledGroup(!_hasScanned || totalNew == 0);
            if (GUILayout.Button($"Copy {totalNew} Missing States", GUILayout.Height(30)))
            {
                CopySelected();
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPreview()
        {
            if (!_hasScanned) return;

            if (_scanResults.Count == 0)
            {
                EditorGUILayout.HelpBox("No layers found or Source is empty.", MessageType.Warning);
                return;
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, EditorStyles.helpBox);

            foreach (var layerName in _scanResults.Keys)
            {
                DrawLayerSection(layerName, _scanResults[layerName]);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawLayerSection(string layerName, List<StatePreview> items)
        {
            EditorGUILayout.LabelField($"Layer: {layerName}", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            if (items.Count == 0)
            {
                EditorGUILayout.LabelField("No stats found.", EditorStyles.miniLabel);
            }
            else if (items[0].IsTargetMissing)
            {
                EditorGUILayout.HelpBox($"Target controller does NOT have a layer named '{layerName}'.\nScanning skipped for this layer.", MessageType.Warning);
            }
            else
            {
                foreach (var item in items)
                {
                    DrawPreviewItem(item);
                }
            }
            
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _boxStyle = new GUIStyle(EditorStyles.helpBox);

            _existsLabelStyle = new GUIStyle(EditorStyles.foldout);
            _existsLabelStyle.normal.textColor = Color.gray;
            _existsLabelStyle.onNormal.textColor = Color.gray;
            _existsLabelStyle.focused.textColor = Color.gray;
            _existsLabelStyle.onFocused.textColor = Color.gray;

            var green = EditorGUIUtility.isProSkin ? new Color(0.4f, 1f, 0.4f) : new Color(0f, 0.5f, 0f);
            _newLabelStyle = new GUIStyle(EditorStyles.foldout);
            _newLabelStyle.normal.textColor = green;
            _newLabelStyle.onNormal.textColor = green;
            _newLabelStyle.focused.textColor = green;
            _newLabelStyle.onFocused.textColor = green;
            _newLabelStyle.active.textColor = green;
            _newLabelStyle.onActive.textColor = green;

            _stylesInitialized = true;
        }

        private void DrawPreviewItem(StatePreview item)
        {
            InitializeStyles();

            EditorGUILayout.BeginVertical(_boxStyle);

            EditorGUILayout.BeginHorizontal();

            // Icon
            var icon = item.ExistsInTarget ? "collab" : "d_CreateAddNew";
            GUILayout.Label(EditorGUIUtility.IconContent(icon), GUILayout.Width(20));

            // Use cached style based on state
            var labelStyle = item.ExistsInTarget ? _existsLabelStyle : _newLabelStyle;

            // Name (Color coded, no tag)
            item.IsExpanded = EditorGUILayout.Foldout(item.IsExpanded, item.StateName, true, labelStyle);
            
            GUILayout.FlexibleSpace();
            if (!item.ExistsInTarget)
            {
                GUILayout.Label($"({item.Transitions.Count} Transitions)", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();

            // Details
            if (item.IsExpanded)
            {
                EditorGUI.indentLevel++;
                if (item.ExistsInTarget)
                {
                    EditorGUILayout.LabelField("State already exists in target. Will be skipped.", EditorStyles.miniLabel);
                }
                else
                {
                    if (item.Transitions.Count > 0)
                    {
                        EditorGUILayout.LabelField("Transitions:", EditorStyles.miniLabel);
                        foreach (var trans in item.Transitions)
                        {
                            EditorGUILayout.LabelField($" -> {trans}", EditorStyles.miniLabel);
                        }
                    }
                    else
                    {
                        EditorGUILayout.LabelField("No outgoing transitions.", EditorStyles.miniLabel);
                    }
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void ScanAllLayers()
        {
            _scanResults.Clear();
            _hasScanned = true;

            foreach (var srcLayer in _sourceController.layers)
            {
                var layerName = srcLayer.name;
                var dstLayer = _targetController.layers.FirstOrDefault(l => l.name == layerName);
                
                var layerItems = new List<StatePreview>();

                if (dstLayer == null)
                {
                    // Mark as missing target layer
                    layerItems.Add(new StatePreview { IsTargetMissing = true });
                }
                else
                {
                    // Scan states
                    foreach (var state in srcLayer.stateMachine.states)
                    {
                        var preview = new StatePreview
                        {
                            StateName = state.state.name,
                            ExistsInTarget = HasState(dstLayer.stateMachine, state.state.name)
                        };

                        // Analyze Transitions
                        foreach (var t in state.state.transitions)
                        {
                            string conditions = string.Join(", ", t.conditions.Select(c => $"{c.parameter} {c.mode} {c.threshold}"));
                            string dstName = t.destinationState != null ? t.destinationState.name : "Exit";
                            preview.Transitions.Add($"{dstName} [{conditions}]");
                        }

                        layerItems.Add(preview);
                    }
                }

                _scanResults.Add(layerName, layerItems);
            }
        }

        private void CopySelected()
        {
            // Copy Parameters
            if (_copyParameters)
            {
                foreach(var p in _sourceController.parameters)
                {
                    if (!_targetController.parameters.Any(tp => tp.name == p.name))
                    {
                        _targetController.AddParameter(p.name, p.type); 
                    }
                }
            }

            int count = 0;

            // Iterate layers in scan results
            foreach (var kvp in _scanResults)
            {
                string layerName = kvp.Key;
                List<StatePreview> items = kvp.Value;

                // Skip if target layer missing
                if (items.Count > 0 && items[0].IsTargetMissing) continue;

                var srcLayer = _sourceController.layers.First(l => l.name == layerName);
                var dstLayer = _targetController.layers.First(l => l.name == layerName);

                foreach(var item in items)
                {
                    if (item.ExistsInTarget) continue;

                    // Find original state object
                    var srcStateObj = srcLayer.stateMachine.states.FirstOrDefault(s => s.state.name == item.StateName);
                    
                    // Create
                    // Note: Copying Motion reference might fail if assets are not available, 
                    // but since we are Editor tool, it usually works if assets are in project.
                    var newState = dstLayer.stateMachine.AddState(item.StateName, srcStateObj.position);
                    newState.speed = srcStateObj.state.speed;
                    newState.motion = srcStateObj.state.motion; 
                    
                    // TODO: Detailed transition copying (Complex)
                    // For now, users can use the dedicated "Copier" scripts logic if we fully migrate that here, 
                    // but for this consolidation pass, basic state copying is the MVP.
                    
                    count++;
                }
            }
            
            Debug.Log($"[Copier] Copied {count} states across matching layers.");
            ScanAllLayers(); // Refresh
        }

        private bool HasState(AnimatorStateMachine sm, string name)
        {
            return sm.states.Any(s => s.state.name == name);
        }

        private class StatePreview
        {
            public string StateName;
            public bool ExistsInTarget;
            public bool IsTargetMissing; // New flag for missing layer
            public List<string> Transitions = new List<string>();
            public bool IsExpanded;
        }
    }
}
