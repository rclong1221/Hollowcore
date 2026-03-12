using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Player.Components;

namespace Player.Authoring
{
    [DisallowMultipleComponent]
    public class ClimbableAuthoring : MonoBehaviour
    {
        [Header("Climb Settings")]
        public ClimbableType Type = ClimbableType.Ladder;
        public float ClimbSpeed = 2.0f;
        public float InteractionRadius = 1.5f;

        [Header("Geometry")]
        [Tooltip("Local position of the bottom anchor point")]
        public Vector3 BottomOffset = new Vector3(0, -1, 0);
        [Tooltip("Local position of the top anchor point")]
        public Vector3 TopOffset = new Vector3(0, 1, 0);

        [Header("Gizmos")]
        public bool ShowGizmos = true;
        public Color GizmoColor = Color.yellow;

        class Baker : Baker<ClimbableAuthoring>
        {
            public override void Bake(ClimbableAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Calculate world positions for top/bottom based on transform
                // Note: We bake the world positions so the runtime system doesn't need to calculate them every frame
                // If the object moves at runtime, we might need a system to update these, but for static ladders this is fine.
                // For moving ladders, we'd need to bake local offsets and apply LocalToWorld at runtime.
                // For now, assuming static or baking world pos is sufficient for the current system design.
                
                // Actually, the current ClimbDetectionSystem uses world positions directly.
                // Let's bake the world positions at the time of baking.
                // If the object is a moving platform, this component needs to be updated by a system.
                
                float3 worldBottom = authoring.transform.TransformPoint(authoring.BottomOffset);
                float3 worldTop = authoring.transform.TransformPoint(authoring.TopOffset);

                AddComponent(entity, new ClimbableObject
                {
                    Type = authoring.Type,
                    BottomPosition = worldBottom,
                    TopPosition = worldTop,
                    ClimbSpeed = authoring.ClimbSpeed,
                    InteractionRadius = authoring.InteractionRadius
                });
            }
        }

        void OnDrawGizmos()
        {
            if (!ShowGizmos) return;

            Gizmos.color = GizmoColor;
            Vector3 bottom = transform.TransformPoint(BottomOffset);
            Vector3 top = transform.TransformPoint(TopOffset);

            Gizmos.DrawWireSphere(bottom, 0.1f);
            Gizmos.DrawWireSphere(top, 0.1f);
            Gizmos.DrawLine(bottom, top);
            
            // Draw interaction radius at midpoint
            Vector3 mid = (bottom + top) * 0.5f;
            Gizmos.color = new Color(GizmoColor.r, GizmoColor.g, GizmoColor.b, 0.3f);
            Gizmos.DrawWireSphere(mid, InteractionRadius);
        }
    }
}
