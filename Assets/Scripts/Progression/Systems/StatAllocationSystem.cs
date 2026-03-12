using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using DIG.Combat.Components;

namespace DIG.Progression
{
    /// <summary>
    /// EPIC 16.14: Processes StatAllocationRequest buffer entries on player entities.
    /// Validates UnspentStatPoints, increments CharacterAttributes fields, decrements points.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(LevelRewardSystem))]
    public partial class StatAllocationSystem : SystemBase
    {
        private EntityQuery _query;

        protected override void OnCreate()
        {
            _query = GetEntityQuery(
                ComponentType.ReadWrite<StatAllocationRequest>(),
                ComponentType.ReadWrite<PlayerProgression>(),
                ComponentType.ReadWrite<CharacterAttributes>());
        }

        protected override void OnUpdate()
        {
            var entities = _query.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var requestBuffer = EntityManager.GetBuffer<StatAllocationRequest>(entities[i]);
                if (requestBuffer.Length == 0) continue;

                var prog = EntityManager.GetComponentData<PlayerProgression>(entities[i]);
                var attrs = EntityManager.GetComponentData<CharacterAttributes>(entities[i]);

                for (int r = 0; r < requestBuffer.Length; r++)
                {
                    var req = requestBuffer[r];
                    if (req.Points <= 0 || req.Points > prog.UnspentStatPoints)
                        continue;

                    prog.UnspentStatPoints -= req.Points;

                    switch (req.Attribute)
                    {
                        case StatAttributeType.Strength:
                            attrs.Strength += req.Points;
                            break;
                        case StatAttributeType.Dexterity:
                            attrs.Dexterity += req.Points;
                            break;
                        case StatAttributeType.Intelligence:
                            attrs.Intelligence += req.Points;
                            break;
                        case StatAttributeType.Vitality:
                            attrs.Vitality += req.Points;
                            break;
                    }
                }

                EntityManager.SetComponentData(entities[i], prog);
                EntityManager.SetComponentData(entities[i], attrs);
                requestBuffer.Clear();
            }

            entities.Dispose();
        }
    }
}
