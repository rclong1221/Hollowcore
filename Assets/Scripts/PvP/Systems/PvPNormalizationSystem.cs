using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using DIG.Combat.Components;
using Player.Components;

namespace DIG.PvP
{
    /// <summary>
    /// EPIC 17.10: Optional stat equalization for competitive fairness.
    /// Overrides AttackStats/DefenseStats/Health when normalization is enabled.
    /// Saves originals in PvPStatOverride, restores on match end.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PvPCollisionSystem))]
    public partial class PvPNormalizationSystem : SystemBase
    {
        private EntityQuery _matchStateQuery;
        private EntityQuery _playerQuery;
        private PvPMatchPhase _previousPhase;
        private bool _normalized;

        protected override void OnCreate()
        {
            _matchStateQuery = GetEntityQuery(ComponentType.ReadOnly<PvPMatchState>());
            _playerQuery = GetEntityQuery(
                ComponentType.ReadWrite<Health>(),
                ComponentType.ReadWrite<AttackStats>(),
                ComponentType.ReadWrite<DefenseStats>(),
                ComponentType.ReadWrite<PvPStatOverride>(),
                ComponentType.ReadOnly<PlayerTag>());
            RequireForUpdate<PvPConfigSingleton>();
        }

        protected override void OnUpdate()
        {
            if (_matchStateQuery.CalculateEntityCount() == 0)
            {
                if (_normalized)
                    RestoreStats();
                return;
            }

            ref var config = ref SystemAPI.GetSingleton<PvPConfigSingleton>().Config.Value;
            if (config.NormalizationEnabled == 0) return;

            var state = SystemAPI.GetSingleton<PvPMatchState>();

            // Apply normalization on warmup entry
            if (state.Phase == PvPMatchPhase.Warmup && _previousPhase == PvPMatchPhase.WaitingForPlayers && !_normalized)
            {
                ApplyNormalization(ref config);
                _normalized = true;
            }

            // Restore on match end
            if (state.Phase == PvPMatchPhase.Ended && _normalized)
            {
                RestoreStats();
            }

            _previousPhase = state.Phase;
        }

        private void ApplyNormalization(ref PvPConfigBlob config)
        {
            var entities = _playerQuery.ToEntityArray(Allocator.Temp);
            var healths = _playerQuery.ToComponentDataArray<Health>(Allocator.Temp);
            var attacks = _playerQuery.ToComponentDataArray<AttackStats>(Allocator.Temp);
            var defenses = _playerQuery.ToComponentDataArray<DefenseStats>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                // Save originals
                var overrides = new PvPStatOverride
                {
                    OriginalMaxHealth = healths[i].Max,
                    OriginalAttackPower = attacks[i].AttackPower,
                    OriginalSpellPower = attacks[i].SpellPower,
                    OriginalDefense = defenses[i].Defense,
                    OriginalArmor = defenses[i].Armor
                };
                EntityManager.SetComponentData(entities[i], overrides);
                EntityManager.SetComponentEnabled<PvPStatOverride>(entities[i], true);

                // Apply normalized values
                var health = healths[i];
                health.Max = config.NormalizedMaxHealth;
                health.Current = config.NormalizedMaxHealth;
                EntityManager.SetComponentData(entities[i], health);

                var attack = attacks[i];
                attack.AttackPower = config.NormalizedAttackPower;
                attack.SpellPower = config.NormalizedSpellPower;
                EntityManager.SetComponentData(entities[i], attack);

                var defense = defenses[i];
                defense.Defense = config.NormalizedDefense;
                defense.Armor = config.NormalizedArmor;
                EntityManager.SetComponentData(entities[i], defense);
            }

            entities.Dispose();
            healths.Dispose();
            attacks.Dispose();
            defenses.Dispose();
        }

        private void RestoreStats()
        {
            _normalized = false;

            var entities = _playerQuery.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                if (!EntityManager.IsComponentEnabled<PvPStatOverride>(entities[i]))
                    continue;

                var overrides = EntityManager.GetComponentData<PvPStatOverride>(entities[i]);

                var health = EntityManager.GetComponentData<Health>(entities[i]);
                health.Max = overrides.OriginalMaxHealth;
                if (health.Current > health.Max) health.Current = health.Max;
                EntityManager.SetComponentData(entities[i], health);

                var attack = EntityManager.GetComponentData<AttackStats>(entities[i]);
                attack.AttackPower = overrides.OriginalAttackPower;
                attack.SpellPower = overrides.OriginalSpellPower;
                EntityManager.SetComponentData(entities[i], attack);

                var defense = EntityManager.GetComponentData<DefenseStats>(entities[i]);
                defense.Defense = overrides.OriginalDefense;
                defense.Armor = overrides.OriginalArmor;
                EntityManager.SetComponentData(entities[i], defense);

                EntityManager.SetComponentEnabled<PvPStatOverride>(entities[i], false);
            }

            entities.Dispose();
        }
    }
}
