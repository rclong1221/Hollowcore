using Hollowcore.Chassis.Definitions;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace Hollowcore.Chassis.Systems
{
    /// <summary>
    /// On player entity spawn: creates chassis child entity with ChassisState,
    /// sets ChassisLink on player, and spawns starting limb entities if configured.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class ChassisBootstrapSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            // Find players that have ChassisLink but where ChassisEntity is still null
            // (freshly spawned, not yet initialized)
            foreach (var (chassisLink, startingLoadout, entity) in
                     SystemAPI.Query<RefRW<ChassisLink>, DynamicBuffer<StartingLimbElement>>()
                     .WithEntityAccess())
            {
                if (chassisLink.ValueRO.ChassisEntity != Entity.Null)
                    continue;

                // Create chassis child entity
                var chassisEntity = ecb.CreateEntity();
                ecb.AddComponent(chassisEntity, new ChassisState());
                ecb.SetName(chassisEntity, "Chassis");

                // Link player to chassis
                ecb.SetComponent(entity, new ChassisLink { ChassisEntity = chassisEntity });

                // Spawn starting limbs if database is available
                if (SystemAPI.HasSingleton<LimbDatabaseReference>())
                {
                    var dbRef = SystemAPI.GetSingleton<LimbDatabaseReference>();
                    ref var db = ref dbRef.Value.Value;

                    for (int i = 0; i < startingLoadout.Length; i++)
                    {
                        int limbId = startingLoadout[i].LimbDefinitionId;
                        SpawnLimbFromDatabase(ecb, chassisEntity, ref db, limbId);
                    }
                }
            }

            // Handle players with ChassisLink but no StartingLimbElement buffer
            // (minimal bootstrap — just create the chassis entity)
            foreach (var (chassisLink, entity) in
                     SystemAPI.Query<RefRW<ChassisLink>>()
                     .WithNone<StartingLimbElement>()
                     .WithEntityAccess())
            {
                if (chassisLink.ValueRO.ChassisEntity != Entity.Null)
                    continue;

                var chassisEntity = ecb.CreateEntity();
                ecb.AddComponent(chassisEntity, new ChassisState());
                ecb.SetName(chassisEntity, "Chassis");

                ecb.SetComponent(entity, new ChassisLink { ChassisEntity = chassisEntity });
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private static void SpawnLimbFromDatabase(
            EntityCommandBuffer ecb, Entity chassisEntity,
            ref LimbDefinitionDatabase db, int limbId)
        {
            for (int d = 0; d < db.Definitions.Length; d++)
            {
                ref var def = ref db.Definitions[d];
                if (def.LimbId != limbId) continue;

                var limbEntity = ecb.CreateEntity();
                ecb.AddComponent(limbEntity, new LimbInstance
                {
                    LimbDefinitionId = def.LimbId,
                    SlotType = def.SlotType,
                    CurrentIntegrity = def.MaxIntegrity,
                    MaxIntegrity = def.MaxIntegrity,
                    Rarity = def.Rarity,
                    DurabilityType = LimbDurability.Permanent,
                    ElapsedTime = 0f,
                    ExpirationTime = 0f,
                    DistrictAffinityId = def.DistrictAffinityId,
                    DisplayName = def.DisplayName.ToString()
                });
                ecb.AddComponent(limbEntity, new LimbStatBlock
                {
                    BonusDamage = def.BonusDamage,
                    BonusArmor = def.BonusArmor,
                    BonusMoveSpeed = def.BonusMoveSpeed,
                    BonusMaxHealth = def.BonusMaxHealth,
                    BonusAttackSpeed = def.BonusAttackSpeed,
                    BonusStamina = def.BonusStamina,
                    HeatResistance = def.HeatResistance,
                    ToxinResistance = def.ToxinResistance,
                    FallDamageReduction = def.FallDamageReduction
                });
                ecb.SetName(limbEntity, def.DisplayName.ToString());

                // We can't set ChassisState slots via ECB on the not-yet-created entity,
                // so we add a deferred equip command
                ecb.AddComponent(limbEntity, new PendingSlotAssignment
                {
                    ChassisEntity = chassisEntity,
                    Slot = def.SlotType
                });

                break;
            }
        }
    }

    /// <summary>
    /// Buffer element for starting limb loadout, baked from authoring.
    /// </summary>
    public struct StartingLimbElement : IBufferElementData
    {
        public int LimbDefinitionId;
    }

    /// <summary>
    /// Temporary tag to assign a limb to a chassis slot after both entities exist.
    /// Processed by ChassisSlotAssignmentSystem.
    /// </summary>
    public struct PendingSlotAssignment : IComponentData
    {
        public Entity ChassisEntity;
        public ChassisSlot Slot;
    }

    /// <summary>
    /// Processes deferred slot assignments from ChassisBootstrapSystem.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(ChassisBootstrapSystem))]
    public partial class ChassisSlotAssignmentSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (assignment, limbEntity) in
                     SystemAPI.Query<RefRO<PendingSlotAssignment>>()
                     .WithEntityAccess())
            {
                var chassisEntity = assignment.ValueRO.ChassisEntity;
                if (!EntityManager.Exists(chassisEntity)) continue;
                if (!EntityManager.HasComponent<ChassisState>(chassisEntity)) continue;

                var state = EntityManager.GetComponentData<ChassisState>(chassisEntity);
                state.SetSlot(assignment.ValueRO.Slot, limbEntity);
                EntityManager.SetComponentData(chassisEntity, state);

                ecb.RemoveComponent<PendingSlotAssignment>(limbEntity);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
