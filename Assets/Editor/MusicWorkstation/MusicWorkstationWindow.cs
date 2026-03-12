#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DIG.Music.Editor
{
    /// <summary>
    /// EPIC 17.5: Editor window for music system debugging and configuration.
    /// Menu: DIG > Music Workstation.
    /// Sidebar + content area with modules.
    /// </summary>
    public class MusicWorkstationWindow : EditorWindow
    {
        private IMusicWorkstationModule[] _modules;
        private int _selectedModule;
        private Vector2 _sidebarScroll;
        private Vector2 _contentScroll;

        [MenuItem("DIG/Music Workstation")]
        public static void ShowWindow()
        {
            var window = GetWindow<MusicWorkstationWindow>("Music Workstation");
            window.minSize = new Vector2(750, 450);
        }

        private void OnEnable()
        {
            _modules = new IMusicWorkstationModule[]
            {
                new TrackBrowserModule(),
                new StemPreviewerModule(),
                new ZoneMapperModule(),
                new StingerTesterModule(),
                new LiveDebugModule(),
                new IntensityCurveModule()
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

            // Repaint during play mode for live data
            if (Application.isPlaying)
                Repaint();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_selectedModule >= 0 && _selectedModule < _modules.Length)
                _modules[_selectedModule].OnSceneGUI(sceneView);
        }

        /// <summary>
        /// Creates MusicConfigSO and MusicDatabaseSO assets in Resources/ if they don't exist.
        /// </summary>
        [MenuItem("DIG/Music Workstation/Create Default Assets")]
        public static void CreateDefaultAssets()
        {
            // Ensure Resources folder exists
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            // MusicConfig
            if (Resources.Load<MusicConfigSO>("MusicConfig") == null)
            {
                var config = ScriptableObject.CreateInstance<MusicConfigSO>();
                AssetDatabase.CreateAsset(config, "Assets/Resources/MusicConfig.asset");
                Debug.Log("[MusicWorkstation] Created Resources/MusicConfig.asset");
            }
            else
            {
                Debug.Log("[MusicWorkstation] Resources/MusicConfig.asset already exists.");
            }

            // MusicDatabase
            if (Resources.Load<MusicDatabaseSO>("MusicDatabase") == null)
            {
                var db = ScriptableObject.CreateInstance<MusicDatabaseSO>();
                AssetDatabase.CreateAsset(db, "Assets/Resources/MusicDatabase.asset");
                Debug.Log("[MusicWorkstation] Created Resources/MusicDatabase.asset");
            }
            else
            {
                Debug.Log("[MusicWorkstation] Resources/MusicDatabase.asset already exists.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
#endif
