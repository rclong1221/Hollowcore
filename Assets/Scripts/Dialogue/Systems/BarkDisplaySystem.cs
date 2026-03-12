using Unity.Collections;
using Unity.Entities;

namespace DIG.Dialogue
{
    /// <summary>
    /// EPIC 16.16: Processes BarkRequest transient entities — pushes bark text
    /// to DialogueUIRegistry for world-space text bubble display, and optionally
    /// creates SoundEventRequest for voice audio. Destroys requests after processing.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class BarkDisplaySystem : SystemBase
    {
        private EntityQuery _barkQuery;

        protected override void OnCreate()
        {
            _barkQuery = GetEntityQuery(ComponentType.ReadOnly<BarkRequest>());
        }

        protected override void OnUpdate()
        {
            if (_barkQuery.IsEmptyIgnoreFilter) return;
            if (!SystemAPI.ManagedAPI.TryGetSingleton<DialogueRegistryManaged>(out var registry)) return;

            var entities = _barkQuery.ToEntityArray(Allocator.Temp);
            var requests = _barkQuery.ToComponentDataArray<BarkRequest>(Allocator.Temp);

            for (int i = 0; i < requests.Length; i++)
            {
                var req = requests[i];

                if (!EntityManager.HasComponent<BarkEmitter>(req.EmitterEntity))
                {
                    EntityManager.DestroyEntity(entities[i]);
                    continue;
                }

                var emitter = EntityManager.GetComponentData<BarkEmitter>(req.EmitterEntity);
                var barkCollection = registry.GetBarkCollection(emitter.BarkCollectionId);

                if (barkCollection == null || barkCollection.Lines.Length == 0)
                {
                    EntityManager.DestroyEntity(entities[i]);
                    continue;
                }

                // Select line (random weighted if LineIndex == -1)
                int lineIndex = req.LineIndex;
                if (lineIndex < 0 || lineIndex >= barkCollection.Lines.Length)
                    lineIndex = PickWeightedLine(barkCollection);

                if (lineIndex < 0 || lineIndex >= barkCollection.Lines.Length)
                {
                    EntityManager.DestroyEntity(entities[i]);
                    continue;
                }

                var line = barkCollection.Lines[lineIndex];

                // Push to UI
                if (DialogueUIRegistry.HasBark)
                {
                    DialogueUIRegistry.Bark.ShowBark(
                        DialogueLocalization.Resolve(line.Text),
                        req.Position,
                        barkCollection.MaxRange);
                }

                // Audio via SoundEventRequest
                if (!string.IsNullOrEmpty(line.AudioClipPath) &&
                    EntityManager.HasComponent<Unity.Transforms.LocalToWorld>(req.EmitterEntity))
                {
                    var ltw = EntityManager.GetComponentData<Unity.Transforms.LocalToWorld>(req.EmitterEntity);
                    var soundEntity = EntityManager.CreateEntity();
                    EntityManager.AddComponentData(soundEntity, new DIG.Aggro.Components.SoundEventRequest
                    {
                        Position = ltw.Position,
                        SourceEntity = req.EmitterEntity,
                        Loudness = 0.3f,
                        MaxRange = barkCollection.MaxRange,
                        Category = DIG.Aggro.Components.SoundCategory.Environmental
                    });
                }

                EntityManager.DestroyEntity(entities[i]);
            }

            entities.Dispose();
            requests.Dispose();
        }

        private int PickWeightedLine(BarkCollectionSO collection)
        {
            float totalWeight = 0f;
            for (int i = 0; i < collection.Lines.Length; i++)
                totalWeight += collection.Lines[i].Weight;

            if (totalWeight <= 0f) return 0;

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float cumulative = 0f;
            for (int i = 0; i < collection.Lines.Length; i++)
            {
                cumulative += collection.Lines[i].Weight;
                if (roll <= cumulative) return i;
            }
            return collection.Lines.Length - 1;
        }
    }
}
