using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Survival.Throwables
{
    /// <summary>
    /// Types of throwable items available in the game.
    /// </summary>
    public enum ThrowableType : byte
    {
        None = 0,
        Flare = 1,
        Glowstick = 2,
        SoundLure = 3,
        Decoy = 4
    }

    /// <summary>
    /// Types of creature attraction methods.
    /// </summary>
    public enum AttractionType : byte
    {
        None = 0,
        Heat = 1,
        Sound = 2,
        Visual = 3
    }

    /// <summary>
    /// Types of sounds emitted by lures.
    /// </summary>
    public enum LureSoundType : byte
    {
        Beep = 0,
        Voice = 1,
        Siren = 2
    }

    /// <summary>
    /// Buffer element tracking throwable items in player inventory.
    /// Each element represents a type of throwable and its quantity.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    [InternalBufferCapacity(4)] // Max 4 throwable types
    public struct ThrowableInventory : IBufferElementData
    {
        /// <summary>
        /// The type of throwable item.
        /// </summary>
        [GhostField]
        public ThrowableType Type;

        /// <summary>
        /// How many of this throwable the player has.
        /// </summary>
        [GhostField]
        public int Quantity;
    }

    /// <summary>
    /// Component on thrown objects tracking their state.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct ThrownObject : IComponentData
    {
        /// <summary>
        /// The type of throwable this is.
        /// </summary>
        [GhostField]
        public ThrowableType Type;

        /// <summary>
        /// Seconds until this object despawns.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float RemainingLifetime;

        /// <summary>
        /// Initial lifetime for percentage calculations.
        /// </summary>
        public float InitialLifetime;

        /// <summary>
        /// Current intensity (brightness for flares, volume for lures).
        /// Decreases near end of lifetime.
        /// </summary>
        public float Intensity;

        /// <summary>
        /// The player entity that threw this object.
        /// </summary>
        [GhostField]
        public Entity ThrowerEntity;

        /// <summary>
        /// Whether the object has landed (stopped moving).
        /// </summary>
        [GhostField]
        public bool HasLanded;
    }

    /// <summary>
    /// Component enabling creature attraction for thrown objects.
    /// AI systems query this to modify creature behavior.
    /// </summary>
    public struct AttractsCreatures : IComponentData
    {
        /// <summary>
        /// What type of attraction this emits (Heat, Sound, Visual).
        /// </summary>
        public AttractionType AttractionType;

        /// <summary>
        /// Radius within which creatures are attracted (meters).
        /// </summary>
        public float Radius;

        /// <summary>
        /// Priority level for attraction. Higher priority overrides lower.
        /// Players might be priority 5, flares priority 10.
        /// </summary>
        public int Priority;

        /// <summary>
        /// Whether this attraction is currently active.
        /// </summary>
        public bool IsActive;
    }

    /// <summary>
    /// Component for throwables that emit light (flares, glowsticks).
    /// </summary>
    public struct EmitsLight : IComponentData
    {
        /// <summary>
        /// Light color (RGB, 0-1 range).
        /// </summary>
        public float3 Color;

        /// <summary>
        /// Current light intensity (0-1).
        /// </summary>
        public float Intensity;

        /// <summary>
        /// Maximum light intensity.
        /// </summary>
        public float MaxIntensity;

        /// <summary>
        /// Light range in meters.
        /// </summary>
        public float Range;

        /// <summary>
        /// Whether the light should flicker when near end of lifetime.
        /// </summary>
        public bool FlickerAtEnd;

        /// <summary>
        /// Reference to the light entity (for presentation layer).
        /// </summary>
        public Entity LightEntity;
    }

    /// <summary>
    /// Component for throwables that emit sound (lures).
    /// </summary>
    public struct EmitsSound : IComponentData
    {
        /// <summary>
        /// Type of sound pattern to emit.
        /// </summary>
        public LureSoundType SoundType;

        /// <summary>
        /// Volume/audible range of the sound.
        /// </summary>
        public float Volume;

        /// <summary>
        /// Whether the sound is currently playing.
        /// </summary>
        public bool IsPlaying;
    }

    /// <summary>
    /// Component tracking the selected throwable type for quick-throw.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct SelectedThrowable : IComponentData
    {
        /// <summary>
        /// Currently selected throwable type for quick-throw.
        /// </summary>
        [GhostField]
        public ThrowableType Type;
    }

    /// <summary>
    /// Settings for throwable physics and behavior.
    /// </summary>
    public struct ThrowableSettings : IComponentData
    {
        /// <summary>
        /// Base throw speed in m/s.
        /// </summary>
        public float ThrowSpeed;

        /// <summary>
        /// Upward arc angle in degrees.
        /// </summary>
        public float ArcAngle;

        /// <summary>
        /// Default lifetime in seconds.
        /// </summary>
        public float DefaultLifetime;

        /// <summary>
        /// Default settings for throwables.
        /// </summary>
        public static ThrowableSettings Default => new()
        {
            ThrowSpeed = 15f,
            ArcAngle = 15f,
            DefaultLifetime = 30f
        };
    }
}
