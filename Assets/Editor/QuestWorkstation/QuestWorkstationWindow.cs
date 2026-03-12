using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Unity.Entities;

namespace DIG.Quest.Editor
{
    /// <summary>
    /// EPIC 16.12: Quest Workstation editor window.
    /// Sidebar + module pattern matching AIWorkstationWindow.
    /// </summary>
    public class QuestWorkstationWindow : EditorWindow
    {
        private int _selectedTab;
        private readonly string[] _tabs = { "Quest Editor", "Flow Viewer", "Live Debug", "Validator" };
        private Dictionary<string, IQuestModule> _modules;
        private Vector2 _scrollPosition;

        [MenuItem("DIG/Quest Workstation")]
        public static void ShowWindow()
        {
            var window = GetWindow<QuestWorkstationWindow>("Quest Workstation");
            window.minSize = new Vector2(750, 550);
        }

        private void OnEnable()
        {
            InitializeModules();
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void InitializeModules()
        {
            _modules = new Dictionary<string, IQuestModule>
            {
                { "Quest Editor", new Modules.QuestEditorModule() },
                { "Flow Viewer", new Modules.QuestFlowModule() },
                { "Live Debug", new Modules.QuestLiveDebugModule() },
                { "Validator", new Modules.QuestValidatorModule() }
            };
        }

        private void OnGUI()
        {
            DrawHeader();
            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();

            // Sidebar
            EditorGUILayout.BeginVertical("box", GUILayout.Width(130), GUILayout.ExpandHeight(true));
            _selectedTab = GUILayout.SelectionGrid(_selectedTab, _tabs, 1, EditorStyles.miniButton);
            EditorGUILayout.EndVertical();

            // Content
            EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            string currentTabName = _tabs[_selectedTab];
            if (_modules != null && _modules.ContainsKey(currentTabName))
                _modules[currentTabName].OnGUI();

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            if (Application.isPlaying)
                Repaint();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Quest Workstation", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Reload DB", EditorStyles.toolbarButton, GUILayout.Width(80)))
                ReloadDatabase();

            EditorGUILayout.EndHorizontal();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_modules == null) return;
            string currentTabName = _tabs[_selectedTab];
            if (_modules.ContainsKey(currentTabName))
                _modules[currentTabName].OnSceneGUI(sceneView);
        }

        private void ReloadDatabase()
        {
            var db = Resources.Load<QuestDatabaseSO>("QuestDatabase");
            if (db != null)
            {
                db.BuildLookupTable();
                Debug.Log($"[QuestWorkstation] Reloaded {db.Quests.Count} quests.");
            }
            else
            {
                Debug.LogWarning("[QuestWorkstation] No QuestDatabaseSO at Resources/QuestDatabase.");
            }
        }

        public static World GetQuestWorld()
        {
            if (!Application.isPlaying) return null;

            foreach (var world in World.All)
            {
                if (world.IsCreated && (world.Flags & WorldFlags.GameServer) != 0)
                    return world;
            }
            return World.DefaultGameObjectInjectionWorld;
        }
    }
}
