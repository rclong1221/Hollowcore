using System.Collections.Generic;
using Player.Bridges;
using Player.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.NetCode.Hybrid;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Client-side presentation system that keeps the hybrid Animator prefab in sync with networked player ghosts.
/// It copies LocalTransform values to the companion GameObject and forwards replicated animation parameters
/// into the Animator via <see cref="AnimatorRigBridge"/>.
/// Also applies visual smoothing to the local player's presentation transform to eliminate
/// choppy movement caused by the simulation tick rate (30Hz) being lower than the render frame rate.
/// </summary>
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(GhostPresentationGameObjectSystem))]
public partial class PlayerAnimatorBridgeSystem : SystemBase
{
    private GhostPresentationGameObjectSystem _presentationSystem;
    // Use InstanceID as key to avoid holding references to destroyed GameObjects
    private readonly Dictionary<int, Player.Bridges.IPlayerAnimationBridge[]> _bridgeCache = new();

    // Visual smoothing: interpolate presentation transform between 30Hz tick steps
    private struct SmoothedTransform
    {
        public Vector3 Position;
        public Quaternion Rotation;
    }
    private readonly Dictionary<int, SmoothedTransform> _smoothedTransforms = new();

    // Smoothing factor: higher = more responsive (less lag), lower = smoother
    // At 60fps with factor 25: lerp = 25*0.016 = 0.4 per frame → converges in ~3 frames (~50ms)
    // This eliminates visible 30Hz stepping while adding minimal visual latency
    private const float PositionSmoothFactor = 25f;
    private const float RotationSmoothFactor = 20f;

    protected override void OnCreate()
    {
        RequireForUpdate<NetworkStreamInGame>();
        _presentationSystem = World.GetExistingSystemManaged<GhostPresentationGameObjectSystem>();
    }

