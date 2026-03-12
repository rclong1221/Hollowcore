using Unity.Entities;
using UnityEngine;

namespace DIG.Party
{
    /// <summary>
    /// EPIC 17.2: MonoBehaviour implementing IPartyUIProvider.
    /// Shows 2-5 party member frames with health, mana, level, leader crown,
    /// out-of-range dimming, and dead state graying.
    /// Designers wire up UI elements in Inspector.
    /// </summary>
    public class PartyFrameView : MonoBehaviour, IPartyUIProvider
    {
        [Header("Party Frame Container")]
        [Tooltip("Parent transform for party member frame slots.")]
        [SerializeField] private Transform _frameContainer;

        [Header("Display")]
        [Tooltip("Show party frames when in a party.")]
        [SerializeField] private GameObject _rootPanel;

        private PartyUIState _lastState;

        private void OnEnable()
        {
            PartyUIRegistry.RegisterPartyUI(this);
        }

        private void OnDisable()
        {
            PartyUIRegistry.UnregisterPartyUI(this);
        }

        public void UpdatePartyState(PartyUIState state)
        {
            _lastState = state;

            if (_rootPanel != null)
                _rootPanel.SetActive(state.InParty);

            if (!state.InParty || state.Members == null) return;

            // Designers implement specific frame rendering here.
            // Each member slot shows: name, health bar, mana bar, level, leader icon, range dimming, alive state.
        }

        public void OnMemberJoined(PartyMemberUIState member)
        {
            Debug.Log($"[PartyUI] Member joined: Entity {member.PlayerEntity.Index}");
        }

        public void OnMemberLeft(Entity memberEntity)
        {
            Debug.Log($"[PartyUI] Member left: Entity {memberEntity.Index}");
        }

        public void OnInviteReceived(PartyInviteUIState invite)
        {
            Debug.Log($"[PartyUI] Invite received from Entity {invite.InviterEntity.Index}");
        }

        public void OnInviteExpired()
        {
            Debug.Log("[PartyUI] Invite expired");
        }

        public void OnLootRollStart(LootRollUIState roll)
        {
            Debug.Log("[PartyUI] Loot roll started");
        }

        public void OnLootRollResult(LootRollResultUIState result)
        {
            Debug.Log($"[PartyUI] Loot roll result: winner Entity {result.WinnerEntity.Index}, vote {result.WinningVote}");
        }

        public void OnPartyDisbanded()
        {
            Debug.Log("[PartyUI] Party disbanded");
            if (_rootPanel != null)
                _rootPanel.SetActive(false);
        }

        public void OnLootModeChanged(LootMode newMode)
        {
            Debug.Log($"[PartyUI] Loot mode changed to {newMode}");
        }

        public void OnLeaderChanged(Entity newLeader)
        {
            Debug.Log($"[PartyUI] Leader changed to Entity {newLeader.Index}");
        }
    }
}
