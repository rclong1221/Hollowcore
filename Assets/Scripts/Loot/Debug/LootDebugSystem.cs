using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using DIG.Items;
using DIG.Loot.Components;
using UnityEngine;

namespace DIG.Loot.Debug
{
    /// <summary>
    /// EPIC 16.6: Runtime debug overlay for loot entities.
    /// Draws gizmo dots at loot positions, shows hover info, logs drops.
    /// Only active in development builds.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class LootDebugSystem : SystemBase
    {
        private bool _enabled;

        protected override void OnCreate()
        {
            #if !DEVELOPMENT_BUILD && !UNITY_EDITOR
            Enabled = false;
            #endif
        }

        protected override void OnUpdate()
        {
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_enabled) return;

            foreach (var (pickup, lifetime, lootTransform) in
                     SystemAPI.Query<RefRO<ItemPickup>, RefRO<LootLifetimeECS>, RefRO<LocalTransform>>()
                     .WithAll<LootEntity>())
            {
                float3 pos = lootTransform.ValueRO.Position;
                float remaining = lifetime.ValueRO.Lifetime - ((float)SystemAPI.Time.ElapsedTime - lifetime.ValueRO.SpawnTime);

                // Debug draw (visible in Scene view)
                UnityEngine.Debug.DrawRay(
                    new Vector3(pos.x, pos.y, pos.z),
                    Vector3.up * 2f,
                    remaining > 10f ? Color.green : Color.red,
                    0f
                );
            }
            #endif
        }

        /// <summary>
        /// Toggle debug visualization. Call from console or debug UI.
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
        }
    }
}
