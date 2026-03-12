using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Player.Components;

namespace DIG.Surface
{
    /// <summary>
    /// EPIC 16.10 Phase 2: Loads SurfaceGameplayConfig SO from Resources,
    /// builds a BlobAsset indexed by SurfaceID for O(1) Burst-compatible lookup.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class SurfaceGameplayConfigSystem : SystemBase
    {
        private bool _initialized;

        protected override void OnUpdate()
        {
            if (_initialized) return;

            var config = Resources.Load<SurfaceGameplayConfig>("SurfaceGameplayConfig");
            if (config == null)
            {
                // No config asset — create default blob with 1.0 multipliers
                BuildDefaultBlob();
            }
            else
            {
                BuildBlobFromConfig(config);
            }

            _initialized = true;
            Enabled = false;
        }

        private void BuildBlobFromConfig(SurfaceGameplayConfig config)
        {
            int surfaceCount = (int)SurfaceID.Energy_Shield + 1; // 24 entries

            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<SurfaceGameplayBlob>();
            var modifiers = builder.Allocate(ref root.Modifiers, surfaceCount);

            // Initialize all with defaults
            var defaultMods = new SurfaceGameplayModifiers
            {
                NoiseMultiplier = 1.0f,
                SpeedMultiplier = 1.0f,
                SlipFactor = 0f,
                FallDamageMultiplier = 1.0f,
                DamagePerSecond = 0f,
                DamageType = (byte)DamageType.Physical
            };

            for (int i = 0; i < surfaceCount; i++)
                modifiers[i] = defaultMods;

            // Override from config entries
            foreach (var entry in config.Entries)
            {
                int index = (int)entry.SurfaceId;
                if (index < 0 || index >= surfaceCount) continue;

                modifiers[index] = new SurfaceGameplayModifiers
                {
                    NoiseMultiplier = entry.NoiseMultiplier,
                    SpeedMultiplier = entry.SpeedMultiplier,
                    SlipFactor = entry.SlipFactor,
                    FallDamageMultiplier = entry.FallDamageMultiplier,
                    DamagePerSecond = entry.DamagePerSecond,
                    DamageType = (byte)entry.DamageType
                };
            }

            var blobRef = builder.CreateBlobAssetReference<SurfaceGameplayBlob>(Allocator.Persistent);
            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, new SurfaceGameplayConfigSingleton { Config = blobRef });
        }

        private void BuildDefaultBlob()
        {
            int surfaceCount = (int)SurfaceID.Energy_Shield + 1;

            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<SurfaceGameplayBlob>();
            var modifiers = builder.Allocate(ref root.Modifiers, surfaceCount);

            // Initialize all with neutral defaults
            var defaultMods = new SurfaceGameplayModifiers
            {
                NoiseMultiplier = 1.0f,
                SpeedMultiplier = 1.0f,
                SlipFactor = 0f,
                FallDamageMultiplier = 1.0f,
                DamagePerSecond = 0f,
                DamageType = (byte)DamageType.Physical
            };

            for (int i = 0; i < surfaceCount; i++)
                modifiers[i] = defaultMods;

            // Recommended per-surface gameplay values from EPIC 16.10 design doc
            //                                     Noise  Speed  Slip   FallDmg
            SetMod(ref modifiers, SurfaceID.Concrete,   1.3f, 1.0f,  0.0f,  1.2f);
            SetMod(ref modifiers, SurfaceID.Metal_Thin,  1.5f, 1.0f,  0.0f,  1.3f);
            SetMod(ref modifiers, SurfaceID.Metal_Thick, 1.5f, 1.0f,  0.0f,  1.3f);
            SetMod(ref modifiers, SurfaceID.Wood,        1.1f, 1.0f,  0.0f,  1.0f);
            SetMod(ref modifiers, SurfaceID.Dirt,        0.7f, 0.9f,  0.0f,  0.7f);
            SetMod(ref modifiers, SurfaceID.Sand,        0.5f, 0.7f,  0.0f,  0.5f);
            SetMod(ref modifiers, SurfaceID.Grass,       0.6f, 0.95f, 0.0f,  0.6f);
            SetMod(ref modifiers, SurfaceID.Gravel,      1.4f, 0.85f, 0.0f,  0.9f);
            SetMod(ref modifiers, SurfaceID.Snow,        0.4f, 0.8f,  0.15f, 0.4f);
            SetMod(ref modifiers, SurfaceID.Ice,         0.3f, 1.1f,  0.8f,  1.4f);
            SetMod(ref modifiers, SurfaceID.Water,       0.3f, 0.6f,  0.0f,  0.3f);
            SetMod(ref modifiers, SurfaceID.Mud,         0.5f, 0.5f,  0.0f,  0.4f);
            SetMod(ref modifiers, SurfaceID.Glass,       1.6f, 1.0f,  0.1f,  1.5f);
            SetMod(ref modifiers, SurfaceID.Stone,       1.2f, 1.0f,  0.0f,  1.1f);
            SetMod(ref modifiers, SurfaceID.Foliage,     0.4f, 0.9f,  0.0f,  0.5f);
            SetMod(ref modifiers, SurfaceID.Rubber,      0.3f, 1.0f,  0.0f,  0.5f);

            var blobRef = builder.CreateBlobAssetReference<SurfaceGameplayBlob>(Allocator.Persistent);
            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, new SurfaceGameplayConfigSingleton { Config = blobRef });
        }

        private static void SetMod(ref BlobBuilderArray<SurfaceGameplayModifiers> modifiers,
            SurfaceID id, float noise, float speed, float slip, float fallDmg)
        {
            modifiers[(int)id] = new SurfaceGameplayModifiers
            {
                NoiseMultiplier = noise,
                SpeedMultiplier = speed,
                SlipFactor = slip,
                FallDamageMultiplier = fallDmg,
                DamagePerSecond = 0f,
                DamageType = (byte)DamageType.Physical
            };
        }

        protected override void OnDestroy()
        {
            if (SystemAPI.TryGetSingleton<SurfaceGameplayConfigSingleton>(out var singleton))
            {
                if (singleton.Config.IsCreated)
                    singleton.Config.Dispose();
            }
        }
    }
}
