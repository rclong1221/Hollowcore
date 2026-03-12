using UnityEditor;
using UnityEngine;
using Unity.Entities;

namespace DIG.Validation.Editor
{
    /// <summary>
    /// EPIC 17.11: Validation Workstation EditorWindow.
    /// Menu: DIG > Validation Workstation.
    /// Sidebar + content area with 6 modules.
    /// </summary>
    public class ValidationWorkstationWindow : EditorWindow
    {
        private IValidationWorkstationModule[] _modules;
        private int _selectedModule;
        private Vector2 _sidebarScroll;
        private Vector2 _contentScroll;

        [MenuItem("DIG/Validation Workstation")]
        public static void ShowWindow()
        {
            var window = GetWindow<ValidationWorkstationWindow>("Validation Workstation");
            window.minSize = new Vector2(750, 450);
        }

        private void OnEnable()
        {
            _modules = new IValidationWorkstationModule[]
            {
                new Modules.PlayerMonitorModule(),
                new Modules.RateLimitModule(),
                new Modules.MovementTrailModule(),
                new Modules.EconomyAuditModule(),
                new Modules.BanManagerModule(),
                new Modules.ViolationTimelineModule()
            };
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
            EditorGUILayout.BeginVertical("box", GUILayout.Width(160));
            _sidebarScroll = EditorGUILayout.BeginScrollView(_sidebarScroll);
            EditorGUILayout.LabelField("Modules", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (_modules != null)
            {
                for (int i = 0; i < _modules.Length; i++)
                {
                    var prevBg = GUI.backgroundColor;
                    if (i == _selectedModule)
                        GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
                    if (GUILayout.Button(_modules[i].ModuleName, GUILayout.Height(26)))
                        _selectedModule = i;
                    GUI.backgroundColor = prevBg;
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            // Content
            EditorGUILayout.BeginVertical();
            _contentScroll = EditorGUILayout.BeginScrollView(_contentScroll);

            if (_modules != null && _selectedModule >= 0 && _selectedModule < _modules.Length)
                _modules[_selectedModule].OnGUI();

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            if (Application.isPlaying)
                Repaint();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_modules != null && _selectedModule >= 0 && _selectedModule < _modules.Length)
                _modules[_selectedModule].OnSceneGUI(sceneView);
        }

        /// <summary>
        /// Get the server world for validation inspection.
        /// </summary>
        public static World GetValidationWorld()
        {
            if (!Application.isPlaying) return null;

            foreach (var world in World.All)
            {
                if (world.IsCreated && (world.Flags & WorldFlags.GameServer) != 0)
                    return world;
            }

            foreach (var world in World.All)
            {
                if (world.IsCreated && (world.Flags & WorldFlags.Game) != 0)
                    return world;
            }

            return World.DefaultGameObjectInjectionWorld;
        }
    }
}
