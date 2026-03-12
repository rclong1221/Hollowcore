using System;
using System.Collections.Generic;
using DIG.Identity;
using UnityEngine;

namespace DIG.Lobby
{
    /// <summary>
    /// EPIC 17.4: MonoBehaviour singleton managing lobby lifecycle.
    /// Runs BEFORE ECS worlds exist. DontDestroyOnLoad.
    /// State machine: Idle→Creating→InLobby→Transitioning→InGame→ReturningToLobby.
    /// </summary>
    public class LobbyManager : MonoBehaviour
    {
        private static LobbyManager _instance;
        public static LobbyManager Instance => _instance;

        // Configuration
        private LobbyConfigSO _config;
        public LobbyConfigSO Config => _config;

        // State
        public LobbyPhase Phase { get; private set; } = LobbyPhase.Idle;
        public LobbyState CurrentLobby { get; private set; }
        public bool IsHost { get; private set; }
        public int LocalSlotIndex { get; private set; } = -1;

        // Transport
        private LobbyNetworkTransport _transport;
        public LobbyNetworkTransport Transport => _transport;

        // Timers
        private float _heartbeatTimer;
        private float _connectStartTime;
        private int _stateDirtyFrame;
        private bool _stateDirty;

        // Chat history (host stores, sends to late joiners) — queue for O(1) dequeue
        private const int MaxChatHistory = 100;
        private readonly Queue<ChatMessageMessage> _chatHistory = new Queue<ChatMessageMessage>(MaxChatHistory + 1);

        // Events
        public event Action<LobbyState> OnLobbyStateChanged;
        public event Action<LobbyPlayerSlot> OnPlayerJoined;
        public event Action<LobbyPlayerSlot> OnPlayerLeft;
        public event Action<string, string> OnChatReceived; // senderName, text
        public event Action OnTransitionStarted;
        public event Action<string> OnError;
        public event Action<DenyReason> OnJoinDenied;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            _config = Resources.Load<LobbyConfigSO>("LobbyConfig");
            if (_config == null)
                Debug.LogWarning("[LobbyManager] No LobbyConfig found in Resources/. Using defaults.");
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            _transport?.Dispose();
        }

        private void Update()
        {
            if (Phase == LobbyPhase.Idle || Phase == LobbyPhase.InGame) return;
            if (_transport == null || !_transport.IsInitialized) return;

            // Pump network events
            var messages = _transport.PumpEvents();
            for (int i = 0; i < messages.Count; i++)
                ProcessMessage(messages[i].connectionId, messages[i].message);

            // Connect timeout — client waiting too long for handshake/join acceptance
            if (Phase == LobbyPhase.Creating && !IsHost)
            {
                float timeout = _config != null ? _config.TransitionTimeoutSeconds : 15;
                if (Time.realtimeSinceStartup - _connectStartTime > timeout)
                {
                    OnError?.Invoke("Connection timed out. Could not reach host.");
                    Cleanup();
                    return;
                }
            }

            // Heartbeat — only when connection is established
            if (Phase == LobbyPhase.InLobby)
            {
                _heartbeatTimer += Time.unscaledDeltaTime;
                float heartbeatInterval = (_config != null ? _config.HeartbeatIntervalMs : 2000) / 1000f;
                if (_heartbeatTimer >= heartbeatInterval)
                {
                    _heartbeatTimer = 0f;
                    SendHeartbeat();
                }
            }

            // Host: check timeouts and broadcast state
            if (IsHost && Phase == LobbyPhase.InLobby)
            {
                CheckClientTimeouts();

                if (_stateDirty && Time.frameCount - _stateDirtyFrame >= 6)
                {
                    BroadcastState();
                    _stateDirty = false;
                }
            }
        }

        // ========== PUBLIC API ==========

