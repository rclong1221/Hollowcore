using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace DIG.Crafting
{
    /// <summary>
    /// EPIC 16.13: Links player entities to their CraftingKnowledge child entities at runtime.
    /// Follows TargetingModuleLinkSystem pattern:
    /// finds unlinked CraftingKnowledgeTag entities, searches LinkedEntityGroup to find parent player.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct CraftingKnowledgeLinkSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CraftingKnowledgeTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (_, knowledgeEntity) in
                SystemAPI.Query<RefRO<CraftingKnowledgeTag>>().WithEntityAccess())
            {
                // Find parent player via LinkedEntityGroup
                Entity playerEntity = FindParentWithLink(ref state, knowledgeEntity);
                if (playerEntity == Entity.Null) continue;

                // Check if already linked
                var link = SystemAPI.GetComponentRW<CraftingKnowledgeLink>(playerEntity);
                if (link.ValueRO.KnowledgeEntity != Entity.Null) continue;

                // Establish link
                link.ValueRW.KnowledgeEntity = knowledgeEntity;
            }
        }

        private Entity FindParentWithLink(ref SystemState state, Entity childEntity)
        {
            foreach (var (link, linkedGroup, playerEntity) in
                SystemAPI.Query<RefRO<CraftingKnowledgeLink>, DynamicBuffer<LinkedEntityGroup>>()
                    .WithEntityAccess())
            {
                if (link.ValueRO.KnowledgeEntity != Entity.Null) continue;

                for (int i = 0; i < linkedGroup.Length; i++)
                {
                    if (linkedGroup[i].Value == childEntity)
                        return playerEntity;
                }
            }
            return Entity.Null;
        }
    }
}
