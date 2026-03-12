using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace DIG.Editor.DebugWorkstation
{
    /// <summary>
    /// EPIC 15.5: Debug Workstation Window.
    /// Runtime testing and QA tools.
    /// </summary>
    public class DebugWorkstationWindow : EditorWindow
    {
        private int _selectedTab = 0;
        private string[] _tabs = new string[] {
            "Testing Sandbox", "Target Spawner", "Damage Log", "A/B Testing", "Profiler Link", "Network Debug"
#if DIG_DEV_CONSOLE
            , "Console"
#endif
        };

        private Dictionary<string, IDebugModule> _modules;

        [MenuItem("DIG/Debug Workstation")]
        public static void ShowWindow()
        {
            var window = GetWindow<DebugWorkstationWindow>("Debug Workstation");
            window.minSize = new Vector2(850, 600);
        }

        private void OnEnable()
        {
            InitializeModules();
        }

        private void InitializeModules()
        {
            _modules = new Dictionary<string, IDebugModule>();
            
            _modules.Add("Testing Sandbox", new Modules.TestingSandboxModule());
            _modules.Add("Target Spawner", new Modules.TargetSpawnerModule());
            _modules.Add("Damage Log", new Modules.DamageLogModule());
            _modules.Add("A/B Testing", new Modules.ABTestingModule());
            _modules.Add("Profiler Link", new Modules.ProfilerLinkModule());
            _modules.Add("Network Debug", new Modules.NetworkDebugModule());
#if DIG_DEV_CONSOLE
            _modules.Add("Console", new Modules.ConsoleWorkstationModule());
#endif
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
            GUILayout.Label("DIG Debug Workstation", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            
            if (Application.isPlaying)
            {
                GUI.color = Color.green;
                GUILayout.Label("● PLAY MODE", EditorStyles.boldLabel);
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = Color.yellow;
                GUILayout.Label("○ Editor Only", EditorStyles.miniLabel);
                GUI.color = Color.white;
            }
            
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

        private void Update()
        {
            // Repaint during play mode for live updates
            if (Application.isPlaying)
            {
                Repaint();
            }
        }
    }

    public interface IDebugModule
    {
        void OnGUI();
    }
}
