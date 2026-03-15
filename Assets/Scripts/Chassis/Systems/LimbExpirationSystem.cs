using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.Chassis.Systems
{
    /// <summary>
    /// Expires temporary limbs after their duration elapses.
    /// Increments ElapsedTime on Temporary limbs and destroys them when expired.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class LimbExpirationSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            var chassisStateLookup = GetComponentLookup<ChassisState>();

            // Update elapsed time on all temporary limbs
            foreach (var (limbInstance, limbEntity) in
                     SystemAPI.Query<RefRW<LimbInstance>>()
                     .WithEntityAccess())
            {
                if (limbInstance.ValueRO.DurabilityType != LimbDurability.Temporary)
                    continue;

                limbInstance.ValueRW.ElapsedTime += deltaTime;

                if (limbInstance.ValueRO.ElapsedTime < limbInstance.ValueRO.ExpirationTime)
                    continue;

                // Limb expired — find and clear the chassis slot
                ClearLimbFromChassis(limbEntity, limbInstance.ValueRO.SlotType, ref chassisStateLookup);

                // Fire event for UI notification
                var eventEntity = ecb.CreateEntity();
                ecb.AddComponent(eventEntity, new LimbLostEvent
                {
                    SlotType = limbInstance.ValueRO.SlotType,
                    LimbDefinitionId = limbInstance.ValueRO.LimbDefinitionId,
                    Reason = LimbLostReason.Expired
                });

                // Destroy the limb entity
                ecb.DestroyEntity(limbEntity);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private void ClearLimbFromChassis(
            Entity limbEntity, ChassisSlot slot,
            ref ComponentLookup<ChassisState> chassisStateLookup)
        {
            // Search all chassis states for the one referencing this limb
            foreach (var (chassisState, _) in
                     SystemAPI.Query<RefRW<ChassisState>>()
                     .WithEntityAccess())
            {
                if (chassisState.ValueRO.GetSlot(slot) == limbEntity)
                {
                    chassisState.ValueRW.SetSlot(slot, Entity.Null);
                    return;
                }
            }
        }
    }

    public enum LimbLostReason : byte
    {
        Expired = 0,
        Destroyed = 1,
        Unequipped = 2
    }

    /// <summary>
    /// One-frame event fired when a limb is lost. Consumed by UI systems.
    /// </summary>
    public struct LimbLostEvent : IComponentData
    {
        public ChassisSlot SlotType;
        public int LimbDefinitionId;
        public LimbLostReason Reason;
    }
}
