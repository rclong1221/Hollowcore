using Unity.Entities;
using UnityEngine;
using Visuals.Components;

namespace Visuals.Authoring
{
    public class VisorAuthoring : MonoBehaviour
    {
        public float MaxBatterySeconds = 3600f; // 1 hour
        public float DefaultIntensity = 500.0f;
        public float DefaultRange = 100.0f;
        public float DrainRate = 1.0f;
        public float RechargeRate = 0.5f; // Recharges at half the drain speed

        [Header("Debug")]
        public bool EnableRecharge = true; // Set to true for debug purposes
        
        [Header("References (Auto-found if null)")]
        public Light Flashlight;
        public Transform HudRoot;
        public Renderer VisorRenderer;

        public class Baker : Baker<VisorAuthoring>
        {
            public override void Bake(VisorAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // NEW: Optimized split components for bandwidth efficiency
                // FlashlightState - replicated to ALL clients (2 bits only)
                AddComponent(entity, new FlashlightState
                {
                    IsOn = false,
                    IsFlickering = false
                });

                // FlashlightConfig - replicated only to predicted clients (~200 bits)
                AddComponent(entity, new FlashlightConfig
                {
                    BatteryCurrent = authoring.MaxBatterySeconds,
                    BatteryMax = authoring.MaxBatterySeconds,
                    Intensity = authoring.DefaultIntensity,
                    Range = authoring.DefaultRange,
                    DrainRate = authoring.DrainRate,
                    RechargeRate = authoring.RechargeRate,
                    RechargeEnabled = authoring.EnableRecharge,
                    LastInputFrame = 0
                });

                // LEGACY: Keep FlashlightData for backward compatibility during migration
                // TODO: Remove once all systems are migrated to FlashlightState/FlashlightConfig
                AddComponent(entity, new FlashlightData
                {
                    IsOn = false,
                    BatteryCurrent = authoring.MaxBatterySeconds,
                    BatteryMax = authoring.MaxBatterySeconds,
                    Intensity = authoring.DefaultIntensity,
                    Range = authoring.DefaultRange,
                    DrainRate = authoring.DrainRate,
                    RechargeRate = authoring.RechargeRate,
                    RechargeEnabled = authoring.EnableRecharge,
                    LastInputFrame = 0
                });

                // Visor Data
                AddComponent(entity, new HelmetVisor
                {
                    CrackLevel = 0f,
                    DirtLevel = 0f,
                    IceLevel = 0f
                });

                // HUD Data
                AddComponent(entity, new DiegeticHUD
                {
                    MasterOpacity = 1.0f,
                    GlitchAmount = 0f,
                    SwayOffset = Unity.Mathematics.float2.zero
                });

                // Managed References
                // Flashlight
                if (authoring.Flashlight == null) 
                    authoring.Flashlight = authoring.GetComponentInChildren<Light>();
                
                if (authoring.Flashlight != null)
                {
                    AddComponentObject(entity, new FlashlightReference
                    {
                        LightSource = authoring.Flashlight
                    });
                }

                // Visor/HUD
                if (authoring.HudRoot == null)
                {
                    var hud = authoring.transform.Find("HUD");
                    if (hud != null) authoring.HudRoot = hud;
                }

                Material visorMat = null;
                if (authoring.VisorRenderer != null)
                {
                    // Use sharedMaterial in editor, usually we want instance at runtime?
                    // We'll trust the renderer reference.
                    visorMat = authoring.VisorRenderer.sharedMaterial;
                }

                AddComponentObject(entity, new VisorReference
                {
                    HudRoot = authoring.HudRoot,
                    VisorMaterial = visorMat
                });
            }
        }
    }
}
