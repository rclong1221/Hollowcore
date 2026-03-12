using Unity.Entities;
using Unity.NetCode;
using DIG.Combat.Components;
using Player.Components;

namespace DIG.Party
{
    /// <summary>
    /// EPIC 17.2: Managed SystemBase that reads party state and pushes to PartyUIRegistry.
    /// Runs in PresentationSystemGroup, Client|Local only.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class PartyUIBridgeSystem : SystemBase
    {
        private int _noProviderFrameCount;
        private PartyMemberUIState[] _cachedMemberStates;
        private int _cachedMemberCount;

        protected override void OnCreate()
        {
            RequireForUpdate<PartyConfigSingleton>();
        }

        protected override void OnUpdate()
        {
            if (!PartyUIRegistry.HasPartyUI)
            {
                _noProviderFrameCount++;
                if (_noProviderFrameCount == 120)
                    UnityEngine.Debug.LogWarning("[PartyUIBridge] No IPartyUIProvider registered after 120 frames.");
                DrainVisualQueue();
                return;
            }

            var provider = PartyUIRegistry.PartyUI;

            // Process visual queue events
            while (PartyVisualQueue.TryDequeue(out var evt))
            {
                switch (evt.Type)
                {
                    case PartyNotifyType.MemberJoined:
                        provider.OnMemberJoined(new PartyMemberUIState { PlayerEntity = evt.SourcePlayer });
                        break;
                    case PartyNotifyType.MemberLeft:
                    case PartyNotifyType.MemberKicked:
                        provider.OnMemberLeft(evt.SourcePlayer);
                        break;
                    case PartyNotifyType.InviteReceived:
                        provider.OnInviteReceived(new PartyInviteUIState { InviterEntity = evt.SourcePlayer });
                        break;
                    case PartyNotifyType.InviteExpired:
                        provider.OnInviteExpired();
                        break;
                    case PartyNotifyType.PartyDisbanded:
                        provider.OnPartyDisbanded();
                        break;
                    case PartyNotifyType.LootModeChanged:
                        provider.OnLootModeChanged((LootMode)evt.Payload);
                        break;
                    case PartyNotifyType.LeaderChanged:
                        provider.OnLeaderChanged(evt.SourcePlayer);
                        break;
                    case PartyNotifyType.LootRollStart:
                        provider.OnLootRollStart(new LootRollUIState());
                        break;
                    case PartyNotifyType.LootRollResult:
                        provider.OnLootRollResult(new LootRollResultUIState
                        {
                            WinnerEntity = evt.SourcePlayer,
                            WinningVote = (LootVoteType)evt.Payload
                        });
                        break;
                }
            }

            // Find local player's party and push full state
            var localPlayerFound = false;
            foreach (var (link, entity) in SystemAPI.Query<RefRO<PartyLink>>()
                         .WithAll<GhostOwnerIsLocal>()
                         .WithEntityAccess())
            {
                localPlayerFound = true;
                var partyEntity = link.ValueRO.PartyEntity;

                if (partyEntity == Entity.Null || !EntityManager.HasComponent<PartyState>(partyEntity))
                {
                    provider.UpdatePartyState(new PartyUIState { InParty = false });
                    break;
                }

                var partyState = EntityManager.GetComponentData<PartyState>(partyEntity);
                var members = EntityManager.GetBuffer<PartyMemberElement>(partyEntity, true);

                var healthLookup = GetComponentLookup<Health>(true);
                var attrsLookup = GetComponentLookup<CharacterAttributes>(true);

                // Reuse cached array; only reallocate when member count changes
                if (_cachedMemberStates == null || _cachedMemberCount != members.Length)
                {
                    _cachedMemberStates = new PartyMemberUIState[members.Length];
                    _cachedMemberCount = members.Length;
                }
                for (int m = 0; m < members.Length; m++)
                {
                    var memberEntity = members[m].PlayerEntity;
                    var memberState = new PartyMemberUIState
                    {
                        PlayerEntity = memberEntity,
                        IsLeader = memberEntity == partyState.LeaderEntity,
                        IsInRange = true,
                        IsAlive = true
                    };

                    if (healthLookup.HasComponent(memberEntity))
                    {
                        var health = healthLookup[memberEntity];
                        memberState.HealthCurrent = health.Current;
                        memberState.HealthMax = health.Max;
                        memberState.IsAlive = health.Current > 0;
                    }

                    if (attrsLookup.HasComponent(memberEntity))
                        memberState.Level = attrsLookup[memberEntity].Level;

                    _cachedMemberStates[m] = memberState;
                }

                provider.UpdatePartyState(new PartyUIState
                {
                    InParty = true,
                    IsLeader = partyState.LeaderEntity == entity,
                    CurrentLootMode = partyState.LootMode,
                    MemberCount = partyState.MemberCount,
                    MaxSize = partyState.MaxSize,
                    Members = _cachedMemberStates
                });
                break;
            }

            if (!localPlayerFound)
                provider.UpdatePartyState(new PartyUIState { InParty = false });
        }

        private void DrainVisualQueue()
        {
            while (PartyVisualQueue.TryDequeue(out _)) { }
        }
    }
}
