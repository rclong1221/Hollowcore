using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace DIG.Editor.CharacterWorkstation
{
    /// <summary>
    /// EPIC 15.5: Character Workstation Window.
    /// Tools for character/enemy setup: hitboxes, animations, damage receivers.
    /// </summary>
    public class CharacterWorkstationWindow : EditorWindow
    {
        private int _selectedTab = 0;
        private string[] _tabs = new string[] { 
            "Hitbox Rig", "Hitbox Copy", "Anim Binding", "Combo Builder", "Character Stats", "IK Setup", "Damageable", "Testing"
        };

        private Dictionary<string, ICharacterModule> _modules;

        [MenuItem("DIG/Character Workstation")]
        public static void ShowWindow()
        {
            var window = GetWindow<CharacterWorkstationWindow>("Character Workstation");
            window.minSize = new Vector2(650, 550);
        }

        private void OnEnable()
        {
            InitializeModules();
        }

        private void InitializeModules()
        {
            _modules = new Dictionary<string, ICharacterModule>();
            
            _modules.Add("Hitbox Rig", new Modules.HitboxRigModule());
            _modules.Add("Hitbox Copy", new Modules.HitboxCopyModule());
            _modules.Add("Anim Binding", new Modules.AnimBindingModule());
            _modules.Add("Combo Builder", new Modules.ComboBuilderModule());
            _modules.Add("Character Stats", new Modules.CharacterStatsModule());
            _modules.Add("IK Setup", new Modules.IKSetupModule());
            _modules.Add("Damageable", new Modules.DamageableSetupModule());
            _modules.Add("Testing", new Modules.TestingModule());
        }

        private void OnGUI()
        {
            DrawHeader();
            
            EditorGUILayout.BeginHorizontal();
            
            // Sidebar
            EditorGUILayout.BeginVertical("box", GUILayout.Width(110), GUILayout.ExpandHeight(true));
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
            GUILayout.Label("DIG Character Workstation", EditorStyles.boldLabel);
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

    public interface ICharacterModule
    {
        void OnGUI();
    }
}
