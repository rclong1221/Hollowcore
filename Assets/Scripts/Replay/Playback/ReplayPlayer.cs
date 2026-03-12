using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.Replay
{
    /// <summary>
    /// EPIC 18.10: MonoBehaviour that loads and plays .digreplay files.
    /// Instantiates proxy GameObjects for each recorded entity and drives
    /// their transforms frame-by-frame with interpolation.
    /// Does NOT create an ECS World. Pure managed playback.
    /// </summary>
    public class ReplayPlayer : MonoBehaviour
    {
        public static ReplayPlayer Instance { get; private set; }

        [Header("Config")]
        [SerializeField] private GameObject _entityProxyPrefab;

        // Loaded replay data
        private ReplayFileHeader _header;
        private List<ReplayFrame> _frames;
        private List<ReplayEventData> _allEvents;
        private List<ReplayPlayerInfo> _playerInfos;

        // Playback state
        private ReplayState _state = ReplayState.Idle;
        private float _playbackTime;
        private float _playbackSpeed = 1f;
        private int _currentFrameIndex;

        // Entity proxy management
        private readonly Dictionary<ushort, GameObject> _entityProxies = new();

        // Reusable collections (avoid per-frame allocations)
        private readonly List<ushort> _staleIds = new();
        private readonly Dictionary<ushort, EntityComponentData> _interpolatedData = new();

        // Public API
        public ReplayState State => _state;
        public float CurrentTime => _playbackTime;
        public float Duration => _header.DurationSeconds;
        public float PlaybackSpeed => _playbackSpeed;
        public int FrameCount => _frames?.Count ?? 0;
        public int CurrentFrame => _currentFrameIndex;
        public ReplayFileHeader Header => _header;
        public List<ReplayPlayerInfo> Players => _playerInfos;
        public bool IsLoaded => _frames != null && _frames.Count > 0;
        public bool IsLoading { get; private set; }

        // Events for UI binding
        public event Action OnPlaybackStarted;
        public event Action OnPlaybackPaused;
        public event Action OnPlaybackStopped;
        public event Action<float> OnTimeChanged;
        public event Action<int> OnFrameChanged;
        public event Action OnLoadComplete;
        public event Action<string> OnLoadFailed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            CleanupProxies();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() { Instance = null; }

        /// <summary>
        /// Load a .digreplay file asynchronously to avoid blocking the main thread.
        /// Fires OnLoadComplete or OnLoadFailed when done.
        /// Returns true if loading started (false if already loading or file missing).
        /// </summary>
        public bool LoadAsync(string filePath)
        {
            if (IsLoading) return false;
            if (!File.Exists(filePath))
            {
                Debug.LogError($"[ReplayPlayer] File not found: {filePath}");
                OnLoadFailed?.Invoke("File not found");
                return false;
            }

            IsLoading = true;
            Task.Run(() => LoadFileData(filePath)).ContinueWith(task =>
            {
                // Marshal back to main thread via Unity's synchronization context
                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    IsLoading = false;
                    if (task.IsFaulted)
                    {
                        Debug.LogError($"[ReplayPlayer] Async load failed: {task.Exception?.InnerException?.Message}");
                        OnLoadFailed?.Invoke(task.Exception?.InnerException?.Message ?? "Unknown error");
                    }
                    else if (task.Result != null)
                    {
                        ApplyLoadedData(task.Result);
                        OnLoadComplete?.Invoke();
                    }
                });
            });
            return true;
        }

        /// <summary>
        /// Load a .digreplay file synchronously (for editor or quick loads).
        /// Returns true if successful.
        /// </summary>
        public bool Load(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogError($"[ReplayPlayer] File not found: {filePath}");
                return false;
            }

            try
            {
                var data = LoadFileData(filePath);
                if (data == null) return false;
                ApplyLoadedData(data);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ReplayPlayer] Failed to load replay: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Pure data loading — safe to call from background thread.
        /// Returns null on failure.
        /// </summary>
        private LoadedReplayData LoadFileData(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536);
            using var br = new BinaryReader(fs);

            // Read header (64 bytes)
            var header = new ReplayFileHeader
            {
                Magic = br.ReadUInt32(),
                FormatVersion = br.ReadUInt16(),
                TickRate = br.ReadUInt16(),
                StartTimestampUnix = br.ReadInt64(),
                DurationSeconds = br.ReadSingle(),
                TotalFrames = br.ReadUInt32(),
                PeakEntityCount = br.ReadUInt16(),
                PlayerCount = br.ReadByte(),
                MapHash = br.ReadUInt32(),
                CRC32 = br.ReadUInt32()
            };

            // Skip padding to 64 bytes
            int headerRead = 4 + 2 + 2 + 8 + 4 + 4 + 2 + 1 + 4 + 4; // 35
            br.ReadBytes(64 - headerRead);

            // Validate magic
            if (header.Magic != ReplayFileHeader.MagicValue)
                throw new InvalidDataException("Invalid replay file: bad magic number.");

            // Read player info table
            var playerInfos = new List<ReplayPlayerInfo>();
            int playerCount = br.ReadByte();
            for (int i = 0; i < playerCount; i++)
            {
                playerInfos.Add(new ReplayPlayerInfo
                {
                    NetworkId = br.ReadInt32(),
                    GhostId = br.ReadUInt16(),
                    TeamId = br.ReadByte()
                });
            }

            // Read frames
            var frames = new List<ReplayFrame>();
            var allEvents = new List<ReplayEventData>();
            Dictionary<ushort, EntityComponentData> lastKeyframe = null;
            var deltaApplyBuffer = new Dictionary<ushort, EntityComponentData>();

            while (fs.Position < fs.Length)
            {
                // Read frame header
                uint tick = br.ReadUInt32();
                var frameType = (ReplayFrameType)br.ReadByte();
                ushort entityCount = br.ReadUInt16();
                ushort eventCount = br.ReadUInt16();
                int dataSizeBytes = br.ReadInt32();

                var frame = new ReplayFrame
                {
                    Tick = tick,
                    FrameType = frameType,
                    EntityData = new Dictionary<ushort, EntityComponentData>(),
                    Events = new List<ReplayEventData>()
                };

                // Read entity snapshots
                for (int i = 0; i < entityCount; i++)
                {
                    ushort entityId = br.ReadUInt16();
                    byte prefabTypeId = br.ReadByte();
                    ushort entityDataSize = br.ReadUInt16();

                    var data = new EntityComponentData
                    {
                        Position = new float3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()),
                        Rotation = new quaternion(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle()),
                        Velocity = new float3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()),
                        HealthCurrent = br.ReadSingle(),
                        HealthMax = br.ReadSingle(),
                        DeathPhase = br.ReadByte()
                    };

                    frame.EntityData[entityId] = data;
                }

                // Read events
                for (int i = 0; i < eventCount; i++)
                {
                    var evt = new ReplayEventData
                    {
                        EventType = (ReplayEventType)br.ReadByte(),
                        Tick = br.ReadUInt32(),
                        SourceEntityId = br.ReadUInt16(),
                        TargetEntityId = br.ReadUInt16(),
                        Position = new float3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()),
                        Value = br.ReadSingle()
                    };
                    frame.Events.Add(evt);
                    allEvents.Add(evt);
                }

                // For delta frames, reconstruct full entity data
                if (frameType == ReplayFrameType.Keyframe)
                {
                    lastKeyframe = new Dictionary<ushort, EntityComponentData>(frame.EntityData);
                }
                else if (lastKeyframe != null)
                {
                    var deltaList = new List<KeyValuePair<ushort, EntityComponentData>>();
                    foreach (var kvp in frame.EntityData)
                        deltaList.Add(kvp);
                    DeltaEncoder.ApplyDelta(lastKeyframe, deltaList, deltaApplyBuffer);
                    // Copy result into frame's EntityData
                    frame.EntityData.Clear();
                    foreach (var kvp in deltaApplyBuffer)
                        frame.EntityData[kvp.Key] = kvp.Value;
                    // Update lastKeyframe for next delta
                    lastKeyframe.Clear();
                    foreach (var kvp in frame.EntityData)
                        lastKeyframe[kvp.Key] = kvp.Value;
                }

                frames.Add(frame);
            }

            return new LoadedReplayData
            {
                Header = header,
                Frames = frames,
                AllEvents = allEvents,
                PlayerInfos = playerInfos
            };
        }

        private void ApplyLoadedData(LoadedReplayData data)
        {
            _header = data.Header;
            _frames = data.Frames;
            _allEvents = data.AllEvents;
            _playerInfos = data.PlayerInfos;
            Debug.Log($"[ReplayPlayer] Loaded replay: {_frames.Count} frames, {_allEvents.Count} events, {_playerInfos.Count} players");
        }

        /// <summary>
        /// Simple main-thread dispatcher for marshalling async results.
        /// </summary>
        private static class UnityMainThreadDispatcher
        {
            private static readonly Queue<Action> _pending = new();
            private static MonoBehaviour _runner;

            public static void Enqueue(Action action)
            {
                lock (_pending)
                    _pending.Enqueue(action);
            }

            public static void ProcessQueue()
            {
                lock (_pending)
                {
                    while (_pending.Count > 0)
                        _pending.Dequeue()?.Invoke();
                }
            }
        }

        /// <summary>
        /// Internal data container for background thread loading.
        /// </summary>
        private class LoadedReplayData
        {
            public ReplayFileHeader Header;
            public List<ReplayFrame> Frames;
            public List<ReplayEventData> AllEvents;
            public List<ReplayPlayerInfo> PlayerInfos;
        }

        public void Play()
        {
            if (!IsLoaded) return;
            _state = ReplayState.Playing;
            OnPlaybackStarted?.Invoke();
        }

        public void Pause()
        {
            if (_state != ReplayState.Playing) return;
            _state = ReplayState.Paused;
            OnPlaybackPaused?.Invoke();
        }

        public void Stop()
        {
            _state = ReplayState.Idle;
            _playbackTime = 0;
            _currentFrameIndex = 0;
            CleanupProxies();
            OnPlaybackStopped?.Invoke();
        }

        public void SetSpeed(float speed)
        {
            _playbackSpeed = Mathf.Clamp(speed, 0.1f, 8f);
        }

        /// <summary>
        /// Seek to a normalized time position (0-1).
        /// </summary>
        public void Seek(float normalizedTime)
        {
            if (!IsLoaded) return;

            normalizedTime = Mathf.Clamp01(normalizedTime);
            _playbackTime = normalizedTime * _header.DurationSeconds;
            int targetFrame = TimeToFrameIndex(_playbackTime);
            targetFrame = Mathf.Clamp(targetFrame, 0, _frames.Count - 1);

            _currentFrameIndex = targetFrame;
            ApplyFrame(targetFrame);
            OnTimeChanged?.Invoke(normalizedTime);
            OnFrameChanged?.Invoke(targetFrame);
        }

        public void StepForward()
        {
            if (!IsLoaded) return;
            if (_state == ReplayState.Playing) Pause();

            int nextFrame = Mathf.Min(_currentFrameIndex + 1, _frames.Count - 1);
            _currentFrameIndex = nextFrame;
            _playbackTime = FrameIndexToTime(nextFrame);
            ApplyFrame(nextFrame);
            OnFrameChanged?.Invoke(nextFrame);
            OnTimeChanged?.Invoke(_playbackTime / _header.DurationSeconds);
        }

        public void StepBackward()
        {
            if (!IsLoaded) return;
            if (_state == ReplayState.Playing) Pause();

            int prevFrame = Mathf.Max(_currentFrameIndex - 1, 0);
            _currentFrameIndex = prevFrame;
            _playbackTime = FrameIndexToTime(prevFrame);
            ApplyFrame(prevFrame);
            OnFrameChanged?.Invoke(prevFrame);
            OnTimeChanged?.Invoke(_playbackTime / _header.DurationSeconds);
        }

        public ReplayEventData[] GetEvents() => _allEvents?.ToArray();

        private void Update()
        {
            // Process any pending async callbacks
            UnityMainThreadDispatcher.ProcessQueue();

            if (_state != ReplayState.Playing || !IsLoaded) return;

            _playbackTime += Time.deltaTime * _playbackSpeed;
            int targetFrame = TimeToFrameIndex(_playbackTime);

            if (targetFrame >= _frames.Count)
            {
                Stop();
                return;
            }

            if (targetFrame != _currentFrameIndex)
            {
                _currentFrameIndex = targetFrame;

                // Interpolate between current and next frame
                if (targetFrame + 1 < _frames.Count)
                {
                    var fromFrame = _frames[targetFrame];
                    var toFrame = _frames[targetFrame + 1];
                    uint targetTick = (uint)(_playbackTime * _header.TickRate);

                    FrameInterpolator.Interpolate(
                        fromFrame.EntityData, toFrame.EntityData,
                        fromFrame.Tick, toFrame.Tick, targetTick,
                        _interpolatedData);
                    ApplyEntityData(_interpolatedData);
                }
                else
                {
                    ApplyFrame(targetFrame);
                }

                OnFrameChanged?.Invoke(targetFrame);
                OnTimeChanged?.Invoke(_playbackTime / _header.DurationSeconds);
            }
        }

        private void ApplyFrame(int frameIndex)
        {
            if (frameIndex < 0 || frameIndex >= _frames.Count) return;
            ApplyEntityData(_frames[frameIndex].EntityData);
        }

        private void ApplyEntityData(Dictionary<ushort, EntityComponentData> entityData)
        {
            // Find stale proxies (entities no longer in frame) using reusable list
            _staleIds.Clear();
            foreach (var kvp in _entityProxies)
            {
                if (!entityData.ContainsKey(kvp.Key))
                    _staleIds.Add(kvp.Key);
            }

            // Remove stale proxies
            foreach (var id in _staleIds)
            {
                if (_entityProxies.TryGetValue(id, out var staleProxy))
                {
                    Destroy(staleProxy);
                    _entityProxies.Remove(id);
                }
            }

            // Update or create proxies
            foreach (var kvp in entityData)
            {
                if (!_entityProxies.TryGetValue(kvp.Key, out var proxy))
                {
                    proxy = _entityProxyPrefab != null
                        ? Instantiate(_entityProxyPrefab)
                        : GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    proxy.name = $"ReplayEntity_{kvp.Key}";
                    _entityProxies[kvp.Key] = proxy;
                }

                proxy.transform.SetPositionAndRotation(kvp.Value.Position, kvp.Value.Rotation);
            }
        }

        private void CleanupProxies()
        {
            foreach (var kvp in _entityProxies)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value);
            }
            _entityProxies.Clear();
        }

        private int TimeToFrameIndex(float time)
        {
            if (_frames == null || _frames.Count == 0) return 0;
            float ticksPerSecond = _header.TickRate;
            uint targetTick = (uint)(time * ticksPerSecond);

            // Binary search for nearest frame
            int lo = 0, hi = _frames.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (_frames[mid].Tick <= targetTick)
                    lo = mid;
                else
                    hi = mid - 1;
            }
            return lo;
        }

        private float FrameIndexToTime(int frameIndex)
        {
            if (_frames == null || frameIndex < 0 || frameIndex >= _frames.Count) return 0f;
            return _frames[frameIndex].Tick / (float)_header.TickRate;
        }
    }
}
