using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using DIG.Combat.Components;

namespace DIG.Progression
{
    /// <summary>
    /// EPIC 16.14: Checks PlayerProgression.CurrentXP against thresholds,
    /// increments CharacterAttributes.Level, awards stat points, enables LevelUpEvent.
    /// Burst-compiled ISystem for maximum performance.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(XPAwardSystem))]
    public partial struct LevelUpSystem : ISystem
    {
        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _query = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<PlayerProgression>()
                .WithAllRW<CharacterAttributes>()
                .WithAllRW<LevelUpEvent>()
                .Build(ref state);
            state.RequireForUpdate<ProgressionConfigSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<ProgressionConfigSingleton>();
            ref var curve = ref config.Curve.Value;

            var entities = _query.ToEntityArray(Allocator.Temp);
            var progressions = _query.ToComponentDataArray<PlayerProgression>(Allocator.Temp);
            var attrs = _query.ToComponentDataArray<CharacterAttributes>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var prog = progressions[i];
                var attr = attrs[i];
                int startLevel = attr.Level;
                bool leveled = false;

                // Loop to handle multi-level-ups from massive XP gains
                while (attr.Level < curve.MaxLevel)
                {
                    int levelIndex = attr.Level - 1;
                    if (levelIndex < 0 || levelIndex >= curve.XPPerLevel.Length)
                        break;

                    int xpRequired = curve.XPPerLevel[levelIndex];
                    if (prog.CurrentXP < xpRequired)
                        break;

                    // Level up! Carry excess XP
                    prog.CurrentXP -= xpRequired;
                    attr.Level++;
                    prog.UnspentStatPoints += curve.StatPointsPerLevel;
                    leveled = true;
                }

                // Clamp excess XP at max level
                if (attr.Level >= curve.MaxLevel)
                    prog.CurrentXP = 0;

                if (leveled)
                {
                    // Enable LevelUpEvent (IEnableableComponent)
                    state.EntityManager.SetComponentData(entities[i], new LevelUpEvent
                    {
                        NewLevel = attr.Level,
                        PreviousLevel = startLevel
                    });
                    state.EntityManager.SetComponentEnabled<LevelUpEvent>(entities[i], true);
                }

                state.EntityManager.SetComponentData(entities[i], prog);
                state.EntityManager.SetComponentData(entities[i], attr);
            }

            entities.Dispose();
            progressions.Dispose();
            attrs.Dispose();
        }
    }
}
