using Unity.Entities;
using Unity.NetCode;
using Unity.Physics;
using UnityEngine;
using Player.Components;
using Player.Systems;
using DIG.Player.Components;
using DIG.Ship.Stations;
using DIG.Ship.Airlocks;
using DIG.Targeting.Core;
using DIG.Vision.Components;

using System;
using System.Collections.Generic;
using Traits;

public struct PlayerTag : IComponentData
{
}

[Serializable]
public struct AttributeConfig
{
    public string Name;
    public float MaxValue;
    public float StartValue;
    public float RegenRate;
    public float RegenDelay;
}

[DisallowMultipleComponent]
public class PlayerAuthoring : MonoBehaviour
{
    [Header("Damage Mitigation")]
    [Tooltip("Resistance multipliers (1.0 = normal, 0.5 = half damage, 0 = immune)")]
    public float PhysicalResistance = 1f;
    public float HeatResistance = 1f;
    public float RadiationResistance = 1f;
    public float SuffocationResistance = 1f;
    public float ExplosionResistance = 1f;
    public float ToxicResistance = 1f;

    [Header("Camera Configuration")]
    public CameraViewType DefaultViewType = CameraViewType.Combat;
    public Vector3 CombatPivotOffset = new Vector3(0, 2.2f, 0);
    public Vector3 CombatCameraOffset = new Vector3(0, 0, -2);
    public float CombatMinPitch = -89f;
    public float CombatMaxPitch = 89f;
    public Vector3 FPSOffset = new Vector3(0, 1.7f, 0);
    
    [Header("Camera Spring Physics")]
    public float PositionSpringStiffness = 0.2f;
    public float PositionSpringDamping = 0.25f;
    public float RotationSpringStiffness = 0.2f;
    public float RotationSpringDamping = 0.25f;
    public float MaxSpringVelocity = 100f;

    [Header("Attributes")]
    public List<AttributeConfig> Attributes = new List<AttributeConfig>
    {
        new AttributeConfig { Name = "Health", MaxValue = 100, StartValue = 100, RegenRate = 0, RegenDelay = 0 },
        new AttributeConfig { Name = "Stamina", MaxValue = 100, StartValue = 100, RegenRate = 10, RegenDelay = 2 },
        new AttributeConfig { Name = "Energy", MaxValue = 100, StartValue = 100, RegenRate = 5, RegenDelay = 1 }
    };

