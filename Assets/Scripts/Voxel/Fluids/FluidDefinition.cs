using UnityEngine;

namespace DIG.Voxel.Fluids
{
    /// <summary>
    /// Defines a fluid type with its properties, appearance, and behavior.
    /// Used by designers to configure water, lava, oil, etc.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/World/Fluid Definition")]
    public class FluidDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique ID for this fluid (1-255, 0 = none)")]
        public byte FluidID = 1;
        
        [Tooltip("Display name of this fluid")]
        public string FluidName = "Water";
        
        [Tooltip("Fluid type category")]
        public FluidType Type = FluidType.Water;
        
        [Header("Appearance")]
        [Tooltip("Primary color of the fluid")]
        public Color FluidColor = new Color(0.2f, 0.4f, 0.8f, 0.7f);
        
        [Tooltip("Material for fluid rendering")]
        public Material FluidMaterial;
        
        [Tooltip("Transparency (0 = opaque, 1 = fully transparent)")]
        [Range(0f, 1f)]
        public float Transparency = 0.6f;
        
        [Tooltip("Surface reflectivity")]
        [Range(0f, 1f)]
        public float Reflectivity = 0.4f;
        
        [Tooltip("Emissive (glowing) - for lava")]
        public bool IsEmissive = false;
        
        [Tooltip("Emission color if emissive")]
        [ColorUsage(true, true)]
        public Color EmissionColor = Color.black;
        
        [Header("Physics")]
        [Tooltip("Flow speed multiplier (water=1, lava=0.1, gas=2)")]
        [Range(0.01f, 5f)]
        public float Viscosity = 1f;
        
        [Tooltip("Density for buoyancy (water=1, oil=0.8, lava=3)")]
        public float Density = 1f;
        
        [Tooltip("How fast fluid spreads per tick")]
        [Range(1, 16)]
        public int SpreadRate = 4;
        
        [Header("Pressure")]
        [Tooltip("Is this a pressurized fluid (erupts when released)")]
        public bool IsPressurized = false;
        
        [Tooltip("Pressure level (higher = more violent eruption)")]
        [Range(1f, 20f)]
        public float PressureLevel = 1f;
        
        [Tooltip("Prefab spawned on eruption")]
        public GameObject EruptionPrefab;
        
        [Tooltip("Radius affected by eruption")]
        public float EruptionRadius = 10f;
        
        [Header("Damage")]
        [Tooltip("Type of damage this fluid deals")]
        public FluidDamageType DamageType = FluidDamageType.None;
        
        [Tooltip("Damage per second while submerged")]
        public float DamagePerSecond = 0f;
        
        [Tooltip("Depth at which damage starts (for water drowning)")]
        public float DamageStartDepth = 2f;
        
        [Header("Special Properties")]
        [Tooltip("Can this fluid catch fire (oil)")]
        public bool IsFlammable = false;
        
        [Tooltip("Is this fluid toxic (damages over time)")]
        public bool IsToxic = false;
        
        [Tooltip("Can this fluid cool to solid (lava -> obsidian)")]
        public bool CoolsToSolid = false;
        
        [Tooltip("Material ID when cooled")]
        public byte CooledMaterialID = 0;
        
        [Tooltip("Temperature at which fluid cools to solid")]
        public float CoolingTemperature = 500f;
        
        [Header("Audio")]
        [Tooltip("Ambient sound when near fluid")]
        public AudioClip AmbientSound;
        
        [Tooltip("Sound when entering fluid")]
        public AudioClip EnterSound;
        
        [Tooltip("Sound when fluid flows")]
        public AudioClip FlowSound;
        
        [Header("VFX")]
        [Tooltip("Particle effect when entering fluid")]
        public GameObject SplashPrefab;
        
        [Tooltip("Surface steam/vapor effect")]
        public GameObject SurfaceEffectPrefab;
        
        private void OnValidate()
        {
            // Water must allow drowning
            if (Type == FluidType.Water && DamageType == FluidDamageType.None && DamagePerSecond > 0)
            {
                DamageType = FluidDamageType.Drowning;
            }
            
            // Lava must burn
            if (Type == FluidType.Lava)
            {
                DamageType = FluidDamageType.Burning;
                if (DamagePerSecond <= 0) DamagePerSecond = 50f;
                IsEmissive = true;
            }
            
            // Toxic gas must be toxic
            if (Type == FluidType.ToxicGas)
            {
                DamageType = FluidDamageType.Toxic;
                if (DamagePerSecond <= 0) DamagePerSecond = 10f;
                IsToxic = true;
            }
        }
    }
}
