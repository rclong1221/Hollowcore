using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Player.Components;

namespace Player.Authoring
{
    /// <summary>
    /// Creates an external force zone (wind, conveyor, etc.).
    /// Objects entering this trigger will have forces applied.
    /// <para>
    /// <b>Setup:</b>
    /// 1. Add this component to an object with a Collider set as Trigger
    /// 2. Configure force direction and magnitude
    /// 3. Players entering the trigger will be pushed
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ExternalForceZoneAuthoring : MonoBehaviour
    {
        [Header("Force Configuration")]
        [Tooltip("Force to apply (direction and magnitude). For radial forces, only magnitude is used.")]
        public Vector3 Force = new Vector3(0, 0, 10);
        
        [Tooltip("If true, force pushes in Force direction. If false, pushes away from zone center.")]
        public bool IsDirectional = true;
        
        [Tooltip("For radial forces, the center point. If empty, uses transform position.")]
        public Transform RadialCenter;
        
        [Header("Force Behavior")]
        [Tooltip("Continuous: Apply every frame while in zone. Impulse: Apply once on entry.")]
        public ForceMode Mode = ForceMode.Continuous;
        
        [Tooltip("How quickly force stops when leaving zone (higher = quicker stop)")]
        [Range(0f, 20f)]
        public float ExitDamping = 5f;
        
        [Tooltip("Priority when zones overlap. Higher priority overrides.")]
        public int Priority = 0;
        
        [Header("Visualization")]
        [Tooltip("Color for editor gizmo")]
        public Color GizmoColor = new Color(0.3f, 0.7f, 1f, 0.3f);
        
        [Tooltip("Size of force arrows in gizmo")]
        [Range(0.1f, 5f)]
        public float ArrowSize = 1f;
        
        public enum ForceMode
        {
            Continuous,
            Impulse
        }
        
        private void OnValidate()
        {
            // Ensure collider is trigger
            var collider = GetComponent<Collider>();
            if (collider != null && !collider.isTrigger)
            {
                Debug.LogWarning($"[ExternalForceZone] Collider on {gameObject.name} should be set as Trigger!");
            }
        }
        
        private void OnDrawGizmos()
        {
            DrawForceGizmos(false);
        }
        
        private void OnDrawGizmosSelected()
        {
            DrawForceGizmos(true);
        }
        
        private void DrawForceGizmos(bool selected)
        {
            Gizmos.color = selected ? GizmoColor : new Color(GizmoColor.r, GizmoColor.g, GizmoColor.b, GizmoColor.a * 0.5f);
            
            // Draw zone bounds
            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                if (collider is BoxCollider box)
                {
                    Gizmos.DrawCube(box.center, box.size);
                    Gizmos.DrawWireCube(box.center, box.size);
                }
                else if (collider is SphereCollider sphere)
                {
                    Gizmos.DrawWireSphere(sphere.center, sphere.radius);
                }
            }
            
            Gizmos.matrix = Matrix4x4.identity;
            
            // Draw force direction arrows
            if (IsDirectional && Force.magnitude > 0.01f)
            {
                Vector3 start = transform.position;
                Vector3 direction = Force.normalized;
                float arrowLength = ArrowSize * Mathf.Log10(Force.magnitude + 1);
                
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(start, direction * arrowLength);
                
                // Arrow head
                Vector3 end = start + direction * arrowLength;
                Vector3 right = Vector3.Cross(direction, Vector3.up).normalized;
                if (right.magnitude < 0.1f) right = Vector3.Cross(direction, Vector3.forward).normalized;
                
                Gizmos.DrawLine(end, end - direction * 0.3f + right * 0.15f);
                Gizmos.DrawLine(end, end - direction * 0.3f - right * 0.15f);
            }
            else if (!IsDirectional)
            {
                // Draw radial explosion lines
                Vector3 center = RadialCenter != null ? RadialCenter.position : transform.position;
                Gizmos.color = Color.red;
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI * 2f / 8f;
                    Vector3 dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                    Gizmos.DrawRay(center, dir * ArrowSize);
                }
            }
        }
        
        class Baker : Baker<ExternalForceZoneAuthoring>
        {
            public override void Bake(ExternalForceZoneAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                float3 center = authoring.RadialCenter != null 
                    ? (float3)authoring.RadialCenter.position 
                    : (float3)authoring.transform.position;
                
                AddComponent(entity, new ExternalForceZone
                {
                    Force = authoring.Force,
                    ExitDamping = authoring.ExitDamping,
                    IsDirectional = (byte)(authoring.IsDirectional ? 1 : 0),
                    Center = center,
                    IsContinuous = (byte)(authoring.Mode == ForceMode.Continuous ? 1 : 0),
                    Priority = authoring.Priority
                });
            }
        }
    }
}