        public void CreateLobby(MapDefinitionSO map, DifficultyDefinitionSO difficulty, bool isPrivate)
        {
            if (Phase != LobbyPhase.Idle)
            {
                OnError?.Invoke("Cannot create lobby: not in Idle state.");
                return;
            }

            Phase = LobbyPhase.Creating;
            IsHost = true;

            _transport = new LobbyNetworkTransport();
            _transport.OnClientConnected += OnTransportClientConnected;
            _transport.OnClientDisconnected += OnTransportClientDisconnected;
            _transport.Initialize(isHost: true);

            if (!_transport.IsInitialized)
            {
                OnError?.Invoke("Failed to start lobby server (port 7980 may be in use). Try restarting Unity.");
                _transport.Dispose();
                _transport = null;
                Phase = LobbyPhase.Idle;
                return;
            }

            // Create lobby state
            CurrentLobby = new LobbyState
            {
                LobbyId = Guid.NewGuid().ToString("N")[..12],
                HostPlayerId = PlayerIdentity.Local.PlayerId,
                MapId = map != null ? map.MapId : 0,
                DifficultyId = difficulty != null ? difficulty.DifficultyId : 0,
                IsPrivate = isPrivate,
                JoinCode = GenerateJoinCode(),
                CreatedAtUtcTicks = DateTime.UtcNow.Ticks
            };

            int maxPlayers = _config != null ? _config.MaxPlayersPerLobby : 4;
            if (map != null) maxPlayers = Mathf.Min(maxPlayers, map.MaxPlayers);
            CurrentLobby.InitializeSlots(maxPlayers);

            // Host occupies slot 0
            var hostSlot = CurrentLobby.Players[0];
            hostSlot.PlayerId = PlayerIdentity.Local.PlayerId;
            hostSlot.DisplayName = PlayerIdentity.Local.DisplayName;
            hostSlot.Level = PlayerIdentity.Local.LastLevel;
            hostSlot.ClassId = PlayerIdentity.Local.LastClassId;
            hostSlot.IsHost = true;
            hostSlot.IsReady = false;
            hostSlot.ConnectionId = -1; // Host is local
            LocalSlotIndex = 0;

            Phase = LobbyPhase.InLobby;
            UpdateRichPresence();
            OnLobbyStateChanged?.Invoke(CurrentLobby);

            // Log host IP for clients on other machines
            string localIp = GetLocalIPAddress();
            Debug.Log($"[LobbyManager] Created lobby {CurrentLobby.LobbyId}, JoinCode: {CurrentLobby.JoinCode}, HostIP: {localIp}");
        }

        public void JoinLobby(string joinCode, string hostAddress = "127.0.0.1")
        {
            if (Phase != LobbyPhase.Idle)
            {
                OnError?.Invoke("Cannot join lobby: not in Idle state.");
                return;
            }

            Phase = LobbyPhase.Creating;
            IsHost = false;

            _transport = new LobbyNetworkTransport();
            _transport.OnClientDisconnected += OnTransportClientDisconnected;
            _transport.OnConnectedToServer += OnTransportConnectedToServer;
            _transport.Initialize(isHost: false);

            // Connect to host
            if (string.IsNullOrEmpty(hostAddress)) hostAddress = "127.0.0.1";
            _transport.ConnectTo(hostAddress, 7980);

            _pendingJoinCode = joinCode;
            _connectStartTime = Time.realtimeSinceStartup;

            Debug.Log($"[LobbyManager] Joining lobby with code: {joinCode}");
        }

        private string _pendingJoinCode;

        public void SetReady(bool ready)
        {
            if (Phase != LobbyPhase.InLobby) return;

            if (IsHost)
            {
                var slot = CurrentLobby.Players[LocalSlotIndex];
                slot.IsReady = ready;
                MarkStateDirty();
            }
            else
            {
                _transport.SendToServer(new ReadyChangedMessage { IsReady = ready });
            }
        }

        public void SetMap(int mapId)
        {
            if (!IsHost || Phase != LobbyPhase.InLobby) return;
            CurrentLobby.MapId = mapId;
            // Reset ready states on map change
            ResetAllReady();
            MarkStateDirty();
        }

        public void SetDifficulty(int difficultyId)
        {
            if (!IsHost || Phase != LobbyPhase.InLobby) return;
            CurrentLobby.DifficultyId = difficultyId;
            MarkStateDirty();
        }

