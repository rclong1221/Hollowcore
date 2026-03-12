using Unity.Entities;
using Audio.Systems;
using Unity.Collections;
using Unity.Transforms;
using Unity.Mathematics;

// Collects FootstepEvent entities and appends them to a singleton buffer so a networking layer
// can read & transmit them. This is intentionally minimal scaffolding — networking serialization
// should be implemented separately by the NetCode adapter that consumes `NetworkedAudioBufferElement`.
public partial class NetworkedAudioSystem : SystemBase
{
    EntityQuery m_FootstepQuery;

    protected override void OnCreate()
    {
        m_FootstepQuery = GetEntityQuery(ComponentType.ReadOnly(typeof(FootstepEvent)), ComponentType.ReadOnly(typeof(LocalTransform)));
    }

    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var entityManager = EntityManager;

        // Ensure singleton transmitter entity exists and has a dynamic buffer
        Entity transmitter;

        var senderQuery = GetEntityQuery(ComponentType.ReadOnly<NetworkedAudioSenderTag>());
        if (senderQuery.IsEmptyIgnoreFilter)
        {
            transmitter = entityManager.CreateEntity();
            entityManager.AddComponent<NetworkedAudioSenderTag>(transmitter);
            entityManager.AddBuffer<NetworkedAudioBufferElement>(transmitter);
        }

        // Consume footstep events and append into buffer. Use an explicit main-thread loop
        // to avoid capturing managed types inside an Entities.ForEach lambda.
        var transmitterEntity = Entity.Null;
        try
        {
            transmitterEntity = SystemAPI.GetSingletonEntity<NetworkedAudioSenderTag>();
        }
        catch
        {
            // If there's no singleton yet, skip processing this frame — it will be created above.
            transmitterEntity = Entity.Null;
        }

        if (transmitterEntity != Entity.Null)
        {
            using (var entities = m_FootstepQuery.ToEntityArray(Unity.Collections.Allocator.Temp))
            {
                foreach (var e in entities)
                {
                    var fe = entityManager.GetComponentData<FootstepEvent>(e);
                    var xf = entityManager.GetComponentData<LocalTransform>(e);

                    var buf = entityManager.GetBuffer<NetworkedAudioBufferElement>(transmitterEntity);
                    buf.Add(new NetworkedAudioBufferElement
                    {
                        MaterialId = fe.MaterialId,
                        Position = fe.Position,
                        Intensity = 1.0f,
                        Stance = fe.Stance,
                        FootIndex = 0
                    });

                    ecb.RemoveComponent<FootstepEvent>(e);
                }
            }
        }

        ecb.Playback(entityManager);
        ecb.Dispose();
    }
}
