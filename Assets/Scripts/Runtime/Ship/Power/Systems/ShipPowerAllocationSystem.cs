using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using DIG.Ship.LocalSpace;

namespace DIG.Ship.Power
{
    /// <summary>
    /// System that aggregates power producers/consumers per ship and allocates power by priority.
    /// Runs on server to ensure authoritative power state.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct ShipPowerAllocationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Process each ship
            foreach (var (powerState, shipEntity) in
                     SystemAPI.Query<RefRW<ShipPowerState>>()
                     .WithAll<ShipRoot>()
                     .WithEntityAccess())
            {
                // Aggregate total production from all online producers for this ship
                float totalProduced = 0f;
                foreach (var producer in SystemAPI.Query<RefRO<ShipPowerProducer>>())
                {
                    if (producer.ValueRO.ShipEntity == shipEntity && producer.ValueRO.IsOnline)
                    {
                        totalProduced += producer.ValueRO.CurrentOutput;
                    }
                }

                // Collect all consumers for this ship with their priorities
                var consumers = new NativeList<ConsumerInfo>(Allocator.Temp);
                foreach (var (consumer, consumerEntity) in
                         SystemAPI.Query<RefRO<ShipPowerConsumer>>()
                         .WithEntityAccess())
                {
                    if (consumer.ValueRO.ShipEntity == shipEntity)
                    {
                        consumers.Add(new ConsumerInfo
                        {
                            Entity = consumerEntity,
                            RequiredPower = consumer.ValueRO.RequiredPower,
                            Priority = consumer.ValueRO.Priority
                        });
                    }
                }

                // Sort by priority (highest first)
                consumers.Sort(new ConsumerPriorityComparer());

                // Calculate total demand
                float totalDemand = 0f;
                for (int i = 0; i < consumers.Length; i++)
                {
                    totalDemand += consumers[i].RequiredPower;
                }

                // Allocate power by priority
                float remainingPower = totalProduced;
                float totalConsumed = 0f;

                for (int i = 0; i < consumers.Length; i++)
                {
                    var consumerInfo = consumers[i];
                    var consumer = SystemAPI.GetComponentRW<ShipPowerConsumer>(consumerInfo.Entity);

                    // Allocate as much as possible up to requirement
                    float allocated = Unity.Mathematics.math.min(remainingPower, consumerInfo.RequiredPower);
                    consumer.ValueRW.CurrentPower = allocated;
                    remainingPower -= allocated;
                    totalConsumed += allocated;
                }

                // Update ship power state
                powerState.ValueRW.TotalProduced = totalProduced;
                powerState.ValueRW.TotalDemand = totalDemand;
                powerState.ValueRW.TotalConsumed = totalConsumed;
                powerState.ValueRW.IsBrownout = totalDemand > totalProduced;

                consumers.Dispose();
            }

            // Ensure ships with ShipRoot have ShipPowerState
            foreach (var (_, entity) in
                     SystemAPI.Query<RefRO<ShipRoot>>()
                     .WithNone<ShipPowerState>()
                     .WithEntityAccess())
            {
                ecb.AddComponent(entity, ShipPowerState.Default);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private struct ConsumerInfo
        {
            public Entity Entity;
            public float RequiredPower;
            public int Priority;
        }

        private struct ConsumerPriorityComparer : IComparer<ConsumerInfo>
        {
            public int Compare(ConsumerInfo a, ConsumerInfo b)
            {
                // Higher priority first (descending)
                return b.Priority.CompareTo(a.Priority);
            }
        }
    }
}
