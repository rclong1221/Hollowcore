using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace DIG.Quest
{
    /// <summary>
    /// EPIC 16.12: Place on NPC alongside InteractableAuthoring with InteractionVerb.Talk.
    /// Baker builds a BlobArray of quest IDs this NPC can offer.
    /// </summary>
    [AddComponentMenu("DIG/Quest/Quest Giver")]
    public class QuestGiverAuthoring : MonoBehaviour
    {
        [Tooltip("Quest definitions this NPC can offer")]
        public List<QuestDefinitionSO> AvailableQuests = new();
    }

    public class QuestGiverBaker : Baker<QuestGiverAuthoring>
    {
        public override void Bake(QuestGiverAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            var builder = new BlobBuilder(Unity.Collections.Allocator.Temp);
            ref var root = ref builder.ConstructRoot<QuestGiverBlob>();

            var questIds = builder.Allocate(ref root.QuestIds, authoring.AvailableQuests.Count);
            for (int i = 0; i < authoring.AvailableQuests.Count; i++)
            {
                var quest = authoring.AvailableQuests[i];
                questIds[i] = quest != null ? quest.QuestId : 0;
            }

            var blobRef = builder.CreateBlobAssetReference<QuestGiverBlob>(Unity.Collections.Allocator.Persistent);
            builder.Dispose();

            AddBlobAsset(ref blobRef, out _);

            AddComponent(entity, new QuestGiverData
            {
                AvailableQuests = blobRef
            });
        }
    }
}
