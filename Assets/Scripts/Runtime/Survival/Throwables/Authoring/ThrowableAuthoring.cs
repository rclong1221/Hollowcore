using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Survival.Throwables.Authoring
{
    /// <summary>
    /// Authoring component for throwable prefabs (flares, glowsticks, lures, etc.)
    /// </summary>
    public class ThrowableAuthoring : MonoBehaviour
    {
        [Header("Throwable Type")]
        public ThrowableType Type = ThrowableType.Flare;

        [Header("Lifetime")]
        [Tooltip("How long the throwable lasts before despawning (seconds)")]
        public float Lifetime = 30f;

        [Header("Light Settings (Flare/Glowstick)")]
        [Tooltip("Does this throwable emit light?")]
        public bool EmitsLight = true;

        [Tooltip("Light color")]
        public Color LightColor = Color.red;

        [Tooltip("Maximum light intensity")]
        [Range(0f, 10f)]
        public float LightIntensity = 2f;

        [Tooltip("Light range in meters")]
        public float LightRange = 15f;

        [Tooltip("Flicker when near end of lifetime")]
        public bool FlickerAtEnd = true;

        [Header("Sound Settings (Lure)")]
        [Tooltip("Does this throwable emit sound?")]
        public bool EmitsSound = false;

        [Tooltip("Sound pattern type")]
        public LureSoundType SoundType = LureSoundType.Beep;

        [Tooltip("Audible range")]
        public float SoundVolume = 20f;

        [Header("Creature Attraction")]
        [Tooltip("Does this attract creatures?")]
        public bool AttractsCreatures = true;

        [Tooltip("Type of attraction")]
        public AttractionType AttractionType = AttractionType.Heat;

        [Tooltip("Attraction radius in meters")]
        public float AttractionRadius = 25f;

        [Tooltip("Attraction priority (higher overrides lower)")]
        public int AttractionPriority = 10;
    }

    /// <summary>
    /// Baker for ThrowableAuthoring.
    /// </summary>
    public class ThrowableBaker : Baker<ThrowableAuthoring>
    {
        public override void Bake(ThrowableAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Add thrown object component
            AddComponent(entity, new ThrownObject
            {
                Type = authoring.Type,
                RemainingLifetime = authoring.Lifetime,
                InitialLifetime = authoring.Lifetime,
                Intensity = 1f,
                ThrowerEntity = Entity.Null, // Set at runtime
                HasLanded = false
            });

            // Add velocity component for physics
            AddComponent(entity, new ThrownObjectVelocity
            {
                Linear = float3.zero // Set at runtime
            });

            // Add light emission if configured
            if (authoring.EmitsLight)
            {
                AddComponent(entity, new EmitsLight
                {
                    Color = new float3(authoring.LightColor.r, authoring.LightColor.g, authoring.LightColor.b),
                    Intensity = authoring.LightIntensity,
                    MaxIntensity = authoring.LightIntensity,
                    Range = authoring.LightRange,
                    FlickerAtEnd = authoring.FlickerAtEnd,
                    LightEntity = Entity.Null // Set by presentation layer
                });

                // Add visual state for presentation
                AddComponent(entity, new FlareVisualState
                {
                    IsEmittingParticles = true,
                    ParticleRate = 1f
                });
            }

            // Add sound emission if configured
            if (authoring.EmitsSound)
            {
                AddComponent(entity, new EmitsSound
                {
                    SoundType = authoring.SoundType,
                    Volume = authoring.SoundVolume,
                    IsPlaying = false
                });
            }

            // Add creature attraction if configured
            if (authoring.AttractsCreatures)
            {
                AddComponent(entity, new AttractsCreatures
                {
                    AttractionType = authoring.AttractionType,
                    Radius = authoring.AttractionRadius,
                    Priority = authoring.AttractionPriority,
                    IsActive = false // Activated when landed
                });
            }
        }
    }

    /// <summary>
    /// Authoring component for the ThrowablePrefabs singleton.
    /// Place on a GameObject in the subscene.
    /// </summary>
    public class ThrowablePrefabsAuthoring : MonoBehaviour
    {
        [Header("Throwable Prefabs")]
        public GameObject FlarePrefab;
        public GameObject GlowstickPrefab;
        public GameObject SoundLurePrefab;
        public GameObject DecoyPrefab;
    }

    /// <summary>
    /// Baker for ThrowablePrefabsAuthoring.
    /// </summary>
    public class ThrowablePrefabsBaker : Baker<ThrowablePrefabsAuthoring>
    {
        public override void Bake(ThrowablePrefabsAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new ThrowablePrefabs
            {
                FlarePrefab = authoring.FlarePrefab != null
                    ? GetEntity(authoring.FlarePrefab, TransformUsageFlags.Dynamic)
                    : Entity.Null,
                GlowstickPrefab = authoring.GlowstickPrefab != null
                    ? GetEntity(authoring.GlowstickPrefab, TransformUsageFlags.Dynamic)
                    : Entity.Null,
                SoundLurePrefab = authoring.SoundLurePrefab != null
                    ? GetEntity(authoring.SoundLurePrefab, TransformUsageFlags.Dynamic)
                    : Entity.Null,
                DecoyPrefab = authoring.DecoyPrefab != null
                    ? GetEntity(authoring.DecoyPrefab, TransformUsageFlags.Dynamic)
                    : Entity.Null
            });
        }
    }

    /// <summary>
    /// Authoring component for players with throwable capability.
    /// Adds inventory buffer and selected throwable tracking.
    /// </summary>
    public class PlayerThrowablesAuthoring : MonoBehaviour
    {
        [Header("Starting Inventory")]
        public int StartingFlares = 3;
        public int StartingGlowsticks = 2;
        public int StartingSoundLures = 1;
        public int StartingDecoys = 0;

        [Header("Default Selection")]
        public ThrowableType DefaultSelected = ThrowableType.Flare;
    }

    /// <summary>
    /// Baker for PlayerThrowablesAuthoring.
    /// </summary>
    public class PlayerThrowablesBaker : Baker<PlayerThrowablesAuthoring>
    {
        public override void Bake(PlayerThrowablesAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Add inventory buffer
            var buffer = AddBuffer<ThrowableInventory>(entity);

            if (authoring.StartingFlares > 0)
            {
                buffer.Add(new ThrowableInventory
                {
                    Type = ThrowableType.Flare,
                    Quantity = authoring.StartingFlares
                });
            }

            if (authoring.StartingGlowsticks > 0)
            {
                buffer.Add(new ThrowableInventory
                {
                    Type = ThrowableType.Glowstick,
                    Quantity = authoring.StartingGlowsticks
                });
            }

            if (authoring.StartingSoundLures > 0)
            {
                buffer.Add(new ThrowableInventory
                {
                    Type = ThrowableType.SoundLure,
                    Quantity = authoring.StartingSoundLures
                });
            }

            if (authoring.StartingDecoys > 0)
            {
                buffer.Add(new ThrowableInventory
                {
                    Type = ThrowableType.Decoy,
                    Quantity = authoring.StartingDecoys
                });
            }

            // Add selected throwable
            AddComponent(entity, new SelectedThrowable
            {
                Type = authoring.DefaultSelected
            });
        }
    }
}
