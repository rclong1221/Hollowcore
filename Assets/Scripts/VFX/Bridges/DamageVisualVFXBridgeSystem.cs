using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using DIG.Combat.UI;
using DIG.Targeting.Theming;

namespace DIG.VFX.Bridges
{
    /// <summary>
    /// EPIC 16.7 Phase 2: Drains DamageVisualQueue for hit-reaction VFX only
    /// (blood splatter, elemental bursts). Does NOT replace damage number UI routing —
    /// CombatUIBridgeSystem continues to read DamageVisualQueue independently.
    ///
    /// NOTE: This bridge peeks at the queue without consuming. It lets CombatUIBridgeSystem
    /// consume the queue as usual — this bridge reads a separate copy stored each frame.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(Systems.VFXExecutionSystem))]
    public partial class DamageVisualVFXBridgeSystem : SystemBase
    {
        private static readonly System.Collections.Generic.List<DamageVisualData> _frameBuffer = new(32);

        protected override void OnCreate()
        {
            RequireForUpdate<VFXBudgetConfig>();
            // Disabled by default — enable when VFX assets exist
            Enabled = false;
        }

        protected override void OnUpdate()
        {
            if (!SystemAPI.HasSingleton<VFXBudgetConfig>()) return;
            if (_frameBuffer.Count == 0) return;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            for (int i = 0; i < _frameBuffer.Count; i++)
            {
                var data = _frameBuffer[i];

                var entity = ecb.CreateEntity();
                ecb.AddComponent(entity, new VFXRequest
                {
                    Position = data.HitPosition,
                    Rotation = quaternion.identity,
                    VFXTypeId = MapDamageTypeToVFX(data.DamageType),
                    Category = VFXCategory.Combat,
                    Intensity = math.saturate(data.Damage / 100f),
                    Scale = 1f,
                    ColorTint = default,
                    Duration = 0f,
                    SourceEntity = Entity.Null,
                    Priority = MapHitPriority(data.HitType)
                });
                ecb.AddComponent<VFXCulled>(entity);
                ecb.SetComponentEnabled<VFXCulled>(entity, false);
                ecb.AddComponent<VFXCleanupTag>(entity);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
            _frameBuffer.Clear();
        }

        /// <summary>
        /// Called by CombatUIBridgeSystem or similar to feed hit data for VFX generation.
        /// This avoids double-consuming the DamageVisualQueue.
        /// </summary>
        public static void NotifyDamageVisual(DamageVisualData data)
        {
            _frameBuffer.Add(data);
        }

        private static int MapDamageTypeToVFX(DamageType damageType) => damageType switch
        {
            DamageType.Physical => VFXTypeIds.DeathBloodSplatter,
            DamageType.Fire => VFXTypeIds.AbilityFireBurst,
            DamageType.Ice => VFXTypeIds.AbilityIceBurst,
            DamageType.Lightning => VFXTypeIds.AbilityLightningStrike,
            DamageType.Poison => VFXTypeIds.AbilityPoisonCloud,
            DamageType.Holy => VFXTypeIds.AbilityHolySmite,
            DamageType.Shadow => VFXTypeIds.AbilityShadowBlast,
            DamageType.Arcane => VFXTypeIds.AbilityArcanePulse,
            _ => VFXTypeIds.BulletImpactDefault
        };

        private static int MapHitPriority(HitType hitType) => hitType switch
        {
            HitType.Critical => 80,
            HitType.Hit => 30,
            HitType.Graze => 10,
            HitType.Miss => 0,
            _ => 20
        };
    }
}
