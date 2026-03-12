using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace DIG.Editor.AudioWorkstation
{
    /// <summary>
    /// EPIC 15.5: Audio Workstation Window.
    /// Comprehensive audio management for weapons and combat sounds.
    /// </summary>
    public class AudioWorkstationWindow : EditorWindow
    {
        private int _selectedTab = 0;
        private string[] _tabs = new string[] {
            "Audio Events", "Sound Banks", "Impact Surfaces", "Randomization", "Distance Atten", "Batch Assign", "Audio Preview",
            "Bus Monitor", "Occlusion Debug", "Reverb Zones", "Telemetry"
        };

        private Dictionary<string, IAudioModule> _modules;

        [MenuItem("DIG/Audio Workstation")]
        public static void ShowWindow()
        {
            var window = GetWindow<AudioWorkstationWindow>("Audio Workstation");
            window.minSize = new Vector2(700, 550);
        }

        private void OnEnable()
        {
            InitializeModules();
        }

        private void InitializeModules()
        {
            _modules = new Dictionary<string, IAudioModule>();
            
            _modules.Add("Audio Events", new Modules.AudioEventModule());
            _modules.Add("Sound Banks", new Modules.SoundBanksModule());
            _modules.Add("Impact Surfaces", new Modules.ImpactSurfacesModule());
            _modules.Add("Randomization", new Modules.RandomizationModule());
            _modules.Add("Distance Atten", new Modules.DistanceAttenModule());
            _modules.Add("Batch Assign", new Modules.BatchAssignModule());
            _modules.Add("Audio Preview", new Modules.AudioPreviewModule());
            _modules.Add("Bus Monitor", new Modules.BusMonitorModule());
            _modules.Add("Occlusion Debug", new Modules.OcclusionDebugModule());
            _modules.Add("Reverb Zones", new Modules.ReverbZoneModule());
            _modules.Add("Telemetry", new Modules.TelemetryModule());
        }

        private void OnGUI()
        {
            DrawHeader();
            
            EditorGUILayout.BeginHorizontal();
            
            // Sidebar
            EditorGUILayout.BeginVertical("box", GUILayout.Width(130), GUILayout.ExpandHeight(true));
            _selectedTab = GUILayout.SelectionGrid(_selectedTab, _tabs, 1, EditorStyles.miniButton);
            EditorGUILayout.EndVertical();
            
            // Content
            EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            string currentTabName = _tabs[_selectedTab];
            
            if (_modules != null && _modules.ContainsKey(currentTabName))
            {
                _modules[currentTabName].OnGUI();
            }
            else
            {
                DrawPlaceholder(currentTabName);
            }
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("DIG Audio Workstation", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reload", EditorStyles.toolbarButton))
            {
                InitializeModules();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPlaceholder(string tabName)
        {
            EditorGUILayout.HelpBox($"The module '{tabName}' has not been implemented yet.", MessageType.Info);
        }
    }

    public interface IAudioModule
    {
        void OnGUI();
    }
}
