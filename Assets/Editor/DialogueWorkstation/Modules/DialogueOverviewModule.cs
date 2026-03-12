#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DIG.Dialogue.Editor
{
    /// <summary>
    /// EPIC 18.5: Overview module for the Dialogue Workstation.
    /// Tree browser with validation badges, speaker registry, and statistics.
    /// Statistics are cached and recomputed only on button press (not per-repaint).
    /// </summary>
    public class DialogueOverviewModule : IDialogueModule
    {
        private DialogueDatabaseSO _database;
        private DialogueSpeakerProfileSO[] _speakerProfiles;
        private Vector2 _treeScrollPos;
        private Vector2 _speakerScrollPos;
        private int _subTab; // 0=Trees, 1=Speakers, 2=Stats

        // Cached validation
        private Dictionary<string, List<DialogueValidator.ValidationIssue>> _validationCache;

        // Cached statistics (recomputed on button press only — never per-repaint)
        private bool _statsValid;
        private int _statTotalNodes, _statTotalSpeech, _statTotalChoice, _statTotalCondition;
        private int _statTotalTrees, _statMaxDepth, _statTotalChoiceCount;
        private int _statNodesWithVO, _statSpeechWithExpression;

        private static readonly string[] SubTabNames = { "Trees", "Speakers", "Statistics" };

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Dialogue Overview (EPIC 18.5)", EditorStyles.boldLabel);

            var newDatabase = (DialogueDatabaseSO)EditorGUILayout.ObjectField(
                "Database", _database, typeof(DialogueDatabaseSO), false);
            if (newDatabase != _database)
            {
                _database = newDatabase;
                _validationCache = null;
                _statsValid = false;
            }

            EditorGUILayout.Space(4);
            _subTab = GUILayout.Toolbar(_subTab, SubTabNames, GUILayout.Height(24));
            EditorGUILayout.Space(4);

            switch (_subTab)
            {
                case 0: DrawTreeBrowser(); break;
                case 1: DrawSpeakerRegistry(); break;
                case 2: DrawStatistics(); break;
            }
        }

        public void OnSceneGUI(SceneView sceneView) { }

        private void DrawTreeBrowser()
        {
            if (_database == null)
            {
                EditorGUILayout.HelpBox("Select a DialogueDatabaseSO to browse trees.", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Validate All", GUILayout.Width(100)))
            {
                _validationCache = DialogueValidator.ValidateDatabase(_database);
            }
            if (GUILayout.Button("Refresh", GUILayout.Width(80)))
            {
                _validationCache = null;
                _statsValid = false;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            _treeScrollPos = EditorGUILayout.BeginScrollView(_treeScrollPos);

            foreach (var tree in _database.Trees)
            {
                if (tree == null) continue;

                EditorGUILayout.BeginHorizontal("box");

                // Validation badge
                if (_validationCache != null)
                {
                    string name = tree.DisplayName ?? tree.name;
                    if (_validationCache.TryGetValue(name, out var issues))
                    {
                        int errors = 0, warnings = 0;
                        foreach (var issue in issues)
                        {
                            if (issue.Severity == MessageType.Error) errors++;
                            else if (issue.Severity == MessageType.Warning) warnings++;
                        }

                        if (errors > 0)
                        {
                            var prevColor = GUI.color;
                            GUI.color = new Color(1f, 0.4f, 0.4f);
                            GUILayout.Label($"E:{errors}", EditorStyles.miniBoldLabel, GUILayout.Width(36));
                            GUI.color = prevColor;
                        }
                        else if (warnings > 0)
                        {
                            var prevColor = GUI.color;
                            GUI.color = new Color(1f, 0.8f, 0.2f);
                            GUILayout.Label($"W:{warnings}", EditorStyles.miniBoldLabel, GUILayout.Width(36));
                            GUI.color = prevColor;
                        }
                        else
                        {
                            var prevColor = GUI.color;
                            GUI.color = new Color(0.3f, 0.9f, 0.3f);
                            GUILayout.Label("OK", EditorStyles.miniBoldLabel, GUILayout.Width(36));
                            GUI.color = prevColor;
                        }
                    }
                    else
                    {
                        var prevColor = GUI.color;
                        GUI.color = new Color(0.3f, 0.9f, 0.3f);
                        GUILayout.Label("OK", EditorStyles.miniBoldLabel, GUILayout.Width(36));
                        GUI.color = prevColor;
                    }
                }

                // Tree info
                EditorGUILayout.LabelField($"[{tree.TreeId}] {tree.DisplayName ?? tree.name}",
                    EditorStyles.boldLabel, GUILayout.Width(200));
                EditorGUILayout.LabelField(
                    $"{(tree.Nodes != null ? tree.Nodes.Length : 0)} nodes | {tree.Priority}",
                    EditorStyles.miniLabel, GUILayout.Width(150));

                if (GUILayout.Button("Graph", GUILayout.Width(50)))
                    DialogueGraphEditorWindow.OpenTree(tree);

                if (GUILayout.Button("Select", GUILayout.Width(50)))
                    Selection.activeObject = tree;

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawSpeakerRegistry()
        {
            if (GUILayout.Button("Load Speaker Profiles", GUILayout.Width(180)))
            {
                _speakerProfiles = Resources.LoadAll<DialogueSpeakerProfileSO>("SpeakerProfiles");
                if (_speakerProfiles.Length == 0)
                {
                    var guids = AssetDatabase.FindAssets("t:DialogueSpeakerProfileSO");
                    var list = new List<DialogueSpeakerProfileSO>(guids.Length);
                    foreach (var guid in guids)
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        var profile = AssetDatabase.LoadAssetAtPath<DialogueSpeakerProfileSO>(path);
                        if (profile != null) list.Add(profile);
                    }
                    _speakerProfiles = list.ToArray();
                }
            }

            if (_speakerProfiles == null || _speakerProfiles.Length == 0)
            {
                EditorGUILayout.HelpBox("No speaker profiles loaded. Click 'Load Speaker Profiles' or create profiles in Resources/SpeakerProfiles/.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"{_speakerProfiles.Length} speaker profiles", EditorStyles.miniLabel);
            EditorGUILayout.Space(4);

            _speakerScrollPos = EditorGUILayout.BeginScrollView(_speakerScrollPos);

            foreach (var profile in _speakerProfiles)
            {
                if (profile == null) continue;

                EditorGUILayout.BeginHorizontal("box");

                if (profile.DefaultPortrait != null)
                {
                    var rect = GUILayoutUtility.GetRect(32, 32, GUILayout.Width(32));
                    GUI.DrawTexture(rect, profile.DefaultPortrait.texture, ScaleMode.ScaleToFit);
                }
                else
                {
                    GUILayout.Space(32);
                }

                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(profile.SpeakerName, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    $"{profile.Portraits?.Length ?? 0} expressions, {profile.VoiceBank?.Length ?? 0} voice clips",
                    EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();

                var colorRect = GUILayoutUtility.GetRect(20, 20, GUILayout.Width(20));
                EditorGUI.DrawRect(colorRect, profile.NamePlateColor);

                if (GUILayout.Button("Select", GUILayout.Width(50)))
                    Selection.activeObject = profile;

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawStatistics()
        {
            if (_database == null)
            {
                EditorGUILayout.HelpBox("Select a DialogueDatabaseSO to view statistics.", MessageType.Info);
                return;
            }

            if (GUILayout.Button("Recompute Statistics", GUILayout.Height(24)))
                RecomputeStatistics();

            if (!_statsValid)
            {
                EditorGUILayout.HelpBox("Click 'Recompute Statistics' to calculate.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField($"Total Trees: {_statTotalTrees}");
            EditorGUILayout.LabelField($"Total Nodes: {_statTotalNodes}");
            EditorGUILayout.LabelField($"  Speech: {_statTotalSpeech}");
            EditorGUILayout.LabelField($"  Choice: {_statTotalChoice} ({_statTotalChoiceCount} total choices)");
            EditorGUILayout.LabelField($"  Condition: {_statTotalCondition}");
            EditorGUILayout.LabelField($"Max Tree Depth: {_statMaxDepth}");
            EditorGUILayout.LabelField($"Average Nodes/Tree: {(_statTotalTrees > 0 ? (float)_statTotalNodes / _statTotalTrees : 0):F1}");

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("EPIC 18.5 Coverage", EditorStyles.boldLabel);
            float voCoverage = _statTotalSpeech > 0 ? (float)_statNodesWithVO / _statTotalSpeech * 100f : 0f;
            float exprCoverage = _statTotalSpeech > 0 ? (float)_statSpeechWithExpression / _statTotalSpeech * 100f : 0f;
            EditorGUILayout.LabelField($"  VO Coverage: {_statNodesWithVO}/{_statTotalSpeech} ({voCoverage:F0}%)");
            EditorGUILayout.LabelField($"  Expression Coverage: {_statSpeechWithExpression}/{_statTotalSpeech} ({exprCoverage:F0}%)");

            EditorGUILayout.EndVertical();
        }

        private void RecomputeStatistics()
        {
            _statTotalNodes = 0; _statTotalSpeech = 0; _statTotalChoice = 0;
            _statTotalCondition = 0; _statTotalTrees = 0; _statMaxDepth = 0;
            _statTotalChoiceCount = 0; _statNodesWithVO = 0; _statSpeechWithExpression = 0;

            foreach (var tree in _database.Trees)
            {
                if (tree == null || tree.Nodes == null) continue;
                _statTotalTrees++;
                _statTotalNodes += tree.Nodes.Length;

                int treeDepth = CalculateTreeDepthIterative(tree);
                if (treeDepth > _statMaxDepth) _statMaxDepth = treeDepth;

                for (int i = 0; i < tree.Nodes.Length; i++)
                {
                    ref var node = ref tree.Nodes[i];
                    switch (node.NodeType)
                    {
                        case DialogueNodeType.Speech:
                            _statTotalSpeech++;
                            if (node.VoiceClip != null || !string.IsNullOrEmpty(node.AudioClipPath))
                                _statNodesWithVO++;
                            if (!string.IsNullOrEmpty(node.Expression))
                                _statSpeechWithExpression++;
                            break;
                        case DialogueNodeType.PlayerChoice:
                            _statTotalChoice++;
                            if (node.Choices != null) _statTotalChoiceCount += node.Choices.Length;
                            break;
                        case DialogueNodeType.Condition:
                            _statTotalCondition++;
                            break;
                    }
                }
            }

            _statsValid = true;
        }

        /// <summary>
        /// Iterative BFS-based depth calculation. No recursion (no stack overflow risk),
        /// no exponential blowup on diamond DAGs. O(N+E).
        /// </summary>
        private static int CalculateTreeDepthIterative(DialogueTreeSO tree)
        {
            if (tree.Nodes == null || tree.Nodes.Length == 0) return 0;

            var nodeIndex = new Dictionary<int, int>(tree.Nodes.Length);
            for (int i = 0; i < tree.Nodes.Length; i++)
                nodeIndex[tree.Nodes[i].NodeId] = i;

            var depth = new Dictionary<int, int>(tree.Nodes.Length);
            var queue = new Queue<int>();

            if (nodeIndex.ContainsKey(tree.StartNodeId))
            {
                queue.Enqueue(tree.StartNodeId);
                depth[tree.StartNodeId] = 1;
            }

            int maxDepth = 0;
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                int d = depth[current];
                if (d > maxDepth) maxDepth = d;

                if (!nodeIndex.TryGetValue(current, out int idx)) continue;
                ref var node = ref tree.Nodes[idx];

                void TryEnqueue(int childId)
                {
                    if (childId != 0 && !depth.ContainsKey(childId))
                    {
                        depth[childId] = d + 1;
                        queue.Enqueue(childId);
                    }
                }

                TryEnqueue(node.NextNodeId);
                TryEnqueue(node.TrueNodeId);
                TryEnqueue(node.FalseNodeId);
                if (node.Choices != null)
                    for (int c = 0; c < node.Choices.Length; c++)
                        TryEnqueue(node.Choices[c].NextNodeId);
                if (node.RandomEntries != null)
                    for (int r = 0; r < node.RandomEntries.Length; r++)
                        TryEnqueue(node.RandomEntries[r].NodeId);
            }

            return maxDepth;
        }
    }
}
#endif
