using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine.Profiling;
using Unity.Profiling;
using DIG.Voxel.Debug;

namespace DIG.Performance
{
    /// <summary>
    /// Captures performance metrics over time and generates a text report for analysis.
    ///
    /// Usage:
    /// 1. Add this component to a GameObject
    /// 2. Press F9 (or configured key) to start/stop capture
    /// 3. After capture, click "Copy Report to Clipboard" in Inspector
    /// 4. Paste the report for analysis
    /// </summary>
    public class PerformanceCaptureSession : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Header("Capture Settings")]
        [Tooltip("Duration in seconds to capture data")]
        [SerializeField] private float _captureDuration = 30f;

        [Tooltip("Key to toggle capture on/off")]
        [SerializeField] private KeyCode _toggleKey = KeyCode.F9;

        [Tooltip("Budget configuration for threshold comparisons")]
        [SerializeField] private VoxelPerformanceBudget _budgetConfig;

        [Header("Runtime State")]
        [SerializeField] private bool _isCapturing;
        [SerializeField] private float _captureProgress;
        [SerializeField] private int _framesCaptured;

        // Ring buffer for frame data (10 minutes at 60 FPS = 36000 frames)
        private const int MAX_FRAMES = 36000;
        private FrameSnapshot[] _frameBuffer;
        private int _writeIndex;
        private int _frameCount;

        // System timing accumulators
        private Dictionary<string, SystemTiming> _systemTimings;

        // Memory sampling for trend analysis
        private List<MemorySnapshot> _memorySamples;
        private float _lastMemorySampleTime;
        private const float MEMORY_SAMPLE_INTERVAL = 1f;

        // Frame timing (requires Unity 6+ or frame timing stats enabled)
        private FrameTiming[] _frameTimings;

        // ProfilerRecorders for rendering stats
        private ProfilerRecorder _drawCallsRecorder;
        private ProfilerRecorder _batchesRecorder;
        private ProfilerRecorder _trianglesRecorder;
        private ProfilerRecorder _setPassRecorder;

        // ProfilerRecorders for memory allocation tracking
        private ProfilerRecorder _gcAllocRecorder;
        private ProfilerRecorder _gcAllocCountRecorder;

        // ProfilerRecorders for voxel systems (from VoxelProfilerMarkers)
        private Dictionary<string, ProfilerRecorder> _voxelSystemRecorders;

        // Per-frame allocation tracking
        private long _gcAllocStart;
        private List<long> _frameAllocations;
        private Dictionary<string, long> _systemAllocations;

        // Capture timing
        private float _captureStartTime;
        private float _captureEndTime;

        // Initial GC counts
        private int _initialGCGen0;
        private int _initialGCGen1;
        private int _initialGCGen2;

        // Initial memory
        private long _initialManagedHeap;
        private long _initialNativeAlloc;

        // Generated report (cached)
        private string _lastReport;
        private Dictionary<string, float> _tempTimings;
        public string LastReport => _lastReport;
        public bool IsCapturing => _isCapturing;
        public float CaptureProgress => _captureProgress;

        private bool _initialized;

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (_initialized) return;
            _frameBuffer = new FrameSnapshot[MAX_FRAMES];
            _systemTimings = new Dictionary<string, SystemTiming>(32);
            _memorySamples = new List<MemorySnapshot>(128);
            _frameTimings = new FrameTiming[1];
            _frameAllocations = new List<long>(MAX_FRAMES);
            _systemAllocations = new Dictionary<string, long>(32);
            _voxelSystemRecorders = new Dictionary<string, ProfilerRecorder>(32);
            _initialized = true;
        }

        private void OnEnable()
        {
            Initialize();
            InitializeRecorders();
        }

        private void OnDisable()
        {
            DisposeRecorders();
            if (_isCapturing)
            {
                StopCapture();
            }
        }

        private void InitializeRecorders()
        {
            // Rendering stats
            _drawCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _batchesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            _trianglesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            _setPassRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");

            // Memory allocation tracking
            _gcAllocRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            _gcAllocCountRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocation In Frame Count");

            // Voxel system recorders (from VoxelProfilerMarkers)
            InitializeVoxelSystemRecorders();
        }

        private void InitializeVoxelSystemRecorders()
        {
            // Core voxel systems
            TryAddRecorder("ChunkStreaming", "DIG.Voxel.ChunkStreaming");
            TryAddRecorder("ChunkVisibility", "DIG.Voxel.ChunkVisibility");
            TryAddRecorder("ChunkPhysics", "DIG.Voxel.ChunkPhysics");
            TryAddRecorder("DecoratorSpawn", "DIG.Voxel.DecoratorSpawn");
            TryAddRecorder("FluidSimulation", "DIG.Voxel.FluidSimulation");
            TryAddRecorder("FluidMesh", "DIG.Voxel.FluidMesh");
            TryAddRecorder("VoxelInteraction", "DIG.Voxel.Interaction");
            TryAddRecorder("VoxelExplosion", "DIG.Voxel.Explosion");
            TryAddRecorder("ChunkLookup", "DIG.Voxel.ChunkLookup");
            TryAddRecorder("FrameBudget", "DIG.Voxel.FrameBudget");
            TryAddRecorder("CameraData", "DIG.Voxel.CameraData");
            TryAddRecorder("VoxelModification", "DIG.Voxel.Modification");
            TryAddRecorder("VoxelBatching", "DIG.Voxel.Batching");
            TryAddRecorder("DecoratorInstancing", "DIG.Voxel.DecoratorInstancing");
            TryAddRecorder("ColliderBuild", "DIG.Voxel.ColliderBuild");
            
            // === UNITY ECS SYSTEM GROUPS (High Level) ===
            TryAddRecorder("ECS.PhysicsGroup", "PhysicsSystemGroup"); // Totals Client+Server
            TryAddRecorder("ECS.PhysicsSim", "PhysicsSimulationGroup"); // Narrowphase/Collision
            TryAddRecorder("ECS.PhysicsSolve", "PhysicsSolveAndIntegrateGroup"); // Solver
            TryAddRecorder("ECS.GhostSim", "GhostSimulationSystemGroup"); // NetCode
            TryAddRecorder("ECS.Prediction", "PredictedSimulationSystemGroup"); // Prediction
            
            // === UNITY ECS / DOTS ===
            TryAddRecorderCategory("ECS.World.Update", ProfilerCategory.Scripts, "WorldTimeUpdate");
            TryAddRecorderCategory("ECS.SimulationGroup", ProfilerCategory.Scripts, "SimulationSystemGroup");
            TryAddRecorderCategory("ECS.PresentationGroup", ProfilerCategory.Scripts, "PresentationSystemGroup");
            TryAddRecorderCategory("ECS.InitGroup", ProfilerCategory.Scripts, "InitializationSystemGroup");
            TryAddRecorderCategory("ECS.FixedStep", ProfilerCategory.Scripts, "FixedStepSimulationSystemGroup");
            TryAddRecorderCategory("ECS.BeginFrame", ProfilerCategory.Scripts, "BeginFrameSystemGroup");
            TryAddRecorderCategory("ECS.EndFrame", ProfilerCategory.Scripts, "EndFrameSystemGroup");
            TryAddRecorderCategory("ECS.Structural", ProfilerCategory.Scripts, "StructuralChanges");
            
            // === UNITY PHYSICS ===
            TryAddRecorderCategory("Physics.Simulate", ProfilerCategory.Scripts, "Physics.Simulate");
            TryAddRecorderCategory("Physics.FetchResults", ProfilerCategory.Scripts, "Physics.FetchResults");
            // Also try legacy
            TryAddRecorderCategory("Physics.Step", ProfilerCategory.Physics, "Physics.Simulate");
            
            TryAddRecorderCategory("Physics.BuildBroadphase", ProfilerCategory.Physics, "BuildBroadphase");
            TryAddRecorderCategory("Physics.Broadphase", ProfilerCategory.Physics, "Broadphase");
            TryAddRecorderCategory("Physics.NarrowPhase", ProfilerCategory.Physics, "NarrowPhase");
            TryAddRecorderCategory("Physics.Solver", ProfilerCategory.Physics, "Solver");
            TryAddRecorderCategory("Physics.Integrate", ProfilerCategory.Physics, "IntegrateMotionsJob");
            TryAddRecorderCategory("Physics.Colliders", ProfilerCategory.Physics, "CreateColliders");
            TryAddRecorderCategory("Physics.Contacts", ProfilerCategory.Physics, "CreateContacts");
            
            // === RENDERING ===
            TryAddRecorderCategory("Render.Culling", ProfilerCategory.Render, "Culling");
            TryAddRecorderCategory("Render.Draw", ProfilerCategory.Render, "Drawing");
            TryAddRecorderCategory("Render.Shadows", ProfilerCategory.Render, "Shadows.Draw");
            TryAddRecorderCategory("Render.Transparents", ProfilerCategory.Render, "Render.TransparentGeometry");
            TryAddRecorderCategory("Render.Opaque", ProfilerCategory.Render, "Render.OpaqueGeometry");
            TryAddRecorderCategory("Render.CommandBuffer", ProfilerCategory.Render, "CommandBuffer.Flush");
            TryAddRecorderCategory("Render.PostProcess", ProfilerCategory.Render, "PostProcessing");
            TryAddRecorderCategory("Render.URP", ProfilerCategory.Render, "UniversalRenderPipeline.RenderCamera");
            TryAddRecorderCategory("Render.OpaquePass", ProfilerCategory.Render, "RenderOpaqueForwardPass");
            TryAddRecorderCategory("Render.ShadowPass", ProfilerCategory.Render, "MainLightShadowmapPass");
            
            // === ANIMATION ===
            TryAddRecorderCategory("Animation", ProfilerCategory.Animation, "Animation");
            TryAddRecorderCategory("Animator.Update", ProfilerCategory.Animation, "Animator.Update");
            TryAddRecorderCategory("Animation.Jobs", ProfilerCategory.Animation, "ProcessAnimationJobs");
            
            // === SCRIPTING / GAME LOGIC ===
            TryAddRecorderCategory("Scripts.Update", ProfilerCategory.Scripts, "Update.ScriptRunBehaviourUpdate");
            TryAddRecorderCategory("Scripts.LateUpdate", ProfilerCategory.Scripts, "LateUpdate.ScriptRunBehaviourLateUpdate");
            TryAddRecorderCategory("Scripts.FixedUpdate", ProfilerCategory.Scripts, "FixedUpdate.ScriptRunBehaviourFixedUpdate");
            TryAddRecorderCategory("Scripts.Coroutines", ProfilerCategory.Scripts, "Coroutines");
            
            // === GUI / UI ===
            TryAddRecorderCategory("UI.Layout", ProfilerCategory.Gui, "UI.Layout");
            TryAddRecorderCategory("UI.Render", ProfilerCategory.Gui, "UI.Render");
            TryAddRecorderCategory("Canvas.Update", ProfilerCategory.Gui, "Canvas.SendWillRenderCanvases");
            
            // === NETCODE FOR ENTITIES (Unity.NetCode) ===
            // Ghost Systems
            TryAddRecorder("NetCode.GhostUpdate", "Unity.NetCode.GhostUpdateSystem");
            TryAddRecorder("NetCode.GhostSend", "Unity.NetCode.GhostSendSystem");
            TryAddRecorder("NetCode.GhostReceive", "Unity.NetCode.GhostReceiveSystem");
            TryAddRecorder("NetCode.GhostCollection", "Unity.NetCode.GhostCollectionSystem");
            TryAddRecorder("NetCode.GhostSpawn", "Unity.NetCode.GhostSpawnSystem");
            TryAddRecorder("NetCode.GhostDespawn", "Unity.NetCode.GhostDespawnSystem");
            
            // Prediction Systems
            TryAddRecorder("NetCode.PredictionGroup", "Unity.NetCode.PredictedSimulationSystemGroup");
            TryAddRecorder("NetCode.PredictionRollback", "Unity.NetCode.GhostPredictionHistorySystem");
            TryAddRecorder("NetCode.PredictionSwitch", "Unity.NetCode.GhostPredictionSwitchingSystem");
            TryAddRecorder("NetCode.PredictionDebug", "Unity.NetCode.GhostPredictionDebugSystem");
            
            // Command Systems
            TryAddRecorder("NetCode.CommandSend", "Unity.NetCode.CommandSendPacketSystem");
            TryAddRecorder("NetCode.CommandReceive", "Unity.NetCode.CommandReceiveSystem");
            TryAddRecorder("NetCode.RpcSend", "Unity.NetCode.RpcSystem");
            TryAddRecorder("NetCode.RpcCommand", "Unity.NetCode.RpcCommandRequestSystem");
            
            // Snapshot/State Systems
            TryAddRecorder("NetCode.SnapshotData", "Unity.NetCode.GhostSnapshotDataSystem");
            TryAddRecorder("NetCode.SnapshotValue", "Unity.NetCode.GhostSnapshotValueSystem");
            TryAddRecorder("NetCode.Interpolation", "Unity.NetCode.GhostInterpolationSystem");
            TryAddRecorder("NetCode.UpdateLen", "Unity.NetCode.UpdateConnectionInterpolationDelay");
            
            // Network Systems
            TryAddRecorder("NetCode.NetworkStream", "Unity.NetCode.NetworkStreamReceiveSystem");
            TryAddRecorder("NetCode.NetworkGroup", "Unity.NetCode.NetworkReceiveSystemGroup");
            TryAddRecorder("NetCode.Connect", "Unity.NetCode.NetworkStreamConnectSystem");
            TryAddRecorder("NetCode.DriverUpdate", "Unity.NetCode.NetworkStreamDriver");
            
            // Client/Server Simulation Groups (these are the big groups)
            TryAddRecorder("NetCode.ClientSim", "Unity.NetCode.ClientSimulationSystemGroup");
            TryAddRecorder("NetCode.ServerSim", "Unity.NetCode.ServerSimulationSystemGroup");
            TryAddRecorder("NetCode.ClientAndServer", "Unity.NetCode.ClientAndServerSimulationSystemGroup");
            TryAddRecorder("NetCode.GhostSimGroup", "Unity.NetCode.GhostSimulationSystemGroup");
            
            // Time/Tick Systems
            TryAddRecorder("NetCode.NetworkTime", "Unity.NetCode.NetworkTimeSystem");
            TryAddRecorder("NetCode.TickRate", "Unity.NetCode.UpdateNetworkTickRateSystem");
            
            // Physics Ghosting
            TryAddRecorder("NetCode.PhysicsGhost", "Unity.NetCode.GhostPhysicsBodySystem");
            TryAddRecorder("NetCode.PhysicsPredict", "Unity.Physics.Systems.PredictedPhysicsSystemGroup");
            TryAddRecorder("NetCode.BuildPhysics", "Unity.Physics.Systems.BuildPhysicsWorld");
            TryAddRecorder("NetCode.StepPhysics", "Unity.Physics.Systems.StepPhysicsWorld");
            TryAddRecorder("NetCode.ExportPhysics", "Unity.Physics.Systems.ExportPhysicsWorld");
            
            // World Updates (top-level)
            TryAddRecorder("World.ServerUpdate", "ServerWorld");
            TryAddRecorder("World.ClientUpdate", "ClientWorld");
            
            // === TRANSFORM SYSTEMS ===
            TryAddRecorderCategory("Transform.Hierarchy", ProfilerCategory.Scripts, "TransformAccessArrayUpdate");
            TryAddRecorder("ECS.LocalToWorld", "Unity.Transforms.LocalToWorldSystem");
            TryAddRecorder("ECS.ParentSystem", "Unity.Transforms.ParentSystem");
        }

        private void TryAddRecorder(string name, string markerName)
        {
            if (_voxelSystemRecorders == null) return;
            var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, markerName);
            _voxelSystemRecorders[name] = recorder;
        }
        
        private void TryAddRecorderCategory(string name, ProfilerCategory category, string markerName)
        {
            if (_voxelSystemRecorders == null) return;
            var recorder = ProfilerRecorder.StartNew(category, markerName);
            _voxelSystemRecorders[name] = recorder;
        }

        private void DisposeRecorders()
        {
            _drawCallsRecorder.Dispose();
            _batchesRecorder.Dispose();
            _trianglesRecorder.Dispose();
            _setPassRecorder.Dispose();
            _gcAllocRecorder.Dispose();
            _gcAllocCountRecorder.Dispose();

            if (_voxelSystemRecorders != null)
            {
                foreach (var recorder in _voxelSystemRecorders.Values)
                {
                    recorder.Dispose();
                }
                _voxelSystemRecorders.Clear();
            }
        }

        private Coroutine _captureCoroutine;

        private void Update()
        {
            // Toggle capture with key
            if (IsToggleKeyPressed())
            {
                if (_isCapturing)
                    StopCapture();
                else
                    StartCapture();
            }
        }

        private bool IsToggleKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            if (Keyboard.current == null)
            {
                return false;
            }

            if (Enum.TryParse(_toggleKey.ToString(), out Key key))
            {
                return Keyboard.current[key].wasPressedThisFrame;
            }

            return false;
