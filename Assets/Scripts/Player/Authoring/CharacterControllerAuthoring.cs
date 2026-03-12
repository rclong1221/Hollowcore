using UnityEngine;
using Unity.Entities;
using Unity.Physics;
using Unity.Mathematics;
using Player.Components;
using DIG.Player.Components;

namespace Player.Authoring
{
    [DisallowMultipleComponent]
    public class CharacterControllerAuthoring : MonoBehaviour
    {
        [Header("Shape")]
        public float Radius = 0.4f;
        public float Height = 2.0f;
        public float SkinWidth = 0.02f;
        public float StepHeight = 0.3f;
        public float MaxSlopeAngleDeg = 45f;

        [Header("Movement")]
        public float WalkSpeed = 2f;
        public float RunSpeed = 4f;
        public float Acceleration = 20f;

        [Header("Hybrid (Client) Visuals")]
        [Tooltip("When enabled, this prefab will be baked with a HybridLocalControllerTag so the DOTS CharacterController system will skip moving it. Use a KinematicCharacterController MonoBehaviour on the prefab for local designer-driven motion.")]
        public bool UseHybridLocalController = false;

        [Header("Push")]
        public bool PushRigidbodies = true;

        [Header("Physics Collision Layers")]
        [Tooltip("Collision filter category - what layer this entity belongs to (default: Player layer)")]
        public uint BelongsTo = CollisionLayers.Player;
        
        [Tooltip("Collision filter mask - what this entity collides with (default: Player, Environment, Hazards, Ship, Creatures)")]
        public uint CollidesWith = CollisionLayers.PlayerCollidesWith;

        class Baker : Baker<CharacterControllerAuthoring>
        {
            public override void Bake(CharacterControllerAuthoring authoring)
            {
                var e = GetEntity(TransformUsageFlags.Dynamic);
                var settings = new CharacterControllerSettings
                {
                    Radius = authoring.Radius,
                    Height = authoring.Height,
                    SkinWidth = authoring.SkinWidth,
                    StepHeight = authoring.StepHeight,
                    MaxSlopeAngleDeg = authoring.MaxSlopeAngleDeg,
                    WalkSpeed = authoring.WalkSpeed,
                    RunSpeed = authoring.RunSpeed,
                    Acceleration = authoring.Acceleration,
                    PushRigidbodies = (byte)(authoring.PushRigidbodies ? 1 : 0)
                };

                AddComponent(e, settings);
                
                if (authoring.UseHybridLocalController)
                {
                    AddComponent<Player.Components.HybridLocalControllerTag>(e);
                }

                // CRITICAL: Add Unity Physics collider for player-vs-player and player-vs-world collision
                // This creates a capsule collider that participates in the ECS physics world
                var collisionFilter = new CollisionFilter
                {
                    BelongsTo = authoring.BelongsTo,
                    CollidesWith = authoring.CollidesWith,
                    GroupIndex = 0
                };

                var capsuleGeometry = new CapsuleGeometry
                {
                    Vertex0 = new float3(0, authoring.Radius, 0), // Bottom of capsule
                    Vertex1 = new float3(0, authoring.Height - authoring.Radius, 0), // Top of capsule
                    Radius = authoring.Radius
                };

                // CRITICAL: Material must specify CollideRaiseCollisionEvents for ICollisionEventsJob to receive events
                var material = new Unity.Physics.Material
                {
                    Friction = 0.5f,
                    Restitution = 0.0f,
                    CollisionResponse = CollisionResponsePolicy.CollideRaiseCollisionEvents
                };

                var collider = Unity.Physics.CapsuleCollider.Create(capsuleGeometry, collisionFilter, material);
                AddBlobAsset(ref collider, out var hash);
                AddComponent(e, new PhysicsCollider { Value = collider });

                // Add Kinematic Physics Body components
                // This ensures the entity is treated as a moving body in the physics world,
                // allowing it to be hit by casts from other players correctly.
                // Note: PhysicsVelocity, PhysicsMass, and PhysicsDamping are already added by PlayerAuthoring.
                // We only add the additional components needed for our controller logic.
                AddComponent(e, new PhysicsGravityFactor { Value = 0f });
            }
        }
    }
}
