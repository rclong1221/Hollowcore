#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DIG.Dialogue.Editor
{
    /// <summary>
    /// EPIC 16.16: Inspector for editing a selected dialogue node's properties.
    /// Driven by TreeEditorModule node selection.
    /// </summary>
    public class NodeInspectorModule : IDialogueModule
    {
        private DialogueTreeSO _tree;
        private int _nodeIndex = -1;
        private Vector2 _scrollPos;

        public void SetSelectedNode(DialogueTreeSO tree, int nodeIndex)
        {
            _tree = tree;
            _nodeIndex = nodeIndex;
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Node Inspector", EditorStyles.boldLabel);

            if (_tree == null || _nodeIndex < 0 || _nodeIndex >= _tree.Nodes.Length)
            {
                EditorGUILayout.HelpBox("Select a node in the Tree Editor tab.", MessageType.Info);
                return;
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            ref var node = ref _tree.Nodes[_nodeIndex];
            bool changed = false;

            // Core properties
            EditorGUILayout.LabelField($"Node ID: {node.NodeId}", EditorStyles.boldLabel);

            var newType = (DialogueNodeType)EditorGUILayout.EnumPopup("Node Type", node.NodeType);
            if (newType != node.NodeType) { node.NodeType = newType; changed = true; }

            EditorGUILayout.Space(4);

            // Speech fields
            if (node.NodeType == DialogueNodeType.Speech || node.NodeType == DialogueNodeType.Hub)
            {
                EditorGUILayout.LabelField("Speech", EditorStyles.miniBoldLabel);
                var newSpeaker = EditorGUILayout.TextField("Speaker Name (key)", node.SpeakerName);
                if (newSpeaker != node.SpeakerName) { node.SpeakerName = newSpeaker; changed = true; }

                EditorGUILayout.LabelField("Text (key):");
                var newText = EditorGUILayout.TextArea(node.Text, GUILayout.Height(60));
                if (newText != node.Text) { node.Text = newText; changed = true; }

                var newAudio = EditorGUILayout.TextField("Audio Clip Path", node.AudioClipPath);
                if (newAudio != node.AudioClipPath) { node.AudioClipPath = newAudio; changed = true; }

                var newDuration = EditorGUILayout.FloatField("Auto-Advance (sec, 0=manual)", node.Duration);
                if (!Mathf.Approximately(newDuration, node.Duration)) { node.Duration = newDuration; changed = true; }
            }

            // Navigation
            if (node.NodeType != DialogueNodeType.End)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Navigation", EditorStyles.miniBoldLabel);
                var newNext = EditorGUILayout.IntField("Next Node ID", node.NextNodeId);
                if (newNext != node.NextNodeId) { node.NextNodeId = newNext; changed = true; }
            }

            // Camera
            EditorGUILayout.Space(4);
            var newCam = (DialogueCameraMode)EditorGUILayout.EnumPopup("Camera Mode", node.CameraMode);
            if (newCam != node.CameraMode) { node.CameraMode = newCam; changed = true; }

            // Condition node fields
            if (node.NodeType == DialogueNodeType.Condition)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Condition", EditorStyles.miniBoldLabel);
                var newCondType = (DialogueConditionType)EditorGUILayout.EnumPopup("Type", node.ConditionType);
                if (newCondType != node.ConditionType) { node.ConditionType = newCondType; changed = true; }

                var newCondVal = EditorGUILayout.IntField("Value", node.ConditionValue);
                if (newCondVal != node.ConditionValue) { node.ConditionValue = newCondVal; changed = true; }

                var newTrue = EditorGUILayout.IntField("True Node ID", node.TrueNodeId);
                if (newTrue != node.TrueNodeId) { node.TrueNodeId = newTrue; changed = true; }

                var newFalse = EditorGUILayout.IntField("False Node ID", node.FalseNodeId);
                if (newFalse != node.FalseNodeId) { node.FalseNodeId = newFalse; changed = true; }
            }

            // Choice editing
            if (node.NodeType == DialogueNodeType.PlayerChoice)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Choices", EditorStyles.miniBoldLabel);
                DrawChoicesEditor(ref node, ref changed);
            }

            // Action editing
            if (node.NodeType == DialogueNodeType.Action)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Actions", EditorStyles.miniBoldLabel);
                DrawActionsEditor(ref node, ref changed);
            }

            // Random entries
            if (node.NodeType == DialogueNodeType.Random)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Random Entries", EditorStyles.miniBoldLabel);
                DrawRandomEditor(ref node, ref changed);
            }

            EditorGUILayout.EndScrollView();

            if (changed)
            {
                _tree.Nodes[_nodeIndex] = node;
                EditorUtility.SetDirty(_tree);
            }
        }

        public void OnSceneGUI(SceneView sceneView) { }

        private void DrawChoicesEditor(ref DialogueNode node, ref bool changed)
        {
            if (node.Choices == null) node.Choices = new DialogueChoice[0];

            for (int i = 0; i < node.Choices.Length; i++)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"Choice {i}", EditorStyles.miniLabel);

                var c = node.Choices[i];
                c.Text = EditorGUILayout.TextField("Text (key)", c.Text);
                c.NextNodeId = EditorGUILayout.IntField("Next Node ID", c.NextNodeId);
                c.ConditionType = (DialogueConditionType)EditorGUILayout.EnumPopup("Condition", c.ConditionType);
                if (c.ConditionType != DialogueConditionType.None)
                    c.ConditionValue = EditorGUILayout.IntField("Condition Value", c.ConditionValue);

                if (!c.Equals(node.Choices[i])) { node.Choices[i] = c; changed = true; }

                if (GUILayout.Button("Remove", GUILayout.Width(60)))
                {
                    var list = new System.Collections.Generic.List<DialogueChoice>(node.Choices);
                    list.RemoveAt(i);
                    node.Choices = list.ToArray();
                    changed = true;
                    break;
                }
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("Add Choice", GUILayout.Width(100)))
            {
                var list = new System.Collections.Generic.List<DialogueChoice>(node.Choices);
                list.Add(new DialogueChoice { ChoiceIndex = list.Count, Text = "New choice" });
                node.Choices = list.ToArray();
                changed = true;
            }
        }

        private void DrawActionsEditor(ref DialogueNode node, ref bool changed)
        {
            if (node.Actions == null) node.Actions = new DialogueAction[0];

            for (int i = 0; i < node.Actions.Length; i++)
            {
                EditorGUILayout.BeginVertical("box");
                var a = node.Actions[i];
                a.ActionType = (DialogueActionType)EditorGUILayout.EnumPopup("Action", a.ActionType);
                a.IntValue = EditorGUILayout.IntField("IntValue", a.IntValue);
                a.IntValue2 = EditorGUILayout.IntField("IntValue2", a.IntValue2);

                if (!a.Equals(node.Actions[i])) { node.Actions[i] = a; changed = true; }

                if (GUILayout.Button("Remove", GUILayout.Width(60)))
                {
                    var list = new System.Collections.Generic.List<DialogueAction>(node.Actions);
                    list.RemoveAt(i);
                    node.Actions = list.ToArray();
                    changed = true;
                    break;
                }
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("Add Action", GUILayout.Width(100)))
            {
                var list = new System.Collections.Generic.List<DialogueAction>(node.Actions);
                list.Add(new DialogueAction());
                node.Actions = list.ToArray();
                changed = true;
            }
        }

        private void DrawRandomEditor(ref DialogueNode node, ref bool changed)
        {
            if (node.RandomEntries == null) node.RandomEntries = new RandomNodeEntry[0];

            for (int i = 0; i < node.RandomEntries.Length; i++)
            {
                EditorGUILayout.BeginHorizontal();
                var r = node.RandomEntries[i];
                r.NodeId = EditorGUILayout.IntField("Node ID", r.NodeId);
                r.Weight = EditorGUILayout.FloatField("Weight", r.Weight);

                if (!r.Equals(node.RandomEntries[i])) { node.RandomEntries[i] = r; changed = true; }

                if (GUILayout.Button("X", GUILayout.Width(24)))
                {
                    var list = new System.Collections.Generic.List<RandomNodeEntry>(node.RandomEntries);
                    list.RemoveAt(i);
                    node.RandomEntries = list.ToArray();
                    changed = true;
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Add Entry", GUILayout.Width(100)))
            {
                var list = new System.Collections.Generic.List<RandomNodeEntry>(node.RandomEntries);
                list.Add(new RandomNodeEntry { Weight = 1f });
                node.RandomEntries = list.ToArray();
                changed = true;
            }
        }
    }
}
#endif
