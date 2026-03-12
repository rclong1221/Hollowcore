using System.Collections.Generic;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace DIG.Weather.Editor
{
    public class WeatherWorkstationWindow : EditorWindow
    {
        private readonly string[] _tabNames =
        {
            "Time Controls", "Weather Controls", "Transition Graph",
            "Lighting Preview", "Gameplay Inspector"
        };

        private readonly List<IWeatherWorkstationModule> _modules = new();
        private int _selectedTab;
        private Vector2 _sidebarScroll;

        [MenuItem("DIG/Weather Workstation")]
        public static void ShowWindow()
        {
            var window = GetWindow<WeatherWorkstationWindow>("Weather Workstation");
            window.minSize = new Vector2(750, 450);
        }

        private void OnEnable()
        {
            _modules.Clear();
            _modules.Add(new Modules.TimeControlModule());
            _modules.Add(new Modules.WeatherControlModule());
            _modules.Add(new Modules.TransitionGraphModule());
            _modules.Add(new Modules.LightingPreviewModule());
            _modules.Add(new Modules.GameplayInspectorModule());
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();

            // Sidebar
            EditorGUILayout.BeginVertical("box", GUILayout.Width(160));
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

            // Content
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

        public static World GetWeatherWorld()
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
