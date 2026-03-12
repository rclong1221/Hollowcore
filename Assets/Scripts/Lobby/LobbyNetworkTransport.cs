using System;
using System.Collections.Generic;
using Unity.Networking.Transport;
using UnityEngine;

namespace DIG.Lobby
{
    /// <summary>
    /// EPIC 17.4: Wraps Unity Transport for lobby messaging before ECS worlds exist.
    /// Host: binds + accepts connections. Client: connects to host endpoint.
    /// Relay integration via com.unity.services.multiplayer.
    ///
    /// Connection IDs: We use the list index into _connections as the stable ID
    /// since NetworkConnection.InternalId is internal in Transport 2.x.
    /// </summary>
    public class LobbyNetworkTransport : IDisposable
    {
        public bool IsHost { get; private set; }
        public bool IsInitialized { get; private set; }

        private NetworkDriver _driver;
        private readonly List<NetworkConnection> _connections = new List<NetworkConnection>();
        private NetworkConnection _serverConnection;

        // Map connection list index → last heartbeat time
        private readonly Dictionary<int, float> _lastHeartbeat = new Dictionary<int, float>();

        // Stable ID counter for connections (never reused)
        private int _nextConnectionId;

        // Map NetworkConnection → stable connection ID
        private readonly Dictionary<NetworkConnection, int> _connectionToId = new Dictionary<NetworkConnection, int>();
        // Map stable connection ID → NetworkConnection
        private readonly Dictionary<int, NetworkConnection> _idToConnection = new Dictionary<int, NetworkConnection>();

        // Received messages queued for processing
        private readonly List<(int connectionId, LobbyMessage message)> _receivedMessages = new List<(int, LobbyMessage)>();

        // Reusable receive buffer to avoid per-message allocations
        private byte[] _receiveBuffer = new byte[1024];

        // Reusable timeout list to avoid per-frame allocation
        private readonly List<int> _timedOutCache = new List<int>();

        // Relay data cached for game transition reuse
        public string RelayJoinCode { get; private set; }

        public event Action<int> OnClientConnected;
        public event Action<int> OnClientDisconnected;
        public event Action OnConnectedToServer;

        public void Initialize(bool isHost)
        {
            IsHost = isHost;
            var settings = new NetworkSettings();
            _driver = NetworkDriver.Create(settings);

            if (isHost)
            {
                var endpoint = NetworkEndpoint.AnyIpv4.WithPort(7980); // Lobby port (game uses 7979)
                if (_driver.Bind(endpoint) != 0)
                {
                    Debug.LogError("[LobbyTransport] Failed to bind on port 7980");
                    return;
                }
                _driver.Listen();
                Debug.Log("[LobbyTransport] Host listening on port 7980");
            }

            IsInitialized = true;
        }

        public void ConnectTo(string address, ushort port)
        {
            if (!IsInitialized || IsHost) return;

            var endpoint = NetworkEndpoint.LoopbackIpv4.WithPort(port);
            if (!string.IsNullOrEmpty(address) && address != "127.0.0.1" && address != "localhost")
            {
                if (NetworkEndpoint.TryParse(address, port, out var parsed))
                    endpoint = parsed;
            }

            _serverConnection = _driver.Connect(endpoint);
            Debug.Log($"[LobbyTransport] Connecting to {address}:{port}");
        }

#if UNITY_SERVICES_MULTIPLAYER
        public async System.Threading.Tasks.Task<string> AllocateRelayAsync(int maxConnections, string region = "")
        {
            try
            {
                var allocation = await Unity.Services.Relay.RelayService.Instance.CreateAllocationAsync(maxConnections);
                RelayJoinCode = await Unity.Services.Relay.RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

                // Configure driver for Relay
                var relayData = Unity.Networking.Transport.Relay.RelayUtilities.HostRelayData(allocation);
                var settings = new NetworkSettings();
                settings.WithRelayParameters(ref relayData);

                _driver.Dispose();
                _driver = NetworkDriver.Create(settings);
                _driver.Bind(NetworkEndpoint.AnyIpv4);
                _driver.Listen();

                Debug.Log($"[LobbyTransport] Relay allocated. JoinCode: {RelayJoinCode}");
                return RelayJoinCode;
            }
            catch (Exception e)
            {
                Debug.LogError($"[LobbyTransport] Relay allocation failed: {e.Message}");
                return null;
            }
        }

