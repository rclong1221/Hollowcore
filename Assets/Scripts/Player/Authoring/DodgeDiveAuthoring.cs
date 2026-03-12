using UnityEngine;
using Unity.Entities;
using Player.Components;

namespace Player.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Player/Dodge Dive Authoring")]
    public class DodgeDiveAuthoring : MonoBehaviour
    {
        [Header("Dodge Dive Tuning")]
        [Tooltip("Duration of the dive in seconds")]
        public float Duration = 0.8f;

        [Tooltip("Horizontal distance traveled during dive")]
        public float Distance = 4.0f;

        [Tooltip("When invulnerability starts (seconds from dive start)")]
        public float InvulnWindowStart = 0.1f;

        [Tooltip("When invulnerability ends (seconds from dive start)")]
        public float InvulnWindowEnd = 0.6f;

        [Tooltip("Stamina cost to perform dive")]
        public float StaminaCost = 25f;

        [Tooltip("Cooldown before another dive can be performed")]
        public float Cooldown = 1.5f;

        [Tooltip("If true, player transitions to prone state at end of dive")]
        public bool EndInProne = true;

        [Header("Collision Detection")]
        [Tooltip("Layers to check for collision during dive (0 = all layers)")]
        public uint CollisionLayerMask = 0;

        [Tooltip("Minimum Y component of surface normal to consider it a floor (0.7 = ~45 degrees)")]
        [Range(0f, 1f)]
        public float MinFloorNormalY = 0.7f;

        [Tooltip("Maximum height above ground to still consider a surface hit as floor (meters)")]
        public float MaxFloorHeight = 0.2f;

        class Baker : Baker<DodgeDiveAuthoring>
        {
            public override void Bake(DodgeDiveAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new DodgeDiveComponent
                {
                    Duration = authoring.Duration,
                    Distance = authoring.Distance,
                    InvulnWindowStart = authoring.InvulnWindowStart,
                    InvulnWindowEnd = authoring.InvulnWindowEnd,
                    StaminaCost = authoring.StaminaCost,
                    Cooldown = authoring.Cooldown,
                    EndInProne = (byte)(authoring.EndInProne ? 1 : 0),
                    CollisionLayerMask = authoring.CollisionLayerMask,
                    MinFloorNormalY = authoring.MinFloorNormalY,
                    MaxFloorHeight = authoring.MaxFloorHeight
                });

                // Add DodgeDiveState for gameplay (NetCode replication)
                AddComponent(entity, new DodgeDiveState
                {
                    IsActive = 0,
                    Elapsed = 0f,
                    CooldownRemaining = 0f
                });

                // Add DodgeState for Opsive animation integration
                // DodgeDiveAnimationBridgeSystem syncs DodgeDiveState -> DodgeState
                AddComponent(entity, new DodgeState
                {
                    IsDodging = false,
                    Direction = 0,
                    TimeRemaining = 0f,
                    CooldownRemaining = 0f,
                    DodgeDuration = authoring.Duration,
                    DodgeCooldown = authoring.Cooldown
                });
            }
        }
    }
}
