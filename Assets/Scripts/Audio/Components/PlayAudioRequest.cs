using Unity.Entities;
using Unity.Mathematics;
using Audio.Config;

namespace Audio.Components
{
    /// <summary>
    /// Request to play a one-shot sound at a position or attached to an entity.
    /// Buffer on the AudioRequestSingleton entity. Consumed by AudioSourcePoolSystem each frame.
    /// EPIC 15.27 Phase 2.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct PlayAudioRequest : IBufferElementData
    {
        /// <summary>Clip ID (index into AudioClipBank). -1 for surface-resolved.</summary>
        public int ClipId;

        /// <summary>Surface material ID for surface-resolved clips. -1 if using ClipId directly.</summary>
        public int SurfaceMaterialId;

        /// <summary>World position to play at (if TargetEntity is Entity.Null).</summary>
        public float3 Position;

        /// <summary>Entity to attach the source to (Entity.Null for fire-and-forget at Position).</summary>
        public Entity TargetEntity;

        /// <summary>Audio bus routing.</summary>
        public AudioBusType Bus;

        /// <summary>Voice priority (higher = harder to cull).</summary>
        public byte Priority;

        /// <summary>Volume multiplier (0-1).</summary>
        public float Volume;

        /// <summary>Pitch multiplier (default 1.0).</summary>
        public float Pitch;

        /// <summary>Whether to loop this sound.</summary>
        public bool Loop;

        /// <summary>Max audible distance. 0 = use bus default.</summary>
        public float MaxDistance;

        /// <summary>Create a fire-and-forget request at a world position.</summary>
        public static PlayAudioRequest AtPosition(int clipId, float3 position, AudioBusType bus,
            byte priority = 50, float volume = 1f, float pitch = 1f)
        {
            return new PlayAudioRequest
            {
                ClipId = clipId,
                SurfaceMaterialId = -1,
                Position = position,
                TargetEntity = Entity.Null,
                Bus = bus,
                Priority = priority,
                Volume = volume,
                Pitch = pitch,
                Loop = false,
                MaxDistance = 0f
            };
        }

        /// <summary>Create a request attached to an entity (follows entity position).</summary>
        public static PlayAudioRequest OnEntity(int clipId, Entity entity, AudioBusType bus,
            byte priority = 100, float volume = 1f, float pitch = 1f, bool loop = false)
        {
            return new PlayAudioRequest
            {
                ClipId = clipId,
                SurfaceMaterialId = -1,
                Position = float3.zero,
                TargetEntity = entity,
                Bus = bus,
                Priority = priority,
                Volume = volume,
                Pitch = pitch,
                Loop = loop,
                MaxDistance = 0f
            };
        }
    }

    /// <summary>
    /// Singleton tag. The entity with this component holds the PlayAudioRequest buffer.
    /// Created by AudioSourcePoolSystem on startup.
    /// </summary>
    public struct AudioRequestSingleton : IComponentData { }
}
