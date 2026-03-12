using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using DIG.Tutorial.Editor.Modules;

namespace DIG.Tutorial.Editor
{
    /// <summary>
    /// EPIC 18.4: Tutorial Workstation editor window.
    /// Menu: DIG/Tutorial Workstation. Sidebar + module pattern.
    /// </summary>
    public class TutorialWorkstationWindow : EditorWindow
    {
        private readonly Dictionary<string, ITutorialModule> _modules = new();
        private string[] _moduleNames;
        private int _selectedModule;

        [MenuItem("DIG/Tutorial Workstation")]
        public static void ShowWindow()
        {
            var window = GetWindow<TutorialWorkstationWindow>("Tutorial Workstation");
            window.minSize = new Vector2(500, 350);
        }

        private void OnEnable()
        {
            _modules.Clear();
            _modules["Sequence Browser"] = new SequenceBrowserModule();
            _modules["Trigger Map"] = new TriggerMapModule();

            _moduleNames = new string[_modules.Count];
            int i = 0;
            foreach (var key in _modules.Keys)
                _moduleNames[i++] = key;
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();

            // Sidebar
            EditorGUILayout.BeginVertical("box", GUILayout.Width(140));
            EditorGUILayout.LabelField("Modules", EditorStyles.boldLabel);
            _selectedModule = GUILayout.SelectionGrid(_selectedModule, _moduleNames, 1);
            EditorGUILayout.EndVertical();

            // Content area
            EditorGUILayout.BeginVertical();
            if (_selectedModule >= 0 && _selectedModule < _moduleNames.Length)
            {
                var moduleName = _moduleNames[_selectedModule];
                if (_modules.TryGetValue(moduleName, out var module))
                    module.OnGUI();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }
    }
}
