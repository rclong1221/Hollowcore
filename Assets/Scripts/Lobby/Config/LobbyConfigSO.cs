using UnityEngine;

namespace DIG.Lobby
{
    /// <summary>
    /// EPIC 17.4: Designer-facing lobby configuration.
    /// Place at Resources/LobbyConfig for LobbyManager to load.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Lobby/Lobby Config")]
    public class LobbyConfigSO : ScriptableObject
    {
        [Header("Party Size")]
        [Range(2, 6)] public int MaxPlayersPerLobby = 4;

        [Header("Heartbeat")]
        [Tooltip("Milliseconds between heartbeat sends.")]
        public int HeartbeatIntervalMs = 2000;

        [Tooltip("Milliseconds before a player is considered disconnected.")]
        public int HeartbeatTimeoutMs = 8000;

        [Header("Timeouts")]
        [Tooltip("Minutes before an idle lobby is auto-closed.")]
        public int LobbyTimeoutMinutes = 30;

        [Tooltip("Seconds for game start transition before timeout error.")]
        public float TransitionTimeoutSeconds = 15f;

        [Tooltip("Seconds to wait during QuickMatch before giving up.")]
        public float QuickMatchTimeoutSeconds = 10f;

        [Header("Join Code")]
        [Range(4, 8)] public int JoinCodeLength = 6;

        [Header("Discovery")]
        [Tooltip("Milliseconds between lobby list refresh queries.")]
        public int DiscoveryRefreshIntervalMs = 3000;

        [Range(10, 100)] public int DiscoveryMaxResults = 50;

        [Header("Game Start")]
        [Tooltip("Minimum players needed to start the game.")]
        [Range(1, 6)] public int MinPlayersToStart = 1;

        [Header("Relay")]
        [Tooltip("Unity Relay region (empty = auto).")]
        public string RelayRegion = "";
    }
}
