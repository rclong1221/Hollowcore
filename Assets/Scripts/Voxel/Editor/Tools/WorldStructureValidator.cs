using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using Unity.Mathematics;
using DIG.Voxel.Geology;

namespace DIG.Voxel.Editor.Tools
{
    public class WorldStructureValidator : EditorWindow
    {
        [MenuItem("DIG/World/Validate World Structure")]
        static void ShowWindow() => GetWindow<WorldStructureValidator>("Validator");
        
        private WorldStructureConfig _config;
        private List<ValidationResult> _results = new();
        
        private void OnGUI()
        {
            EditorGUILayout.LabelField("World Config Validator", EditorStyles.boldLabel);
            
            _config = (WorldStructureConfig)EditorGUILayout.ObjectField(
                "Config", _config, typeof(WorldStructureConfig), false);
            
            if (GUILayout.Button("Validate", GUILayout.Height(30)))
            {
                Validate();
            }
            
            if (_results.Count > 0)
            {
                EditorGUILayout.Space();
                
                int errors = _results.Count(r => r.Type == ResultType.Error);
                int warnings = _results.Count(r => r.Type == ResultType.Warning);
                int info = _results.Count(r => r.Type == ResultType.Info);
                
                EditorGUILayout.LabelField($"Results: {errors} errors, {warnings} warnings, {info} info");
                
                EditorGUILayout.BeginScrollView(Vector2.zero);
                foreach (var result in _results)
                {
                    MessageType msgType = result.Type switch
                    {
                        ResultType.Error => MessageType.Error,
                        ResultType.Warning => MessageType.Warning,
                        _ => MessageType.Info
                    };
                    
                    EditorGUILayout.HelpBox(result.Message, msgType);
                }
                EditorGUILayout.EndScrollView();
            }
        }
        
        private void Validate()
        {
            _results.Clear();
            
            if (_config == null)
            {
                _results.Add(new ValidationResult(ResultType.Error, "No config assigned"));
                return;
            }
            
            // Check layers exist
            if (_config.Layers == null || _config.Layers.Length == 0)
            {
                _results.Add(new ValidationResult(ResultType.Error, "No layers defined"));
                return;
            }
            
            // Check layer ordering
            float lastBottom = 0;
            for (int i = 0; i < _config.Layers.Length; i++)
            {
                var layer = _config.Layers[i];
                if (layer == null) continue;
                
                // Gap Check
                if (i > 0 && math.abs(layer.TopDepth - lastBottom) > 0.1f)
                {
                    if (layer.TopDepth > lastBottom)
                         _results.Add(new ValidationResult(ResultType.Error,
                            $"Gap detected between Layer {i-1} (ends at {lastBottom}m) and Layer {i} (starts at {layer.TopDepth}m)"));
                    else
                         _results.Add(new ValidationResult(ResultType.Error,
                            $"Overlap detected between Layer {i-1} (ends at {lastBottom}m) and Layer {i} (starts at {layer.TopDepth}m)"));
                }
                
                lastBottom = layer.BottomDepth;
                
                // Check hollow layers have profiles
                if (layer.Type == LayerType.Hollow && layer.HollowProfile == null)
                {
                    _results.Add(new ValidationResult(ResultType.Error,
                        $"Hollow layer '{layer.LayerName}' missing HollowEarthProfile"));
                }
                
                // Check solid layers have strata
                if (layer.Type == LayerType.Solid && layer.StrataProfile == null)
                {
                    _results.Add(new ValidationResult(ResultType.Warning,
                        $"Solid layer '{layer.LayerName}' missing StrataProfile"));
                }
                
                // Check playtime targets
                if (layer.TargetPlaytimeMinutes < 15)
                {
                    _results.Add(new ValidationResult(ResultType.Warning,
                        $"Layer '{layer.LayerName}' has short playtime target: {layer.TargetPlaytimeMinutes}min"));
                }
                
                // Check hollow dimensions
                if (layer.Type == LayerType.Hollow && layer.HollowProfile != null)
                {
                    if (layer.HollowProfile.AverageHeight > layer.Thickness)
                    {
                        _results.Add(new ValidationResult(ResultType.Error,
                            $"Hollow '{layer.LayerName}': Height {layer.HollowProfile.AverageHeight}m exceeds layer thickness {layer.Thickness}m"));
                    }
                    
                    if (layer.HollowProfile.AverageHeight < 100)
                    {
                        _results.Add(new ValidationResult(ResultType.Warning,
                            $"Hollow '{layer.LayerName}' has low height ({layer.HollowProfile.AverageHeight}m). Consider 300m+"));
                    }
                }
            }
            
            // Check total depth
            float totalDepth = _config.GetTotalDepth();
            if (totalDepth < 2000)
            {
                _results.Add(new ValidationResult(ResultType.Info,
                    $"Total depth {totalDepth}m is shallow. Consider 5000m+ for full experience"));
            }
            
            // Check hollow count
            int hollowCount = _config.Layers.Count(l => l != null && l.Type == LayerType.Hollow);
            if (hollowCount < 4)
            {
                _results.Add(new ValidationResult(ResultType.Info,
                    $"Only {hollowCount} hollow layers. Consider 5-6 for varied experience"));
            }
            
            if (_results.Count == 0)
            {
                _results.Add(new ValidationResult(ResultType.Info, "✅ All validations passed! Configuration is healthy."));
            }
        }
        
        private enum ResultType { Error, Warning, Info }
        
        private struct ValidationResult
        {
            public ResultType Type;
            public string Message;
            
            public ValidationResult(ResultType type, string message)
            {
                Type = type;
                Message = message;
            }
        }
    }
}
