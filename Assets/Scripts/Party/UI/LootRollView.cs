using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace DIG.Party
{
    /// <summary>
    /// EPIC 17.2: MonoBehaviour for NeedGreed voting UI.
    /// Shows item icon + Need/Greed/Pass buttons + timer bar.
    /// </summary>
    public class LootRollView : MonoBehaviour
    {
        [SerializeField] private GameObject _rollPanel;

        private LootRollUIState _currentRoll;

        public void ShowRoll(LootRollUIState roll)
        {
            _currentRoll = roll;
            if (_rollPanel != null)
                _rollPanel.SetActive(true);
        }

        public void Hide()
        {
            if (_rollPanel != null)
                _rollPanel.SetActive(false);
        }

        public void OnNeedClicked() => SendVote(LootVoteType.Need);
        public void OnGreedClicked() => SendVote(LootVoteType.Greed);
        public void OnPassClicked() => SendVote(LootVoteType.Pass);

        private void SendVote(LootVoteType vote)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;

            var em = world.EntityManager;
            var rpcEntity = em.CreateEntity();
            em.AddComponentData(rpcEntity, new PartyRpc
            {
                Type = PartyRpcType.LootVote,
                TargetPlayer = Entity.Null,
                Payload = (byte)vote
            });
            em.AddComponent<SendRpcCommandRequest>(rpcEntity);

            Hide();
        }
    }
}