        public void KickPlayer(int slotIndex)
        {
            if (!IsHost || Phase != LobbyPhase.InLobby) return;
            if (slotIndex < 0 || slotIndex >= CurrentLobby.Players.Count) return;

            var slot = CurrentLobby.Players[slotIndex];
            if (slot.IsEmpty || slot.IsHost) return;

            int connId = slot.ConnectionId;

            // Send kick message to client
            _transport.Send(connId, new JoinDeniedMessage { Reason = DenyReason.Kicked });
            _transport.DisconnectClient(connId);

            var removedSlot = new LobbyPlayerSlot
            {
                PlayerId = slot.PlayerId,
                DisplayName = slot.DisplayName,
                SlotIndex = slotIndex
            };
            slot.Clear();
            slot.SlotIndex = slotIndex;

            OnPlayerLeft?.Invoke(removedSlot);
            MarkStateDirty();
        }

        public void SendChat(string text)
        {
            if (Phase != LobbyPhase.InLobby || string.IsNullOrEmpty(text)) return;

            var msg = new ChatMessageMessage
            {
                SenderName = PlayerIdentity.Local.DisplayName,
                Text = text
            };

            if (IsHost)
            {
                RecordChat(msg);
                _transport.Broadcast(msg);
                OnChatReceived?.Invoke(msg.SenderName, msg.Text);
            }
            else
            {
                _transport.SendToServer(msg);
                // Show locally immediately (host won't relay back to us)
                OnChatReceived?.Invoke(msg.SenderName, msg.Text);
            }
        }

        public void StartGame()
        {
            if (!IsHost || Phase != LobbyPhase.InLobby) return;

            int minPlayers = _config != null ? _config.MinPlayersToStart : 1;
            if (CurrentLobby.PlayerCount < minPlayers)
            {
                OnError?.Invoke($"Need at least {minPlayers} player(s) to start.");
                return;
            }

            // Check all non-host players are ready
            for (int i = 0; i < CurrentLobby.Players.Count; i++)
            {
                var p = CurrentLobby.Players[i];
                if (!p.IsEmpty && !p.IsHost && !p.IsReady)
                {
                    OnError?.Invoke("All players must be ready to start.");
                    return;
                }
            }

            Phase = LobbyPhase.Transitioning;
            ClearRichPresence();
            OnTransitionStarted?.Invoke();

            // Broadcast start to all clients
            var startMsg = new StartGameMessage
            {
                MapId = CurrentLobby.MapId,
                DifficultyId = CurrentLobby.DifficultyId,
                RelayJoinCode = _transport.RelayJoinCode ?? ""
            };
            _transport.Broadcast(startMsg);

            // Begin transition (deferred to allow message to send)
            _pendingTransition = true;
        }

        private bool _pendingTransition;

        public void LeaveLobby()
        {
            if (Phase == LobbyPhase.Idle) return;

            if (IsHost)
            {
                // Notify all clients
                _transport?.Broadcast(new JoinDeniedMessage { Reason = DenyReason.LobbyClosing });
            }
            else
            {
                _transport?.SendToServer(new LeaveNotifyMessage());
            }

            Cleanup();
        }

        public void ReturnToLobby()
        {
            if (Phase != LobbyPhase.InGame) return;

            Phase = LobbyPhase.ReturningToLobby;
            LobbyToGameTransition.ReturnToLobby();

            // Dispose old transport before creating new one
            _transport?.Dispose();
            _transport = new LobbyNetworkTransport();
            if (IsHost)
            {
                _transport.OnClientConnected += OnTransportClientConnected;
                _transport.OnClientDisconnected += OnTransportClientDisconnected;
                _transport.Initialize(isHost: true);
                ResetAllReady();
            }
            else
            {
                _transport.OnClientDisconnected += OnTransportClientDisconnected;
                _transport.Initialize(isHost: false);
            }

            Phase = LobbyPhase.InLobby;
            OnLobbyStateChanged?.Invoke(CurrentLobby);
        }

        // ========== MESSAGE PROCESSING ==========

