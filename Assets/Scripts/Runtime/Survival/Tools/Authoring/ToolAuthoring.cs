using UnityEngine;
using Unity.Entities;
using Unity.Collections;

namespace DIG.Survival.Tools.Authoring
{
    /// <summary>
    /// Authoring component for individual tool entities.
    /// Attach to tool prefabs that will be spawned for players.
    /// </summary>
    public class ToolAuthoring : MonoBehaviour
    {
        [Header("Tool Identity")]
        [Tooltip("The type of tool")]
        public ToolType ToolType = ToolType.None;

        [Tooltip("Display name for UI")]
        public string DisplayName = "Tool";

        [Header("Durability")]
        [Tooltip("Maximum durability/ammo")]
        public float MaxDurability = 100f;

        [Tooltip("Durability consumed per second while in use")]
        public float DegradeRatePerSecond = 1f;

        [Header("Drill Settings")]
        [Tooltip("Damage per second to voxels (Drill only)")]
        public float DrillVoxelDamage = 10f;

        [Tooltip("Drill range in meters")]
        public float DrillRange = 3f;

        [Tooltip("Resource collection multiplier")]
        public float DrillResourceMultiplier = 1f;

        [Header("Welder Settings")]
        [Tooltip("Health healed per second (Welder only)")]
        public float WelderHealPerSecond = 5f;

        [Tooltip("Damage per second to creatures")]
        public float WelderDamagePerSecond = 15f;

        [Tooltip("Welder range in meters")]
        public float WelderRange = 2f;

        [Header("Sprayer Settings")]
        [Tooltip("Foam prefab to spawn")]
        public GameObject FoamPrefab;

        [Tooltip("Ammo consumed per spray")]
        public float SprayerAmmoPerShot = 10f;

        [Tooltip("Sprayer range in meters")]
        public float SprayerRange = 5f;

        [Tooltip("Cooldown between sprays in seconds")]
        public float SprayerCooldown = 0.5f;

        [Header("Flashlight Settings")]
        [Tooltip("Light GameObject child (optional)")]
        public GameObject LightObject;

        [Tooltip("Battery drain per second while on")]
        public float FlashlightBatteryDrain = 0.5f;

        [Header("Geiger Settings")]
        [Tooltip("Scan radius in meters")]
        public float GeigerScanRadius = 10f;

        [Tooltip("Update interval in seconds")]
        public float GeigerUpdateInterval = 0.25f;
    }

    /// <summary>
    /// Baker for ToolAuthoring.
    /// </summary>
    public class ToolBaker : Baker<ToolAuthoring>
    {
        public override void Bake(ToolAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Add core tool components
            AddComponent(entity, new Tool
            {
                ToolType = authoring.ToolType,
                DisplayName = new FixedString32Bytes(authoring.DisplayName)
            });

            AddComponent(entity, new ToolDurability
            {
                Current = authoring.MaxDurability,
                Max = authoring.MaxDurability,
                DegradeRatePerSecond = authoring.DegradeRatePerSecond,
                IsDepleted = false
            });

            AddComponent(entity, new ToolUsageState
            {
                IsInUse = false,
                UseTimer = 0f,
                TargetPoint = Unity.Mathematics.float3.zero,
                TargetEntity = Entity.Null,
                TargetNormal = Unity.Mathematics.float3.zero,
                HasTarget = false
            });

            // ToolOwner will be set at runtime when spawned for a player
            AddComponent(entity, new ToolOwner { OwnerEntity = Entity.Null });

            // Add tool-specific components based on type
            switch (authoring.ToolType)
            {
                case ToolType.Drill:
                    AddComponent(entity, new DrillTool
                    {
                        VoxelDamagePerSecond = authoring.DrillVoxelDamage,
                        Range = authoring.DrillRange,
                        ResourceMultiplier = authoring.DrillResourceMultiplier
                    });
                    break;

                case ToolType.Welder:
                    AddComponent(entity, new WelderTool
                    {
                        HealPerSecond = authoring.WelderHealPerSecond,
                        DamagePerSecond = authoring.WelderDamagePerSecond,
                        Range = authoring.WelderRange
                    });
                    break;

                case ToolType.Sprayer:
                    var foamEntity = authoring.FoamPrefab != null
                        ? GetEntity(authoring.FoamPrefab, TransformUsageFlags.Dynamic)
                        : Entity.Null;

                    AddComponent(entity, new SprayerTool
                    {
                        FoamPrefab = foamEntity,
                        AmmoPerShot = authoring.SprayerAmmoPerShot,
                        Range = authoring.SprayerRange,
                        Cooldown = authoring.SprayerCooldown,
                        TimeSinceLastShot = 999f
                    });
                    break;

                case ToolType.Flashlight:
                    var lightEntity = authoring.LightObject != null
                        ? GetEntity(authoring.LightObject, TransformUsageFlags.Dynamic)
                        : Entity.Null;

                    AddComponent(entity, new FlashlightTool
                    {
                        LightEntity = lightEntity,
                        IsOn = false,
                        BatteryDrainPerSecond = authoring.FlashlightBatteryDrain
                    });
                    break;

                case ToolType.Geiger:
                    AddComponent(entity, new GeigerTool
                    {
                        ScanRadius = authoring.GeigerScanRadius,
                        UpdateInterval = authoring.GeigerUpdateInterval,
                        TimeSinceUpdate = 0f,
                        CurrentRadiationLevel = 0f
                    });
                    break;
            }
        }
    }
}
