using System;
using System.Collections.Generic;
using UnityEngine;

namespace DIG.Lobby
{
    /// <summary>
    /// EPIC 17.4: Summary info for a discovered lobby, displayed in the browser.
    /// </summary>
    [Serializable]
    public struct LobbyInfo
    {
        public string LobbyId;
        public string HostName;
        public string JoinCode;
        public int MapId;
        public int DifficultyId;
        public int CurrentPlayers;
        public int MaxPlayers;
        public int PingMs;
    }

    /// <summary>
    /// EPIC 17.4: Filter criteria for lobby browsing.
    /// </summary>
    [Serializable]
    public class LobbyFilter
    {
        public string SearchText;
        public int MapId = -1;       // -1 = any
        public int DifficultyId = -1; // -1 = any
        public bool ShowFull;
        public int MaxPing = 999;
    }

    /// <summary>
    /// EPIC 17.4: Handles public lobby browsing via broadcast discovery.
    /// Collects DiscoveryResponse entries into a filterable list.
    /// </summary>
    public class LobbyDiscoveryService
    {
        private readonly List<LobbyInfo> _lobbies = new List<LobbyInfo>();
        private readonly Dictionary<string, int> _lobbyIdToIndex = new Dictionary<string, int>();
        private readonly int _maxResults;
        private float _lastQueryTime;
        private float _refreshInterval;

        public IReadOnlyList<LobbyInfo> Lobbies => _lobbies;
        public event Action OnLobbiesUpdated;

        public LobbyDiscoveryService(int maxResults = 50, float refreshIntervalMs = 3000f)
        {
            _maxResults = maxResults;
            _refreshInterval = refreshIntervalMs / 1000f;
        }

        /// <summary>Process a discovery response and add to the lobby list.</summary>
        public void ProcessResponse(DiscoveryResponseMessage response)
        {
            // O(1) lookup by lobby ID
            if (_lobbyIdToIndex.TryGetValue(response.LobbyId, out int idx))
            {
                _lobbies[idx] = ToInfo(response);
                OnLobbiesUpdated?.Invoke();
                return;
            }

            if (_lobbies.Count < _maxResults)
            {
                _lobbyIdToIndex[response.LobbyId] = _lobbies.Count;
                _lobbies.Add(ToInfo(response));
                OnLobbiesUpdated?.Invoke();
            }
        }

        /// <summary>Filter lobbies by criteria into a pre-allocated list (avoids GC alloc).</summary>
        public void GetFiltered(LobbyFilter filter, List<LobbyInfo> results)
        {
            results.Clear();
            if (filter == null)
            {
                results.AddRange(_lobbies);
                return;
            }

            for (int i = 0; i < _lobbies.Count; i++)
            {
                var lobby = _lobbies[i];

                if (!filter.ShowFull && lobby.CurrentPlayers >= lobby.MaxPlayers)
                    continue;
                if (filter.MapId >= 0 && lobby.MapId != filter.MapId)
                    continue;
                if (filter.DifficultyId >= 0 && lobby.DifficultyId != filter.DifficultyId)
                    continue;
                if (lobby.PingMs > filter.MaxPing)
                    continue;
                if (!string.IsNullOrEmpty(filter.SearchText) &&
                    !lobby.HostName.Contains(filter.SearchText, StringComparison.OrdinalIgnoreCase))
                    continue;

                results.Add(lobby);
            }
        }

        /// <summary>Filter lobbies by criteria (allocating convenience overload).</summary>
        public List<LobbyInfo> GetFiltered(LobbyFilter filter)
        {
            var results = new List<LobbyInfo>();
            GetFiltered(filter, results);
            return results;
        }

        /// <summary>Clear all discovered lobbies.</summary>
        public void Clear()
        {
            _lobbies.Clear();
            _lobbyIdToIndex.Clear();
            OnLobbiesUpdated?.Invoke();
        }

        /// <summary>Check if enough time has passed for a refresh query.</summary>
        public bool ShouldRefresh()
        {
            if (Time.realtimeSinceStartup - _lastQueryTime >= _refreshInterval)
            {
                _lastQueryTime = Time.realtimeSinceStartup;
                return true;
            }
            return false;
        }

        private static LobbyInfo ToInfo(DiscoveryResponseMessage response) => new LobbyInfo
        {
            LobbyId = response.LobbyId,
            HostName = response.HostName,
            JoinCode = response.JoinCode,
            MapId = response.MapId,
            DifficultyId = response.DifficultyId,
            CurrentPlayers = response.CurrentPlayers,
            MaxPlayers = response.MaxPlayers,
            PingMs = response.PingMs
        };
    }
}
