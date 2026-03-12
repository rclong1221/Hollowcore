using Unity.Entities;
using Unity.NetCode;
using Unity.NetCode.Hybrid;
using UnityEngine;
using Unity.Collections;

namespace Player.Systems
{
    /// <summary>
    /// Client-side presentation system that triggers dodge roll animations
    /// for ALL players (local and remote). Detects DodgeRollState transitions
    /// and drives DodgeRollAnimatorBridge if present.
    ///
    /// NOTE: The primary animation path for Opsive-based controllers is through
    /// AbilityIndex/AbilityChange (driven by DodgeRollAnimationBridgeSystem →
    /// PlayerAnimationStateSystem → ClimbAnimatorBridge). This system provides
    /// an additional hook for custom animator parameters (RollTrigger, IsRolling)
    /// when a DodgeRollAnimatorBridge is present and the controller supports them.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class DodgeRollAnimationTriggerSystem : SystemBase
    {
        public bool EnableDebugLog = false;

        private GhostPresentationGameObjectSystem _presentationSystem;
        private NativeHashMap<Entity, uint> _lastRollFrame;
        private NativeHashMap<Entity, byte> _lastIsActive;
        private NativeList<Entity> _entitiesToRemove;

        protected override void OnCreate()
        {
            RequireForUpdate<NetworkStreamInGame>();
            _presentationSystem = World.GetExistingSystemManaged<GhostPresentationGameObjectSystem>();
            _lastRollFrame = new NativeHashMap<Entity, uint>(16, Allocator.Persistent);
            _lastIsActive = new NativeHashMap<Entity, byte>(16, Allocator.Persistent);
            _entitiesToRemove = new NativeList<Entity>(16, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            if (_lastRollFrame.IsCreated) _lastRollFrame.Dispose();
            if (_lastIsActive.IsCreated) _lastIsActive.Dispose();
            if (_entitiesToRemove.IsCreated) _entitiesToRemove.Dispose();
            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            if (_presentationSystem == null)
            {
                _presentationSystem = World.GetExistingSystemManaged<GhostPresentationGameObjectSystem>();
                if (_presentationSystem == null)
                    return;
            }

            foreach (var (rollState, entity) in SystemAPI.Query<RefRO<Player.Components.DodgeRollState>>()
                .WithAll<PlayerTag>()
                .WithEntityAccess())
            {
                var roll = rollState.ValueRO;
                bool isLocal = EntityManager.HasComponent<GhostOwnerIsLocal>(entity);

                // Track state transitions to detect new rolls
                _lastIsActive.TryGetValue(entity, out var lastActive);
                bool isNewRoll = false;
                bool rollEnded = false;

                if (roll.IsActive == 1)
                {
                    if (lastActive == 0)
                    {
                        // IsActive transition: 0 -> 1
                        isNewRoll = true;
                        if (EnableDebugLog)
                            Debug.Log($"[DodgeRollAnim] {(isLocal ? "LOCAL" : "REMOTE")} Entity {entity.Index} IsActive 0->1, StartFrame={roll.StartFrame}, Elapsed={roll.Elapsed:F3}");
                    }
                    else if (!isLocal && _lastRollFrame.TryGetValue(entity, out var lastFrame)
                             && lastFrame != roll.StartFrame && roll.StartFrame != 0)
                    {
                        // Remote only: StartFrame changed (late replication or packet recovery)
                        isNewRoll = true;
                        if (EnableDebugLog)
                            Debug.Log($"[DodgeRollAnim] REMOTE Entity {entity.Index} StartFrame changed {lastFrame}->{roll.StartFrame}");
                    }
                }
                else if (lastActive == 1)
                {
                    rollEnded = true;
                    if (EnableDebugLog)
                        Debug.Log($"[DodgeRollAnim] {(isLocal ? "LOCAL" : "REMOTE")} Entity {entity.Index} roll ended");
                }

                // Update tracking
                _lastIsActive[entity] = roll.IsActive;
                if (roll.IsActive == 1)
                    _lastRollFrame[entity] = roll.StartFrame;

                if (!isNewRoll && !rollEnded)
                    continue;

                // Get the presentation GameObject
                var presentation = _presentationSystem.GetGameObjectForEntity(EntityManager, entity);
                if (presentation == null)
                    continue;

                // Drive bridge if present (validates its own parameters — no-ops if controller lacks them)
                var bridge = presentation.GetComponentInChildren<Player.Bridges.DodgeRollAnimatorBridge>();
                if (bridge != null)
                {
                    if (isNewRoll)
                        bridge.TriggerRoll();
                    else if (rollEnded)
                        bridge.EndRoll();
                }
            }

            // Clean up tracking for entities that no longer exist
            _entitiesToRemove.Clear();
            if (!_lastRollFrame.IsEmpty)
            {
                var keys = _lastRollFrame.GetKeyArray(Allocator.Temp);
                for (int i = 0; i < keys.Length; i++)
                {
                    var entity = keys[i];
                    if (!EntityManager.Exists(entity) || !EntityManager.HasComponent<Player.Components.DodgeRollState>(entity))
                    {
                        _entitiesToRemove.Add(entity);
                    }
                }
                keys.Dispose();
            }

            for (int i = 0; i < _entitiesToRemove.Length; i++)
            {
                var entity = _entitiesToRemove[i];
                _lastRollFrame.Remove(entity);
                _lastIsActive.Remove(entity);
            }
        }
    }
}
