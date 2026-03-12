using UnityEditor;
using UnityEngine;

namespace DIG.Lobby.Editor
{
    /// <summary>
    /// EPIC 17.4: Editor window for lobby debugging and configuration.
    /// Menu: DIG > Lobby Workstation.
    /// Sidebar + content area with modules.
    /// </summary>
    public class LobbyWorkstationWindow : EditorWindow
    {
        private ILobbyWorkstationModule[] _modules;
        private int _selectedModule;
        private Vector2 _sidebarScroll;
        private Vector2 _contentScroll;

        [MenuItem("DIG/Lobby Workstation")]
        public static void ShowWindow()
        {
            var window = GetWindow<LobbyWorkstationWindow>("Lobby Workstation");
            window.minSize = new Vector2(700, 400);
        }

        private void OnEnable()
        {
            _modules = new ILobbyWorkstationModule[]
            {
                new LobbySetupModule(),
                new LobbyInspectorModule(),
                new MapRegistryModule(),
                new DifficultyRegistryModule(),
                new IdentityInspectorModule()
            };
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();

            // Sidebar
            EditorGUILayout.BeginVertical(GUILayout.Width(180));
            _sidebarScroll = EditorGUILayout.BeginScrollView(_sidebarScroll);

            EditorGUILayout.LabelField("Modules", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            for (int i = 0; i < _modules.Length; i++)
            {
                var style = i == _selectedModule ? EditorStyles.toolbarButton : EditorStyles.miniButton;
                if (GUILayout.Button(_modules[i].ModuleName, style))
                    _selectedModule = i;
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            // Separator
            EditorGUILayout.Space(2);

            // Content
            EditorGUILayout.BeginVertical();
            _contentScroll = EditorGUILayout.BeginScrollView(_contentScroll);

            if (_selectedModule >= 0 && _selectedModule < _modules.Length)
                _modules[_selectedModule].OnGUI();

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_selectedModule >= 0 && _selectedModule < _modules.Length)
                _modules[_selectedModule].OnSceneGUI(sceneView);
        }

        public static LobbyManager GetLobbyManager()
        {
            if (!Application.isPlaying) return null;
            return LobbyManager.Instance;
        }
    }
}