        private void ProcessMessage(int connectionId, LobbyMessage message)
        {
            switch (message.Type)
            {
                case LobbyMessageType.JoinRequest:
                    if (IsHost) HandleJoinRequest(connectionId, (JoinRequestMessage)message);
                    break;
                case LobbyMessageType.JoinAccepted:
                    if (!IsHost) HandleJoinAccepted((JoinAcceptedMessage)message);
                    break;
                case LobbyMessageType.JoinDenied:
                    if (!IsHost) HandleJoinDenied((JoinDeniedMessage)message);
                    break;
                case LobbyMessageType.StateUpdate:
                    if (!IsHost) HandleStateUpdate((StateUpdateMessage)message);
                    break;
                case LobbyMessageType.ReadyChanged:
                    if (IsHost) HandleReadyChanged(connectionId, (ReadyChangedMessage)message);
                    break;
                case LobbyMessageType.ChatMessage:
                    HandleChatMessage(connectionId, (ChatMessageMessage)message);
                    break;
                case LobbyMessageType.LeaveNotify:
                    if (IsHost) HandleLeaveNotify(connectionId);
                    break;
                case LobbyMessageType.StartGame:
                    if (!IsHost) HandleStartGame((StartGameMessage)message);
                    break;
                case LobbyMessageType.Heartbeat:
                    // Heartbeat received — timeout tracker updated by transport
                    break;
                case LobbyMessageType.PingRequest:
                    HandlePingRequest(connectionId, (PingRequestMessage)message);
                    break;
                case LobbyMessageType.PingResponse:
                    HandlePingResponse(connectionId, (PingResponseMessage)message);
                    break;
            }

        }

        private void HandleJoinRequest(int connectionId, JoinRequestMessage msg)
        {
            if (CurrentLobby.IsFull)
            {
                _transport.Send(connectionId, new JoinDeniedMessage { Reason = DenyReason.LobbyFull });
                return;
            }

            // Join code validation (skip for public lobbies or direct connect with empty code)
            if (!string.IsNullOrEmpty(CurrentLobby.JoinCode) &&
                !string.IsNullOrEmpty(msg.JoinCode) &&
                !string.Equals(msg.JoinCode, CurrentLobby.JoinCode, System.StringComparison.OrdinalIgnoreCase))
            {
                _transport.Send(connectionId, new JoinDeniedMessage { Reason = DenyReason.InvalidCode });
                return;
            }

            // Version check
            if (!string.IsNullOrEmpty(msg.GameVersion) && msg.GameVersion != Application.version)
            {
                _transport.Send(connectionId, new JoinDeniedMessage { Reason = DenyReason.VersionMismatch });
                return;
            }

            int slotIndex = CurrentLobby.FindEmptySlot();
            if (slotIndex < 0)
            {
                _transport.Send(connectionId, new JoinDeniedMessage { Reason = DenyReason.LobbyFull });
                return;
            }

            var slot = CurrentLobby.Players[slotIndex];
            slot.PlayerId = msg.PlayerId;
            slot.DisplayName = msg.DisplayName;
            slot.Level = msg.Level;
            slot.ClassId = msg.ClassId;
            slot.IsReady = false;
            slot.IsHost = false;
            slot.ConnectionId = connectionId;
            slot.SlotIndex = slotIndex;

            // Send acceptance
            _transport.Send(connectionId, new JoinAcceptedMessage
            {
                AssignedSlotIndex = slotIndex,
                FullState = CurrentLobby
            });

            // Send chat history to the new player so they see prior messages
            foreach (var historyMsg in _chatHistory)
                _transport.Send(connectionId, historyMsg);

            // Broadcast system join message to everyone (including new player)
            BroadcastSystemChat($"{msg.DisplayName} joined the lobby.");

            OnPlayerJoined?.Invoke(slot);
            MarkStateDirty();
            Debug.Log($"[LobbyManager] Player {msg.DisplayName} joined slot {slotIndex}");
        }

        private void HandleJoinAccepted(JoinAcceptedMessage msg)
        {
            CurrentLobby = msg.FullState;
            LocalSlotIndex = msg.AssignedSlotIndex;
            Phase = LobbyPhase.InLobby;
            UpdateRichPresence();
            OnLobbyStateChanged?.Invoke(CurrentLobby);
            Debug.Log($"[LobbyManager] Joined lobby, assigned slot {LocalSlotIndex}");
        }

        private void HandleJoinDenied(JoinDeniedMessage msg)
        {
            OnJoinDenied?.Invoke(msg.Reason);
            OnError?.Invoke($"Join denied: {msg.Reason}");
            Cleanup();
        }

        private void HandleStateUpdate(StateUpdateMessage msg)
        {
            CurrentLobby = msg.State;
            OnLobbyStateChanged?.Invoke(CurrentLobby);
        }

