using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace DIG.Editor.UtilitiesWorkstation
{
    /// <summary>
    /// EPIC 15.5: Utilities Workstation Window.
    /// General-purpose tools spanning multiple systems.
    /// </summary>
    public class UtilitiesWorkstationWindow : EditorWindow
    {
        private int _selectedTab = 0;
        private string[] _tabs = new string[] { 
            "Dependency Validator", "Bulk Operations", "Asset Search", "Template Manager", "Migration Tools"
        };

        private Dictionary<string, IUtilitiesModule> _modules;

        [MenuItem("DIG/Utilities Workstation")]
        public static void ShowWindow()
        {
            var window = GetWindow<UtilitiesWorkstationWindow>("Utilities Workstation");
            window.minSize = new Vector2(800, 600);
        }

        private void OnEnable()
        {
            InitializeModules();
        }

        private void InitializeModules()
        {
            _modules = new Dictionary<string, IUtilitiesModule>();
            
            _modules.Add("Dependency Validator", new Modules.DependencyValidatorModule());
            _modules.Add("Bulk Operations", new Modules.BulkOperationsModule());
            _modules.Add("Asset Search", new Modules.AssetSearchModule());
            _modules.Add("Template Manager", new Modules.TemplateManagerModule());
            _modules.Add("Migration Tools", new Modules.MigrationToolsModule());
        }

        private void OnGUI()
        {
            DrawHeader();
            
            EditorGUILayout.BeginHorizontal();
            
            // Sidebar
            EditorGUILayout.BeginVertical("box", GUILayout.Width(140), GUILayout.ExpandHeight(true));
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
            GUILayout.Label("DIG Utilities Workstation", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            
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

    public interface IUtilitiesModule
    {
        void OnGUI();
    }
}
