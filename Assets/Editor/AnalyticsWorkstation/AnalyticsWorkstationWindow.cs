using System.Collections.Generic;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace DIG.Analytics.Editor
{
    public class AnalyticsWorkstationWindow : EditorWindow
    {
        private readonly string[] _tabNames =
        {
            "Live Event Stream", "Session Timeline", "Event Frequency",
            "Dispatch Queue", "Privacy Simulator", "A/B Test Override"
        };

        private readonly List<IAnalyticsWorkstationModule> _modules = new();
        private int _selectedTab;
        private Vector2 _sidebarScroll;

        [MenuItem("DIG/Analytics Workstation")]
        public static void ShowWindow()
        {
            var window = GetWindow<AnalyticsWorkstationWindow>("Analytics Workstation");
            window.minSize = new Vector2(800, 500);
        }

        private void OnEnable()
        {
            _modules.Clear();
            _modules.Add(new Modules.LiveEventStreamModule());
            _modules.Add(new Modules.SessionTimelineModule());
            _modules.Add(new Modules.EventFrequencyModule());
            _modules.Add(new Modules.DispatchQueueModule());
            _modules.Add(new Modules.PrivacySimulatorModule());
            _modules.Add(new Modules.ABTestOverrideModule());
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical("box", GUILayout.Width(170));
            _sidebarScroll = EditorGUILayout.BeginScrollView(_sidebarScroll);
            for (int i = 0; i < _tabNames.Length; i++)
            {
                var prevBg = GUI.backgroundColor;
                if (i == _selectedTab)
                    GUI.backgroundColor = new Color(0.3f, 0.6f, 1f);
                if (GUILayout.Button(_tabNames[i], GUILayout.Height(28)))
                    _selectedTab = i;
                GUI.backgroundColor = prevBg;
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical();
            if (_selectedTab >= 0 && _selectedTab < _modules.Count)
                _modules[_selectedTab].OnGUI();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            if (Application.isPlaying)
                Repaint();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_selectedTab >= 0 && _selectedTab < _modules.Count)
                _modules[_selectedTab].OnSceneGUI(sceneView);
        }

        public static World GetAnalyticsWorld()
        {
            if (!Application.isPlaying) return null;

            foreach (var world in World.All)
            {
                if (world.IsCreated && (world.Flags & WorldFlags.GameServer) != 0)
                    return world;
            }
            foreach (var world in World.All)
            {
                if (world.IsCreated && (world.Flags & WorldFlags.Game) != 0)
                    return world;
            }
            return World.DefaultGameObjectInjectionWorld;
        }
    }
}
