using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Audio.Systems
{
    /// <summary>
    /// Compact buffer element used to serialize/playback footstep audio across network.
    /// Kept intentionally small: material id, position, intensity, stance, foot index.
    /// </summary>
    public struct NetworkedAudioBufferElement : IBufferElementData
    {
        public int MaterialId;
        public float3 Position;
        public float Intensity;
        public int Stance;
        public int FootIndex;
    }

    public struct NetworkedAudioSenderTag : IComponentData { }

    /// <summary>
    /// Managed helper for MonoBehaviour publishers (AnimatorEventBridge) to append compact
    /// audio events into the singleton transmitter buffer that `NetworkedAudioSystem` or
    /// a netcode adapter can consume.
    /// </summary>
    public static class NetworkedAudioPublisher
    {
        public static void Publish(int materialId, Vector3 worldPos, int stance = 0, int footIndex = 0, float intensity = 1f)
        {
            // Try to resolve a default world/entity manager
            var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            var em = world.EntityManager;

            // Ensure singleton transmitter exists
            Entity transmitter = Entity.Null;
            var senderQuery = em.CreateEntityQuery(ComponentType.ReadOnly<NetworkedAudioSenderTag>());
            if (senderQuery.IsEmptyIgnoreFilter)
            {
                transmitter = em.CreateEntity();
                em.AddComponent<NetworkedAudioSenderTag>(transmitter);
                em.AddBuffer<NetworkedAudioBufferElement>(transmitter);
            }
            transmitter = GetSingletonSafe(em);

            if (transmitter == Entity.Null)
            {
                // Fallback: try to get any entity with the tag
                using (var ents = senderQuery.ToEntityArray(Unity.Collections.Allocator.Temp))
                {
                    if (ents.Length > 0) transmitter = ents[0];
                }
            }

            if (transmitter == Entity.Null) return;

            var buf = em.GetBuffer<NetworkedAudioBufferElement>(transmitter);
            buf.Add(new NetworkedAudioBufferElement
            {
                MaterialId = materialId,
                Position = new float3(worldPos.x, worldPos.y, worldPos.z),
                Intensity = intensity,
                Stance = stance,
                FootIndex = footIndex
            });
        }

        static Entity GetSingletonSafe(EntityManager em)
        {
            try
            {
                return em.CreateEntityQuery(ComponentType.ReadOnly<NetworkedAudioSenderTag>()).GetSingletonEntity();
            }
            catch
            {
                return Entity.Null;
            }
        }
    }
}
