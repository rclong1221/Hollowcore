#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DIG.Map.Editor.Modules
{
    /// <summary>
    /// EPIC 17.6: Edit MinimapConfigSO fields — zoom range, icon scale, frame spread,
    /// fog resolution. Live preview slider for zoom. RT size dropdown.
    /// </summary>
    public class ConfigEditorModule : IMapWorkstationModule
    {
        public string ModuleName => "Config Editor";

        private MinimapConfigSO _config;
        private UnityEditor.Editor _configEditor;
        private float _previewZoom = 40f;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Minimap Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space(8);

            if (_config == null)
                _config = Resources.Load<MinimapConfigSO>("MinimapConfig");

            if (_config == null)
            {
                EditorGUILayout.HelpBox("No MinimapConfig found at Resources/MinimapConfig.\nUse DIG > Map Workstation > Create Default Assets.", MessageType.Warning);
                return;
            }

            if (_configEditor == null || _configEditor.target != _config)
                _configEditor = UnityEditor.Editor.CreateEditor(_config);

            _configEditor.OnInspectorGUI();

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Live Zoom Preview", EditorStyles.boldLabel);
            _previewZoom = EditorGUILayout.Slider("Preview Zoom", _previewZoom, _config.MinZoom, _config.MaxZoom);
            EditorGUILayout.HelpBox($"At zoom {_previewZoom:F0}, the minimap shows a {_previewZoom * 2:F0}x{_previewZoom * 2:F0} world-unit area.", MessageType.Info);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Render Texture Size", EditorStyles.boldLabel);
            int[] rtOptions = { 256, 512, 1024 };
            string[] rtLabels = { "256x256 (Low)", "512x512 (Default)", "1024x1024 (High)" };
            int currentIdx = System.Array.IndexOf(rtOptions, _config.RenderTextureSize);
            if (currentIdx < 0) currentIdx = 1;
            int newIdx = EditorGUILayout.Popup("RT Resolution", currentIdx, rtLabels);
            if (newIdx != currentIdx)
            {
                Undo.RecordObject(_config, "Change RT Size");
                _config.RenderTextureSize = rtOptions[newIdx];
                EditorUtility.SetDirty(_config);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("VRAM Budget", EditorStyles.miniLabel);
            int rtSize = _config.RenderTextureSize;
            int fogW = _config.FogTextureWidth;
            int fogH = _config.FogTextureHeight;
            float minimapMB = rtSize * rtSize * 4f / (1024f * 1024f);
            float fogMB = fogW * fogH * 1f / (1024f * 1024f);
            EditorGUILayout.LabelField($"  Minimap RT: {minimapMB:F2} MB (ARGB32)");
            EditorGUILayout.LabelField($"  Fog RT: {fogMB:F2} MB (R8)");
            EditorGUILayout.LabelField($"  Total: {(minimapMB + fogMB):F2} MB");
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }
}
#endif