        private void HandleReadyChanged(int connectionId, ReadyChangedMessage msg)
        {
            var slot = CurrentLobby.FindPlayerByConnection(connectionId);
            if (slot != null)
            {
                slot.IsReady = msg.IsReady;
                MarkStateDirty();
            }
        }

        private void HandleChatMessage(int connectionId, ChatMessageMessage msg)
        {
            if (IsHost)
            {
                RecordChat(msg);
                // Relay to all clients EXCEPT the sender (they already show it locally)
                _transport.BroadcastExcept(msg, connectionId);
                OnChatReceived?.Invoke(msg.SenderName, msg.Text);
            }
            else
            {
                // Client received relayed message from host
                OnChatReceived?.Invoke(msg.SenderName, msg.Text);
            }
        }

        private void HandleLeaveNotify(int connectionId)
        {
            var slot = CurrentLobby.FindPlayerByConnection(connectionId);
            if (slot != null)
            {
                string displayName = slot.DisplayName;
                var removedSlot = new LobbyPlayerSlot
                {
                    PlayerId = slot.PlayerId,
                    DisplayName = slot.DisplayName,
                    SlotIndex = slot.SlotIndex
                };
                slot.Clear();
                slot.SlotIndex = removedSlot.SlotIndex;

                BroadcastSystemChat($"{displayName} left the lobby.");
                OnPlayerLeft?.Invoke(removedSlot);
                MarkStateDirty();
            }

            _transport.DisconnectClient(connectionId);
        }

        private void HandleStartGame(StartGameMessage msg)
        {
            Phase = LobbyPhase.Transitioning;
            ClearRichPresence();
            OnTransitionStarted?.Invoke();

            // Client: begin transition
            LobbyToGameTransition.BeginTransition(CurrentLobby, IsHost, msg.RelayJoinCode);

            // EPIC 18.6: Notify SceneService to orchestrate loading screen and lifecycle
            DIG.SceneManagement.SceneService.Instance?.RequestTransition("Gameplay");

            Phase = LobbyPhase.InGame;
        }

        private void HandlePingRequest(int connectionId, PingRequestMessage msg)
        {
            if (IsHost)
                _transport.Send(connectionId, new PingResponseMessage { OriginalTimestampTicks = msg.TimestampTicks });
            else
                _transport.SendToServer(new PingResponseMessage { OriginalTimestampTicks = msg.TimestampTicks });
        }

        private void HandlePingResponse(int connectionId, PingResponseMessage msg)
        {
            long rttTicks = DateTime.UtcNow.Ticks - msg.OriginalTimestampTicks;
            int rttMs = (int)(rttTicks / TimeSpan.TicksPerMillisecond);

            if (IsHost)
            {
                var slot = CurrentLobby.FindPlayerByConnection(connectionId);
                if (slot != null) slot.PingMs = rttMs;
            }
        }

        // ========== TRANSPORT CALLBACKS ==========

        private void OnTransportClientConnected(int connectionId)
        {
            // Client just connected — wait for JoinRequest message
        }

        private void OnTransportConnectedToServer()
        {
            // Send join request now that connection is established
            var identity = PlayerIdentity.Local;
            _transport.SendToServer(new JoinRequestMessage
            {
                PlayerId = identity.PlayerId,
                DisplayName = identity.DisplayName,
                Level = identity.LastLevel,
                ClassId = identity.LastClassId,
                GameVersion = Application.version,
                JoinCode = _pendingJoinCode ?? ""
            });
        }

        private void OnTransportClientDisconnected(int connectionId)
        {
            if (IsHost)
            {
                var slot = CurrentLobby.FindPlayerByConnection(connectionId);
                if (slot != null)
                {
                    string displayName = slot.DisplayName;
                    var removedSlot = new LobbyPlayerSlot
                    {
                        PlayerId = slot.PlayerId,
                        DisplayName = slot.DisplayName,
                        SlotIndex = slot.SlotIndex
                    };
                    slot.Clear();
                    slot.SlotIndex = removedSlot.SlotIndex;

                    BroadcastSystemChat($"{displayName} disconnected.");
                    OnPlayerLeft?.Invoke(removedSlot);
                    MarkStateDirty();
                }
            }
            else
            {
                // Lost connection to host
                OnError?.Invoke("Lost connection to host.");
                Cleanup();
            }
        }

