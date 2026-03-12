using System.Collections.Generic;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using DIG.Crafting.Editor.Modules;

namespace DIG.Crafting.Editor
{
    /// <summary>
    /// EPIC 16.13: Crafting Workstation editor window.
    /// Menu: DIG/Crafting Workstation. Sidebar + module pattern.
    /// </summary>
    public class CraftingWorkstationWindow : EditorWindow
    {
        private readonly Dictionary<string, ICraftingModule> _modules = new();
        private string[] _moduleNames;
        private int _selectedModule;

        [MenuItem("DIG/Crafting Workstation")]
        public static void ShowWindow()
        {
            GetWindow<CraftingWorkstationWindow>("Crafting Workstation");
        }

        private void OnEnable()
        {
            _modules.Clear();
            _modules["Recipe Editor"] = new RecipeEditorModule();
            _modules["Balance Sim"] = new BalanceSimulatorModule();
            _modules["Station Config"] = new StationConfigModule();
            _modules["Live Debug"] = new LiveDebugModule();

            _moduleNames = new string[_modules.Count];
            int i = 0;
            foreach (var key in _modules.Keys)
                _moduleNames[i++] = key;

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

        public static World GetCraftingWorld()
        {
            foreach (var world in World.All)
            {
                if (world.IsCreated &&
                    (world.Flags.HasFlag(WorldFlags.GameServer) ||
                     world.Flags.HasFlag(WorldFlags.GameClient) ||
                     world.Flags.HasFlag(WorldFlags.Game)))
                    return world;
            }
            return null;
        }
    }
}
