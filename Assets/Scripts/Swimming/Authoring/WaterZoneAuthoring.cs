using UnityEngine;
using Unity.Entities;
using DIG.Swimming;
using DIG.Survival.Environment;
using DIG.Survival.Authoring;

namespace DIG.Swimming.Authoring
{
    /// <summary>
    /// Authoring component for water zones.
    /// Extends EnvironmentZoneAuthoring with water-specific properties.
    /// Add this alongside EnvironmentZoneAuthoring set to Underwater type.
    /// </summary>
    [AddComponentMenu("DIG/Swimming/Water Zone Properties")]
    public class WaterZoneAuthoring : MonoBehaviour
    {
        [Header("Water Properties")]
        [Tooltip("Water density (kg/m³). 1000 = fresh water, 1025 = seawater")]
        public float Density = 1000f;
        
        [Tooltip("Viscosity affects movement drag. Higher = slower movement")]
        [Range(0.1f, 2f)]
        public float Viscosity = 0.5f;
        
        [Tooltip("Buoyancy modifier. Positive = float up, negative = sink, 0 = neutral")]
        [Range(-1f, 1f)]
        public float BuoyancyModifier = 0.1f;
        
        [Header("Current/Flow")]
        [Tooltip("Direction and speed of water current")]
        public Vector3 CurrentVelocity = Vector3.zero;
        
        [Header("Surface")]
        [Tooltip("Y coordinate of water surface. If 0, will use top of zone bounds.")]
        public float SurfaceY = 0f;
        
        [Tooltip("Auto-calculate surface Y from zone bounds")]
        public bool AutoCalculateSurface = true;

        class Baker : Baker<WaterZoneAuthoring>
        {
            public override void Bake(WaterZoneAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                
                // Calculate surface Y
                float surfaceY = authoring.SurfaceY;
                if (authoring.AutoCalculateSurface)
                {
                    // Get the zone bounds from parent EnvironmentZoneAuthoring if present
                    var envZone = authoring.GetComponent<EnvironmentZoneAuthoring>();
                    if (envZone != null)
                    {
                        Vector3 worldCenter = authoring.transform.TransformPoint(envZone.Center);
                        float halfHeight = envZone.BoxSize.y * authoring.transform.lossyScale.y * 0.5f;
                        surfaceY = worldCenter.y + halfHeight;
                    }
                    else
                    {
                        surfaceY = authoring.transform.position.y;
                    }
                }
                
                AddComponent(entity, new WaterProperties
                {
                    Density = authoring.Density,
                    Viscosity = authoring.Viscosity,
                    CurrentVelocity = new Unity.Mathematics.float3(
                        authoring.CurrentVelocity.x,
                        authoring.CurrentVelocity.y,
                        authoring.CurrentVelocity.z
                    ),
                    BuoyancyModifier = authoring.BuoyancyModifier,
                    SurfaceY = surfaceY
                });
            }
        }
    }
}
