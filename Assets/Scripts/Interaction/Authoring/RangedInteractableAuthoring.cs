using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.Interaction.Authoring
{
    /// <summary>
    /// EPIC 16.1 Phase 6: Authoring component for ranged interactables.
    ///
    /// Designer workflow:
    /// 1. Add InteractableAuthoring (Type = Instant or Timed)
    /// 2. Add RangedInteractableAuthoring (configure range, init type, etc.)
    /// 3. Players can initiate interaction from MaxRange via raycast or projectile
    /// 4. On connection, falls through to standard InteractAbilitySystem
    /// </summary>
    public class RangedInteractableAuthoring : MonoBehaviour
    {
        [Header("Ranged Configuration")]
        [Tooltip("Maximum range for initiating this interaction")]
        public float MaxRange = 20f;

        [Tooltip("How the ranged interaction starts")]
        public RangedInitType InitType = RangedInitType.Raycast;

        [Tooltip("Projectile travel speed (units/sec). Only used for Projectile/ArcProjectile types")]
        public float ProjectileSpeed = 15f;

        [Tooltip("Whether the player must aim within a cone to initiate")]
        public bool RequireAimAtTarget = true;

        public class Baker : Baker<RangedInteractableAuthoring>
        {
            public override void Bake(RangedInteractableAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new RangedInteraction
                {
                    MaxRange = authoring.MaxRange,
                    ProjectileSpeed = authoring.ProjectileSpeed,
                    InitType = authoring.InitType,
                    RequireAimAtTarget = authoring.RequireAimAtTarget
                });

                AddComponent(entity, new RangedInteractionState
                {
                    IsConnecting = false,
                    ConnectionProgress = 0,
                    InitiatorEntity = Entity.Null,
                    LaunchPosition = float3.zero,
                    TargetPosition = float3.zero
                });
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Max range sphere (orange)
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, MaxRange);

            // Direction arrow (forward)
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.8f);
            Gizmos.DrawRay(transform.position, transform.forward * 2f);

            // Init type label position
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.position, 0.1f);
        }
#endif
    }
}
