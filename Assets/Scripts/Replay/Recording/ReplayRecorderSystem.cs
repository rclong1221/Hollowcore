using System;
using System.Collections.Generic;
using System.IO;
using Player.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace DIG.Replay
{
    /// <summary>
    /// EPIC 18.10: Server-side replay recorder.
    /// Captures ghost entity snapshots at configurable tick intervals,
    /// delta-encodes them, writes to ring buffer, and flushes to disk
    /// via ReplaySerializer's background thread.
    ///
    /// SystemBase (not ISystem) because it accesses managed types:
    /// Dictionary, BinaryWriter, List, ReplaySerializer static methods.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class ReplayRecorderSystem : SystemBase
    {
        private EntityQuery _ghostQuery;
        private EntityQuery _playerQuery;

        // Cached component lookups (refreshed per-frame via .Update)
        private ComponentLookup<Health> _healthLookup;
        private ComponentLookup<DeathState> _deathLookup;
        private ComponentLookup<PhysicsVelocity> _velocityLookup;

        private ReplayConfigSO _config;
        private bool _isRecording;
        private uint _startTick;
        private int _frameCount;
        private int _ticksSinceLastRecord;
        private float _timeSinceLastFlush;

        // Ring buffer of serialized frame byte arrays
        private List<byte[]> _ringBuffer;
        private int _ringWriteIndex;
        private int _maxRingFrames;

        // Delta tracking
        private Dictionary<ushort, EntityComponentData> _lastKeyframe;
        private int _framesSinceKeyframe;

        // Current recording file path
        private string _currentFilePath;

        // Player info table
        private List<ReplayPlayerInfo> _playerInfos;

        // Pending events for the current frame
        private List<ReplayEventData> _pendingEvents;

        // Previous frame health for death/kill detection
        private Dictionary<ushort, float> _prevHealth;

        // Reusable frame data dictionary (cleared each frame instead of new allocation)
        private Dictionary<ushort, EntityComponentData> _currentFrame;

        // Reusable serialization stream (reset position instead of new allocation)
        private MemoryStream _serializeStream;
        private BinaryWriter _serializeWriter;

        // Reusable delta list (passed to DeltaEncoder instead of allocating)
        private List<KeyValuePair<ushort, EntityComponentData>> _deltaBuffer;

        public bool IsRecording => _isRecording;

        protected override void OnCreate()
        {
            _ghostQuery = GetEntityQuery(
                ComponentType.ReadOnly<GhostInstance>(),
                ComponentType.ReadOnly<LocalTransform>()
            );

            _playerQuery = GetEntityQuery(
                ComponentType.ReadOnly<GhostOwner>(),
                ComponentType.ReadOnly<GhostInstance>()
            );

            // Cache component lookups
            _healthLookup = GetComponentLookup<Health>(true);
            _deathLookup = GetComponentLookup<DeathState>(true);
            _velocityLookup = GetComponentLookup<PhysicsVelocity>(true);

            _ringBuffer = new List<byte[]>();
            _lastKeyframe = new Dictionary<ushort, EntityComponentData>();
            _playerInfos = new List<ReplayPlayerInfo>();
            _pendingEvents = new List<ReplayEventData>();
            _prevHealth = new Dictionary<ushort, float>();
            _currentFrame = new Dictionary<ushort, EntityComponentData>();
            _deltaBuffer = new List<KeyValuePair<ushort, EntityComponentData>>();

            // Pre-allocate serialization stream (4KB initial, grows as needed)
            _serializeStream = new MemoryStream(4096);
            _serializeWriter = new BinaryWriter(_serializeStream);

            _config = Resources.Load<ReplayConfigSO>("ReplayConfig");
        }

        protected override void OnDestroy()
        {
            if (_isRecording)
                StopRecording();

            _serializeWriter?.Dispose();
            _serializeStream?.Dispose();
        }

        protected override void OnUpdate()
        {
            if (_config == null || !_config.RecordingEnabled) return;

            // Auto-start recording if configured
            if (!_isRecording && _config.AutoRecord && _ghostQuery.CalculateEntityCount() > 0)
                StartRecording();

            if (!_isRecording) return;

            // Check tick interval
            _ticksSinceLastRecord++;
            if (_ticksSinceLastRecord < _config.TickInterval) return;
            _ticksSinceLastRecord = 0;

            // CRITICAL: Complete any pending jobs writing LocalTransform/Health
            CompleteDependency();

            // Refresh cached lookups for this frame
            _healthLookup.Update(this);
            _deathLookup.Update(this);
            _velocityLookup.Update(this);

            CaptureFrame();

            // Check flush interval
            _timeSinceLastFlush += SystemAPI.Time.DeltaTime * _config.TickInterval;
            if (_timeSinceLastFlush >= _config.FlushIntervalSeconds)
            {
                FlushToDisk();
                _timeSinceLastFlush = 0f;
            }

            // Check max duration
            float elapsedSeconds = _frameCount * _config.TickInterval / 60f;
            if (elapsedSeconds >= _config.MaxDurationMinutes * 60f)
                StopRecording();
        }

        public void StartRecording()
        {
            if (_isRecording) return;

            _isRecording = true;
            _frameCount = 0;
            _ticksSinceLastRecord = 0;
            _timeSinceLastFlush = 0f;
            _framesSinceKeyframe = 0;
            _lastKeyframe.Clear();
            _ringBuffer.Clear();
            _ringWriteIndex = 0;
            _prevHealth.Clear();

            // Calculate ring buffer capacity
            int tickRate = 60; // approximate
            _maxRingFrames = Mathf.Max(64, (int)(_config.RingBufferSeconds * tickRate / _config.TickInterval));

            // Build file path
            string dir = Path.Combine(Application.persistentDataPath, _config.SaveSubdirectory);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _currentFilePath = Path.Combine(dir, $"replay_{timestamp}.digreplay");

            // Start serializer thread
            ReplaySerializer.Start();

            // Capture player info
            CapturePlayerInfo();

            // Write initial header
            WriteHeader();

            _startTick = SystemAPI.HasSingleton<NetworkTime>()
                ? SystemAPI.GetSingleton<NetworkTime>().ServerTick.TickIndexForValidTick
                : 0;

            Debug.Log($"[ReplayRecorder] Recording started: {_currentFilePath}");
        }

        public void StopRecording()
        {
            if (!_isRecording) return;

            _isRecording = false;

            // Flush remaining data
            FlushToDisk();

            // Update header with final stats and finalize file
            byte[] headerBytes = SerializeHeader();
            ReplaySerializer.EnqueueFlush(_currentFilePath, headerBytes, isHeader: false, isFinal: true);

            Debug.Log($"[ReplayRecorder] Recording stopped. Frames: {_frameCount}");
        }

        private void CapturePlayerInfo()
        {
            _playerInfos.Clear();

            if (_playerQuery.CalculateEntityCount() == 0) return;

            var entities = _playerQuery.ToEntityArray(Allocator.Temp);
            var owners = _playerQuery.ToComponentDataArray<GhostOwner>(Allocator.Temp);
            var ghosts = _playerQuery.ToComponentDataArray<GhostInstance>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                _playerInfos.Add(new ReplayPlayerInfo
                {
                    NetworkId = owners[i].NetworkId,
                    GhostId = (ushort)ghosts[i].ghostId,
                    TeamId = 0
                });
            }

            entities.Dispose();
            owners.Dispose();
            ghosts.Dispose();
        }

        private void CaptureFrame()
        {
            var entities = _ghostQuery.ToEntityArray(Allocator.Temp);
            var transforms = _ghostQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var ghosts = _ghostQuery.ToComponentDataArray<GhostInstance>(Allocator.Temp);

            _currentFrame.Clear();
            _pendingEvents.Clear();

            uint currentTick = SystemAPI.HasSingleton<NetworkTime>()
                ? SystemAPI.GetSingleton<NetworkTime>().ServerTick.TickIndexForValidTick
                : (uint)_frameCount;

            for (int i = 0; i < entities.Length; i++)
            {
                var ghostId = (ushort)ghosts[i].ghostId;
                var data = new EntityComponentData
                {
                    Position = transforms[i].Position,
                    Rotation = transforms[i].Rotation,
                    Velocity = float3.zero
                };

                if (_velocityLookup.HasComponent(entities[i]))
                    data.Velocity = _velocityLookup[entities[i]].Linear;

                if (_healthLookup.HasComponent(entities[i]))
                {
                    var h = _healthLookup[entities[i]];
                    data.HealthCurrent = h.Current;
                    data.HealthMax = h.Max;

                    // Detect death events
                    if (_prevHealth.TryGetValue(ghostId, out float prevHp) && prevHp > 0f && h.Current <= 0f)
                    {
                        _pendingEvents.Add(new ReplayEventData
                        {
                            EventType = ReplayEventType.Death,
                            Tick = currentTick,
                            TargetEntityId = ghostId,
                            Position = transforms[i].Position,
                            Value = prevHp
                        });
                    }

                    _prevHealth[ghostId] = h.Current;
                }

                if (_deathLookup.HasComponent(entities[i]))
                    data.DeathPhase = (byte)_deathLookup[entities[i]].Phase;

                _currentFrame[ghostId] = data;
            }

            entities.Dispose();
            transforms.Dispose();
            ghosts.Dispose();

            // Determine frame type
            bool isKeyframe = _framesSinceKeyframe >= _config.KeyframeInterval || _frameCount == 0;

            // Serialize frame
            byte[] frameBytes = SerializeFrame(_currentFrame, isKeyframe, currentTick);
            WriteToRingBuffer(frameBytes);

            if (isKeyframe)
            {
                // Reuse _lastKeyframe dictionary: clear and copy instead of new allocation
                _lastKeyframe.Clear();
                foreach (var kvp in _currentFrame)
                    _lastKeyframe[kvp.Key] = kvp.Value;
                _framesSinceKeyframe = 0;
            }
            else
            {
                _framesSinceKeyframe++;
            }

            _frameCount++;
        }

        private byte[] SerializeFrame(
            Dictionary<ushort, EntityComponentData> frame,
            bool isKeyframe,
            uint tick)
        {
            // Reset stream position instead of creating new MemoryStream
            _serializeStream.Position = 0;
            _serializeStream.SetLength(0);

            // Determine entities to write
            ICollection<KeyValuePair<ushort, EntityComponentData>> entitiesToWrite;

            if (isKeyframe || !_config.DeltaEncoding)
            {
                entitiesToWrite = frame;
            }
            else
            {
                DeltaEncoder.EncodeDelta(frame, _lastKeyframe, _deltaBuffer);
                entitiesToWrite = _deltaBuffer;
            }

            // Frame header
            _serializeWriter.Write(tick);
            _serializeWriter.Write((byte)(isKeyframe ? ReplayFrameType.Keyframe : ReplayFrameType.DeltaFrame));
            _serializeWriter.Write((ushort)entitiesToWrite.Count);
            _serializeWriter.Write((ushort)_pendingEvents.Count);
            _serializeWriter.Write(0); // DataSizeBytes placeholder

            long dataSizeStart = _serializeStream.Position;

            // Entity snapshots
            foreach (var kvp in entitiesToWrite)
            {
                _serializeWriter.Write(kvp.Key); // EntityId
                _serializeWriter.Write((byte)0); // PrefabTypeId
                _serializeWriter.Write((ushort)49); // DataSizeBytes (fixed)

                _serializeWriter.Write(kvp.Value.Position.x);
                _serializeWriter.Write(kvp.Value.Position.y);
                _serializeWriter.Write(kvp.Value.Position.z);
                _serializeWriter.Write(kvp.Value.Rotation.value.x);
                _serializeWriter.Write(kvp.Value.Rotation.value.y);
                _serializeWriter.Write(kvp.Value.Rotation.value.z);
                _serializeWriter.Write(kvp.Value.Rotation.value.w);
                _serializeWriter.Write(kvp.Value.Velocity.x);
                _serializeWriter.Write(kvp.Value.Velocity.y);
                _serializeWriter.Write(kvp.Value.Velocity.z);
                _serializeWriter.Write(kvp.Value.HealthCurrent);
                _serializeWriter.Write(kvp.Value.HealthMax);
                _serializeWriter.Write(kvp.Value.DeathPhase);
            }

            // Events
            foreach (var evt in _pendingEvents)
            {
                _serializeWriter.Write((byte)evt.EventType);
                _serializeWriter.Write(evt.Tick);
                _serializeWriter.Write(evt.SourceEntityId);
                _serializeWriter.Write(evt.TargetEntityId);
                _serializeWriter.Write(evt.Position.x);
                _serializeWriter.Write(evt.Position.y);
                _serializeWriter.Write(evt.Position.z);
                _serializeWriter.Write(evt.Value);
            }

            // Patch DataSizeBytes
            long dataSizeEnd = _serializeStream.Position;
            int dataSize = (int)(dataSizeEnd - dataSizeStart);
            _serializeStream.Position = dataSizeStart - 4; // 4 bytes before dataSizeStart
            _serializeWriter.Write(dataSize);
            _serializeStream.Position = dataSizeEnd;

            // Copy to byte[] for enqueue (ToArray copies only written portion)
            _serializeWriter.Flush();
            return _serializeStream.ToArray();
        }

        private void WriteToRingBuffer(byte[] frameBytes)
        {
            if (_ringWriteIndex >= _ringBuffer.Count)
                _ringBuffer.Add(frameBytes);
            else
                _ringBuffer[_ringWriteIndex] = frameBytes;

            _ringWriteIndex = (_ringWriteIndex + 1) % _maxRingFrames;
        }

        private void FlushToDisk()
        {
            if (_ringBuffer.Count == 0) return;

            // Collect all ring buffer data into a single byte array
            int totalBytes = 0;
            foreach (var frame in _ringBuffer)
                totalBytes += frame.Length;

            byte[] combined = new byte[totalBytes];
            int offset = 0;
            foreach (var frame in _ringBuffer)
            {
                Buffer.BlockCopy(frame, 0, combined, offset, frame.Length);
                offset += frame.Length;
            }

            ReplaySerializer.EnqueueFlush(_currentFilePath, combined);

            _ringBuffer.Clear();
            _ringWriteIndex = 0;
        }

        private void WriteHeader()
        {
            byte[] headerBytes = SerializeHeader();
            ReplaySerializer.EnqueueFlush(_currentFilePath, headerBytes, isHeader: true);
        }

        private byte[] SerializeHeader()
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // 64-byte header
            bw.Write(ReplayFileHeader.MagicValue);     // 4
            bw.Write(ReplayFileHeader.CurrentVersion);  // 2
            bw.Write((ushort)60);                       // 2 TickRate
            bw.Write(DateTimeOffset.UtcNow.ToUnixTimeSeconds()); // 8
            bw.Write(_frameCount * _config.TickInterval / 60f);  // 4 DurationSeconds
            bw.Write((uint)_frameCount);                // 4
            bw.Write((ushort)_ghostQuery.CalculateEntityCount()); // 2
            bw.Write((byte)_playerInfos.Count);         // 1
            bw.Write((uint)0);                          // 4 MapHash
            bw.Write((uint)0);                          // 4 CRC32 (patched on finalize)
            // Pad to 64 bytes
            int written = 4 + 2 + 2 + 8 + 4 + 4 + 2 + 1 + 4 + 4; // = 35
            for (int i = written; i < 64; i++)
                bw.Write((byte)0);

            // Player info table
            bw.Write((byte)_playerInfos.Count);
            foreach (var p in _playerInfos)
            {
                bw.Write(p.NetworkId);
                bw.Write(p.GhostId);
                bw.Write(p.TeamId);
            }

            return ms.ToArray();
        }
    }
}