        public async System.Threading.Tasks.Task<bool> JoinRelayAsync(string joinCode)
        {
            try
            {
                var allocation = await Unity.Services.Relay.RelayService.Instance.JoinAllocationAsync(joinCode);
                var relayData = Unity.Networking.Transport.Relay.RelayUtilities.PlayerRelayData(allocation);
                var settings = new NetworkSettings();
                settings.WithRelayParameters(ref relayData);

                _driver.Dispose();
                _driver = NetworkDriver.Create(settings);
                _driver.Bind(NetworkEndpoint.AnyIpv4);
                _serverConnection = _driver.Connect(NetworkEndpoint.AnyIpv4);

                RelayJoinCode = joinCode;
                Debug.Log($"[LobbyTransport] Joined Relay with code: {joinCode}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[LobbyTransport] Relay join failed: {e.Message}");
                return false;
            }
        }
#endif

        /// <summary>Process network events and return received messages.</summary>
        public List<(int connectionId, LobbyMessage message)> PumpEvents()
        {
            _receivedMessages.Clear();
            if (!IsInitialized || !_driver.IsCreated) return _receivedMessages;

            _driver.ScheduleUpdate().Complete();

            if (IsHost)
                PumpHostEvents();
            else
                PumpClientEvents();

            return _receivedMessages;
        }

        private void PumpHostEvents()
        {
            // Accept new connections
            NetworkConnection c;
            while ((c = _driver.Accept()) != default)
            {
                int connId = _nextConnectionId++;
                _connections.Add(c);
                _connectionToId[c] = connId;
                _idToConnection[connId] = c;
                _lastHeartbeat[connId] = Time.realtimeSinceStartup;
                OnClientConnected?.Invoke(connId);
                Debug.Log($"[LobbyTransport] Client connected: {connId}");
            }

            // Process events on existing connections
            for (int i = _connections.Count - 1; i >= 0; i--)
            {
                var conn = _connections[i];
                if (!conn.IsCreated)
                {
                    RemoveConnection(i);
                    continue;
                }

                NetworkEvent.Type evt;
                while ((evt = _driver.PopEventForConnection(conn, out var stream)) != NetworkEvent.Type.Empty)
                {
                    if (!_connectionToId.TryGetValue(conn, out int connId))
                        continue;

                    switch (evt)
                    {
                        case NetworkEvent.Type.Data:
                            ProcessIncomingData(connId, stream);
                            _lastHeartbeat[connId] = Time.realtimeSinceStartup;
                            break;
                        case NetworkEvent.Type.Disconnect:
                            OnClientDisconnected?.Invoke(connId);
                            RemoveConnection(i);
                            Debug.Log($"[LobbyTransport] Client disconnected: {connId}");
                            break;
                    }
                }
            }
        }

        private void PumpClientEvents()
        {
            if (!_serverConnection.IsCreated) return;

            NetworkEvent.Type evt;
            while ((evt = _driver.PopEventForConnection(_serverConnection, out var stream)) != NetworkEvent.Type.Empty)
            {
                switch (evt)
                {
                    case NetworkEvent.Type.Connect:
                        OnConnectedToServer?.Invoke();
                        break;
                    case NetworkEvent.Type.Data:
                        ProcessIncomingData(0, stream);
                        break;
                    case NetworkEvent.Type.Disconnect:
                        _serverConnection = default;
                        OnClientDisconnected?.Invoke(0);
                        Debug.Log("[LobbyTransport] Disconnected from host");
                        break;
                }
            }
        }

        private void ProcessIncomingData(int connectionId, Unity.Collections.DataStreamReader stream)
        {
            int length = stream.Length;
            if (_receiveBuffer.Length < length)
                _receiveBuffer = new byte[length];
            stream.ReadBytes(new Span<byte>(_receiveBuffer, 0, length));

            var msg = LobbyMessageSerializer.Deserialize(_receiveBuffer, 0, length);
            if (msg != null)
                _receivedMessages.Add((connectionId, msg));
        }

