using UnityEngine;
using Unity.Entities;
using Player.Components;
using Player.Systems;

namespace Player.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Player/Dodge Roll Authoring")]
    public class DodgeRollAuthoring : MonoBehaviour
    {
        [Header("Dodge Roll Tuning")]
        public float Duration = 0.6f;
        public float Distance = 3.0f;
        public float InvulnWindowStart = 0.05f;
        public float InvulnWindowEnd = 0.45f;
        public float StaminaCost = 20f;

        [Header("Collision Detection")]
        [Tooltip("Layers to check for collision during roll (0 = all layers). Use LayerMask value.")]
        public uint CollisionLayerMask = 0;

        [Tooltip("Minimum Y component of surface normal to consider it a floor (0.7 = ~45 degrees)")]
        [Range(0f, 1f)]
        public float MinFloorNormalY = 0.7f;

        [Tooltip("Maximum height above ground to still consider a surface hit as floor (meters)")]
        public float MaxFloorHeight = 0.2f;

        class Baker : Baker<DodgeRollAuthoring>
        {
            public override void Bake(DodgeRollAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new DodgeRollComponent
                {
                    Duration = authoring.Duration,
                    Distance = authoring.Distance,
                    InvulnWindowStart = authoring.InvulnWindowStart,
                    InvulnWindowEnd = authoring.InvulnWindowEnd,
                    StaminaCost = authoring.StaminaCost,
                    CollisionLayerMask = authoring.CollisionLayerMask,
                    MinFloorNormalY = authoring.MinFloorNormalY,
                    MaxFloorHeight = authoring.MaxFloorHeight
                });

                // Ensure DodgeRollState is on the prefab so it can be replicated by NetCode
                AddComponent(entity, new DodgeRollState { IsActive = 0 });

                // Add Enableable components (disabled by default) to avoid structural changes at runtime
                AddComponent(entity, new DodgeRollInvuln());
                SetComponentEnabled<DodgeRollInvuln>(entity, false);

                AddComponent(entity, new PredictedDodgeRoll());
                SetComponentEnabled<PredictedDodgeRoll>(entity, false);

                // Add RollState for Opsive animation integration
                // DodgeRollAnimationBridgeSystem syncs DodgeRollState -> RollState
                AddComponent(entity, new RollState
                {
                    IsRolling = false,
                    RollType = 0,
                    TimeRemaining = 0f,
                    CooldownRemaining = 0f,
                    RollDuration = authoring.Duration,
                    RollCooldown = 0.2f
                });
            }
        }
    }
}
