using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace DIG.Editor.CombatWorkstation
{
    /// <summary>
    /// EPIC 15.5: Combat Workstation Window.
    /// Tools for combat mechanics, damage pipeline, and feedback systems.
    /// </summary>
    public class CombatWorkstationWindow : EditorWindow
    {
        private int _selectedTab = 0;
        private string[] _tabs = new string[] {
            "Feedback Setup", "Camera Juice", "Damage Debug", "Hit Recording", "Network Sim", "Target Dummies", "Combo System", "Abilities"
        };

        private Dictionary<string, ICombatModule> _modules;

        [MenuItem("DIG/Combat Workstation")]
        public static void ShowWindow()
        {
            var window = GetWindow<CombatWorkstationWindow>("Combat Workstation");
            window.minSize = new Vector2(650, 550);
        }

        private void OnEnable()
        {
            InitializeModules();
        }

        private void InitializeModules()
        {
            _modules = new Dictionary<string, ICombatModule>();
            
            _modules.Add("Feedback Setup", new Modules.FeedbackSetupModule());
            _modules.Add("Camera Juice", new Modules.CameraJuiceModule());
            _modules.Add("Damage Debug", new Modules.DamageDebugModule());
            _modules.Add("Hit Recording", new Modules.HitRecordingModule());
            _modules.Add("Network Sim", new Modules.NetworkSimModule());
            _modules.Add("Target Dummies", new Modules.TargetDummiesModule());
            _modules.Add("Combo System", new Modules.ComboSystemModule());
            _modules.Add("Abilities", new Modules.AbilityManagementModule());
        }

        private void OnGUI()
        {
            DrawHeader();
            
            EditorGUILayout.BeginHorizontal();
            
            // Sidebar
            EditorGUILayout.BeginVertical("box", GUILayout.Width(120), GUILayout.ExpandHeight(true));
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
            GUILayout.Label("DIG Combat Workstation", EditorStyles.boldLabel);
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

    public interface ICombatModule
    {
        void OnGUI();
    }
}