    class Baker : Baker<PlayerAuthoring>
    {
        public override void Bake(PlayerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<PlayerTag>(entity);
            
            // NOTE: EPIC 15.19 Detectable component is added via DetectableAuthoring 
            // on the player prefab to avoid duplicate component errors.
            // DO NOT add Detectable here - it causes baking conflicts.

            // Add camera target component with default values
            AddComponent(entity, new CameraTarget
            {
                Position = Unity.Mathematics.float3.zero,
                Rotation = Unity.Mathematics.quaternion.identity,
                FOV = 60f,
                NearClip = 0.1f,
                FarClip = 1000f
            });

            // Add camera settings for orbit camera with FPS zoom
            AddComponent(entity, PlayerCameraSettings.Default);

            // Add Modular Camera View Config (Epic 13.21)
            AddComponent(entity, new CameraViewConfig
            {
                ActiveViewType = authoring.DefaultViewType,
                CombatPivotOffset = (Unity.Mathematics.float3)authoring.CombatPivotOffset,
                CombatCameraOffset = (Unity.Mathematics.float3)authoring.CombatCameraOffset,
                CombatMinPitch = authoring.CombatMinPitch,
                CombatMaxPitch = authoring.CombatMaxPitch,
                FPSOffset = (Unity.Mathematics.float3)authoring.FPSOffset,
                AdventurePivotOffset = (Unity.Mathematics.float3)authoring.CombatPivotOffset, // Default to same
                AdventureDistance = 8.0f
            });

            // Add Camera Spring State (Epic 13.21)
            AddComponent(entity, new CameraSpringState
            {
                PositionStiffness = new Unity.Mathematics.float3(authoring.PositionSpringStiffness),
                PositionDamping = new Unity.Mathematics.float3(authoring.PositionSpringDamping),
                RotationStiffness = new Unity.Mathematics.float3(authoring.RotationSpringStiffness),
                RotationDamping = new Unity.Mathematics.float3(authoring.RotationSpringDamping),
                MaxVelocity = authoring.MaxSpringVelocity,
                MinValue = new Unity.Mathematics.float3(-1000),
                MaxValue = new Unity.Mathematics.float3(1000),
                PositionValue = Unity.Mathematics.float3.zero,
                PositionVelocity = Unity.Mathematics.float3.zero,
                RotationValue = Unity.Mathematics.float3.zero,
                RotationVelocity = Unity.Mathematics.float3.zero
            });

            // Add player state components
            AddComponent(entity, PlayerState.Default);
            AddComponent(entity, PlayerStanceConfig.Default);
            AddComponent(entity, PlayerInputPreferences.Default);

            // Add animation state so ghosts replicate animator parameters
            AddComponent(entity, PlayerAnimationState.Default);

            AddComponent(entity, PlayerMovementSettings.Default);
            
            // Add Generic Attributes (Epic 13.22)
            var attributeBuffer = AddBuffer<AttributeData>(entity);
            foreach (var attr in authoring.Attributes)
            {
                attributeBuffer.Add(new AttributeData
                {
                    NameHash = new Unity.Collections.FixedString32Bytes(attr.Name),
                    CurrentValue = attr.StartValue,
                    MinValue = 0,
                    MaxValue = attr.MaxValue,
                    RegenRate = attr.RegenRate,
                    RegenDelay = attr.RegenDelay,
                    LastChangeTime = 0
                });
            }

            AddComponent(entity, PlayerStamina.Default);
            
            // Epic 4.1: Add health and death state components
            AddComponent(entity, Health.Default);
            AddComponent(entity, ShieldComponent.Default); // 13.16.3 Shield
            AddComponent(entity, PlayerBlockingState.Default); // 15.7: Shield Block System
            AddComponent(entity, DeathState.Default);

            // Epic 13.19: Server-authoritative ragdoll hips sync for multiplayer visibility
            AddComponent(entity, new RagdollHipsSync
            {
                Position = Unity.Mathematics.float3.zero,
                Rotation = Unity.Mathematics.quaternion.identity,
                IsActive = false
            });
            
            // Epic 13.16: Event Systems (Enableable)
            AddComponent(entity, new HealthStateTracker { PreviousHealth = Health.Default.Max });
            AddComponent<HealthChangedEvent>(entity);
            SetComponentEnabled<HealthChangedEvent>(entity, false);
            
            AddComponent<WillDieEvent>(entity);
            SetComponentEnabled<WillDieEvent>(entity, false);
            
            AddComponent<DiedEvent>(entity);
            SetComponentEnabled<DiedEvent>(entity, false);
            AddBuffer<DamageEvent>(entity);
            // Note: SurvivalDamageEvent is added by SurvivalAuthoring (must be on prefab)
            // Note: SurvivalDamageEvent is added by SurvivalAuthoring (must be on prefab)
            AddComponent(entity, DeathPresentationState.Default);
            
            // Epic 13.16.12: Kill Attribution
            AddComponent(entity, new CombatState { LastAttacker = Entity.Null, LastAttackTime = 0 });
            AddBuffer<RecentAttackerElement>(entity);
            
            // Epic 4.2: Damage Mitigation
            AddComponent(entity, new DamageResistance
            {
                PhysicalMult = authoring.PhysicalResistance,
                HeatMult = authoring.HeatResistance,
                RadiationMult = authoring.RadiationResistance,
                SuffocationMult = authoring.SuffocationResistance,
                ExplosionMult = authoring.ExplosionResistance,
                ToxicMult = authoring.ToxicResistance
            });
            AddComponent(entity, default(DamageCooldown));
            AddComponent(entity, default(DamageInvulnerabilityWindow));
            
            // Epic 4.3: Status Effects
            AddBuffer<StatusEffect>(entity);
            AddBuffer<StatusEffectRequest>(entity);

            // Epic 4.4: Healing
            AddBuffer<HealEvent>(entity);

            // Epic 4.5: Revival
            AddBuffer<ReviveRequest>(entity);

            AddComponent(entity, new PlayerJumpState
            {
                TimeLeftGround = 0,
                TimeJumpRequested = 0,
                UsedCoyoteJump = false
            });
            
            // Add dodge/dive/prone/slide state components so NetCode includes them in ghost snapshots
            // These must be present at bake time for replication to work
            AddComponent(entity, new DodgeRollState { IsActive = 0 });
            AddComponent(entity, new DodgeDiveState { IsActive = 0 });
            AddComponent(entity, new ProneStateComponent { IsProne = 0 });
            AddComponent(entity, new SlideState { IsSliding = false });
            
            // Add Ship Local Space component (initially detached) for replication
            AddComponent(entity, new DIG.Ship.LocalSpace.InShipLocalSpace 
            { 
                ShipEntity = Entity.Null, 
                IsAttached = false,
                LocalPosition = Unity.Mathematics.float3.zero,
                LocalRotation = Unity.Mathematics.quaternion.identity
            });
            
            // Epic 15.4: Camera Target Lock State
            AddComponent<CameraTargetLockState>(entity);
            
            // Epic 15.16: Crosshair data for modular aiming/targeting source
            AddComponent(entity, new CrosshairData
            {
                RayOrigin = Unity.Mathematics.float3.zero,
                RayDirection = new Unity.Mathematics.float3(0, 0, 1),
                ScreenPosition = new Unity.Mathematics.float2(0.5f, 0.5f),
                HitValid = false,
                HitDistance = 0
            });
            
            // Epic 15.16: Advanced Targeting Components moved to child entity (TargetingModuleAuthoring)
            // to stay under the 16KB archetype size limit. Only the link component is added here.
            // The TargetingModuleAuthoring baker creates the child entity with:
            // - AimAssistState, PartTargetingState, PredictiveAimState
            // - MultiLockState, LockedTargetElement buffer, OverTheShoulderState
            AddComponent(entity, new TargetingModuleLink { TargetingModule = Entity.Null });
            
            // Add Stealth components (Epic 15.3)
            AddComponent(entity, new PlayerNoiseStatus { CurrentNoiseLevel = 0f, IsEmittingNoise = false });
            AddComponent(entity, StealthSettings.Default);
            AddComponent<NoiseEventTag>(entity);
            SetComponentEnabled<NoiseEventTag>(entity, false);
            
            // Add default SlideComponent config so SlideSystem can find the entity
            // (can be overridden by SlideAuthoring if attached to prefab)
            AddComponent(entity, new SlideComponent
            {
                Duration = 1.5f,
                MinSpeed = 0.5f,  // Lowered from 3.0 to allow slide at walking speed
                MaxSpeed = 12.0f,
                Acceleration = 8.0f,
                Friction = 2.0f,
                StaminaCost = 5.0f,
                Cooldown = 1.0f,
                MinSlopeAngle = 15.0f,
                SlipperyFrictionMultiplier = 0.1f
            });

            // Add physics components for movement
            AddComponent(entity, new PhysicsVelocity
            {
                Linear = Unity.Mathematics.float3.zero,
                Angular = Unity.Mathematics.float3.zero
            });
            // CRITICAL: Use Dynamic mass (not Kinematic) so entity is included in PhysicsWorld.Bodies
            // Kinematic bodies are excluded from collision detection. We need dynamic bodies
            // with realistic mass for proper physics interactions.
            // NOTE: PhysicsCollider is added by CharacterControllerAuthoring baker
            var physicsMass = PhysicsMass.CreateDynamic(MassProperties.UnitSphere, 80f);
            // Lock rotation on all axes - player rotation is controlled manually, not by physics
            physicsMass.InverseInertia = Unity.Mathematics.float3.zero;
            physicsMass.AngularExpansionFactor = 0f;
            AddComponent(entity, physicsMass);
            
            // CRITICAL: Add PhysicsWorldIndex for NetCode multi-world physics
            // Without this, BuildPhysicsWorld won't include the entity in PhysicsWorld.Bodies
            // Value 0 is the default physics world index
            AddSharedComponent(entity, new PhysicsWorldIndex(0));
            
            // Add heavy angular damping to kill any residual rotation from collisions
            AddComponent(entity, new PhysicsDamping
            {
                Linear = 0.01f,   // Minimal linear damping - we control movement manually
                Angular = 1.0f    // Maximum angular damping - prevent all rotation
            });
            
            // Add collision state and event buffer for player-player collision response
            AddComponent(entity, new DIG.Player.Components.PlayerCollisionState
            {
                LastCollisionTick = 0,
                CollisionCooldown = 0,
                LastCollisionEntity = Entity.Null
            });
            AddBuffer<DIG.Player.Components.CollisionEvent>(entity);
            
            // Add stagger/knockdown/evading enableable tags (required for collision response system queries)
            // These must be present (even if disabled) for EnabledRefRW queries to match
            AddComponent<DIG.Player.Components.Staggered>(entity);
            SetComponentEnabled<DIG.Player.Components.Staggered>(entity, false);
            AddComponent<DIG.Player.Components.KnockedDown>(entity);
            SetComponentEnabled<DIG.Player.Components.KnockedDown>(entity, false);
            AddComponent<DIG.Player.Components.Evading>(entity);
            SetComponentEnabled<DIG.Player.Components.Evading>(entity, false);

            // Epic 7.4.2: Add TackleState component for tackle system
            AddComponent(entity, new DIG.Player.Components.TackleState
            {
                TackleTimeRemaining = 0f,
                TackleDirection = Unity.Mathematics.float3.zero,
                TackleCooldown = 0f,
                DidHitTarget = false,
                TackleSpeed = 0f,
                HasProcessedHit = false
            });

            // Epic 7.6.3: Add TeamId component for team-based collision filtering
            // Default to 0 (no team) - game mode systems can assign team IDs at runtime
            AddComponent(entity, new TeamId { Value = 0 });

            // Epic 3.1: Add airlock interaction components
            AddBuffer<AirlockUseRequest>(entity);
            AddComponent(entity, new AirlockPromptState());
            AddComponent(entity, new AirlockInteractDebounce
            {
                LastRequestTick = 0,
                DebounceTickCount = 10
            });

            // Epic 3.2: Add station interaction components
            AddBuffer<StationUseRequest>(entity);
            AddComponent(entity, new StationPromptState());
            AddComponent(entity, new StationInteractDebounce
            {
                LastRequestTick = 0,
                DebounceTickCount = 10
            });

            // Add AutoCommandTarget so this entity receives input commands
            // If the prefab already contains a GhostAuthoringComponent (added by other authoring),
            // that baker may add the AutoCommandTarget; avoid adding duplicates by guarding here.
            bool hasGhostAuthoring = authoring.GetComponentInChildren<GhostAuthoringComponent>(true) != null
                                      || authoring.GetComponentInParent<GhostAuthoringComponent>() != null;
            if (!hasGhostAuthoring)
            {
                AddComponent<AutoCommandTarget>(entity);
            }
        }
    }
}
