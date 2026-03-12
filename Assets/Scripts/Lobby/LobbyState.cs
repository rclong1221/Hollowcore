using System;
using System.Collections.Generic;
using UnityEngine;

namespace DIG.Lobby
{
    /// <summary>
    /// EPIC 17.4: Available game modes for lobby sessions.
    /// </summary>
    public enum GameMode : byte
    {
        Cooperative = 0,
        PvPArena = 1
    }

    /// <summary>
    /// EPIC 17.4: Reasons a player may be denied entry or removed from a lobby.
    /// </summary>
    public enum DenyReason : byte
    {
        LobbyFull = 0,
        Kicked = 1,
        LobbyClosing = 2,
        InvalidCode = 3,
        VersionMismatch = 4,
        Banned = 5
    }

    /// <summary>
    /// EPIC 17.4: Lobby state machine phases.
    /// </summary>
    public enum LobbyPhase : byte
    {
        Idle = 0,
        Creating = 1,
        InLobby = 2,
        Transitioning = 3,
        InGame = 4,
        ReturningToLobby = 5
    }

    /// <summary>
    /// EPIC 17.4: Persistent player identity stored in PlayerPrefs.
    /// Generated once on first launch, survives game restarts.
    /// </summary>
    [Serializable]
    public class PlayerIdentity
    {
        private const string PlayerIdKey = "DIG_PlayerId";
        private const string DisplayNameKey = "DIG_DisplayName";
        private const string LastLevelKey = "DIG_LastLevel";
        private const string LastClassIdKey = "DIG_LastClassId";

        public string PlayerId;
        public string DisplayName;
        public int LastLevel;
        public int LastClassId;

        private static PlayerIdentity _instance;

        public static PlayerIdentity Local
        {
            get
            {
                if (_instance != null) return _instance;

                _instance = new PlayerIdentity();

                // EPIC 17.14: Use IdentityManager when available, otherwise PlayerPrefs fallback
                var identityMgr = DIG.Identity.IdentityManager.Instance;
                if (identityMgr != null && identityMgr.IsReady)
                {
                    _instance.PlayerId = identityMgr.ActiveProvider.PlatformId;
                    _instance.DisplayName = identityMgr.ActiveProvider.DisplayName;
                }
                else
                {
                    _instance.PlayerId = PlayerPrefs.GetString(PlayerIdKey, "");
                    if (string.IsNullOrEmpty(_instance.PlayerId))
                    {
                        _instance.PlayerId = Guid.NewGuid().ToString("N");
                        PlayerPrefs.SetString(PlayerIdKey, _instance.PlayerId);
                        PlayerPrefs.Save();
                    }
                    _instance.DisplayName = PlayerPrefs.GetString(DisplayNameKey, $"Player_{_instance.PlayerId[..6]}");
                }

                _instance.LastLevel = PlayerPrefs.GetInt(LastLevelKey, 1);
                _instance.LastClassId = PlayerPrefs.GetInt(LastClassIdKey, 0);

                return _instance;
            }
        }

        public void Save()
        {
            PlayerPrefs.SetString(PlayerIdKey, PlayerId);
            PlayerPrefs.SetString(DisplayNameKey, DisplayName);
            PlayerPrefs.SetInt(LastLevelKey, LastLevel);
            PlayerPrefs.SetInt(LastClassIdKey, LastClassId);
            PlayerPrefs.Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }
    }

    /// <summary>
    /// EPIC 17.4: Data for a single player slot in the lobby.
    /// </summary>
    [Serializable]
    public class LobbyPlayerSlot
    {
        public string PlayerId;
        public string DisplayName;
        public int Level;
        public int ClassId;
        public bool IsReady;
        public bool IsHost;
        public int SlotIndex;
        public int ConnectionId;
        public int PingMs;

        public void Clear()
        {
            PlayerId = null;
            DisplayName = null;
            Level = 0;
            ClassId = 0;
            IsReady = false;
            IsHost = false;
            SlotIndex = -1;
            ConnectionId = -1;
            PingMs = 0;
        }

        public bool IsEmpty => string.IsNullOrEmpty(PlayerId);
    }

