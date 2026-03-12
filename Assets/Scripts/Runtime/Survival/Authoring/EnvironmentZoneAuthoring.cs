using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using DIG.Survival.Environment;
using ZoneShapeType = DIG.Survival.Environment.ZoneShapeType;

namespace DIG.Survival.Authoring
{
    /// <summary>
    /// Local collision layer constants for trigger zones.
    /// Matches values from DIG.Player.Components.CollisionLayers.
    /// </summary>
    internal static class TriggerLayers
    {
        public const uint Default = 1u << 0;  // Some players may use this layer
        public const uint Player = 1u << 1;
        public const uint Trigger = 1u << 6;
        public const uint Creature = 1u << 8;
    }

    // ZoneShapeType is defined in DIG.Survival.Environment.ZoneShapeType
    // Use that enum directly to avoid ambiguity

    /// <summary>
    /// Authoring component for environment zone triggers.
    /// Defines zone bounds directly without requiring Unity Collider components.
    /// This avoids conflicts with Unity Physics' built-in collider bakers.
    /// </summary>
    [AddComponentMenu("DIG/Environment/Environment Zone")]
    public class EnvironmentZoneAuthoring : MonoBehaviour
    {
        [Header("Zone Shape")]
        [Tooltip("Shape of the trigger volume")]
        public ZoneShapeType Shape = ZoneShapeType.Box;
        
        [Tooltip("Size of the box (for Box shape)")]
        public Vector3 BoxSize = new Vector3(5f, 3f, 5f);
        
        [Tooltip("Radius (for Sphere and Capsule shapes)")]
        public float Radius = 5f;
        
        [Tooltip("Height (for Capsule shape only)")]
        public float CapsuleHeight = 3f;
        
        [Tooltip("Center offset from transform position")]
        public Vector3 Center = Vector3.zero;

        [Header("Zone Type")]
        [Tooltip("Type of environment in this zone")]
        public EnvironmentZoneType ZoneType = EnvironmentZoneType.Pressurized;

        [Header("Oxygen Settings")]
        [Tooltip("Does oxygen deplete in this zone?")]
        public bool OxygenRequired = false;
        
        [Tooltip("Multiplier for oxygen depletion (1.0 = normal, 2.0 = double)")]
        [Range(0f, 5f)]
        public float OxygenDepletionMultiplier = 1f;

        [Header("Temperature")]
        [Tooltip("Temperature in Celsius")]
        public float Temperature = 20f;

        [Header("Radiation")]
        [Tooltip("Radiation accumulation rate per second (0 = none)")]
        public float RadiationRate = 0f;

        [Header("Stress / Darkness")]
        [Tooltip("Is this zone dark? (Increases stress if lights off)")]
        public bool IsDark = false;

        [Tooltip("Multiplier for stress gain.")]
        [Range(0f, 5f)]
        public float StressMultiplier = 1f;

        [Header("Display")]
        [Tooltip("Name shown in UI when entering zone")]
        public string DisplayName = "";

        private void OnValidate()
        {
            // Ensure logical bounds
            if (OxygenDepletionMultiplier < 0) OxygenDepletionMultiplier = 0;
            if (StressMultiplier < 0) StressMultiplier = 0;
            if (Radius < 0.01f) Radius = 0.01f;
            if (CapsuleHeight < Radius * 2f) CapsuleHeight = Radius * 2f;
            if (BoxSize.x < 0.01f) BoxSize.x = 0.01f;
            if (BoxSize.y < 0.01f) BoxSize.y = 0.01f;
            if (BoxSize.z < 0.01f) BoxSize.z = 0.01f;
        }

