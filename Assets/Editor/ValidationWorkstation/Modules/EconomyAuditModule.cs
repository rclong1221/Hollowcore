using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEditor;
using UnityEngine;

namespace DIG.Validation.Editor.Modules
{
    /// <summary>
    /// EPIC 17.11: Per-player transaction log (EconomyAuditEntry history).
    /// Sortable by tick, amount, type. Balance reconciliation check.
    /// </summary>
    public class EconomyAuditModule : IValidationWorkstationModule
    {
        public string ModuleName => "Economy Audit";

        private Vector2 _scroll;
        private int _selectedPlayer;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Economy Audit Trail", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play mode to see economy audit data.", MessageType.Info);
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
                ComponentType.ReadOnly<EconomyAuditEntry>());

            int count = query.CalculateEntityCount();
            if (count == 0)
            {
                EditorGUILayout.HelpBox("No players with audit buffers found.", MessageType.Info);
                return;
            }

            var entities = query.ToEntityArray(Allocator.Temp);
            var owners = query.ToComponentDataArray<ValidationOwner>(Allocator.Temp);

            // Player selector
            string[] playerLabels = new string[count];
            for (int i = 0; i < count; i++)
            {
                int netId = 0;
                var player = owners[i].Owner;
                if (em.Exists(player) && em.HasComponent<GhostOwner>(player))
                    netId = em.GetComponentData<GhostOwner>(player).NetworkId;
                playerLabels[i] = $"Player NetID: {netId}";
            }

            _selectedPlayer = Mathf.Clamp(_selectedPlayer, 0, count - 1);
            _selectedPlayer = EditorGUILayout.Popup("Player", _selectedPlayer, playerLabels);

            EditorGUILayout.Space(4);

            // Show audit entries for selected player
            var selectedEntity = entities[_selectedPlayer];
            if (em.HasBuffer<EconomyAuditEntry>(selectedEntity))
            {
                var buffer = em.GetBuffer<EconomyAuditEntry>(selectedEntity, true);

                EditorGUILayout.LabelField($"Audit entries: {buffer.Length}");

                // Header
                EditorGUILayout.BeginHorizontal("box");
                GUILayout.Label("Tick", EditorStyles.boldLabel, GUILayout.Width(60));
                GUILayout.Label("Type", EditorStyles.boldLabel, GUILayout.Width(60));
                GUILayout.Label("Source", EditorStyles.boldLabel, GUILayout.Width(70));
                GUILayout.Label("Amount", EditorStyles.boldLabel, GUILayout.Width(70));
                GUILayout.Label("Before", EditorStyles.boldLabel, GUILayout.Width(60));
                GUILayout.Label("After", EditorStyles.boldLabel, GUILayout.Width(60));
                EditorGUILayout.EndHorizontal();

                _scroll = EditorGUILayout.BeginScrollView(_scroll);

                // Show most recent first
                for (int j = buffer.Length - 1; j >= 0; j--)
                {
                    var entry = buffer[j];
                    EditorGUILayout.BeginHorizontal("box");
                    GUILayout.Label(entry.ServerTick.ToString(), GUILayout.Width(60));
                    GUILayout.Label(entry.TransactionType == 0 ? "Gold" : entry.TransactionType == 1 ? "Premium" : "Craft", GUILayout.Width(60));
                    GUILayout.Label(((TransactionSourceSystem)entry.SourceSystem).ToString(), GUILayout.Width(70));

                    var prevColor = GUI.contentColor;
                    GUI.contentColor = entry.Amount >= 0 ? Color.green : Color.red;
                    GUILayout.Label((entry.Amount >= 0 ? "+" : "") + entry.Amount.ToString(), GUILayout.Width(70));
                    GUI.contentColor = prevColor;

                    GUILayout.Label(entry.BalanceBefore.ToString(), GUILayout.Width(60));
                    GUILayout.Label(entry.BalanceAfter.ToString(), GUILayout.Width(60));
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndScrollView();
            }

            entities.Dispose();
            owners.Dispose();
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }
}
