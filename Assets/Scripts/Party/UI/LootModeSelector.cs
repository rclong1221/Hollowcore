using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace DIG.Party
{
    /// <summary>
    /// EPIC 17.2: MonoBehaviour for loot mode dropdown (leader only).
    /// Sends PartyRpc(SetLootMode) when selection changes.
    /// </summary>
    public class LootModeSelector : MonoBehaviour
    {
        [SerializeField] private GameObject _selectorPanel;

        private bool _isLeader;

        public void SetLeader(bool isLeader)
        {
            _isLeader = isLeader;
            if (_selectorPanel != null)
                _selectorPanel.SetActive(isLeader);
        }

        public void OnLootModeSelected(int modeIndex)
        {
            if (!_isLeader) return;
            if (modeIndex < 0 || modeIndex > (int)LootMode.MasterLoot) return;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;

            var em = world.EntityManager;
            var rpcEntity = em.CreateEntity();
            em.AddComponentData(rpcEntity, new PartyRpc
            {
                Type = PartyRpcType.SetLootMode,
                TargetPlayer = Entity.Null,
                Payload = (byte)modeIndex
            });
            em.AddComponent<SendRpcCommandRequest>(rpcEntity);
        }
    }
}
