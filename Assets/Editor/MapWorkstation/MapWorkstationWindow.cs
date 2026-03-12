#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DIG.Map.Editor
{
    /// <summary>
    /// EPIC 17.6: Editor window for map system configuration and live debugging.
    /// Menu: DIG > Map Workstation.
    /// 5 tabs: Config Editor, Icon Theme, POI Manager, Fog Preview, Live Inspector.
    /// </summary>
    public class MapWorkstationWindow : EditorWindow
    {
        private IMapWorkstationModule[] _modules;
        private int _selectedModule;
        private Vector2 _sidebarScroll;
        private Vector2 _contentScroll;

        [MenuItem("DIG/Map Workstation")]
        public static void ShowWindow()
        {
            var window = GetWindow<MapWorkstationWindow>("Map Workstation");
            window.minSize = new Vector2(750, 450);
        }

        private void OnEnable()
        {
            _modules = new IMapWorkstationModule[]
            {
                new Modules.ConfigEditorModule(),
                new Modules.IconThemeModule(),
                new Modules.POIManagerModule(),
                new Modules.FogPreviewModule(),
                new Modules.LiveInspectorModule()
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

            if (Application.isPlaying)
                Repaint();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_selectedModule >= 0 && _selectedModule < _modules.Length)
                _modules[_selectedModule].OnSceneGUI(sceneView);
        }

        [MenuItem("DIG/Map Workstation/Create Default Assets")]
        public static void CreateDefaultAssets()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            if (Resources.Load<MinimapConfigSO>("MinimapConfig") == null)
            {
                var config = ScriptableObject.CreateInstance<MinimapConfigSO>();
                AssetDatabase.CreateAsset(config, "Assets/Resources/MinimapConfig.asset");
                Debug.Log("[MapWorkstation] Created Resources/MinimapConfig.asset");
            }

            if (Resources.Load<MapIconThemeSO>("MapIconTheme") == null)
            {
                var theme = ScriptableObject.CreateInstance<MapIconThemeSO>();
                AssetDatabase.CreateAsset(theme, "Assets/Resources/MapIconTheme.asset");
                Debug.Log("[MapWorkstation] Created Resources/MapIconTheme.asset");
            }

            if (Resources.Load<POIRegistrySO>("POIRegistry") == null)
            {
                var registry = ScriptableObject.CreateInstance<POIRegistrySO>();
                AssetDatabase.CreateAsset(registry, "Assets/Resources/POIRegistry.asset");
                Debug.Log("[MapWorkstation] Created Resources/POIRegistry.asset");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
#endif
