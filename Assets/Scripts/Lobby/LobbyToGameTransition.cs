using System;
using System.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;

namespace DIG.Lobby
{
    /// <summary>
    /// EPIC 17.4: Orchestrates lobby → game world creation.
    /// Creates ECS worlds, injects LobbySpawnData singleton, triggers subscene load,
    /// and establishes network connections (with optional Relay).
    /// </summary>
    public static class LobbyToGameTransition
    {
        public static event Action<string, float> OnProgressUpdated; // phase, progress 0-1
        public static event Action<string> OnTransitionError;

        /// <summary>
        /// Begin the transition from lobby to ECS game worlds.
        /// Called by LobbyManager after StartGame.
        /// </summary>
        public static void BeginTransition(LobbyState lobbyState, bool isHost, string relayJoinCode)
        {
            if (lobbyState == null)
            {
                OnTransitionError?.Invoke("No lobby state for transition.");
                return;
            }

            // Create a helper MonoBehaviour to run the coroutine (survives scene loads)
            var go = new GameObject("LobbyTransitionHelper");
            UnityEngine.Object.DontDestroyOnLoad(go);
            var helper = go.AddComponent<TransitionCoroutineRunner>();
            helper.StartCoroutine(TransitionCoroutine(lobbyState, isHost, relayJoinCode, helper));
        }

        private static IEnumerator TransitionCoroutine(LobbyState lobbyState, bool isHost, string relayJoinCode, MonoBehaviour runner)
        {
            OnProgressUpdated?.Invoke("Shutting down lobby transport...", 0.1f);
            yield return null;

            // Shutdown lobby transport (LobbyManager handles this)
            var lobbyManager = LobbyManager.Instance;
            lobbyManager?.Transport?.Shutdown();

            OnProgressUpdated?.Invoke("Creating game worlds...", 0.2f);
            yield return null;

            try
            {
                if (isHost)
                {
                    // Use existing GameBootstrap pattern
                    GameBootstrap.CreateHost();
                }
                else
                {
                    GameBootstrap.CreateClient();
                }
            }
            catch (Exception e)
            {
                OnTransitionError?.Invoke($"World creation failed: {e.Message}");
                UnityEngine.Object.Destroy(runner.gameObject);
                yield break;
            }

            OnProgressUpdated?.Invoke("Loading subscenes...", 0.4f);

            // Wait for worlds to be ready
            float timeout = 15f;
            float elapsed = 0f;
            float lastProgressTime = 0f;
            while (!GameBootstrap.HasInitialized && elapsed < timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                if (elapsed - lastProgressTime >= 0.25f)
                {
                    OnProgressUpdated?.Invoke("Loading subscenes...", 0.4f + (elapsed / timeout) * 0.3f);
                    lastProgressTime = elapsed;
                }
                yield return null;
            }

            if (!GameBootstrap.HasInitialized)
            {
                OnTransitionError?.Invoke("Timed out waiting for game worlds to initialize.");
                UnityEngine.Object.Destroy(runner.gameObject);
                yield break;
            }

            OnProgressUpdated?.Invoke("Injecting spawn data...", 0.75f);
            yield return null;

            // Inject LobbySpawnData into ServerWorld (host only)
            if (isHost)
            {
                InjectSpawnData(lobbyState);
            }

            OnProgressUpdated?.Invoke("Ready!", 1.0f);
            yield return null;

            UnityEngine.Object.Destroy(runner.gameObject);
        }

