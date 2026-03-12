#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DIG.Dialogue.Editor
{
    /// <summary>
    /// EPIC 16.16: Central editor window for dialogue system design and debugging.
    /// Menu: DIG/Dialogue Workstation. Sidebar + 5 module tabs.
    /// </summary>
    public class DialogueWorkstationWindow : EditorWindow
    {
        private int _selectedTab;
        private Vector2 _scrollPos;

        private static readonly string[] TabNames =
        {
            "Overview", "Tree Editor", "Node Inspector", "Bark Editor", "Live Preview", "Validator"
        };

        private Dictionary<string, IDialogueModule> _modules;
        private TreeEditorModule _treeEditor;
        private NodeInspectorModule _nodeInspector;

        [MenuItem("DIG/Dialogue Workstation")]
        public static void ShowWindow()
        {
            var window = GetWindow<DialogueWorkstationWindow>("Dialogue Workstation");
            window.minSize = new Vector2(800, 600);
        }

        private void OnEnable()
        {
            _treeEditor = new TreeEditorModule();
            _nodeInspector = new NodeInspectorModule();

            _treeEditor.OnNodeSelected += nodeIndex =>
            {
                _nodeInspector.SetSelectedNode(_treeEditor.SelectedTree, nodeIndex);
            };

            _modules = new Dictionary<string, IDialogueModule>
            {
                { "Overview", new DialogueOverviewModule() },
                { "Tree Editor", _treeEditor },
                { "Node Inspector", _nodeInspector },
                { "Bark Editor", new BarkEditorModule() },
                { "Live Preview", new LivePreviewModule() },
                { "Validator", new ValidatorModule() }
            };
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();

            // Sidebar
            EditorGUILayout.BeginVertical("box", GUILayout.Width(120), GUILayout.ExpandHeight(true));
            for (int i = 0; i < TabNames.Length; i++)
            {
                var style = i == _selectedTab ? EditorStyles.toolbarButton : EditorStyles.miniButton;
                if (GUILayout.Button(TabNames[i], style, GUILayout.Height(28)))
                    _selectedTab = i;
            }

            GUILayout.FlexibleSpace();

            EditorGUILayout.LabelField("EPIC 16.16 / 18.5", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndVertical();

            // Content area
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            string currentTab = TabNames[_selectedTab];
            if (_modules != null && _modules.ContainsKey(currentTab))
                _modules[currentTab].OnGUI();

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void OnFocus()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDestroy()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            string currentTab = TabNames[_selectedTab];
            if (_modules != null && _modules.ContainsKey(currentTab))
                _modules[currentTab].OnSceneGUI(sceneView);
        }
    }
}
#endif
