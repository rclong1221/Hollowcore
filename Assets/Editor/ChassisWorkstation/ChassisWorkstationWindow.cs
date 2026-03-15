using System.Collections.Generic;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace Hollowcore.Editor.ChassisWorkstation
{
    /// <summary>
    /// Chassis Workstation: Unified design tools for the chassis and limb system.
    /// Tabs: Limb Browser, Loadout Previewer, Balance Matrix, ID Audit.
    /// </summary>
    public class ChassisWorkstationWindow : EditorWindow
    {
        private int _selectedTab;
        private readonly string[] _tabs = { "Limb Browser", "Loadout Previewer", "Balance Matrix", "ID Audit" };

        private Dictionary<string, IChassisModule> _modules;
        private Vector2 _scrollPosition;

        [MenuItem("Hollowcore/Chassis Workstation")]
        public static void ShowWindow()
        {
            var window = GetWindow<ChassisWorkstationWindow>("Chassis Workstation");
            window.minSize = new Vector2(700, 550);
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
            _modules = new Dictionary<string, IChassisModule>
            {
                { "Limb Browser", new Modules.LimbBrowserModule() },
                { "Loadout Previewer", new Modules.LoadoutPreviewerModule() },
                { "Balance Matrix", new Modules.BalanceMatrixModule() },
                { "ID Audit", new Modules.IDAuditModule() }
            };
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabs, EditorStyles.toolbarButton);
            EditorGUILayout.EndHorizontal();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            if (_modules != null && _modules.TryGetValue(_tabs[_selectedTab], out var module))
            {
                module.OnGUI();
            }

            EditorGUILayout.EndScrollView();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_modules != null && _modules.TryGetValue(_tabs[_selectedTab], out var module))
            {
                module.OnSceneGUI(sceneView);
            }
        }
    }
}
