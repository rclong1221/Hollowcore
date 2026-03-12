using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Targeting.Core
{
    /// <summary>
    /// Component providing crosshair/aim point information.
    /// This allows modular crosshair systems - different targeting assets can write to this.
    /// The targeting system reads from here instead of assuming screen center.
    /// </summary>
    public struct CrosshairData : IComponentData
    {
        /// <summary>
        /// World-space ray origin (usually camera position).
        /// </summary>
        public float3 RayOrigin;
        
        /// <summary>
        /// World-space ray direction (normalized, where crosshair points).
        /// </summary>
        public float3 RayDirection;
        
        /// <summary>
        /// Screen-space position of crosshair (0-1 normalized, 0.5,0.5 = center).
        /// </summary>
        public float2 ScreenPosition;
        
        /// <summary>
        /// World-space hit point if crosshair raycasted something.
        /// Use HitValid to check if this is populated.
        /// </summary>
        public float3 HitPoint;
        
        /// <summary>
        /// True if HitPoint contains a valid raycast hit.
        /// </summary>
        public bool HitValid;
        
        /// <summary>
        /// Entity hit by crosshair raycast (if any).
        /// </summary>
        public Entity HitEntity;
        
        /// <summary>
        /// Distance to HitPoint from RayOrigin.
        /// </summary>
        public float HitDistance;
        
        /// <summary>
        /// Default crosshair at screen center.
        /// </summary>
        public static CrosshairData Default => new CrosshairData
        {
            RayOrigin = float3.zero,
            RayDirection = new float3(0, 0, 1),
            ScreenPosition = new float2(0.5f, 0.5f),
            HitPoint = float3.zero,
            HitValid = false,
            HitEntity = Entity.Null,
            HitDistance = 0f
        };
    }
}
