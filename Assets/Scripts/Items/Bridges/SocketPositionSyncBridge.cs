using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DIG.Items.Authoring;
using DIG.Shared;

namespace DIG.Items.Bridges
{
    /// <summary>
    /// Syncs socket world positions from the animated skeleton to ECS.
    /// Place this on the character visual prefab (e.g., Atlas_Client) alongside WeaponEquipVisualBridge.
    /// </summary>
    /// <remarks>
    /// This bridge reads the Socket_MainHand and Socket_OffHand transforms each frame
    /// and writes their world positions to the SocketPositionData component on the player entity.
    /// This allows ECS systems (like throwable spawning) to know the actual hand positions.
    /// The hand position is then sent to the server via PlayerInput.MainHandPosition.
    /// </remarks>
    [RequireComponent(typeof(DIGEquipmentProvider))]
    public class SocketPositionSyncBridge : MonoBehaviour
    {
        private DIGEquipmentProvider _equipmentProvider;
        private Transform _mainHandSocket;
        private Transform _offHandSocket;
        private bool _socketsInitialized;

        private void Awake()
        {
            _equipmentProvider = GetComponent<DIGEquipmentProvider>();
        }

        private void Start()
        {
            InitializeSockets();
        }

        private void InitializeSockets()
        {
            if (_socketsInitialized) return;

            // Find sockets using SocketAuthoring components
            var sockets = GetComponentsInChildren<SocketAuthoring>(true);

            foreach (var socket in sockets)
            {
                switch (socket.Type)
                {
                    case SocketAuthoring.SocketType.MainHand:
                        _mainHandSocket = socket.transform;
                        break;
                    case SocketAuthoring.SocketType.OffHand:
                        _offHandSocket = socket.transform;
                        break;
                }
            }

            if (_mainHandSocket == null)
            {
                Debug.LogError($"[SocketPositionSyncBridge] MainHand socket NOT FOUND on {gameObject.name}. Check that Socket_MainHand has SocketAuthoring with Type=MainHand.");
            }

            _socketsInitialized = true;
        }

        private void LateUpdate()
        {
            // LateUpdate ensures we read positions after animation has been applied
            SyncSocketPositions();
        }

        private void SyncSocketPositions()
        {
            if (_equipmentProvider == null) return;

            var world = _equipmentProvider.EntityWorld;
            var playerEntity = _equipmentProvider.PlayerEntity;

            if (world == null || !world.IsCreated || playerEntity == Entity.Null)
                return;

            var em = world.EntityManager;
            if (!em.Exists(playerEntity))
                return;

            // Ensure SocketPositionData component exists
            if (!em.HasComponent<SocketPositionData>(playerEntity))
            {
                em.AddComponent<SocketPositionData>(playerEntity);
            }

            // Build and write socket position data
            em.SetComponentData(playerEntity, new SocketPositionData
            {
                MainHandPosition = _mainHandSocket != null
                    ? (float3)_mainHandSocket.position
                    : float3.zero,
                OffHandPosition = _offHandSocket != null
                    ? (float3)_offHandSocket.position
                    : float3.zero,
                IsValid = _mainHandSocket != null
            });
        }
    }
}
