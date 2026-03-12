using Unity.Collections;
using Unity.Entities;
using DIG.Party;
using DIG.Combat.Components;
using Player.Components;
using UnityEditor;
using UnityEngine;

namespace DIG.Editor.PartyWorkstation.Modules
{
    /// <summary>
    /// EPIC 17.2: Play-mode live party state viewer.
    /// Shows party entities, member lists with health/level, leader indicator,
    /// loot mode, and proximity flags per member.
    /// </summary>
    public class PartyInspectorModule : IPartyWorkstationModule
    {
        private Vector2 _scroll;

        public void OnGUI()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Party Inspector", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to inspect party data.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            var world = PartyWorkstationWindow.GetPartyWorld();
            if (world == null || !world.IsCreated)
            {
                EditorGUILayout.HelpBox("No valid ECS World found.", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            var em = world.EntityManager;

            // Query for party entities
            EntityQuery partyQuery;
            try
            {
                partyQuery = em.CreateEntityQuery(
                    ComponentType.ReadOnly<PartyTag>(),
                    ComponentType.ReadOnly<PartyState>());
            }
            catch
            {
                EditorGUILayout.HelpBox("PartyTag/PartyState components not found in world.", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            int partyCount = partyQuery.CalculateEntityCount();
            EditorGUILayout.LabelField($"Active Parties: {partyCount}");
            EditorGUILayout.Space(4);

            if (partyCount == 0)
            {
                EditorGUILayout.HelpBox("No active parties. Use PartyRpc(Invite) to create one.", MessageType.Info);
                partyQuery.Dispose();
                EditorGUILayout.EndVertical();
                return;
            }

            var partyEntities = partyQuery.ToEntityArray(Allocator.Temp);
            var partyStates = partyQuery.ToComponentDataArray<PartyState>(Allocator.Temp);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            for (int p = 0; p < partyEntities.Length; p++)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField($"Party {partyEntities[p].Index}:{partyEntities[p].Version}", EditorStyles.boldLabel);

                EditorGUI.indentLevel++;

                var state = partyStates[p];
                EditorGUILayout.LabelField($"Leader: Entity {state.LeaderEntity.Index}");
                EditorGUILayout.LabelField($"Loot Mode: {state.LootMode}");
                EditorGUILayout.LabelField($"Members: {state.MemberCount} / {state.MaxSize}");
                EditorGUILayout.LabelField($"Round Robin Index: {state.RoundRobinIndex}");
                EditorGUILayout.LabelField($"Created Tick: {state.CreationTick}");

                // Members
                if (em.HasBuffer<PartyMemberElement>(partyEntities[p]))
                {
                    var members = em.GetBuffer<PartyMemberElement>(partyEntities[p], true);
                    EditorGUILayout.Space(2);
                    EditorGUILayout.LabelField($"Members ({members.Length}):", EditorStyles.boldLabel);

                    EditorGUI.indentLevel++;
                    for (int m = 0; m < members.Length; m++)
                    {
                        var member = members[m];
                        string label = $"Entity {member.PlayerEntity.Index}";

                        if (member.PlayerEntity == state.LeaderEntity)
                            label += " [LEADER]";

                        // Health
                        if (em.HasComponent<Health>(member.PlayerEntity))
                        {
                            var health = em.GetComponentData<Health>(member.PlayerEntity);
                            label += $" | HP: {health.Current:F0}/{health.Max:F0}";
                        }

                        // Level
                        if (em.HasComponent<CharacterAttributes>(member.PlayerEntity))
                        {
                            var attrs = em.GetComponentData<CharacterAttributes>(member.PlayerEntity);
                            label += $" | Lv{attrs.Level}";
                        }

                        EditorGUILayout.LabelField(label);
                    }
                    EditorGUI.indentLevel--;
                }

                // Proximity
                if (em.HasBuffer<PartyProximityState>(partyEntities[p]))
                {
                    var proximity = em.GetBuffer<PartyProximityState>(partyEntities[p], true);
                    EditorGUILayout.Space(2);
                    EditorGUILayout.LabelField($"Proximity ({proximity.Length}):", EditorStyles.boldLabel);

                    EditorGUI.indentLevel++;
                    for (int px = 0; px < proximity.Length; px++)
                    {
                        var prox = proximity[px];
                        string flags = "";
                        if (prox.InXPRange) flags += "XP ";
                        if (prox.InLootRange) flags += "Loot ";
                        if (prox.InKillCreditRange) flags += "Kill ";
                        if (flags.Length == 0) flags = "(out of range)";

                        EditorGUILayout.LabelField($"Entity {prox.PlayerEntity.Index}: {flags.TrimEnd()}");
                    }
                    EditorGUI.indentLevel--;
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndScrollView();

            partyEntities.Dispose();
            partyStates.Dispose();
            partyQuery.Dispose();

            EditorGUILayout.EndVertical();
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }
}
