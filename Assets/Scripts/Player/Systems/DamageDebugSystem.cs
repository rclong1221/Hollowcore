using Unity.Entities;
using Unity.NetCode;
using Unity.Mathematics;
using DIG.Survival.Core;
using DIG.Survival.Environment;
using DIG.Survival.Oxygen;
using DIG.Ship.Power;
using Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// DEBUG ONLY: System to diagnose damage pipeline issues.
    /// Remove after debugging is complete.
    /// DISABLED: DirectDamage (L key) applies damage to ALL entities with Health+GhostOwner,
    /// causing all players to take damage simultaneously. Must be disabled in multiplayer.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DamageSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class DamageDebugSystem : SystemBase
    {
        private float _logTimer = 0f;
        private const float LOG_INTERVAL = 2f; // Log every 2 seconds

        protected override void OnCreate()
        {
            RequireForUpdate<NetworkTime>();
        }

        protected override void OnUpdate()
        {
            // Debug keys for testing damage pipeline
            #if ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null)
            {
                // K key: Force player's zone to require oxygen (for testing damage pipeline)
                if (keyboard.kKey.wasPressedThisFrame)
                {
                    ForceOxygenRequired();
                }
                
                // J key: Directly drain oxygen to 0 (bypasses zone check)
                if (keyboard.jKey.wasPressedThisFrame)
                {
                    DrainOxygen();
                }
                
                // L key: Directly apply damage (bypasses all survival systems)
                if (keyboard.lKey.wasPressedThisFrame)
                {
                    DirectDamage();
                }
                
                // M key: Force InShipLocalSpace.IsAttached = true (bypasses airlock)
                if (keyboard.mKey.wasPressedThisFrame)
                {
                    ForceInShip();
                }
            }
            #endif

            _logTimer += SystemAPI.Time.DeltaTime;
            if (_logTimer < LOG_INTERVAL)
                return;
            _logTimer = 0f;

            // Check life support status
            bool lifeSupportOnline = true;
            Entity lifeSupportZone = Entity.Null;
            float lifeSupportPower = -1f;
            float lifeSupportRequired = -1f;
            foreach (var (lifeSupport, lifeSupportEntity) in 
                     SystemAPI.Query<RefRO<LifeSupport>>().WithEntityAccess())
            {
                lifeSupportOnline = lifeSupport.ValueRO.IsOnline;
                lifeSupportZone = lifeSupport.ValueRO.InteriorZoneEntity;
                
                if (EntityManager.HasComponent<ShipPowerConsumer>(lifeSupportEntity))
                {
                    var consumer = EntityManager.GetComponentData<ShipPowerConsumer>(lifeSupportEntity);
                    lifeSupportPower = consumer.CurrentPower;
                    lifeSupportRequired = consumer.RequiredPower;
                }
                
                // UnityEngine.Debug.Log($"[DamageDebug] LifeSupport: Online={lifeSupportOnline}, Power={lifeSupportPower}/{lifeSupportRequired}, ZoneEntity={lifeSupportZone.Index}");
                break;
            }

            int playerCount = 0;
            foreach (var (health, entity) in 
                     SystemAPI.Query<RefRO<Health>>()
                     .WithAll<GhostOwner>()
                     .WithEntityAccess())
            {
                playerCount++;
                float currentHealth = health.ValueRO.Current;
                
                bool hasDamageBuffer = EntityManager.HasBuffer<DamageEvent>(entity);
                
                float survivalPending = 0f;
                if (EntityManager.HasComponent<SurvivalDamageEvent>(entity))
                {
                    var survival = EntityManager.GetComponentData<SurvivalDamageEvent>(entity);
                    survivalPending = survival.PendingDamage;
                }

                bool zoneRequiresOxygen = false;
                Entity zoneEntity = Entity.Null;
                EnvironmentZoneType zoneType = EnvironmentZoneType.Pressurized;
                float o2DepMult = 0f;
                if (EntityManager.HasComponent<CurrentEnvironmentZone>(entity))
                {
                    var zone = EntityManager.GetComponentData<CurrentEnvironmentZone>(entity);
                    zoneRequiresOxygen = zone.OxygenRequired;
                    zoneEntity = zone.ZoneEntity;
                    zoneType = zone.ZoneType;
                    o2DepMult = zone.OxygenDepletionMultiplier;
                }

                float oxygenLevel = -1f;
                float depletionRate = -1f;
                float leakMult = -1f;
                if (EntityManager.HasComponent<OxygenTank>(entity))
                {
                    var tank = EntityManager.GetComponentData<OxygenTank>(entity);
                    oxygenLevel = tank.Current;
                    depletionRate = tank.DepletionRatePerSecond;
                    leakMult = tank.LeakMultiplier;
                }

                // Check if player has StatefulTriggerEvent buffer (needed for zone detection)


                // Check if player is in ship local space
                bool isInShip = false;
                int shipIndex = 0;
                if (EntityManager.HasComponent<DIG.Ship.LocalSpace.InShipLocalSpace>(entity))
                {
                    var localSpace = EntityManager.GetComponentData<DIG.Ship.LocalSpace.InShipLocalSpace>(entity);
                    isInShip = localSpace.IsAttached;
                    shipIndex = localSpace.ShipEntity.Index;
                }
                
                // Check for detach request (might explain why IsAttached is being reset)
                bool hasDetachRequest = EntityManager.HasComponent<DIG.Ship.LocalSpace.DetachFromShipRequest>(entity);
                bool hasAttachRequest = EntityManager.HasComponent<DIG.Ship.LocalSpace.AttachToShipRequest>(entity);
                
                // Check for OxygenConsumer tag (required for oxygen depletion)
                bool hasO2Consumer = EntityManager.HasComponent<DIG.Survival.Oxygen.OxygenConsumer>(entity);

                // UnityEngine.Debug.Log($"[DamageDebug] Player {entity.Index}: " +
                //     $"Health={currentHealth:F1}, " +
                //     $"O2={oxygenLevel:F1}, DepRate={depletionRate:F2}, LeakMult={leakMult:F2}, " +
                //     $"O2Consumer={hasO2Consumer}, " +
                //     $"InShip={isInShip}, ShipEnt={shipIndex}, " +
                //     $"ZoneType={zoneType}, " +
                //     $"ZoneOxygenReq={zoneRequiresOxygen}, O2DepMult={o2DepMult:F2}, " +
                //     $"SurvivalPending={survivalPending:F2}");
                    
                break; // Only log first player
            }

            if (playerCount == 0)
            {
                UnityEngine.Debug.Log("[DamageDebug] No players with Health found");
            }
        }

        private void ForceOxygenRequired()
        {
            foreach (var (zone, entity) in 
                     SystemAPI.Query<RefRW<CurrentEnvironmentZone>>()
                     .WithAll<GhostOwner>()
                     .WithEntityAccess())
            {
                zone.ValueRW.OxygenRequired = true;
                zone.ValueRW.ZoneType = EnvironmentZoneType.Vacuum;
                UnityEngine.Debug.Log($"[DamageDebug] FORCED player {entity.Index} zone to OxygenRequired=True (Vacuum)! O2 will deplete, then damage starts.");
            }
        }

        private void DrainOxygen()
        {
            foreach (var (tank, entity) in 
                     SystemAPI.Query<RefRW<OxygenTank>>()
                     .WithAll<GhostOwner>()
                     .WithEntityAccess())
            {
                tank.ValueRW.Current = 0f;
                UnityEngine.Debug.Log($"[DamageDebug] DRAINED player {entity.Index} oxygen to 0! Suffocation damage should start if in oxygen-required zone.");
            }
        }

        private void DirectDamage()
        {
            foreach (var (health, entity) in 
                     SystemAPI.Query<RefRW<Health>>()
                     .WithAll<GhostOwner>()
                     .WithEntityAccess())
            {
                float damage = 10f;
                health.ValueRW.Current -= damage;
                UnityEngine.Debug.Log($"[DamageDebug] DIRECT DAMAGE applied to player {entity.Index}: -{damage} HP. Health now: {health.ValueRO.Current}");
            }
        }

        private void ForceInShip()
        {
            foreach (var (localSpace, entity) in 
                     SystemAPI.Query<RefRW<DIG.Ship.LocalSpace.InShipLocalSpace>>()
                     .WithAll<GhostOwner>()
                     .WithEntityAccess())
            {
                // Read current values
                var current = localSpace.ValueRO;
                UnityEngine.Debug.Log($"[DamageDebug] Player {entity.Index} BEFORE: IsAttached={current.IsAttached}, ShipEntity={current.ShipEntity.Index}");
                
                // Force IsAttached = true
                localSpace.ValueRW.IsAttached = true;
                
                // Read back to confirm
                var after = localSpace.ValueRO;
                UnityEngine.Debug.Log($"[DamageDebug] Player {entity.Index} AFTER: IsAttached={after.IsAttached}, ShipEntity={after.ShipEntity.Index}");
            }
        }
    }
}