#else
            return Input.GetKeyDown(_toggleKey);
#endif
        }

        private IEnumerator CaptureLoop()
        {
            UnityEngine.Debug.Log("[PerformanceCaptureSession] Coroutine started");

            while (_isCapturing)
            {
                // Collect frame data
                CollectFrameData();

                // Sample memory periodically
                if (Time.realtimeSinceStartup - _lastMemorySampleTime >= MEMORY_SAMPLE_INTERVAL)
                {
                    SampleMemory();
                    _lastMemorySampleTime = Time.realtimeSinceStartup;
                }

                // Collect system timings from VoxelProfiler
                CollectSystemTimings();

                // Update progress
                float elapsed = Time.realtimeSinceStartup - _captureStartTime;
                _captureProgress = Mathf.Clamp01(elapsed / _captureDuration);

                // Check if capture should end
                if (elapsed >= _captureDuration)
                {
                    StopCapture();
                    yield break;
                }

                // Wait for end of frame
                yield return null;
            }
        }

        public void StartCapture()
        {
            if (_isCapturing)
                return;

            // Ensure initialized
            Initialize();

            // Reset state
            _writeIndex = 0;
            _frameCount = 0;
            _framesCaptured = 0;
            _systemTimings.Clear();
            _memorySamples.Clear();
            _frameAllocations.Clear();
            _systemAllocations.Clear();
            _lastReport = null;
            _captureProgress = 0f;

            // Record initial state
            _captureStartTime = Time.realtimeSinceStartup;
            _initialGCGen0 = GC.CollectionCount(0);
            _initialGCGen1 = GC.CollectionCount(1);
            _initialGCGen2 = GC.CollectionCount(2);
            _initialManagedHeap = Profiler.GetTotalAllocatedMemoryLong();
            _initialNativeAlloc = Profiler.GetTotalReservedMemoryLong();
            _lastMemorySampleTime = _captureStartTime;

            // Reset VoxelProfiler for fresh data
            VoxelProfiler.Reset();

            // Set capturing flag - ensure this happens
            _isCapturing = true;

            // Ensure component is enabled
            if (!enabled)
            {
                UnityEngine.Debug.LogWarning("[PerformanceCaptureSession] Component was disabled, enabling it.");
                enabled = true;
            }

            // Start the capture coroutine
            if (_captureCoroutine != null)
            {
                StopCoroutine(_captureCoroutine);
            }
            _captureCoroutine = StartCoroutine(CaptureLoop());

            UnityEngine.Debug.Log($"[PerformanceCaptureSession] Started capture for {_captureDuration}s. Press {_toggleKey} to stop early. GameObject active: {gameObject.activeInHierarchy}, Component enabled: {enabled}");
        }

        public void StopCapture()
        {
            if (!_isCapturing)
                return;

            _isCapturing = false;
            _captureEndTime = Time.realtimeSinceStartup;
            _captureProgress = 1f;

            // Stop the coroutine
            if (_captureCoroutine != null)
            {
                StopCoroutine(_captureCoroutine);
                _captureCoroutine = null;
            }

            // Generate report
            _lastReport = GenerateReport();

            UnityEngine.Debug.Log($"[PerformanceCaptureSession] Capture complete. {_framesCaptured} frames captured. Report ready.");
        }

        private void CollectFrameData()
        {
            var snapshot = new FrameSnapshot
            {
                DeltaTimeMs = Time.deltaTime * 1000f,
                ManagedHeapBytes = Profiler.GetTotalAllocatedMemoryLong(),
                NativeAllocBytes = Profiler.GetTotalReservedMemoryLong(),
                GCGen0Count = GC.CollectionCount(0),
                GCGen1Count = GC.CollectionCount(1),
                GCGen2Count = GC.CollectionCount(2)
            };

            // Frame timing (CPU/GPU)
            FrameTimingManager.CaptureFrameTimings();
            uint numTimings = FrameTimingManager.GetLatestTimings(1, _frameTimings);
            if (numTimings > 0)
            {
                snapshot.CpuFrameTimeMs = (float)_frameTimings[0].cpuFrameTime;
                snapshot.GpuFrameTimeMs = (float)_frameTimings[0].gpuFrameTime;
            }
            else
            {
                // Fallback if FrameTimingManager not available
                snapshot.CpuFrameTimeMs = Time.deltaTime * 1000f;
                snapshot.GpuFrameTimeMs = 0;
            }

            // Rendering stats
            if (_drawCallsRecorder.Valid)
                snapshot.DrawCalls = (int)_drawCallsRecorder.LastValue;
            if (_batchesRecorder.Valid)
                snapshot.Batches = (int)_batchesRecorder.LastValue;
            if (_trianglesRecorder.Valid)
                snapshot.Triangles = (int)_trianglesRecorder.LastValue;
            if (_setPassRecorder.Valid)
                snapshot.SetPassCalls = (int)_setPassRecorder.LastValue;

            // Store in ring buffer
            _frameBuffer[_writeIndex] = snapshot;
            _writeIndex = (_writeIndex + 1) % MAX_FRAMES;
            _frameCount = Math.Min(_frameCount + 1, MAX_FRAMES);
            _framesCaptured++;

            // Track per-frame GC allocations
            if (_gcAllocRecorder.Valid)
            {
                long allocThisFrame = _gcAllocRecorder.LastValue;
                _frameAllocations.Add(allocThisFrame);
            }
        }

        private void SampleMemory()
        {
            _memorySamples.Add(new MemorySnapshot
            {
                TimeSeconds = Time.realtimeSinceStartup - _captureStartTime,
                ManagedBytes = Profiler.GetTotalAllocatedMemoryLong(),
                NativeBytes = Profiler.GetTotalReservedMemoryLong()
            });
        }

        private void CollectSystemTimings()
        {
            // Get timings from VoxelProfiler (legacy)
            if (_tempTimings == null) _tempTimings = new Dictionary<string, float>();
            VoxelProfiler.PopulateTimings(_tempTimings);
            
            foreach (var kvp in _tempTimings)
            {
                if (!_systemTimings.TryGetValue(kvp.Key, out var timing))
                {
                    timing = new SystemTiming { Name = kvp.Key };
                }

                timing.TotalMs += kvp.Value;
                timing.SampleCount++;
                if (kvp.Value > timing.MaxMs)
                    timing.MaxMs = kvp.Value;

                _systemTimings[kvp.Key] = timing;
            }

            // Get timings from ProfilerRecorders (new VoxelProfilerMarkers)
            foreach (var kvp in _voxelSystemRecorders)
            {
                if (!kvp.Value.Valid || kvp.Value.LastValue == 0)
                    continue;

                // Convert nanoseconds to milliseconds
                float ms = kvp.Value.LastValue / 1_000_000f;

                if (!_systemTimings.TryGetValue(kvp.Key, out var timing))
                {
                    timing = new SystemTiming { Name = kvp.Key };
                }

                timing.TotalMs += ms;
                timing.SampleCount++;
                if (ms > timing.MaxMs)
                    timing.MaxMs = ms;

                _systemTimings[kvp.Key] = timing;
            }
        }

        public string GenerateReport()
        {
            var sb = new StringBuilder(8192);
            float duration = _captureEndTime - _captureStartTime;
            float avgFps = _framesCaptured / duration;

            // Header
            sb.AppendLine("=== DIG PERFORMANCE CAPTURE ===");
            sb.AppendLine($"Duration: {duration:F1}s | Frames: {_framesCaptured} | Avg FPS: {avgFps:F1}");
            sb.AppendLine($"Platform: {Application.platform} | Build: {(UnityEngine.Debug.isDebugBuild ? "Development" : "Release")}");
            sb.AppendLine($"Captured: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            // Frame Timing
            AppendFrameTimingSection(sb);

            // System Timing
            AppendSystemTimingSection(sb);

            // Memory
            AppendMemorySection(sb);

            // Allocations
            AppendAllocationsSection(sb);

            // Rendering
            AppendRenderingSection(sb);

            // Bottlenecks
            AppendBottlenecksSection(sb);

            // Timeline
            AppendTimelineSection(sb);

            sb.AppendLine();
            sb.AppendLine("--- End of Report ---");

            return sb.ToString();
        }

        private void AppendFrameTimingSection(StringBuilder sb)
        {
            sb.AppendLine("== FRAME TIMING ==");
            sb.AppendLine("| Metric     | Avg    | Min   | Max    | P95    | P99    |");
            sb.AppendLine("|------------|--------|-------|--------|--------|--------|");

            // Extract CPU times
            var cpuTimes = new float[_frameCount];
            var gpuTimes = new float[_frameCount];
            var deltaTimes = new float[_frameCount];

            int readIndex = _frameCount < MAX_FRAMES ? 0 : _writeIndex;
            for (int i = 0; i < _frameCount; i++)
            {
                int idx = (readIndex + i) % MAX_FRAMES;
                cpuTimes[i] = _frameBuffer[idx].CpuFrameTimeMs;
                gpuTimes[i] = _frameBuffer[idx].GpuFrameTimeMs;
                deltaTimes[i] = _frameBuffer[idx].DeltaTimeMs;
            }

            var cpuSummary = MetricSummary.Calculate(cpuTimes, _frameCount);
            var gpuSummary = MetricSummary.Calculate(gpuTimes, _frameCount);
            var deltaSummary = MetricSummary.Calculate(deltaTimes, _frameCount);

            sb.AppendLine($"| CPU (ms)   | {cpuSummary.Average,6:F2} | {cpuSummary.Min,5:F2} | {cpuSummary.Max,6:F2} | {cpuSummary.Percentile95,6:F2} | {cpuSummary.Percentile99,6:F2} |");
            sb.AppendLine($"| GPU (ms)   | {gpuSummary.Average,6:F2} | {gpuSummary.Min,5:F2} | {gpuSummary.Max,6:F2} | {gpuSummary.Percentile95,6:F2} | {gpuSummary.Percentile99,6:F2} |");
            sb.AppendLine($"| Delta (ms) | {deltaSummary.Average,6:F2} | {deltaSummary.Min,5:F2} | {deltaSummary.Max,6:F2} | {deltaSummary.Percentile95,6:F2} | {deltaSummary.Percentile99,6:F2} |");
            sb.AppendLine();
        }

        private void AppendSystemTimingSection(StringBuilder sb)
        {
            if (_systemTimings.Count == 0)
            {
                sb.AppendLine("== SYSTEM TIMING ==");
                sb.AppendLine("No system timing data captured.");
                sb.AppendLine();
                return;
            }

            // Sort by average time descending
            var sortedSystems = new List<SystemTiming>(_systemTimings.Values);
            sortedSystems.Sort((a, b) => b.AverageMs.CompareTo(a.AverageMs));

            sb.AppendLine("== SYSTEM TIMING (sorted by avg) ==");
            sb.AppendLine("| System                | Avg ms | Max ms | Budget | Status |");
            sb.AppendLine("|-----------------------|--------|--------|--------|--------|");

            foreach (var sys in sortedSystems)
            {
                string budget = "-";
                string status = "-";

                if (_budgetConfig != null)
                {
                    var budgetEntry = _budgetConfig.GetBudget(sys.Name);
                    if (budgetEntry.MaxAvgMs > 0)
                    {
                        budget = $"{budgetEntry.MaxAvgMs:F1}";
                        status = sys.AverageMs > budgetEntry.MaxAvgMs ? "OVER" : "OK";
                    }
                }

                sb.AppendLine($"| {sys.Name,-21} | {sys.AverageMs,6:F2} | {sys.MaxMs,6:F2} | {budget,6} | {status,-6} |");
            }
            sb.AppendLine();
        }

        private void AppendMemorySection(StringBuilder sb)
        {
            long endManaged = Profiler.GetTotalAllocatedMemoryLong();
            long endNative = Profiler.GetTotalReservedMemoryLong();
            int endGC0 = GC.CollectionCount(0);
            int endGC1 = GC.CollectionCount(1);
            int endGC2 = GC.CollectionCount(2);

            float managedStartMB = _initialManagedHeap / (1024f * 1024f);
            float managedEndMB = endManaged / (1024f * 1024f);
            float nativeStartMB = _initialNativeAlloc / (1024f * 1024f);
            float nativeEndMB = endNative / (1024f * 1024f);

            var managedTrend = AnalyzeMemoryTrend(true);
            var nativeTrend = AnalyzeMemoryTrend(false);

            sb.AppendLine("== MEMORY ==");
            sb.AppendLine("| Metric           | Start    | End      | Delta   | Trend    |");
            sb.AppendLine("|------------------|----------|----------|---------|----------|");
            sb.AppendLine($"| Managed (MB)     | {managedStartMB,8:F1} | {managedEndMB,8:F1} | {managedEndMB - managedStartMB:+#0.0;-#0.0;0.0} | {managedTrend,-8} |");
            sb.AppendLine($"| Native (MB)      | {nativeStartMB,8:F1} | {nativeEndMB,8:F1} | {nativeEndMB - nativeStartMB:+#0.0;-#0.0;0.0} | {nativeTrend,-8} |");
            sb.AppendLine($"| GC (Gen0/1/2)    | {_initialGCGen0}/{_initialGCGen1}/{_initialGCGen2} | {endGC0}/{endGC1}/{endGC2} | +{endGC0 - _initialGCGen0}/{endGC1 - _initialGCGen1}/{endGC2 - _initialGCGen2} |          |");
            sb.AppendLine();
        }

        private MemoryTrend AnalyzeMemoryTrend(bool managed)
        {
            if (_memorySamples.Count < 2)
                return MemoryTrend.Stable;

            // Simple linear regression
            float sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            int n = _memorySamples.Count;

            for (int i = 0; i < n; i++)
            {
                float x = _memorySamples[i].TimeSeconds;
                float y = (managed ? _memorySamples[i].ManagedBytes : _memorySamples[i].NativeBytes) / (1024f * 1024f);
                sumX += x;
                sumY += y;
                sumXY += x * y;
                sumX2 += x * x;
            }

            float slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);

            // Threshold: 0.1 MB/s
            if (slope > 0.1f) return MemoryTrend.Growing;
            if (slope < -0.1f) return MemoryTrend.Shrinking;
            return MemoryTrend.Stable;
        }

        private void AppendAllocationsSection(StringBuilder sb)
        {
            if (_frameAllocations.Count == 0)
            {
                sb.AppendLine("== ALLOCATIONS ==");
                sb.AppendLine("No allocation data captured (requires Development build).");
                sb.AppendLine();
                return;
            }

            // Calculate allocation statistics
            long totalAlloc = 0;
            long maxAlloc = 0;
            int framesWithAlloc = 0;

            foreach (var alloc in _frameAllocations)
            {
                totalAlloc += alloc;
                if (alloc > maxAlloc) maxAlloc = alloc;
                if (alloc > 0) framesWithAlloc++;
            }

            float avgAllocPerFrame = _frameAllocations.Count > 0 ? totalAlloc / (float)_frameAllocations.Count : 0;
            float duration = _captureEndTime - _captureStartTime;
            float allocPerSecond = totalAlloc / duration;

            sb.AppendLine("== ALLOCATIONS (GC) ==");
            sb.AppendLine($"Total Allocated: {FormatBytes(totalAlloc)}");
            sb.AppendLine($"Avg Per Frame: {FormatBytes((long)avgAllocPerFrame)}");
            sb.AppendLine($"Max Single Frame: {FormatBytes(maxAlloc)}");
            sb.AppendLine($"Allocation Rate: {FormatBytes((long)allocPerSecond)}/s");
            sb.AppendLine($"Frames with Alloc: {framesWithAlloc}/{_frameAllocations.Count} ({100f * framesWithAlloc / _frameAllocations.Count:F0}%)");

            // Find allocation spikes (frames allocating > 10x average)
            var spikes = new List<(int frame, long bytes)>();
            for (int i = 0; i < _frameAllocations.Count && spikes.Count < 5; i++)
            {
                if (_frameAllocations[i] > avgAllocPerFrame * 10 && _frameAllocations[i] > 1024)
                {
                    spikes.Add((i, _frameAllocations[i]));
                }
            }

            if (spikes.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Allocation Spikes (>10x avg):");
                foreach (var spike in spikes)
                {
                    sb.AppendLine($"  Frame {spike.frame}: {FormatBytes(spike.bytes)}");
                }
            }

            sb.AppendLine();
        }

        private string FormatBytes(long bytes)
        {
            if (bytes >= 1024 * 1024)
                return $"{bytes / (1024f * 1024f):F2} MB";
            if (bytes >= 1024)
                return $"{bytes / 1024f:F1} KB";
            return $"{bytes} B";
        }

        private void AppendRenderingSection(StringBuilder sb)
        {
            var drawCalls = new float[_frameCount];
            var batches = new float[_frameCount];
            var triangles = new float[_frameCount];
            var setPass = new float[_frameCount];

            int readIndex = _frameCount < MAX_FRAMES ? 0 : _writeIndex;
            for (int i = 0; i < _frameCount; i++)
            {
                int idx = (readIndex + i) % MAX_FRAMES;
                drawCalls[i] = _frameBuffer[idx].DrawCalls;
                batches[i] = _frameBuffer[idx].Batches;
                triangles[i] = _frameBuffer[idx].Triangles;
                setPass[i] = _frameBuffer[idx].SetPassCalls;
            }

            var drawSummary = MetricSummary.Calculate(drawCalls, _frameCount);
            var batchSummary = MetricSummary.Calculate(batches, _frameCount);
            var triSummary = MetricSummary.Calculate(triangles, _frameCount);
            var setSummary = MetricSummary.Calculate(setPass, _frameCount);

            sb.AppendLine("== RENDERING ==");
            sb.AppendLine("| Metric      | Avg      | Min     | Max      | P95      |");
            sb.AppendLine("|-------------|----------|---------|----------|----------|");
            sb.AppendLine($"| Draw Calls  | {drawSummary.Average,8:F0} | {drawSummary.Min,7:F0} | {drawSummary.Max,8:F0} | {drawSummary.Percentile95,8:F0} |");
            sb.AppendLine($"| Batches     | {batchSummary.Average,8:F0} | {batchSummary.Min,7:F0} | {batchSummary.Max,8:F0} | {batchSummary.Percentile95,8:F0} |");
            sb.AppendLine($"| Triangles   | {FormatLargeNumber(triSummary.Average),8} | {FormatLargeNumber(triSummary.Min),7} | {FormatLargeNumber(triSummary.Max),8} | {FormatLargeNumber(triSummary.Percentile95),8} |");
            sb.AppendLine($"| SetPass     | {setSummary.Average,8:F0} | {setSummary.Min,7:F0} | {setSummary.Max,8:F0} | {setSummary.Percentile95,8:F0} |");
            sb.AppendLine();
        }

        private string FormatLargeNumber(float value)
        {
            if (value >= 1000000)
                return $"{value / 1000000f:F1}M";
            if (value >= 1000)
                return $"{value / 1000f:F1}K";
            return $"{value:F0}";
        }

        private void AppendBottlenecksSection(StringBuilder sb)
        {
            sb.AppendLine("== BOTTLENECKS ==");

            bool hasBottlenecks = false;

            // Check system budgets
            if (_budgetConfig != null)
            {
                foreach (var sys in _systemTimings.Values)
                {
                    var budget = _budgetConfig.GetBudget(sys.Name);
                    if (budget.MaxAvgMs > 0 && sys.AverageMs > budget.MaxAvgMs)
                    {
                        string severity = sys.AverageMs > budget.MaxPeakMs ? "CRITICAL" : "WARNING";
                        sb.AppendLine($"- [{severity}] {sys.Name} over budget ({sys.AverageMs:F2}ms > {budget.MaxAvgMs:F1}ms)");
                        hasBottlenecks = true;
                    }
                }
            }

            // Check memory trend
            var managedTrend = AnalyzeMemoryTrend(true);
            if (managedTrend == MemoryTrend.Growing)
            {
                sb.AppendLine("- [WARNING] Managed memory is growing - potential memory leak");
                hasBottlenecks = true;
            }

            // Check frame times
            float avgFrameTime = 0;
            for (int i = 0; i < _frameCount; i++)
            {
                avgFrameTime += _frameBuffer[i].DeltaTimeMs;
            }
            avgFrameTime /= _frameCount;

            if (avgFrameTime > 33.33f) // Below 30 FPS
            {
                sb.AppendLine($"- [CRITICAL] Average frame time {avgFrameTime:F1}ms (below 30 FPS)");
                hasBottlenecks = true;
            }
            else if (avgFrameTime > 16.67f) // Below 60 FPS
            {
                sb.AppendLine($"- [WARNING] Average frame time {avgFrameTime:F1}ms (below 60 FPS target)");
                hasBottlenecks = true;
            }

            // Check GC frequency
            int gcCount = GC.CollectionCount(0) - _initialGCGen0;
            float duration = _captureEndTime - _captureStartTime;
            float gcPerSecond = gcCount / duration;
            if (gcPerSecond > 1f)
            {
                sb.AppendLine($"- [WARNING] High GC frequency: {gcPerSecond:F1} collections/second");
                hasBottlenecks = true;
            }

            // Check allocation rate
            if (_frameAllocations.Count > 0)
            {
                long totalAlloc = 0;
                foreach (var alloc in _frameAllocations)
                    totalAlloc += alloc;

                float allocPerSecond = totalAlloc / duration;
                float allocPerFrame = totalAlloc / (float)_frameAllocations.Count;

                if (allocPerSecond > 1024 * 1024) // > 1 MB/s
                {
                    sb.AppendLine($"- [WARNING] High allocation rate: {FormatBytes((long)allocPerSecond)}/s");
                    hasBottlenecks = true;
                }

                if (allocPerFrame > 10 * 1024) // > 10 KB/frame average
                {
                    sb.AppendLine($"- [WARNING] High per-frame allocation: {FormatBytes((long)allocPerFrame)}/frame avg");
                    hasBottlenecks = true;
                }
            }

            // Check for unaccounted time
            float totalSystemTime = 0;
            foreach (var sys in _systemTimings.Values)
            {
                totalSystemTime += sys.AverageMs;
            }
            float unaccountedTime = avgFrameTime - totalSystemTime;
            if (unaccountedTime > 5f && unaccountedTime > avgFrameTime * 0.3f)
            {
                sb.AppendLine($"- [INFO] {unaccountedTime:F1}ms unaccounted ({100 * unaccountedTime / avgFrameTime:F0}% of frame) - add more ProfilerMarkers");
                hasBottlenecks = true;
            }

            if (!hasBottlenecks)
            {
                sb.AppendLine("No significant bottlenecks detected.");
            }
            sb.AppendLine();
        }

        private void AppendTimelineSection(StringBuilder sb)
        {
            if (_frameCount == 0)
            {
                sb.AppendLine("== TIMELINE ==");
                sb.AppendLine("No timeline data.");
                return;
            }

            // Aggregate by second
            var secondAggregates = new List<SecondAggregate>();
            float duration = _captureEndTime - _captureStartTime;
            int totalSeconds = Mathf.CeilToInt(duration);

            int framesPerSecondEstimate = Mathf.CeilToInt(_frameCount / duration);
            int readIndex = _frameCount < MAX_FRAMES ? 0 : _writeIndex;

            for (int sec = 0; sec < totalSeconds && sec < 60; sec++) // Limit to first 60 seconds
            {
                var agg = new SecondAggregate { SecondIndex = sec + 1 };
                float cpuSum = 0, gpuSum = 0;
                int drawSum = 0;
                int startFrame = sec * framesPerSecondEstimate;
                int endFrame = Math.Min((sec + 1) * framesPerSecondEstimate, _frameCount);

                for (int f = startFrame; f < endFrame; f++)
                {
                    int idx = (readIndex + f) % MAX_FRAMES;
                    cpuSum += _frameBuffer[idx].CpuFrameTimeMs;
                    gpuSum += _frameBuffer[idx].GpuFrameTimeMs;
                    drawSum += _frameBuffer[idx].DrawCalls;
                    agg.FrameCount++;
                }

                if (agg.FrameCount > 0)
                {
                    agg.AvgFps = agg.FrameCount; // frames in this second
                    agg.AvgCpuMs = cpuSum / agg.FrameCount;
                    agg.AvgGpuMs = gpuSum / agg.FrameCount;
                    agg.AvgDrawCalls = drawSum / agg.FrameCount;

                    // Get memory from samples
                    if (sec < _memorySamples.Count)
                    {
                        agg.MemoryMB = _memorySamples[sec].ManagedBytes / (1024f * 1024f);
                    }

                    secondAggregates.Add(agg);
                }
            }

            sb.AppendLine("== TIMELINE (per-second, first 60s) ==");
            sb.AppendLine("| Sec | FPS  | CPU   | GPU   | Draws | Mem MB |");
            sb.AppendLine("|-----|------|-------|-------|-------|--------|");

            foreach (var agg in secondAggregates)
            {
                sb.AppendLine($"| {agg.SecondIndex,3} | {agg.AvgFps,4:F0} | {agg.AvgCpuMs,5:F1} | {agg.AvgGpuMs,5:F1} | {agg.AvgDrawCalls,5} | {agg.MemoryMB,6:F1} |");
            }
        }

        public void CopyReportToClipboard()
        {
            if (string.IsNullOrEmpty(_lastReport))
            {
                UnityEngine.Debug.LogWarning("[PerformanceCaptureSession] No report available. Run a capture first.");
                return;
            }

            GUIUtility.systemCopyBuffer = _lastReport;
            UnityEngine.Debug.Log("[PerformanceCaptureSession] Report copied to clipboard!");
        }

#else
        // Release build stubs
        public bool IsCapturing => false;
        public float CaptureProgress => 0;
        public string LastReport => "Performance capture not available in release builds.";
        public void StartCapture() { }
        public void StopCapture() { }
        public string GenerateReport() => LastReport;
        public void CopyReportToClipboard() { }
#endif
    }
}
