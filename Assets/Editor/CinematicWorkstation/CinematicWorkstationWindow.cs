#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DIG.Cinematic.Editor
{
    /// <summary>
    /// EPIC 17.9: Editor window for cinematic system inspection and tooling.
    /// Menu: DIG > Cinematic Workstation.
    /// Follows ProgressionWorkstationWindow / LobbyWorkstationWindow pattern.
    /// </summary>
    public class CinematicWorkstationWindow : EditorWindow
    {
        private ICinematicWorkstationModule[] _modules;
        private int _selectedTab;
        private Vector2 _sidebarScroll;
        private Vector2 _contentScroll;

        [MenuItem("DIG/Cinematic Workstation")]
        public static void ShowWindow()
        {
            var window = GetWindow<CinematicWorkstationWindow>("Cinematic Workstation");
            window.minSize = new Vector2(800, 500);
        }

        private void OnEnable()
        {
            InitializeModules();
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void InitializeModules()
        {
            _modules = new ICinematicWorkstationModule[]
            {
                new Modules.CinematicBrowserModule(),
                new Modules.CinematicDebugModule()
            };
        }

        private void OnGUI()
        {
            if (_modules == null) InitializeModules();

            EditorGUILayout.BeginHorizontal();

            // Sidebar
            EditorGUILayout.BeginVertical(GUILayout.Width(180));
            _sidebarScroll = EditorGUILayout.BeginScrollView(_sidebarScroll);

            EditorGUILayout.LabelField("Cinematic Workstation", EditorStyles.boldLabel);
            EditorGUILayout.Space(8);

            for (int i = 0; i < _modules.Length; i++)
            {
                var style = i == _selectedTab
                    ? new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold }
                    : GUI.skin.button;

                if (GUILayout.Button(_modules[i].ModuleName, style, GUILayout.Height(28)))
                    _selectedTab = i;
            }

            EditorGUILayout.Space(16);

            if (GUILayout.Button("Reload", GUILayout.Height(24)))
                InitializeModules();

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            // Separator
            EditorGUILayout.BeginVertical(GUILayout.Width(1));
            var rect = EditorGUILayout.GetControlRect(GUILayout.Width(1), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f));
            EditorGUILayout.EndVertical();

            // Content
            EditorGUILayout.BeginVertical();
            _contentScroll = EditorGUILayout.BeginScrollView(_contentScroll);

            if (_selectedTab >= 0 && _selectedTab < _modules.Length)
                _modules[_selectedTab].OnGUI();

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            if (Application.isPlaying)
                Repaint();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_modules == null) return;
            if (_selectedTab >= 0 && _selectedTab < _modules.Length)
                _modules[_selectedTab].OnSceneGUI(sceneView);
        }

        /// <summary>
        /// Helper to find ECS World for cinematic state inspection.
        /// Returns ServerWorld (listen server) or first ClientWorld.
        /// </summary>
        public static Unity.Entities.World GetCinematicWorld()
        {
            if (!Application.isPlaying) return null;

            foreach (var world in Unity.Entities.World.All)
            {
                if (world.Name.Contains("Server") || world.Name.Contains("Local"))
                    return world;
            }
            foreach (var world in Unity.Entities.World.All)
            {
                if (world.Name.Contains("Client"))
                    return world;
            }
            return null;
        }
    }
}
#endif