        /// <summary>
        /// Creates the LobbySpawnData singleton entity in ServerWorld.
        /// </summary>
        private static void InjectSpawnData(LobbyState lobbyState)
        {
            World serverWorld = null;
            foreach (var world in World.All)
            {
                if (world.Name == "ServerWorld")
                {
                    serverWorld = world;
                    break;
                }
            }

            if (serverWorld == null)
            {
                Debug.LogWarning("[LobbyTransition] No ServerWorld found — skipping spawn data injection.");
                return;
            }

            var spawnData = new LobbySpawnData();

            // Load map definition to get spawn positions (cached O(1) lookup)
            var selectedMap = FindMapById(lobbyState.MapId);

            // Populate spawn positions
            if (selectedMap != null && selectedMap.SpawnPositions != null)
            {
                int posCount = math.min(selectedMap.SpawnPositions.Length, lobbyState.MaxPlayers);
                for (int i = 0; i < posCount; i++)
                {
                    var pos = selectedMap.SpawnPositions[i];
                    spawnData.SpawnPositions.Add(new float3(pos.x, pos.y, pos.z));

                    if (selectedMap.SpawnRotations != null && i < selectedMap.SpawnRotations.Length)
                    {
                        var rot = selectedMap.SpawnRotations[i];
                        spawnData.SpawnRotations.Add(new quaternion(rot.x, rot.y, rot.z, rot.w));
                    }
                    else
                    {
                        spawnData.SpawnRotations.Add(quaternion.identity);
                    }
                }
            }
            else
            {
                // Fallback: default spread positions
                for (int i = 0; i < lobbyState.MaxPlayers; i++)
                {
                    spawnData.SpawnPositions.Add(new float3(i * 2, 1, 0));
                    spawnData.SpawnRotations.Add(quaternion.identity);
                }
            }

            // Slot assignment: players claim slots in connection order via NextSlotIndex
            spawnData.PlayerCount = lobbyState.PlayerCount;
            spawnData.NextSlotIndex = 0;
            spawnData.SpawnedCount = 0;

            // Populate persistent IDs indexed by slot
            for (int i = 0; i < lobbyState.Players.Count; i++)
            {
                var slot = lobbyState.Players[i];
                if (slot.IsEmpty) continue;

                spawnData.AddPersistentId(slot.SlotIndex, slot.PlayerId);
            }

            // Create singleton entity
            var entity = serverWorld.EntityManager.CreateEntity();
            serverWorld.EntityManager.AddComponentData(entity, spawnData);

            Debug.Log($"[LobbyTransition] Injected LobbySpawnData with {spawnData.PlayerCount} players, {spawnData.SpawnPositions.Length} spawn positions");
        }

        private static System.Collections.Generic.Dictionary<int, MapDefinitionSO> _mapCache;

        internal static void ResetStaticState()
        {
            OnProgressUpdated = null;
            OnTransitionError = null;
            _mapCache = null;
        }

        private static MapDefinitionSO FindMapById(int mapId)
        {
            if (_mapCache == null)
            {
                _mapCache = new System.Collections.Generic.Dictionary<int, MapDefinitionSO>();
                var maps = Resources.LoadAll<MapDefinitionSO>("");
                for (int i = 0; i < maps.Length; i++)
                    _mapCache[maps[i].MapId] = maps[i];
            }
            _mapCache.TryGetValue(mapId, out var result);
            return result;
        }

        /// <summary>
        /// Return to lobby: destroy ECS worlds and prepare for lobby re-init.
        /// </summary>
        public static void ReturnToLobby()
        {
            // Destroy NetCode worlds
            var worldsToDestroy = new System.Collections.Generic.List<World>();
            foreach (var world in World.All)
            {
                if (world.Name == "ServerWorld" || world.Name == "ClientWorld")
                    worldsToDestroy.Add(world);
            }

            for (int i = 0; i < worldsToDestroy.Count; i++)
            {
                Debug.Log($"[LobbyTransition] Disposing {worldsToDestroy[i].Name}");
                worldsToDestroy[i].Dispose();
            }

            // EPIC 18.6: Reset bootstrap flag so new worlds can be created
            GameBootstrap.ResetInitialized();

            Debug.Log("[LobbyTransition] Returned to lobby — ECS worlds destroyed");
        }
    }

    /// <summary>Helper MonoBehaviour for running transition coroutine.</summary>
    internal class TransitionCoroutineRunner : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            LobbyToGameTransition.ResetStaticState();
        }
    }
}
