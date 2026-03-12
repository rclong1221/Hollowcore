using System.Collections.Generic;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace DIG.Editor.TradeWorkstation
{
    /// <summary>
    /// EPIC 17.3: Trade Workstation editor window.
    /// Sidebar tabs: Active Trades, Trade History.
    /// Follows PartyWorkstationWindow pattern.
    /// </summary>
    public class TradeWorkstationWindow : EditorWindow
    {
        private int _selectedTab;
        private readonly string[] _tabNames = { "Active Trades", "Trade History" };
        private List<ITradeWorkstationModule> _modules;
        private Vector2 _sidebarScroll;

        [MenuItem("DIG/Trade Workstation")]
        public static void ShowWindow()
        {
            var wnd = GetWindow<TradeWorkstationWindow>("Trade Workstation");
            wnd.minSize = new Vector2(700, 450);
        }

        private void OnEnable()
        {
            _modules = new List<ITradeWorkstationModule>
            {
                new Modules.ActiveTradesModule(),
                new Modules.TradeHistoryModule()
            };

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
            EditorGUILayout.BeginVertical(GUILayout.Width(140));
            _sidebarScroll = EditorGUILayout.BeginScrollView(_sidebarScroll);
            EditorGUILayout.LabelField("Trading System", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            for (int i = 0; i < _tabNames.Length; i++)
            {
                var style = i == _selectedTab ? EditorStyles.toolbarButton : EditorStyles.miniButton;
                if (GUILayout.Button(_tabNames[i], style, GUILayout.Height(28)))
                    _selectedTab = i;
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            // Separator
            EditorGUILayout.BeginVertical(GUILayout.Width(1));
            var rect = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));
            EditorGUILayout.EndVertical();

            // Main content
            EditorGUILayout.BeginVertical();
            if (_selectedTab >= 0 && _selectedTab < _modules.Count)
                _modules[_selectedTab].OnGUI();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_selectedTab >= 0 && _selectedTab < _modules.Count)
                _modules[_selectedTab].OnSceneGUI(sceneView);
        }

        private void OnInspectorUpdate()
        {
            if (Application.isPlaying)
                Repaint();
        }

        /// <summary>
        /// Returns the best available World for ECS queries.
        /// </summary>
        public static World GetTradeWorld()
        {
            foreach (var w in World.All)
            {
                if (w.Name == "GameServer" || w.Name == "ServerWorld")
                    return w;
            }
            foreach (var w in World.All)
            {
                if (w.Name == "Game" || w.Name == "ClientWorld" || w.Name == "LocalWorld")
                    return w;
            }
            return World.DefaultGameObjectInjectionWorld;
        }
    }
}