        private void OnDrawGizmos()
        {
            // Color based on zone type
            Color gizmoColor = ZoneType switch
            {
                EnvironmentZoneType.Pressurized => new Color(0f, 1f, 0f, 0.2f),
                EnvironmentZoneType.Vacuum => new Color(0f, 0f, 0f, 0.3f),
                EnvironmentZoneType.Toxic => new Color(0f, 1f, 0f, 0.3f),
                EnvironmentZoneType.Radioactive => new Color(1f, 1f, 0f, 0.3f),
                EnvironmentZoneType.Cold => new Color(0f, 0.5f, 1f, 0.3f),
                EnvironmentZoneType.Hot => new Color(1f, 0.3f, 0f, 0.3f),
                EnvironmentZoneType.Underwater => new Color(0f, 0.3f, 1f, 0.3f),
                _ => new Color(0.5f, 0.5f, 0.5f, 0.2f)
            };

            Gizmos.color = gizmoColor;
            Gizmos.matrix = transform.localToWorldMatrix;

            switch (Shape)
            {
                case ZoneShapeType.Box:
                    Gizmos.DrawCube(Center, BoxSize);
                    Gizmos.DrawWireCube(Center, BoxSize);
                    break;
                    
                case ZoneShapeType.Sphere:
                    Gizmos.DrawSphere(Center, Radius);
                    Gizmos.DrawWireSphere(Center, Radius);
                    break;
                    
                case ZoneShapeType.Capsule:
                    // Draw capsule as sphere at top and bottom + cylinder in middle
                    float halfHeight = (CapsuleHeight - Radius * 2f) * 0.5f;
                    Gizmos.DrawSphere(Center + Vector3.up * halfHeight, Radius);
                    Gizmos.DrawSphere(Center + Vector3.down * halfHeight, Radius);
                    Gizmos.DrawWireSphere(Center + Vector3.up * halfHeight, Radius);
                    Gizmos.DrawWireSphere(Center + Vector3.down * halfHeight, Radius);
                    break;
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Brighter color when selected
            Color gizmoColor = ZoneType switch
            {
                EnvironmentZoneType.Pressurized => new Color(0f, 1f, 0f, 0.5f),
                EnvironmentZoneType.Vacuum => new Color(0.3f, 0.3f, 0.3f, 0.5f),
                EnvironmentZoneType.Toxic => new Color(0f, 1f, 0f, 0.5f),
                EnvironmentZoneType.Radioactive => new Color(1f, 1f, 0f, 0.5f),
                EnvironmentZoneType.Cold => new Color(0f, 0.5f, 1f, 0.5f),
                EnvironmentZoneType.Hot => new Color(1f, 0.3f, 0f, 0.5f),
                EnvironmentZoneType.Underwater => new Color(0f, 0.3f, 1f, 0.5f),
                _ => new Color(0.5f, 0.5f, 0.5f, 0.5f)
            };

            Gizmos.color = gizmoColor;
            Gizmos.matrix = transform.localToWorldMatrix;

            switch (Shape)
            {
                case ZoneShapeType.Box:
                    Gizmos.DrawWireCube(Center, BoxSize);
                    break;
                case ZoneShapeType.Sphere:
                    Gizmos.DrawWireSphere(Center, Radius);
                    break;
                case ZoneShapeType.Capsule:
                    float halfHeight = (CapsuleHeight - Radius * 2f) * 0.5f;
                    Gizmos.DrawWireSphere(Center + Vector3.up * halfHeight, Radius);
                    Gizmos.DrawWireSphere(Center + Vector3.down * halfHeight, Radius);
                    break;
            }
        }
    }

    /// <summary>
    /// Baker for EnvironmentZoneAuthoring.
    /// Creates a simple bounds-based zone entity WITHOUT physics colliders.
    /// Zone detection is done via AABB/distance checks in EnvironmentZoneDetectionSystem.
    /// This avoids all physics simulation issues (floating, invisible walls, etc.).
    /// </summary>
    public class EnvironmentZoneBaker : Baker<EnvironmentZoneAuthoring>
    {
        public override void Bake(EnvironmentZoneAuthoring authoring)
        {
            // Warn about existing colliders on this object
            var existingCollider = authoring.GetComponent<UnityEngine.Collider>();
            if (existingCollider != null)
            {
                UnityEngine.Debug.LogWarning($"[EnvironmentZone] '{authoring.gameObject.name}' has a {existingCollider.GetType().Name} component. " +
                    "This is no longer needed - EnvironmentZoneAuthoring uses simple bounds checking.");
            }
            
            // Use Renderable - we don't need any physics
            var entity = GetEntity(TransformUsageFlags.Renderable);

            var displayName = new Unity.Collections.FixedString64Bytes();
            if (!string.IsNullOrEmpty(authoring.DisplayName))
            {
                displayName = authoring.DisplayName;
            }

            // Calculate world center position
            Vector3 worldPos = authoring.transform.TransformPoint(authoring.Center);
            Vector3 scale = authoring.transform.lossyScale;

            // Create ZoneBounds based on shape
            var zoneBounds = new ZoneBounds
            {
                Center = new float3(worldPos.x, worldPos.y, worldPos.z)
            };

            switch (authoring.Shape)
            {
                case ZoneShapeType.Box:
                    zoneBounds.Shape = DIG.Survival.Environment.ZoneShapeType.Box;
                    zoneBounds.HalfExtents = new float3(
                        authoring.BoxSize.x * scale.x * 0.5f,
                        authoring.BoxSize.y * scale.y * 0.5f,
                        authoring.BoxSize.z * scale.z * 0.5f
                    );
                    break;
                    
                case ZoneShapeType.Sphere:
                    zoneBounds.Shape = DIG.Survival.Environment.ZoneShapeType.Sphere;
                    // Use average scale for sphere radius
                    float avgScale = (scale.x + scale.y + scale.z) / 3f;
                    zoneBounds.Radius = authoring.Radius * avgScale;
                    break;
                    
                case ZoneShapeType.Capsule:
                    zoneBounds.Shape = DIG.Survival.Environment.ZoneShapeType.Capsule;
                    float capAvgScale = (scale.x + scale.z) / 2f;
                    zoneBounds.Radius = authoring.Radius * capAvgScale;
                    zoneBounds.HalfHeight = (authoring.CapsuleHeight * scale.y - zoneBounds.Radius * 2f) * 0.5f;
                    if (zoneBounds.HalfHeight < 0) zoneBounds.HalfHeight = 0;
                    break;
            }

            // Add ZoneBounds component
            AddComponent(entity, zoneBounds);

            // Add EnvironmentZone component
            AddComponent(entity, new EnvironmentZone
            {
                ZoneType = authoring.ZoneType,
                OxygenRequired = authoring.OxygenRequired,
                OxygenDepletionMultiplier = authoring.OxygenDepletionMultiplier,
                Temperature = authoring.Temperature,
                RadiationRate = authoring.RadiationRate,
                IsDark = authoring.IsDark,
                StressMultiplier = authoring.StressMultiplier,
                DisplayName = displayName
            });
        }
    }
}
