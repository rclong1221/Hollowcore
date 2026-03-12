using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace DIG.Party
{
    /// <summary>
    /// EPIC 17.2: MonoBehaviour for party invite accept/decline popup.
    /// Sends PartyRpc(AcceptInvite) or PartyRpc(DeclineInvite) via NetCode.
    /// </summary>
    public class PartyInviteDialogView : MonoBehaviour
    {
        [SerializeField] private GameObject _dialogPanel;

        private PartyInviteUIState _pendingInvite;

        public void ShowInvite(PartyInviteUIState invite)
        {
            _pendingInvite = invite;
            if (_dialogPanel != null)
                _dialogPanel.SetActive(true);
        }

        public void Hide()
        {
            if (_dialogPanel != null)
                _dialogPanel.SetActive(false);
        }

        public void OnAcceptClicked()
        {
            SendPartyRpc(PartyRpcType.AcceptInvite);
            Hide();
        }

        public void OnDeclineClicked()
        {
            SendPartyRpc(PartyRpcType.DeclineInvite);
            Hide();
        }

        private void SendPartyRpc(PartyRpcType type)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;

            var em = world.EntityManager;
            var rpcEntity = em.CreateEntity();
            em.AddComponentData(rpcEntity, new PartyRpc
            {
                Type = type,
                TargetPlayer = Entity.Null,
                Payload = 0
            });
            em.AddComponent<SendRpcCommandRequest>(rpcEntity);
        }
    }
}
