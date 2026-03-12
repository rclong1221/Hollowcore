#if UNITY_EDITOR
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace DIG.Dialogue.Editor
{
    /// <summary>
    /// EPIC 16.16: Play-mode dialogue tree preview.
    /// Shows active dialogue sessions, current nodes, and allows stepping through trees.
    /// </summary>
    public class LivePreviewModule : IDialogueModule
    {
        private Vector2 _scrollPos;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Live Preview", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to use Live Preview.", MessageType.Info);
                return;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                EditorGUILayout.HelpBox("No active World found.", MessageType.Warning);
                return;
            }

            var em = world.EntityManager;

            // Check for managed registry
            DialogueRegistryManaged registry = null;
            foreach (var w in World.All)
            {
                if (!w.IsCreated) continue;
                var sys = w.GetExistingSystemManaged<DialogueBootstrapSystem>();
                if (sys != null)
                {
                    var query = w.EntityManager.CreateEntityQuery(typeof(DialogueRegistryManaged));
                    if (query.CalculateEntityCount() > 0)
                    {
                        registry = query.GetSingleton<DialogueRegistryManaged>();
                        em = w.EntityManager;
                        break;
                    }
                }
            }

            if (registry == null)
            {
                EditorGUILayout.HelpBox("DialogueRegistryManaged not found. Bootstrap may not have run.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField($"Trees: {registry.TreeLookup?.Count ?? 0}  |  Barks: {registry.BarkLookup?.Count ?? 0}",
                EditorStyles.miniLabel);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Active Dialogue Sessions", EditorStyles.miniBoldLabel);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            var sessionQuery = em.CreateEntityQuery(typeof(DialogueSessionState));
            if (sessionQuery.CalculateEntityCount() == 0)
            {
                EditorGUILayout.LabelField("No active sessions.", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                var entities = sessionQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                var sessions = sessionQuery.ToComponentDataArray<DialogueSessionState>(Unity.Collections.Allocator.Temp);

                for (int i = 0; i < sessions.Length; i++)
                {
                    var session = sessions[i];
                    if (!session.IsActive) continue;

                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.LabelField($"NPC Entity: {entities[i].Index}", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Tree: {session.CurrentTreeId}  |  Node: {session.CurrentNodeId}");
                    EditorGUILayout.LabelField($"Player: {session.InteractingPlayer.Index}  |  Mask: 0x{session.ValidChoicesMask:X2}");
                    EditorGUILayout.LabelField($"Start Tick: {session.SessionStartTick}");

                    // Show current node details
                    var tree = registry.GetTree(session.CurrentTreeId);
                    if (tree != null)
                    {
                        int nodeIdx = tree.FindNodeIndex(session.CurrentNodeId);
                        if (nodeIdx >= 0)
                        {
                            ref var node = ref tree.Nodes[nodeIdx];
                            EditorGUILayout.Space(2);
                            EditorGUILayout.LabelField($"Type: {node.NodeType}", EditorStyles.miniLabel);
                            if (!string.IsNullOrEmpty(node.Text))
                                EditorGUILayout.LabelField($"Text: {node.Text}", EditorStyles.wordWrappedMiniLabel);
                        }
                    }

                    EditorGUILayout.EndVertical();
                }

                entities.Dispose();
                sessions.Dispose();
            }

            EditorGUILayout.EndScrollView();
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }
}
#endif
