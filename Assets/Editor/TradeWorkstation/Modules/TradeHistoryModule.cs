using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using DIG.Trading;

namespace DIG.Editor.TradeWorkstation.Modules
{
    /// <summary>
    /// EPIC 17.3: Ring buffer viewer for TradeAuditLog.
    /// Shows completed/failed trades with ghost IDs, items, currencies, and result.
    /// </summary>
    public class TradeHistoryModule : ITradeWorkstationModule
    {
        private Vector2 _scroll;
        private string _filterGhostId = "";

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Trade History (Audit Log)", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to view trade history.", MessageType.Info);
                return;
            }

            var world = TradeWorkstationWindow.GetTradeWorld();
            if (world == null || !world.IsCreated)
            {
                EditorGUILayout.HelpBox("No ECS World available.", MessageType.Warning);
                return;
            }

            var em = world.EntityManager;
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<TradeConfig>());
            if (query.IsEmpty)
            {
                EditorGUILayout.LabelField("TradeConfig singleton not found.");
                return;
            }

            var configEntity = query.GetSingletonEntity();
            if (!em.HasBuffer<TradeAuditLog>(configEntity))
            {
                EditorGUILayout.LabelField("TradeAuditLog buffer not found.");
                return;
            }

            var auditLog = em.GetBuffer<TradeAuditLog>(configEntity, true);
            if (auditLog.Length == 0)
            {
                EditorGUILayout.LabelField("No trade history recorded yet.");
                return;
            }

            var auditState = em.HasComponent<TradeAuditState>(configEntity)
                ? em.GetComponentData<TradeAuditState>(configEntity)
                : default;
            int totalWritten = auditState.TotalWritten;
            int count = auditLog.Length;

            // Filter
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Filter by Ghost ID:", GUILayout.Width(120));
            _filterGhostId = EditorGUILayout.TextField(_filterGhostId, GUILayout.Width(80));
            if (GUILayout.Button("Clear", GUILayout.Width(50)))
                _filterGhostId = "";
            EditorGUILayout.LabelField($"Total: {totalWritten} (showing {count})", GUILayout.Width(160));
            EditorGUILayout.EndHorizontal();

            bool hasFilter = int.TryParse(_filterGhostId, out int filterGhost);

            EditorGUILayout.Space(4);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            // Header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Tick", EditorStyles.miniLabel, GUILayout.Width(70));
            EditorGUILayout.LabelField("Result", EditorStyles.miniLabel, GUILayout.Width(60));
            EditorGUILayout.LabelField("Initiator", EditorStyles.miniLabel, GUILayout.Width(70));
            EditorGUILayout.LabelField("Target", EditorStyles.miniLabel, GUILayout.Width(70));
            EditorGUILayout.LabelField("Items", EditorStyles.miniLabel, GUILayout.Width(40));
            EditorGUILayout.LabelField("Gold", EditorStyles.miniLabel, GUILayout.Width(60));
            EditorGUILayout.LabelField("Premium", EditorStyles.miniLabel, GUILayout.Width(60));
            EditorGUILayout.LabelField("Crafting", EditorStyles.miniLabel, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            // Display newest first — ring buffer walk from write cursor backwards
            int writeIdx = auditState.NextWriteIndex;
            for (int n = 0; n < count; n++)
            {
                int i = ((writeIdx - 1 - n) % count + count) % count;
                var entry = auditLog[i];

                if (hasFilter && entry.InitiatorGhostId != filterGhost && entry.TargetGhostId != filterGhost)
                    continue;

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.LabelField(entry.Timestamp.ToString(), GUILayout.Width(70));
                EditorGUILayout.LabelField(entry.ResultCode == 0 ? "OK" : "FAIL", GUILayout.Width(60));
                EditorGUILayout.LabelField($"G:{entry.InitiatorGhostId}", GUILayout.Width(70));
                EditorGUILayout.LabelField($"G:{entry.TargetGhostId}", GUILayout.Width(70));
                EditorGUILayout.LabelField(entry.ItemCount.ToString(), GUILayout.Width(40));
                EditorGUILayout.LabelField(FormatDelta(entry.GoldDelta), GUILayout.Width(60));
                EditorGUILayout.LabelField(FormatDelta(entry.PremiumDelta), GUILayout.Width(60));
                EditorGUILayout.LabelField(FormatDelta(entry.CraftingDelta), GUILayout.Width(60));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private static string FormatDelta(int delta)
        {
            if (delta == 0) return "-";
            return delta > 0 ? $"+{delta}" : delta.ToString();
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }
}