    /// <summary>
    /// EPIC 17.4: Full lobby state, serialized and broadcast by host.
    /// </summary>
    [Serializable]
    public class LobbyState
    {
        public string LobbyId;
        public string HostPlayerId;
        public int MapId;
        public int DifficultyId;
        /// <summary>EPIC 23.1: Selected run configuration for rogue-lite mode. -1 = not a rogue-lite run.</summary>
        public int RunConfigId = -1;
        public int MaxPlayers = 4;
        public GameMode Mode = GameMode.Cooperative;
        public bool IsPrivate;
        public string JoinCode;
        public long CreatedAtUtcTicks;
        public List<LobbyPlayerSlot> Players = new List<LobbyPlayerSlot>();

        [NonSerialized] private int _cachedPlayerCount = -1;
        [NonSerialized] private int _playerCountFrame = -1;

        public int PlayerCount
        {
            get
            {
                int frame = UnityEngine.Time.frameCount;
                if (frame != _playerCountFrame)
                {
                    _playerCountFrame = frame;
                    _cachedPlayerCount = 0;
                    for (int i = 0; i < Players.Count; i++)
                        if (!Players[i].IsEmpty) _cachedPlayerCount++;
                }
                return _cachedPlayerCount;
            }
        }

        public bool IsFull => PlayerCount >= MaxPlayers;

        public int FindEmptySlot()
        {
            for (int i = 0; i < Players.Count; i++)
                if (Players[i].IsEmpty) return i;
            return -1;
        }

        public LobbyPlayerSlot FindPlayerById(string playerId)
        {
            for (int i = 0; i < Players.Count; i++)
                if (Players[i].PlayerId == playerId) return Players[i];
            return null;
        }

        public LobbyPlayerSlot FindPlayerByConnection(int connectionId)
        {
            for (int i = 0; i < Players.Count; i++)
                if (Players[i].ConnectionId == connectionId) return Players[i];
            return null;
        }

        public void RemovePlayerByConnection(int connectionId)
        {
            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i].ConnectionId == connectionId)
                {
                    Players[i].Clear();
                    return;
                }
            }
        }

        public void InitializeSlots(int maxPlayers)
        {
            MaxPlayers = maxPlayers;
            Players.Clear();
            for (int i = 0; i < maxPlayers; i++)
            {
                Players.Add(new LobbyPlayerSlot { SlotIndex = i });
            }
        }

        public void WriteTo(System.IO.BinaryWriter w)
        {
            w.Write(LobbyId ?? "");
            w.Write(HostPlayerId ?? "");
            w.Write(MapId);
            w.Write(DifficultyId);
            w.Write(RunConfigId);
            w.Write(MaxPlayers);
            w.Write((byte)Mode);
            w.Write(IsPrivate);
            w.Write(JoinCode ?? "");
            w.Write(CreatedAtUtcTicks);
            w.Write((byte)Players.Count);
            for (int i = 0; i < Players.Count; i++)
            {
                var p = Players[i];
                w.Write(p.PlayerId ?? "");
                w.Write(p.DisplayName ?? "");
                w.Write(p.Level);
                w.Write(p.ClassId);
                w.Write(p.IsReady);
                w.Write(p.IsHost);
                w.Write((byte)p.SlotIndex);
                w.Write(p.ConnectionId);
                w.Write((ushort)p.PingMs);
            }
        }

        public void ReadFrom(System.IO.BinaryReader r)
        {
            LobbyId = LobbyMessageSerializer.ReadSafeString(r, 64);
            HostPlayerId = LobbyMessageSerializer.ReadSafeString(r, 64);
            MapId = r.ReadInt32();
            DifficultyId = r.ReadInt32();
            RunConfigId = r.ReadInt32();
            MaxPlayers = r.ReadInt32();
            Mode = (GameMode)r.ReadByte();
            IsPrivate = r.ReadBoolean();
            JoinCode = LobbyMessageSerializer.ReadSafeString(r, 16);
            CreatedAtUtcTicks = r.ReadInt64();
            int count = r.ReadByte();
            Players.Clear();
            for (int i = 0; i < count; i++)
            {
                var p = new LobbyPlayerSlot();
                p.PlayerId = LobbyMessageSerializer.ReadSafeString(r, 64);
                p.DisplayName = LobbyMessageSerializer.ReadSafeString(r, 64);
                p.Level = r.ReadInt32();
                p.ClassId = r.ReadInt32();
                p.IsReady = r.ReadBoolean();
                p.IsHost = r.ReadBoolean();
                p.SlotIndex = r.ReadByte();
                p.ConnectionId = r.ReadInt32();
                p.PingMs = r.ReadUInt16();
                Players.Add(p);
            }
        }
    }
}
