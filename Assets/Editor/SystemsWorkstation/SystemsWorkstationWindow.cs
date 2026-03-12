using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace DIG.Editor.SystemsWorkstation
{
    /// <summary>
    /// EPIC 15.5: Systems Workstation Window.
    /// Engine-level performance and pooling tools.
    /// </summary>
    public class SystemsWorkstationWindow : EditorWindow
    {
        private int _selectedTab = 0;
        private string[] _tabs = new string[] { 
            "Projectile Pool", "Entity Pool", "VFX Pool", "Pool Monitor", "GC Analysis"
        };

        private Dictionary<string, ISystemsModule> _modules;

        [MenuItem("DIG/Systems Workstation")]
        public static void ShowWindow()
        {
            var window = GetWindow<SystemsWorkstationWindow>("Systems Workstation");
            window.minSize = new Vector2(750, 550);
        }

        private void OnEnable()
        {
            InitializeModules();
        }

        private void InitializeModules()
        {
            _modules = new Dictionary<string, ISystemsModule>();
            
            _modules.Add("Projectile Pool", new Modules.ProjectilePoolModule());
            _modules.Add("Entity Pool", new Modules.EntityPoolModule());
            _modules.Add("VFX Pool", new Modules.VFXPoolModule());
            _modules.Add("Pool Monitor", new Modules.PoolMonitorModule());
            _modules.Add("GC Analysis", new Modules.GCAnalysisModule());
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
            GUILayout.Label("DIG Systems Workstation", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            
            if (Application.isPlaying)
            {
                GUI.color = Color.green;
                GUILayout.Label("● LIVE", EditorStyles.boldLabel);
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
    }

    public interface ISystemsModule
    {
        void OnGUI();
    }
}