    protected override void OnUpdate()
    {
        if (_presentationSystem == null)
        {
            _presentationSystem = World.GetExistingSystemManaged<GhostPresentationGameObjectSystem>();
            if (_presentationSystem == null)
                return;
        }

        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (animationState, playerState, transform, entity) in SystemAPI.Query<RefRO<PlayerAnimationState>, RefRO<PlayerState>, RefRO<LocalTransform>>().WithAll<PlayerTag>().WithEntityAccess())
        {
            var presentation = _presentationSystem.GetGameObjectForEntity(EntityManager, entity);
            if (presentation == null)
                continue;

            int instanceId = presentation.GetInstanceID();

            if (!_bridgeCache.TryGetValue(instanceId, out var bridges) || bridges == null)
            {
                // Self-repair: Ensure AnimatorRigBridge exists to drive core animations
                // This handles cases where the prefab might be missing the component
                if (presentation.GetComponent<AnimatorRigBridge>() == null)
                {
                    Debug.LogWarning($"[PlayerAnimatorBridgeSystem] Auto-adding missing AnimatorRigBridge to {presentation.name}");
                    presentation.AddComponent<AnimatorRigBridge>();
                }

                bridges = presentation.GetComponentsInChildren<Player.Bridges.IPlayerAnimationBridge>(true);
                _bridgeCache[instanceId] = bridges;

                bridges = presentation.GetComponentsInChildren<Player.Bridges.IPlayerAnimationBridge>(true);
                _bridgeCache[instanceId] = bridges;
            }
            
            if (bridges == null || bridges.Length == 0)
            {
               // [SWIM_DIAG]
               // Debug.Log($"[SWIM_DIAG] Entity {entity.Index}: NO BRIDGES FOUND on {presentation.name}!");
               continue;
            }

            // Create a copy of animation state
            // The Server-side PlayerAnimationStateSyncSystem populates IsCrouching/IsProne from PlayerState.Stance
            // For local player, we override with predicted values for responsiveness
            var animState = animationState.ValueRO;
            var pState = playerState.ValueRO;
            
            // For local player, use predicted PlayerState values for responsiveness
            // For remote players, use the replicated PlayerAnimationState values (set by Server)
            // GhostOwnerIsLocal is an enableable component - must check IsComponentEnabled
            bool hasGhostOwnerIsLocal = EntityManager.HasComponent<GhostOwnerIsLocal>(entity);
            bool isLocalPlayer = hasGhostOwnerIsLocal && EntityManager.IsComponentEnabled<GhostOwnerIsLocal>(entity);
            
            if (isLocalPlayer)
            {
                // Local: use predicted state for immediate response
                animState.IsSprinting = pState.MovementState == PlayerMovementState.Sprinting;
                animState.IsCrouching = pState.Stance == PlayerStance.Crouching;
                animState.IsProne = pState.Stance == PlayerStance.Prone;
            }
            // For remote players, animState already has server-replicated values
            
            // FreeClimb State Retrieval
            bool hasFreeClimb = EntityManager.HasComponent<FreeClimbState>(entity);
            FreeClimbState freeClimbState = default;
            if (hasFreeClimb)
            {
                freeClimbState = EntityManager.GetComponentData<FreeClimbState>(entity);
            }

            foreach (var bridge in bridges)
            {
                // Check if the bridge object itself is still valid (Unity null check)
                if (bridge == null || (bridge is UnityEngine.Object obj && obj == null))
                    continue;

                // Only call when the component/adapter is enabled (MonoBehaviour adapters control enabled state)
                if (bridge is UnityEngine.Behaviour b && !b.isActiveAndEnabled)
                    continue;
                


                bridge.ApplyAnimationState(animState, deltaTime);

                // Handling for ClimbAnimatorBridge specialized IK updates
                // This replaces the deprecated FreeClimbIKController logic
                if (hasFreeClimb && bridge is ClimbAnimatorBridge climbBridge)
                {
                    bool isClimbing = freeClimbState.IsClimbing && !freeClimbState.IsTransitioning;
                    bool shouldUpdate = (isClimbing && !freeClimbState.IsWallJumping) || 
                                      (freeClimbState.IsClimbingUp && freeClimbState.IsTransitioning);
                    
                    if (shouldUpdate)
                    {
                        climbBridge.UpdateClimbingIK(freeClimbState.GripWorldPosition, freeClimbState.GripWorldNormal, freeClimbState.IsFreeHanging);
                    }
                }
            }

            Transform goTransform = null;
            // Use the first bridge's transform or presentation transform
            if (bridges.Length > 0 && bridges[0] is Component comp && comp != null)
            {
                goTransform = comp.transform;
            }
            else
            {
                goTransform = presentation.transform;
            }

            var localTransform = transform.ValueRO;
            var scale = localTransform.Scale;
            if (goTransform != null)
                goTransform.localScale = new Vector3(scale, scale, scale);

            // Visual smoothing for local player: interpolate between 30Hz tick positions
            // GhostPresentationGameObjectSystem has already snapped the transform to the
            // latest tick position. We smooth it here to eliminate visible stepping.
            if (isLocalPlayer)
            {
                var presTransform = presentation.transform;
                Vector3 tickPosition = presTransform.position;
                Quaternion tickRotation = presTransform.rotation;

                if (_smoothedTransforms.TryGetValue(instanceId, out var smoothed))
                {
                    smoothed.Position = Vector3.Lerp(smoothed.Position, tickPosition, PositionSmoothFactor * deltaTime);
                    smoothed.Rotation = Quaternion.Slerp(smoothed.Rotation, tickRotation, RotationSmoothFactor * deltaTime);
                }
                else
                {
                    // First frame: snap to current position (no smoothing on spawn)
                    smoothed = new SmoothedTransform { Position = tickPosition, Rotation = tickRotation };
                }

                presTransform.position = smoothed.Position;
                presTransform.rotation = smoothed.Rotation;
                _smoothedTransforms[instanceId] = smoothed;
            }
        }
    }
}
