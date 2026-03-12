using System.Collections.Generic;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace DIG.Progression.Editor
{
    /// <summary>
    /// EPIC 16.14: Central editor window for progression configuration and debugging.
    /// Sidebar + module pattern (matches AI/Quest/Crafting Workstation windows).
    /// </summary>
    public class ProgressionWorkstationWindow : EditorWindow
    {
        private readonly string[] _tabNames = { "Player Inspector", "XP Curve", "XP Simulator" };
        private readonly List<IProgressionWorkstationModule> _modules = new();
        private int _selectedTab;
        private Vector2 _sidebarScroll;

        [MenuItem("DIG/Progression Workstation")]
        public static void ShowWindow()
        {
            var window = GetWindow<ProgressionWorkstationWindow>("Progression Workstation");
            window.minSize = new Vector2(700, 400);
        }

        private void OnEnable()
        {
            _modules.Clear();
            _modules.Add(new Modules.PlayerInspectorModule());
            _modules.Add(new Modules.XPCurveModule());
            _modules.Add(new Modules.XPSimulatorModule());
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
                    GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
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

        /// <summary>
        /// Returns the appropriate ECS World for progression queries.
        /// </summary>
        public static World GetProgressionWorld()
        {
            if (!Application.isPlaying) return null;

            // Prefer server world, fall back to local
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
