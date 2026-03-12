using DIG.Accessibility.Config;
using Unity.Entities;
using UnityEngine;

namespace DIG.Accessibility.Cognitive
{
    /// <summary>
    /// EPIC 18.12: ECS singleton holding accessibility difficulty modifiers.
    /// Combat systems read via SystemAPI.TryGetSingleton&lt;DifficultyModifiers&gt;().
    /// Synced from PlayerPrefs by DifficultyModifierBridgeSystem.
    /// </summary>
    public struct DifficultyModifiers : IComponentData
    {
        /// <summary>Enemy HP multiplier (0.25 to 2.0). Default 1.0.</summary>
        public float EnemyHPMultiplier;

        /// <summary>Enemy damage multiplier (0.25 to 2.0). Default 1.0.</summary>
        public float EnemyDamageMultiplier;

        /// <summary>Timing window multiplier for dodge/parry (0.5 to 2.0). Default 1.0.</summary>
        public float TimingWindowMultiplier;

        /// <summary>Resource/XP gain multiplier (0.5 to 3.0). Default 1.0.</summary>
        public float ResourceGainMultiplier;

        /// <summary>Respawn penalty level. Default Normal.</summary>
        public RespawnPenalty RespawnPenalty;

        public static DifficultyModifiers Default => new()
        {
            EnemyHPMultiplier = 1f,
            EnemyDamageMultiplier = 1f,
            TimingWindowMultiplier = 1f,
            ResourceGainMultiplier = 1f,
            RespawnPenalty = RespawnPenalty.Normal
        };
    }

    /// <summary>
    /// EPIC 18.12: Managed bridge system syncing accessibility difficulty settings
    /// from AccessibilityService (PlayerPrefs) → ECS singleton.
    /// Runs once per frame with dirty check to avoid unnecessary writes.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class DifficultyModifierBridgeSystem : SystemBase
    {
        private Entity _singletonEntity;
        private float _lastEnemyHP = -1f;
        private float _lastEnemyDmg = -1f;
        private float _lastTiming = -1f;
        private float _lastResource = -1f;
        private int _lastRespawn = -1;

        protected override void OnCreate()
        {
            _singletonEntity = EntityManager.CreateEntity(typeof(DifficultyModifiers));
            EntityManager.SetComponentData(_singletonEntity, DifficultyModifiers.Default);
        }

        protected override void OnUpdate()
        {
            if (!AccessibilityService.HasInstance) return;
            var svc = AccessibilityService.Instance;

            float hp = svc.EnemyHPMultiplier;
            float dmg = svc.EnemyDamageMultiplier;
            float tw = svc.TimingWindowMultiplier;
            float rg = svc.ResourceGainMultiplier;
            int rp = (int)svc.RespawnPenalty;

            // Dirty check — avoid SetComponentData every frame
            if (Mathf.Approximately(hp, _lastEnemyHP) &&
                Mathf.Approximately(dmg, _lastEnemyDmg) &&
                Mathf.Approximately(tw, _lastTiming) &&
                Mathf.Approximately(rg, _lastResource) &&
                rp == _lastRespawn)
                return;

            _lastEnemyHP = hp;
            _lastEnemyDmg = dmg;
            _lastTiming = tw;
            _lastResource = rg;
            _lastRespawn = rp;

            EntityManager.SetComponentData(_singletonEntity, new DifficultyModifiers
            {
                EnemyHPMultiplier = hp,
                EnemyDamageMultiplier = dmg,
                TimingWindowMultiplier = tw,
                ResourceGainMultiplier = rg,
                RespawnPenalty = (RespawnPenalty)rp
            });
        }

        protected override void OnDestroy()
        {
            if (EntityManager.Exists(_singletonEntity))
                EntityManager.DestroyEntity(_singletonEntity);
        }
    }
}
