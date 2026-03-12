using Unity.Entities;
using Unity.NetCode;
using Unity.Collections;
using UnityEngine;
using Player.Components;
using DIG.Combat.Components;

namespace DIG.Combat.Systems
{
    /// <summary>
    /// DIAGNOSTIC: Tracks per-player Health changes in real-time.
    /// Logs immediately when any player's health changes, plus periodic snapshots.
    /// Remove after debugging is complete.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class HealthDiagnosticSystem : SystemBase
    {
        private float _snapshotTimer;

        // Track previous health for change detection (up to 8 players)
        private NativeHashMap<Entity, float> _prevHealth;

        protected override void OnCreate()
        {
            RequireForUpdate<NetworkTime>();
            _prevHealth = new NativeHashMap<Entity, float>(8, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            if (_prevHealth.IsCreated) _prevHealth.Dispose();
        }

        protected override void OnUpdate()
        {
            // ====== REAL-TIME HEALTH CHANGE DETECTION (every frame) ======
            foreach (var (health, ghostOwner, entity) in
                SystemAPI.Query<RefRO<Health>, RefRO<GhostOwner>>()
                    .WithAll<PlayerTag>()
                    .WithEntityAccess())
            {
                float current = health.ValueRO.Current;
                int netId = ghostOwner.ValueRO.NetworkId;

                if (_prevHealth.TryGetValue(entity, out float prev))
                {
                    if (prev != current)
                    {
                        float delta = current - prev;
                        Debug.LogWarning($"[HealthDiag] HEALTH CHANGED: entity={entity.Index}:{entity.Version} " +
                            $"NetworkId={netId} " +
                            $"{prev:F1} → {current:F1} (delta={delta:F1})");
                        _prevHealth[entity] = current;
                    }
                }
                else
                {
                    _prevHealth.TryAdd(entity, current);
                }
            }

            // ====== PERIODIC SNAPSHOT (every 3 seconds) ======
            _snapshotTimer += SystemAPI.Time.DeltaTime;
            if (_snapshotTimer < 3f) return;
            _snapshotTimer = 0f;

            int rootCount = 0;
            foreach (var (health, ghostOwner, entity) in
                SystemAPI.Query<RefRO<Health>, RefRO<GhostOwner>>()
                    .WithAll<PlayerTag>()
                    .WithEntityAccess())
            {
                bool hasDamageEvent = EntityManager.HasBuffer<DamageEvent>(entity);
                int damageEventCount = 0;
                if (hasDamageEvent)
                    damageEventCount = EntityManager.GetBuffer<DamageEvent>(entity).Length;

                bool hasHitboxOwnerLink = EntityManager.HasComponent<HitboxOwnerLink>(entity);
                string linkInfo = "";
                if (hasHitboxOwnerLink)
                {
                    var link = EntityManager.GetComponentData<HitboxOwnerLink>(entity);
                    linkInfo = $" HitboxOwnerLink→{link.HitboxOwner.Index}:{link.HitboxOwner.Version}";
                }

                Debug.Log($"[HealthDiag] SNAPSHOT: entity={entity.Index}:{entity.Version} " +
                    $"NetworkId={ghostOwner.ValueRO.NetworkId} " +
                    $"Health={health.ValueRO.Current:F1}/{health.ValueRO.Max:F1} " +
                    $"DamageEvents={damageEventCount}{linkInfo}");
                rootCount++;
            }

            if (rootCount > 0)
                Debug.Log($"[HealthDiag] {rootCount} players tracked");
        }
    }
}
