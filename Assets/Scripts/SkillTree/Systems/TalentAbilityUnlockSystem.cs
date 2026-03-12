using Unity.Entities;

namespace DIG.SkillTree
{
    /// <summary>
    /// EPIC 17.1: Syncs ability unlocks from talent allocations.
    /// For each ActiveAbility node in TalentAllocation, ensures the matching
    /// AbilityDefinition is active in the player's ability buffer.
    /// Runs after TalentPassiveSystem so allocation state is current.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TalentPassiveSystem))]
    public partial class TalentAbilityUnlockSystem : SystemBase
    {
        private ComponentLookup<TalentOwner> _ownerLookup;
        private BufferLookup<DIG.Player.Abilities.AbilityDefinition> _abilityLookup;

        protected override void OnCreate()
        {
            RequireForUpdate<SkillTreeRegistrySingleton>();
            _ownerLookup = GetComponentLookup<TalentOwner>(true);
            _abilityLookup = GetBufferLookup<DIG.Player.Abilities.AbilityDefinition>(false);
        }

        protected override void OnUpdate()
        {
            _ownerLookup.Update(this);
            _abilityLookup.Update(this);

            var registry = SystemAPI.GetSingleton<SkillTreeRegistrySingleton>();
            ref var blob = ref registry.Registry.Value;

            foreach (var (allocations, owner) in
                SystemAPI.Query<DynamicBuffer<TalentAllocation>, RefRO<TalentOwner>>()
                    .WithAll<TalentChildTag>())
            {
                var playerEntity = owner.ValueRO.Owner;
                if (playerEntity == Entity.Null) continue;
                if (!_abilityLookup.HasBuffer(playerEntity)) continue;

                var abilities = _abilityLookup[playerEntity];

                // Collect all ability type IDs that should be unlocked
                for (int a = 0; a < allocations.Length; a++)
                {
                    int abilityTypeId = GetAbilityTypeId(ref blob, allocations[a].TreeId, allocations[a].NodeId);
                    if (abilityTypeId <= 0) continue;

                    // Check if ability already exists in buffer
                    bool found = false;
                    for (int b = 0; b < abilities.Length; b++)
                    {
                        if (abilities[b].AbilityTypeId == abilityTypeId)
                        {
                            // Ensure it's active
                            if (!abilities[b].IsActive)
                            {
                                var def = abilities[b];
                                def.IsActive = true;
                                abilities[b] = def;
                            }
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        abilities.Add(new DIG.Player.Abilities.AbilityDefinition
                        {
                            AbilityTypeId = abilityTypeId,
                            IsActive = true,
                            Priority = 100,
                            CanStart = true,
                            CanStop = true
                        });
                    }
                }
            }
        }

        private static int GetAbilityTypeId(ref SkillTreeRegistryBlob blob, ushort treeId, ushort nodeId)
        {
            for (int t = 0; t < blob.Trees.Length; t++)
            {
                if (blob.Trees[t].TreeId != treeId) continue;
                for (int n = 0; n < blob.Trees[t].Nodes.Length; n++)
                {
                    if (blob.Trees[t].Nodes[n].NodeId != nodeId) continue;
                    if (blob.Trees[t].Nodes[n].NodeType != SkillNodeType.ActiveAbility) return 0;
                    return blob.Trees[t].Nodes[n].AbilityTypeId;
                }
                return 0;
            }
            return 0;
        }
    }
}
