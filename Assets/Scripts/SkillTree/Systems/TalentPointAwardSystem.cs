using Unity.Entities;
using Unity.NetCode;

namespace DIG.SkillTree
{
    /// <summary>
    /// EPIC 17.1: Grants talent points when a player levels up.
    /// Reads LevelUpEvent (IEnableableComponent), awards TalentPointsPerLevel from registry blob.
    /// Runs after LevelRewardSystem which handles the LevelRewardType.TalentPoint case.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DIG.Progression.LevelRewardSystem))]
    public partial class TalentPointAwardSystem : SystemBase
    {
        private ComponentLookup<TalentLink> _talentLinkLookup;
        private ComponentLookup<TalentState> _talentStateLookup;

        protected override void OnCreate()
        {
            RequireForUpdate<SkillTreeRegistrySingleton>();
            _talentLinkLookup = GetComponentLookup<TalentLink>(true);
            _talentStateLookup = GetComponentLookup<TalentState>(false);
        }

        protected override void OnUpdate()
        {
            _talentLinkLookup.Update(this);
            _talentStateLookup.Update(this);

            var registry = SystemAPI.GetSingleton<SkillTreeRegistrySingleton>();
            ref var blob = ref registry.Registry.Value;
            int pointsPerLevel = blob.TalentPointsPerLevel;

            if (pointsPerLevel <= 0) return;

            foreach (var (levelUp, entity) in
                SystemAPI.Query<RefRO<DIG.Progression.LevelUpEvent>>()
                    .WithEntityAccess())
            {
                if (!EntityManager.IsComponentEnabled<DIG.Progression.LevelUpEvent>(entity))
                    continue;

                if (!_talentLinkLookup.HasComponent(entity)) continue;
                var link = _talentLinkLookup[entity];
                if (link.TalentChild == Entity.Null) continue;
                if (!_talentStateLookup.HasComponent(link.TalentChild)) continue;

                int levelsGained = levelUp.ValueRO.NewLevel - levelUp.ValueRO.PreviousLevel;
                if (levelsGained <= 0) continue;

                var state = _talentStateLookup[link.TalentChild];
                state.TotalTalentPoints += pointsPerLevel * levelsGained;
                _talentStateLookup[link.TalentChild] = state;
            }
        }
    }
}
