using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Player.Components;

namespace Player.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Player/Authoring/Climbable Object Authoring")]
    public class ClimbableObjectAuthoring : MonoBehaviour
    {
        public ClimbableType Type = ClimbableType.Ladder;

        [Tooltip("Optional transform used to mark the bottom of the climbable segment. If null, the authoring GameObject position is used.")]
        public Transform BottomPoint;

        [Tooltip("Optional transform used to mark the top of the climbable segment. If null, the authoring GameObject position + (0, 2, 0) is used.")]
        public Transform TopPoint;

        [Min(0f)]
        public float ClimbSpeed = 2.0f;

        [Min(0f)]
        public float InteractionRadius = 1.0f;

        void Reset()
        {
            // Provide sensible defaults
            if (BottomPoint == null)
                BottomPoint = this.transform;
            if (TopPoint == null)
            {
                var go = new GameObject("TopPoint");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 2f, 0f);
                TopPoint = go.transform;
            }
        }

        void OnValidate()
        {
            // Ensure we have at least a bottom reference
            if (BottomPoint == null)
                BottomPoint = this.transform;
        }

        void OnDrawGizmosSelected()
        {
            if (BottomPoint == null) return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(BottomPoint.position, 0.05f);

            if (TopPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(TopPoint.position, 0.05f);
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(BottomPoint.position, TopPoint.position);
            }
        }
    }

    // Baker for conversion to ECS ClimbableObject IComponentData
    public class ClimbableObjectAuthoringBaker : Baker<ClimbableObjectAuthoring>
    {
        public override void Bake(ClimbableObjectAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            float3 bottomPos = authoring.BottomPoint != null
                ? new float3(authoring.BottomPoint.position.x, authoring.BottomPoint.position.y, authoring.BottomPoint.position.z)
                : new float3(authoring.transform.position.x, authoring.transform.position.y, authoring.transform.position.z);

            float3 topPos = authoring.TopPoint != null
                ? new float3(authoring.TopPoint.position.x, authoring.TopPoint.position.y, authoring.TopPoint.position.z)
                : new float3(authoring.transform.position.x, authoring.transform.position.y + 2f, authoring.transform.position.z);

            AddComponent(entity, new ClimbableObject
            {
                Type = authoring.Type,
                BottomPosition = bottomPos,
                TopPosition = topPos,
                ClimbSpeed = authoring.ClimbSpeed,
                InteractionRadius = authoring.InteractionRadius
            });
        }
    }
}
