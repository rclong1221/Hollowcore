#pragma warning disable CS0162 // Unreachable code detected - intentional debug toggle
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using DIG.Items;

namespace DIG.Weapons.Systems
{
    /// <summary>
    /// Debug system to trace weapon firing flow.
    /// Remove this after debugging is complete.
    /// </summary>
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(WeaponFireSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial class WeaponDebugSystem : SystemBase
    {
        private float _lastLogTime;
        // Toggle debug logging for this system. Set to true to enable logs.
        private const bool DebugEnabled = false;

        protected override void OnUpdate()
        {
            // Only log every 2 seconds to avoid spam
            float time = (float)SystemAPI.Time.ElapsedTime;
            if (time - _lastLogTime < 2f) return;
            _lastLogTime = time;

            string worldName = World.Name;

            // Count players with ActiveEquipmentSlot
            int playerCount = 0;
            Entity playerWithWeapon = Entity.Null;
            Entity equippedWeapon = Entity.Null;

            foreach (var (equippedBuffer, playerInput, entity) in
                     SystemAPI.Query<DynamicBuffer<EquippedItemElement>, RefRO<PlayerInput>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                playerCount++;
                // Check first slot for debug
                if (equippedBuffer.Length > 0 && equippedBuffer[0].ItemEntity != Entity.Null)
                {
                    playerWithWeapon = entity;
                    equippedWeapon = equippedBuffer[0].ItemEntity;
                }
            }

            // Count weapons with WeaponFireState
            int weaponCount = 0;
            bool anyFiring = false;
            bool weaponHasUseRequest = false;
            bool useRequestActive = false;

            foreach (var (fireState, entity) in
                     SystemAPI.Query<RefRO<WeaponFireState>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                weaponCount++;
                if (fireState.ValueRO.IsFiring)
                    anyFiring = true;

                if (entity == equippedWeapon)
                {
                    if (SystemAPI.HasComponent<UseRequest>(entity))
                    {
                        weaponHasUseRequest = true;
                        var req = SystemAPI.GetComponent<UseRequest>(entity);
                        useRequestActive = req.StartUse;
                    }
                }
            }

            if (DebugEnabled)
            {
                Debug.Log($"[WeaponDebug] [{worldName}] Players:{playerCount} PlayerWithWeapon:{playerWithWeapon.Index} " +
                          $"EquippedWeapon:{equippedWeapon.Index} Weapons:{weaponCount} AnyFiring:{anyFiring} " +
                          $"HasUseRequest:{weaponHasUseRequest} UseActive:{useRequestActive}");
            }
        }
    }
}
