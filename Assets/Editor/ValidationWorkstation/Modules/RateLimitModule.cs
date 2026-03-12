using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEditor;
using UnityEngine;

namespace DIG.Validation.Editor.Modules
{
    /// <summary>
    /// EPIC 17.11: Per-player per-RPC-type token bucket visualization.
    /// Bar graph showing current token count / max burst.
    /// </summary>
    public class RateLimitModule : IValidationWorkstationModule
    {
        public string ModuleName => "Rate Limits";

        private Vector2 _scroll;

        private static readonly string[] RpcNames =
        {
            "DialogueChoice", "DialogueSkip", "CraftRequest",
            "StatAllocation", "TalentAllocation", "TalentRespec",
            "VoxelDamage", "TradeRequest", "Respawn"
        };

        public void OnGUI()
        {
            EditorGUILayout.LabelField("RPC Rate Limit Monitor", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play mode to see live rate limit data.", MessageType.Info);
                return;
            }

            var world = ValidationWorkstationWindow.GetValidationWorld();
            if (world == null || !world.IsCreated)
            {
                EditorGUILayout.HelpBox("No ECS World available.", MessageType.Warning);
                return;
            }

            var em = world.EntityManager;
            var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<ValidationChildTag>(),
                ComponentType.ReadOnly<ValidationOwner>(),
                ComponentType.ReadOnly<RateLimitEntry>());

            int count = query.CalculateEntityCount();
            if (count == 0)
            {
                EditorGUILayout.HelpBox("No players with rate limit buffers found.", MessageType.Info);
                return;
            }

            var entities = query.ToEntityArray(Allocator.Temp);
            var owners = query.ToComponentDataArray<ValidationOwner>(Allocator.Temp);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            for (int i = 0; i < entities.Length; i++)
            {
                var player = owners[i].Owner;
                int netId = 0;
                if (em.Exists(player) && em.HasComponent<GhostOwner>(player))
                    netId = em.GetComponentData<GhostOwner>(player).NetworkId;

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"Player NetID: {netId}", EditorStyles.boldLabel);

                if (em.HasBuffer<RateLimitEntry>(entities[i]))
                {
                    var buffer = em.GetBuffer<RateLimitEntry>(entities[i], true);
                    for (int j = 0; j < buffer.Length; j++)
                    {
                        var entry = buffer[j];
                        string name = entry.RpcTypeId > 0 && entry.RpcTypeId <= RpcNames.Length
                            ? RpcNames[entry.RpcTypeId - 1]
                            : $"RPC#{entry.RpcTypeId}";

                        float maxBurst = 5f; // Default
                        float ratio = maxBurst > 0 ? entry.TokenCount / maxBurst : 1f;

                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Label(name, GUILayout.Width(120));

                        // Bar graph
                        var barRect = GUILayoutUtility.GetRect(200, 16);
                        EditorGUI.DrawRect(barRect, new Color(0.2f, 0.2f, 0.2f));

                        var fillRect = new Rect(barRect.x, barRect.y,
                            barRect.width * Mathf.Clamp01(ratio), barRect.height);
                        Color barColor = ratio > 0.5f ? Color.green :
                                         ratio > 0.2f ? Color.yellow : Color.red;
                        EditorGUI.DrawRect(fillRect, barColor);

                        GUILayout.Label($"{entry.TokenCount:F1}", GUILayout.Width(40));
                        EditorGUILayout.EndHorizontal();
                    }
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
            }

            EditorGUILayout.EndScrollView();

            entities.Dispose();
            owners.Dispose();
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }
}
