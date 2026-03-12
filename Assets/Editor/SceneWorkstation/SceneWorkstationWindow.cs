using System.Collections.Generic;
using DIG.SceneManagement.Editor.Modules;
using UnityEditor;
using UnityEngine;

namespace DIG.SceneManagement.Editor
{
    /// <summary>
    /// EPIC 18.6: Scene Workstation editor window.
    /// Menu: DIG/Scene Workstation. Sidebar + module pattern (follows CraftingWorkstation).
    /// </summary>
    public class SceneWorkstationWindow : EditorWindow
    {
        private readonly Dictionary<string, ISceneModule> _modules = new();
        private string[] _moduleNames;
        private int _selectedModule;

        [MenuItem("DIG/Scene Workstation")]
        public static void ShowWindow()
        {
            GetWindow<SceneWorkstationWindow>("Scene Workstation");
        }

        private void OnEnable()
        {
            _modules.Clear();
            _modules["Setup Wizard"] = new SetupWizardModule();
            _modules["Flow Graph"] = new FlowGraphModule();
            _modules["Scene Assignment"] = new SceneAssignmentModule();
            _modules["Loading Preview"] = new LoadingScreenPreviewModule();
            _modules["Transition Tester"] = new TransitionTesterModule();

            _moduleNames = new string[_modules.Count];
            int i = 0;
            foreach (var key in _modules.Keys)
                _moduleNames[i++] = key;

            minSize = new Vector2(700, 450);
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
            EditorGUILayout.BeginVertical("box", GUILayout.Width(150));
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

            if (Application.isPlaying)
                Repaint();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_selectedModule >= 0 && _selectedModule < _moduleNames.Length)
            {
                var moduleName = _moduleNames[_selectedModule];
                if (_modules.TryGetValue(moduleName, out var module))
                    module.OnSceneGUI(sceneView);
            }
        }
    }
}
