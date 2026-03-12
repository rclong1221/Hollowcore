#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DIG.Dialogue.Editor
{
    /// <summary>
    /// EPIC 16.16: Custom inspector for DialogueTreeSO.
    /// Shows validation summary, node type counts, and default inspector.
    /// </summary>
    [CustomEditor(typeof(DialogueTreeSO))]
    public class DialogueTreeSOEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var tree = (DialogueTreeSO)target;

            // Validation header
            DrawValidation(tree);
            EditorGUILayout.Space(4);

            // Default inspector
            DrawDefaultInspector();
            EditorGUILayout.Space(8);

            // Summary
            DrawSummary(tree);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Open in Graph Editor"))
            {
                DialogueGraphEditorWindow.OpenTree(tree);
            }
            if (GUILayout.Button("Open in Workstation"))
            {
                DialogueWorkstationWindow.ShowWindow();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawValidation(DialogueTreeSO tree)
        {
            bool hasErrors = false;

            if (tree.TreeId <= 0)
            {
                EditorGUILayout.HelpBox("TreeId must be > 0.", MessageType.Error);
                hasErrors = true;
            }

            if (tree.Nodes == null || tree.Nodes.Length == 0)
            {
                EditorGUILayout.HelpBox("Tree has no nodes.", MessageType.Error);
                hasErrors = true;
            }
            else
            {
                bool startFound = false;
                for (int i = 0; i < tree.Nodes.Length; i++)
                {
                    if (tree.Nodes[i].NodeId == tree.StartNodeId)
                    {
                        startFound = true;
                        break;
                    }
                }
                if (!startFound)
                {
                    EditorGUILayout.HelpBox($"StartNodeId {tree.StartNodeId} does not match any node.", MessageType.Error);
                    hasErrors = true;
                }

                // Check for End node
                bool hasEnd = false;
                for (int i = 0; i < tree.Nodes.Length; i++)
                {
                    if (tree.Nodes[i].NodeType == DialogueNodeType.End)
                    {
                        hasEnd = true;
                        break;
                    }
                }
                if (!hasEnd)
                    EditorGUILayout.HelpBox("Tree has no End node.", MessageType.Warning);
            }

            if (!hasErrors)
            {
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
                EditorGUILayout.LabelField("Tree valid", EditorStyles.helpBox);
                GUI.backgroundColor = prevBg;
            }
        }

        private void DrawSummary(DialogueTreeSO tree)
        {
            if (tree.Nodes == null || tree.Nodes.Length == 0) return;

            EditorGUILayout.LabelField("Node Summary", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            // Count by type
            var counts = new Dictionary<DialogueNodeType, int>();
            for (int i = 0; i < tree.Nodes.Length; i++)
            {
                var type = tree.Nodes[i].NodeType;
                counts.TryGetValue(type, out int count);
                counts[type] = count + 1;
            }

            foreach (var kv in counts)
                EditorGUILayout.LabelField($"  {kv.Key}: {kv.Value}", EditorStyles.miniLabel);

            EditorGUILayout.LabelField($"  Total: {tree.Nodes.Length}", EditorStyles.miniBoldLabel);
            EditorGUILayout.EndVertical();
        }
    }
}
#endif
