using Unity.Entities;

namespace DIG.Party
{
    /// <summary>
    /// EPIC 17.2: Interface for party UI implementations.
    /// MonoBehaviours implement this and register via PartyUIRegistry.
    /// </summary>
    public interface IPartyUIProvider
    {
        void UpdatePartyState(PartyUIState state);
        void OnMemberJoined(PartyMemberUIState member);
        void OnMemberLeft(Entity memberEntity);
        void OnInviteReceived(PartyInviteUIState invite);
        void OnInviteExpired();
        void OnLootRollStart(LootRollUIState roll);
        void OnLootRollResult(LootRollResultUIState result);
        void OnPartyDisbanded();
        void OnLootModeChanged(LootMode newMode);
        void OnLeaderChanged(Entity newLeader);
    }
}
