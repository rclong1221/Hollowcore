using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using DIG.Trading;

namespace DIG.Editor.TradeWorkstation.Modules
{
    /// <summary>
    /// EPIC 17.3: Live list of all TradeSession entities.
    /// Shows participants, state, offer count, and elapsed time.
    /// </summary>
    public class ActiveTradesModule : ITradeWorkstationModule
    {
        private Vector2 _scroll;

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Active Trade Sessions", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to view active trades.", MessageType.Info);
                return;
            }

            var world = TradeWorkstationWindow.GetTradeWorld();
            if (world == null || !world.IsCreated)
            {
                EditorGUILayout.HelpBox("No ECS World available.", MessageType.Warning);
                return;
            }

            var em = world.EntityManager;
            var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<TradeSessionTag>(),
                ComponentType.ReadOnly<TradeSessionState>());

            var sessions = query.ToComponentDataArray<TradeSessionState>(Allocator.Temp);
            var sessionEntities = query.ToEntityArray(Allocator.Temp);

            if (sessions.Length == 0)
            {
                EditorGUILayout.LabelField("No active trade sessions.");
                sessions.Dispose();
                sessionEntities.Dispose();
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            // Header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Entity", EditorStyles.miniLabel, GUILayout.Width(80));
            EditorGUILayout.LabelField("State", EditorStyles.miniLabel, GUILayout.Width(80));
            EditorGUILayout.LabelField("Initiator", EditorStyles.miniLabel, GUILayout.Width(100));
            EditorGUILayout.LabelField("Target", EditorStyles.miniLabel, GUILayout.Width(100));
            EditorGUILayout.LabelField("Offers", EditorStyles.miniLabel, GUILayout.Width(50));
            EditorGUILayout.LabelField("Created Tick", EditorStyles.miniLabel, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < sessions.Length; i++)
            {
                var state = sessions[i];
                var entity = sessionEntities[i];

                int offerCount = 0;
                if (em.HasBuffer<TradeOffer>(entity))
                    offerCount = em.GetBuffer<TradeOffer>(entity, true).Length;

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"E:{entity.Index}", GUILayout.Width(80));
                EditorGUILayout.LabelField(state.State.ToString(), GUILayout.Width(80));
                EditorGUILayout.LabelField($"E:{state.InitiatorEntity.Index}", GUILayout.Width(100));
                EditorGUILayout.LabelField($"E:{state.TargetEntity.Index}", GUILayout.Width(100));
                EditorGUILayout.LabelField(offerCount.ToString(), GUILayout.Width(50));
                EditorGUILayout.LabelField(state.CreationTick.ToString(), GUILayout.Width(80));
                EditorGUILayout.EndHorizontal();
            }

            sessions.Dispose();
            sessionEntities.Dispose();
            EditorGUILayout.EndScrollView();
        }

        public void OnSceneGUI(SceneView sceneView) { }
    }
}
