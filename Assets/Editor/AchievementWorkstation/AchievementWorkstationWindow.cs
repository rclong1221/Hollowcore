#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace DIG.Achievement.Editor
{
    /// <summary>
    /// EPIC 17.7: Central editor window for achievement configuration and debugging.
    /// Menu: DIG/Achievement Workstation. Sidebar + module pattern.
    /// </summary>
    public class AchievementWorkstationWindow : EditorWindow
    {
        private readonly List<IAchievementWorkstationModule> _modules = new();
        private int _selectedTab;
        private Vector2 _sidebarScroll;

        [MenuItem("DIG/Achievement Workstation")]
        public static void ShowWindow()
        {
            var window = GetWindow<AchievementWorkstationWindow>("Achievement Workstation");
            window.minSize = new Vector2(700, 400);
        }

        private void OnEnable()
        {
            _modules.Clear();
            _modules.Add(new Modules.DefinitionEditorModule());
            _modules.Add(new Modules.ProgressInspectorModule());
            _modules.Add(new Modules.ValidatorModule());
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
            for (int i = 0; i < _modules.Count; i++)
            {
                var prevBg = GUI.backgroundColor;
                if (i == _selectedTab)
                    GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
                if (GUILayout.Button(_modules[i].ModuleName, GUILayout.Height(28)))
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
        /// Returns the appropriate ECS World for achievement queries.
        /// </summary>
        public static World GetAchievementWorld()
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
#endif
