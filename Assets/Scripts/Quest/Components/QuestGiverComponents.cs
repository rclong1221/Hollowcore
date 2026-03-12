using Unity.Entities;

namespace DIG.Quest
{
    /// <summary>
    /// EPIC 16.12: Placed on NPC entities alongside InteractableAuthoring with InteractionVerb.Talk.
    /// Baker builds a BlobArray of quest IDs this NPC can offer.
    /// </summary>
    public struct QuestGiverData : IComponentData
    {
        public BlobAssetReference<QuestGiverBlob> AvailableQuests;
    }

    /// <summary>
    /// EPIC 16.12: BlobAsset holding the list of quest IDs a giver can offer.
    /// </summary>
    public struct QuestGiverBlob
    {
        public BlobArray<int> QuestIds;
    }
}