        /// <summary>Send a message to a specific connection (host→client).</summary>
        public void Send(int connectionId, LobbyMessage message)
        {
            if (!IsInitialized || !_driver.IsCreated) return;
            if (!_idToConnection.TryGetValue(connectionId, out var conn)) return;
            if (!conn.IsCreated) return;

            var (data, len) = LobbyMessageSerializer.SerializeShared(message);
            int result = _driver.BeginSend(conn, out var writer);
            if (result != 0) return;
            writer.WriteBytes(new Span<byte>(data, 0, len));
            _driver.EndSend(writer);
        }

        /// <summary>Send a message to the server (client→host).</summary>
        public void SendToServer(LobbyMessage message)
        {
            if (!IsInitialized || !_driver.IsCreated || !_serverConnection.IsCreated) return;

            var (data, len) = LobbyMessageSerializer.SerializeShared(message);
            int result = _driver.BeginSend(_serverConnection, out var writer);
            if (result != 0) return;
            writer.WriteBytes(new Span<byte>(data, 0, len));
            _driver.EndSend(writer);
        }

        /// <summary>Broadcast a message to all connected clients (host only).</summary>
        public void Broadcast(LobbyMessage message)
        {
            BroadcastExcept(message, -1);
        }

        /// <summary>Broadcast a message to all connected clients except one (host only).</summary>
        public void BroadcastExcept(LobbyMessage message, int excludeConnectionId)
        {
            if (!IsHost || !IsInitialized) return;

            var (data, len) = LobbyMessageSerializer.SerializeShared(message);
            for (int i = 0; i < _connections.Count; i++)
            {
                var conn = _connections[i];
                if (!conn.IsCreated) continue;

                // Skip excluded connection
                if (excludeConnectionId >= 0 && _connectionToId.TryGetValue(conn, out int connId) && connId == excludeConnectionId)
                    continue;

                int result = _driver.BeginSend(conn, out var writer);
                if (result != 0) continue;
                writer.WriteBytes(new Span<byte>(data, 0, len));
                _driver.EndSend(writer);
            }
        }

        /// <summary>Check for timed-out connections. Returns IDs of timed-out clients (reused list — do not cache).</summary>
        public List<int> CheckTimeouts(float timeoutSeconds)
        {
            _timedOutCache.Clear();
            float now = Time.realtimeSinceStartup;

            foreach (var kvp in _lastHeartbeat)
            {
                if (now - kvp.Value > timeoutSeconds)
                    _timedOutCache.Add(kvp.Key);
            }

            return _timedOutCache;
        }

        /// <summary>Disconnect a specific client (host only).</summary>
        public void DisconnectClient(int connectionId)
        {
            if (!_idToConnection.TryGetValue(connectionId, out var conn)) return;
            if (conn.IsCreated)
                _driver.Disconnect(conn);

            // Direct cleanup without double-scan
            _connectionToId.Remove(conn);
            _idToConnection.Remove(connectionId);
            _lastHeartbeat.Remove(connectionId);
            _connections.Remove(conn);
        }

        private void RemoveConnection(int listIndex)
        {
            var conn = _connections[listIndex];
            if (_connectionToId.TryGetValue(conn, out int connId))
            {
                _connectionToId.Remove(conn);
                _idToConnection.Remove(connId);
                _lastHeartbeat.Remove(connId);
            }
            _connections.RemoveAt(listIndex);
        }

        public void Shutdown()
        {
            if (!IsInitialized) return;

            if (IsHost)
            {
                for (int i = 0; i < _connections.Count; i++)
                    if (_connections[i].IsCreated) _driver.Disconnect(_connections[i]);
                _connections.Clear();
            }
            else if (_serverConnection.IsCreated)
            {
                _driver.Disconnect(_serverConnection);
                _serverConnection = default;
            }

            if (_driver.IsCreated) _driver.Dispose();
            _lastHeartbeat.Clear();
            _connectionToId.Clear();
            _idToConnection.Clear();
            _nextConnectionId = 0;
            IsInitialized = false;
            Debug.Log("[LobbyTransport] Shutdown");
        }

        public void Dispose() => Shutdown();
    }
}
