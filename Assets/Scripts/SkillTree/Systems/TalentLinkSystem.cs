using Unity.Entities;

namespace DIG.SkillTree
{
    /// <summary>
    /// EPIC 17.1: Wires TalentLink on player entity to TalentChildTag child entity.
    /// Uses LinkedEntityGroup (populated by baker) to find parent.
    /// Runs in InitializationSystemGroup so links are ready before simulation.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation |
                       WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class TalentLinkSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<TalentChildTag>();
        }

        protected override void OnUpdate()
        {
            bool anyUnlinked = false;

            foreach (var (_, childEntity) in
                SystemAPI.Query<RefRO<TalentChildTag>>().WithEntityAccess())
            {
                Entity parentEntity = FindParentWithTalentLink(childEntity);
                if (parentEntity == Entity.Null) continue;

                var link = SystemAPI.GetComponentRW<TalentLink>(parentEntity);
                if (link.ValueRO.TalentChild != Entity.Null) continue;

                link.ValueRW.TalentChild = childEntity;
                SystemAPI.SetComponent(childEntity, new TalentOwner { Owner = parentEntity });
                anyUnlinked = true;
            }

            if (!anyUnlinked)
                Enabled = false;
        }

        private Entity FindParentWithTalentLink(Entity childEntity)
        {
            foreach (var (link, linkedGroup, playerEntity) in
                SystemAPI.Query<RefRO<TalentLink>, DynamicBuffer<LinkedEntityGroup>>()
                    .WithEntityAccess())
            {
                if (link.ValueRO.TalentChild != Entity.Null) continue;

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
