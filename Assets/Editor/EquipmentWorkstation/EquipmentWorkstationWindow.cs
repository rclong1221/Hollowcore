using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using DIG.Editor.EquipmentWorkstation.Modules;

namespace DIG.Editor.EquipmentWorkstation
{
    public class EquipmentWorkstationWindow : EditorWindow
    {
        private int _selectedTab = 0;
        private string[] _tabs = new string[] { 
            "Setup", "Create", "Templates", "Manage", "Validate", "Weapon Check", 
            "Audio/FX", "Debug", "Rigger", "Align", "Board",
            // EPIC 15.5 additions
            "Melee", "Ranged", "Recoil", "Stats", "Bulk Ops"
        };

        // Modules
        private Dictionary<string, IEquipmentModule> _modules;
        
        [MenuItem("DIG/Equipment Workstation")]
        public static void ShowWindow()
        {
            var window = GetWindow<EquipmentWorkstationWindow>("Equipment Workstation");
            window.minSize = new Vector2(600, 500);
        }

        private void OnEnable()
        {
            InitializeModules();
        }

        private void InitializeModules()
        {
            _modules = new Dictionary<string, IEquipmentModule>();

            // Register Modules
            _modules.Add("Setup", new EquipmentSetupModule());
            _modules.Add("Create", new WeaponCreatorModule());
            _modules.Add("Templates", new WeaponTemplatesModule());
            _modules.Add("Manage", new EquipmentManagerModule());
            _modules.Add("Validate", new EquipmentValidatorModule());
            _modules.Add("Weapon Check", new WeaponValidatorModule());
            _modules.Add("Audio/FX", new AudioEffectsSetupModule());
            _modules.Add("Debug", new EquipmentDebugModule());
            _modules.Add("Rigger", new Modules.SocketRiggerModule());
            _modules.Add("Align", new Modules.AlignmentBenchModule());
            _modules.Add("Board", new PipelineDashboardModule());
            
            // EPIC 15.5 additions
            _modules.Add("Melee", new Modules.MeleeSetupModule());
            _modules.Add("Ranged", new Modules.RangedSetupModule());
            _modules.Add("Recoil", new Modules.RecoilDesignerModule());
            _modules.Add("Stats", new Modules.StatsDashboardModule());
            _modules.Add("Bulk Ops", new Modules.BulkOpsModule());
        }

        private void OnGUI()
        {
            DrawHeader();
            
            EditorGUILayout.BeginHorizontal();
            
            // Sidebar
            EditorGUILayout.BeginVertical("box", GUILayout.Width(100), GUILayout.ExpandHeight(true));
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
            GUILayout.Label("DIG Equipment Workstation", EditorStyles.boldLabel);
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
        private void OnDestroy()
        {
             if (_modules != null && _modules.ContainsKey("Align"))
             {
                 var alignModule = _modules["Align"] as Modules.AlignmentBenchModule;
                 if (alignModule != null)
                 {
                     alignModule.CancelSession();
                 }
             }
        }
    }

    public interface IEquipmentModule
    {
        void OnGUI();
    }
}
