using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using DIG.Voxel.Editor.Modules;

namespace DIG.Voxel.Editor
{
    public class VoxelWorkstationWindow : EditorWindow
    {
        private Dictionary<string, IVoxelModule> _modules;
        private string[] _tabs = new string[] { "Setup", "Assets", "Tools", "Debug" };
        private int _selectedTab = 0;
        private IVoxelModule _currentModule;
        
        [MenuItem("Tools/DIG/Voxel Workstation", priority = 10)]
        public static void ShowWindow()
        {
            var window = GetWindow<VoxelWorkstationWindow>("Voxel Station");
            window.minSize = new Vector2(800, 600);
        }
        
        private void OnEnable()
        {
            InitializeModules();
            SceneView.duringSceneGui += OnSceneGUI;
        }
        
        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            if (_modules != null)
            {
                foreach (var module in _modules.Values)
                {
                    module.OnDestroy();
                }
            }
        }
        
        private void InitializeModules()
        {
            _modules = new Dictionary<string, IVoxelModule>
            {
                { "Setup", new VoxelSetupModule() },
                { "Assets", new VoxelAssetsModule() },
                { "Tools", new VoxelToolsModule() },
                { "Debug", new VoxelDebugModule() }
            };
            
            foreach (var module in _modules.Values)
            {
                module.Initialize();
            }
            
            UpdateCurrentModule();
        }
        
        private void UpdateCurrentModule()
        {
            string tabName = _tabs[_selectedTab];
            if (_modules.TryGetValue(tabName, out var module))
            {
                _currentModule = module;
            }
        }
        
        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            
            // Sidebar
            DrawSidebar();
            
            // Content
            DrawContent();
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawSidebar()
        {
            EditorGUILayout.BeginVertical("box", GUILayout.Width(150), GUILayout.ExpandHeight(true));
            
            EditorGUILayout.LabelField("DIG Voxel", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);
            
            int newTab = GUILayout.SelectionGrid(_selectedTab, _tabs, 1, EditorStyles.toolbarButton);
            if (newTab != _selectedTab)
            {
                _selectedTab = newTab;
                UpdateCurrentModule();
            }
            
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("Re-Init", EditorStyles.miniButton))
            {
                InitializeModules();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawContent()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            
            if (_currentModule != null)
            {
                _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
                _currentModule.DrawGUI();
                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.HelpBox("Module not found or not initialized.", MessageType.Error);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private Vector2 _scrollPos;
        
        private void OnSceneGUI(SceneView sceneView)
        {
            if (_currentModule != null)
            {
                _currentModule.DrawSceneGUI(sceneView);
            }
        }
    }
}
