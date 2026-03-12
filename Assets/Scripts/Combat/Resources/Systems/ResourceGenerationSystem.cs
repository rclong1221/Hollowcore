using Unity.Entities;
using Unity.NetCode;
using DIG.Combat.Systems;

namespace DIG.Combat.Resources.Systems
{
    /// <summary>
    /// EPIC 16.8 Phase 1: Generates resources on hit/take damage (rage generation).
    /// Reads CombatResultEvent to detect hits dealt/received.
    /// Server-authoritative only — avoids misprediction on hit confirmation.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CombatResolutionSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct ResourceGenerationSystem : ISystem
    {
        private EntityQuery _creQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ResourcePool>();
            _creQuery = state.GetEntityQuery(ComponentType.ReadOnly<CombatResultEvent>());
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_creQuery.IsEmpty) return;

            var poolLookup = state.GetComponentLookup<ResourcePool>(false);
            float currentTime = (float)SystemAPI.Time.ElapsedTime;

            var events = _creQuery.ToComponentDataArray<CombatResultEvent>(Unity.Collections.Allocator.Temp);

            for (int i = 0; i < events.Length; i++)
            {
                var cre = events[i];
                if (!cre.DidHit) continue;

                // GenerateOnHit: attacker gains resource
                if (cre.AttackerEntity != Entity.Null && poolLookup.HasComponent(cre.AttackerEntity))
                {
                    var pool = poolLookup[cre.AttackerEntity];
                    bool changed = false;
                    changed |= TryGenerate(ref pool.Slot0, ResourceFlags.GenerateOnHit, currentTime);
                    changed |= TryGenerate(ref pool.Slot1, ResourceFlags.GenerateOnHit, currentTime);
                    if (changed) poolLookup[cre.AttackerEntity] = pool;
                }

                // GenerateOnTake: target gains resource
                if (cre.TargetEntity != Entity.Null && poolLookup.HasComponent(cre.TargetEntity))
                {
                    var pool = poolLookup[cre.TargetEntity];
                    bool changed = false;
                    changed |= TryGenerate(ref pool.Slot0, ResourceFlags.GenerateOnTake, currentTime);
                    changed |= TryGenerate(ref pool.Slot1, ResourceFlags.GenerateOnTake, currentTime);
                    if (changed) poolLookup[cre.TargetEntity] = pool;
                }
            }

            events.Dispose();
        }

        private static bool TryGenerate(ref ResourceSlot slot, ResourceFlags flag, float currentTime)
        {
            if (slot.Type == ResourceType.None) return false;
            if ((slot.Flags & flag) == 0) return false;
            if (slot.GenerateAmount <= 0f) return false;

            float cap = (slot.Flags & ResourceFlags.CanOverflow) != 0
                ? float.MaxValue : slot.Max;
            slot.Current = Unity.Mathematics.math.min(slot.Current + slot.GenerateAmount, cap);
            slot.LastDrainTime = currentTime; // Reset decay timer on generation
            return true;
        }
    }
}