        // ========== HELPERS ==========

        private void RecordChat(ChatMessageMessage msg)
        {
            _chatHistory.Enqueue(msg);
            if (_chatHistory.Count > MaxChatHistory)
                _chatHistory.Dequeue();
        }

        private void BroadcastSystemChat(string text)
        {
            var msg = new ChatMessageMessage { SenderName = "System", Text = text };
            RecordChat(msg);
            _transport?.Broadcast(msg);
            OnChatReceived?.Invoke(msg.SenderName, msg.Text);
        }

        private static readonly HeartbeatMessage _cachedHeartbeat = new HeartbeatMessage();

        private void SendHeartbeat()
        {
            if (IsHost)
                _transport.Broadcast(_cachedHeartbeat);
            else
                _transport.SendToServer(_cachedHeartbeat);
        }

        private void CheckClientTimeouts()
        {
            float timeout = (_config != null ? _config.HeartbeatTimeoutMs : 8000) / 1000f;
            var timedOut = _transport.CheckTimeouts(timeout);
            for (int i = 0; i < timedOut.Count; i++)
            {
                Debug.Log($"[LobbyManager] Client {timedOut[i]} timed out");
                HandleLeaveNotify(timedOut[i]);
            }
        }

        private void BroadcastState()
        {
            _transport.Broadcast(new StateUpdateMessage { State = CurrentLobby });
            OnLobbyStateChanged?.Invoke(CurrentLobby);
        }

        private void MarkStateDirty()
        {
            if (!_stateDirty)
            {
                _stateDirty = true;
                _stateDirtyFrame = Time.frameCount;
            }
        }

        private void ResetAllReady()
        {
            for (int i = 0; i < CurrentLobby.Players.Count; i++)
                CurrentLobby.Players[i].IsReady = false;
        }

        private void Cleanup()
        {
            ClearRichPresence();
            _transport?.Shutdown();
            _transport = null;
            CurrentLobby = null;
            Phase = LobbyPhase.Idle;
            IsHost = false;
            LocalSlotIndex = -1;
            _pendingJoinCode = null;
            _pendingTransition = false;
            _chatHistory.Clear();

            // Notify UI to return to browser
            OnLobbyStateChanged?.Invoke(null);
        }

        // EPIC 17.14: Rich Presence
        private void UpdateRichPresence()
        {
            var mgr = IdentityManager.Instance;
            if (mgr == null || !mgr.IsReady || !mgr.ActiveProvider.SupportsPresence) return;
            var config = mgr.Config;
            if (config == null || !config.RichPresenceEnabled) return;

            string status = string.Format(config.RichPresenceInLobby,
                CurrentLobby.PlayerCount, CurrentLobby.MaxPlayers);
            mgr.ActiveProvider.SetRichPresence("status", status);
        }

        private void ClearRichPresence()
        {
            var mgr = IdentityManager.Instance;
            if (mgr != null && mgr.IsReady)
                mgr.ActiveProvider.ClearRichPresence();
        }

        private void LateUpdate()
        {
            // Deferred transition after StartGame message is sent
            if (_pendingTransition && Phase == LobbyPhase.Transitioning)
            {
                _pendingTransition = false;
                LobbyToGameTransition.BeginTransition(CurrentLobby, IsHost, _transport?.RelayJoinCode);

                // EPIC 18.6: Notify SceneService to orchestrate loading screen and lifecycle
                DIG.SceneManagement.SceneService.Instance?.RequestTransition("Gameplay");

                Phase = LobbyPhase.InGame;
            }
        }

        private string GenerateJoinCode()
        {
            int len = _config != null ? _config.JoinCodeLength : 6;
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // No I/O/0/1 to avoid confusion
            var code = new char[len];
            for (int i = 0; i < len; i++)
                code[i] = chars[UnityEngine.Random.Range(0, chars.Length)];
            return new string(code);
        }

        private static string GetLocalIPAddress()
        {
            try
            {
                foreach (var addr in System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList)
                {
                    if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                        && !System.Net.IPAddress.IsLoopback(addr))
                        return addr.ToString();
                }
            }
            catch { }
            return "127.0.0.1";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }
    }
}
