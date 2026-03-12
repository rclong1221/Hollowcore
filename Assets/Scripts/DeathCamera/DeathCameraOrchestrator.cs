using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Player.Components;
using DIG.CameraSystem;
using DIG.CameraSystem.Implementations;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DIG.DeathCamera
{
    /// <summary>
    /// EPIC 18.13: The brain of the death camera system.
    /// Detects local player death, builds context from kill attribution data,
    /// acquires camera authority, and drives a phase-based state machine
    /// through kill cam → death recap → spectator → respawn transition.
    ///
    /// SystemBase because it calls managed MonoBehaviour methods
    /// (CameraModeProvider, CameraTransitionManager, UI views).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(global::Player.Systems.DeathPresentationSystem))]
    public partial class DeathCameraOrchestrator : SystemBase
    {
        private const string AuthorityOwner = "DeathCamera";
        private const int AuthorityPriority = 10;

        // State
        private DeathCameraState _state = DeathCameraState.Inactive;
        private bool _wasAlive = true;
        private int _currentPhaseIndex;
        private readonly List<IDeathCameraPhase> _phases = new();
        private readonly DeathCameraContext _context = new();

        // Config
        private DeathCameraConfigSO _config;
        private DeathCameraConfigSO _runtimeConfig; // Mutable copy for preset application

        // Cached queries
        private EntityQuery _localPlayerQuery;
        private EntityQuery _deathPresentationQuery;

        // Cached local player entity — avoids 3x NativeArray allocs per frame
        private Entity _cachedLocalEntity;
        private ushort _cachedLocalGhostId;
        private int _lastLocalEntityLookupFrame = -1;

        // Phase instances (reusable)
        private KillCamPhase _killCamPhase;
        private DeathRecapPhase _deathRecapPhase;
        private SpectatorPhase _spectatorPhase;
        private RespawnTransitionPhase _respawnTransitionPhase;

        /// <summary>Current state of the death camera system.</summary>
        public DeathCameraState State => _state;

        // ====================================================================
        // Static debug accessors (for editor tooling)
        // ====================================================================
        private static DeathCameraOrchestrator _instance;

        public static bool IsActive => _instance != null && _instance._state != DeathCameraState.Inactive;
        public static DeathCameraPhaseType CurrentPhaseType =>
            _instance != null && _instance._currentPhaseIndex < _instance._phases.Count
                ? _instance._phases[_instance._currentPhaseIndex].PhaseType
                : default;
        public static int CurrentPhaseIndex => _instance?._currentPhaseIndex ?? -1;
        public static float RespawnTimeRemaining => _instance?._context.RespawnTimeRemaining ?? 0f;
        public static string ContextKillerName => _instance?._context.KillerName;
        public static float3 ContextKillPosition => _instance?._context.KillPosition ?? float3.zero;
        public static int AlivePlayerCount => _instance?._context.AlivePlayerGhostIds.Count ?? 0;

        public static void EditorSkipCurrentPhase()
        {
            if (_instance == null || _instance._state != DeathCameraState.RunningPhases) return;
            if (_instance._currentPhaseIndex < _instance._phases.Count)
            {
                var phase = _instance._phases[_instance._currentPhaseIndex];
                if (phase.CanSkip) phase.Skip();
            }
        }

        public static void EditorForceEnd()
        {
            _instance?.ForceExit();
        }

        protected override void OnCreate()
        {
            _localPlayerQuery = GetEntityQuery(
                ComponentType.ReadOnly<DeathState>(),
                ComponentType.ReadOnly<GhostOwnerIsLocal>(),
                ComponentType.ReadOnly<GhostInstance>()
            );

            _deathPresentationQuery = GetEntityQuery(
                ComponentType.ReadOnly<global::Player.Systems.DeathPresentationState>(),
                ComponentType.ReadOnly<GhostOwnerIsLocal>()
            );

            RequireForUpdate(_localPlayerQuery);

            _instance = this;

            // Load config from Resources
            _config = Resources.Load<DeathCameraConfigSO>("DeathCameraConfig");
            if (_config == null)
            {
                // Create default runtime config if none exists
                _config = ScriptableObject.CreateInstance<DeathCameraConfigSO>();
            }

            // Create mutable runtime copy
            _runtimeConfig = ScriptableObject.CreateInstance<DeathCameraConfigSO>();
            CopyConfig(_config, _runtimeConfig);

            // Create phase instances
            _killCamPhase = new KillCamPhase();
            _deathRecapPhase = new DeathRecapPhase();
            _spectatorPhase = new SpectatorPhase();
            _respawnTransitionPhase = new RespawnTransitionPhase();
        }

        protected override void OnDestroy()
        {
            // Clean up if we're still active
            if (_state != DeathCameraState.Inactive)
            {
                ForceExit();
            }

            if (_runtimeConfig != null)
                Object.Destroy(_runtimeConfig);

            if (_instance == this)
                _instance = null;
        }

        protected override void OnUpdate()
        {
            // Resolve local player entity. Use cached entity when valid to avoid
            // 3x NativeArray allocs per frame (ToEntityArray + 2x ToComponentDataArray).
            // Re-lookup when the entity is invalid, destroyed, or every 60 frames as safety net.
            int frame = UnityEngine.Time.frameCount;
            bool needsLookup = _cachedLocalEntity == Entity.Null
                || !EntityManager.Exists(_cachedLocalEntity)
                || !EntityManager.IsComponentEnabled<GhostOwnerIsLocal>(_cachedLocalEntity)
                || (frame - _lastLocalEntityLookupFrame) >= 60;

            if (needsLookup)
            {
                _cachedLocalEntity = Entity.Null;
                _cachedLocalGhostId = 0;
                _lastLocalEntityLookupFrame = frame;

                var entities = _localPlayerQuery.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < entities.Length; i++)
                {
                    if (EntityManager.IsComponentEnabled<GhostOwnerIsLocal>(entities[i]))
                    {
                        _cachedLocalEntity = entities[i];
                        if (EntityManager.HasComponent<GhostInstance>(entities[i]))
                            _cachedLocalGhostId = (ushort)EntityManager.GetComponentData<GhostInstance>(entities[i]).ghostId;
                        break;
                    }
                }
                entities.Dispose();
            }

            Entity localEntity = _cachedLocalEntity;
            ushort localGhostId = _cachedLocalGhostId;

            if (localEntity == Entity.Null)
                return;

            DeathPhase localPhase = EntityManager.GetComponentData<DeathState>(localEntity).Phase;

            bool isDead = localPhase == DeathPhase.Dead || localPhase == DeathPhase.Downed;

            // Detect death transition: alive → dead
            if (isDead && _wasAlive)
            {
                DCamLog.Log($"[DCam] DEATH DETECTED — phase={localPhase}, ghostId={localGhostId}, entity={localEntity}");
                EnterDeathFlow(localEntity, localGhostId);
            }
            // Detect respawn transition: dead → alive
            else if (!isDead && !_wasAlive && _state != DeathCameraState.Inactive)
            {
                DCamLog.Log($"[DCam] RESPAWN DETECTED — phase={localPhase}, state={_state}");
                BeginRespawnTransition();
            }

            _wasAlive = !isDead;

            // Update active state
            switch (_state)
            {
                case DeathCameraState.RunningPhases:
                    UpdatePhases(localGhostId);
                    break;
                case DeathCameraState.WaitingForRespawn:
                    UpdateWaitingForRespawn(localGhostId);
                    break;
            }
        }

        private void EnterDeathFlow(Entity localEntity, ushort localGhostId)
        {
            // Build context from ECS data
            BuildContext(localEntity, localGhostId);

            // Acquire camera authority
            if (!CameraAuthorityGate.Acquire(AuthorityOwner, AuthorityPriority))
            {
                DCamLog.LogWarning("[DCam] EnterDeathFlow: FAILED to acquire authority");
                return;
            }

            // Save current gameplay camera for respawn restore
            bool hasPrevCam = CameraModeProvider.HasInstance && CameraModeProvider.Instance.ActiveCamera != null;
            if (hasPrevCam)
                _context.PreviousCamera = CameraModeProvider.Instance.ActiveCamera;

            // Resolve gameplay paradigm from previous camera
            _context.GameplayMode = _context.PreviousCamera?.Mode ?? CameraMode.ThirdPersonFollow;
            if (_context.PreviousCamera is CameraModeBase baseCam && baseCam.RuntimeConfig != null)
            {
                _context.GameplayCameraConfig = baseCam.RuntimeConfig;
                _context.GameplayZoomLevel = baseCam.GetZoom();
            }
            else
            {
                _context.GameplayCameraConfig = null;
                _context.GameplayZoomLevel = -1f;
            }

            // Capture Camera.main's exact state BEFORE authority override changes it.
            // During gameplay, Cinemachine drives Camera.main with its own body/lens settings
            // that may differ from CameraConfig values. Capturing here ensures the death cam
            // matches the actual gameplay camera output pixel-for-pixel.
            var mainCam = Camera.main;
            if (mainCam != null)
            {
                _context.CapturedCameraOffset = (float3)mainCam.transform.position - _context.KillPosition;
                _context.CapturedCameraRotation = mainCam.transform.rotation;
                _context.CapturedFOV = mainCam.fieldOfView;
                _context.HasCapturedCameraState = true;
            }
            else
            {
                _context.HasCapturedCameraState = false;
            }

            DCamLog.Log($"[DCam] EnterDeathFlow: authority acquired, prevCam={hasPrevCam}, paradigm={_context.GameplayMode}, killPos={_context.KillPosition}, capturedFOV={_context.CapturedFOV}");

            // Build phase sequence from config
            BuildPhaseSequence();

            if (_phases.Count == 0)
            {
                DCamLog.LogWarning("[DCam] EnterDeathFlow: 0 phases enabled — skipping to WaitingForRespawn");
                _state = DeathCameraState.WaitingForRespawn;
                return;
            }

            // Push DeathSpectator input context
            if (DIG.Core.Input.InputContextManager.Instance != null)
                DIG.Core.Input.InputContextManager.Instance.Push(DIG.Core.Input.InputContext.DeathSpectator);

            _state = DeathCameraState.RunningPhases;
            _currentPhaseIndex = 0;
            _phases[0].Enter(_context);

            var phaseNames = string.Join("→", _phases.ConvertAll(p => p.PhaseType.ToString()));
            DCamLog.Log($"[DCam] Death flow STARTED — {_phases.Count} phases: {phaseNames}");
        }

        private void BuildContext(Entity localEntity, ushort localGhostId)
        {
            _context.Config = _runtimeConfig;
            _context.TargetCamera = Camera.main;
            _context.LocalPlayerEntity = localEntity;
            _context.KillerEntity = Entity.Null;
            _context.KillerGhostId = 0;
            _context.KillerName = "Unknown";
            _context.KillPosition = float3.zero;
            _context.DamageContributors.Clear();
            _context.AlivePlayerGhostIds.Clear();

            var em = EntityManager;

            // Get kill position from local player's transform
            if (em.HasComponent<Unity.Transforms.LocalTransform>(localEntity))
            {
                var lt = em.GetComponentData<Unity.Transforms.LocalTransform>(localEntity);
                _context.KillPosition = lt.Position;
            }

            // Get death timing
            if (em.HasComponent<DeathState>(localEntity))
            {
                var ds = em.GetComponentData<DeathState>(localEntity);
                _context.RespawnDelay = ds.RespawnDelay;
                _context.DeathTime = ds.StateStartTime;
            }

            // Try to get killer from CombatState.LastAttacker
            if (em.HasComponent<global::Player.Components.CombatState>(localEntity))
            {
                var cs = em.GetComponentData<global::Player.Components.CombatState>(localEntity);
                if (cs.LastAttacker != Entity.Null && em.Exists(cs.LastAttacker))
                {
                    _context.KillerEntity = cs.LastAttacker;

                    // Get killer's ghost ID
                    if (em.HasComponent<GhostInstance>(cs.LastAttacker))
                    {
                        var gi = em.GetComponentData<GhostInstance>(cs.LastAttacker);
                        _context.KillerGhostId = (ushort)gi.ghostId;
                    }
                }
            }

            // Read damage contributors from RecentAttackerElement buffer
            if (em.HasBuffer<RecentAttackerElement>(localEntity))
            {
                var buffer = em.GetBuffer<RecentAttackerElement>(localEntity);
                for (int i = 0; i < buffer.Length; i++)
                {
                    _context.DamageContributors.Add(new DamageContributor
                    {
                        Name = $"Player", // Name resolution would require additional ghost metadata
                        DamageDealt = buffer[i].DamageDealt,
                        TimeAgo = (float)(SystemAPI.Time.ElapsedTime - buffer[i].Time)
                    });
                }
            }

            // Populate alive player list
            RefreshAlivePlayerList(localGhostId);

            _context.RespawnTimeRemaining = _context.RespawnDelay;

        }

        private void BuildPhaseSequence()
        {
            _phases.Clear();

            foreach (var phaseType in _runtimeConfig.PhaseSequence)
            {
                if (!_runtimeConfig.IsPhaseEnabled(phaseType))
                    continue;

                IDeathCameraPhase phase = phaseType switch
                {
                    DeathCameraPhaseType.KillCam => _killCamPhase,
                    DeathCameraPhaseType.DeathRecap => _deathRecapPhase,
                    DeathCameraPhaseType.Spectator => _spectatorPhase,
                    _ => null
                };

                if (phase != null)
                    _phases.Add(phase);
            }
        }

        private void UpdatePhases(ushort localGhostId)
        {
            if (_currentPhaseIndex >= _phases.Count)
            {
                // All phases complete — wait for respawn
                _state = DeathCameraState.WaitingForRespawn;
                return;
            }

            var currentPhase = _phases[_currentPhaseIndex];

            // Update respawn countdown
            float elapsed = (float)SystemAPI.Time.ElapsedTime - _context.DeathTime;
            _context.RespawnTimeRemaining = Mathf.Max(0f, _context.RespawnDelay - elapsed);

            // Handle skip input (Space skips current phase)
            var kb = Keyboard.current;
            if (kb != null && currentPhase.CanSkip && kb.spaceKey.wasPressedThisFrame)
            {
                DCamLog.Log($"[DCam] SPACE pressed — skipping phase {currentPhase.PhaseType}");
                currentPhase.Skip();
            }

            // Tab skips directly to Spectator phase from any earlier phase
            if (kb != null && currentPhase.PhaseType != DeathCameraPhaseType.Spectator && kb.tabKey.wasPressedThisFrame)
            {
                DCamLog.Log($"[DCam] TAB pressed — skipping from {currentPhase.PhaseType} to Spectator");
                currentPhase.Exit();
                // Find the spectator phase index
                for (int p = _currentPhaseIndex + 1; p < _phases.Count; p++)
                {
                    if (_phases[p].PhaseType == DeathCameraPhaseType.Spectator)
                    {
                        _currentPhaseIndex = p;
                        _phases[p].Enter(_context);
                        return;
                    }
                }
                // No spectator phase in sequence — just skip current
                currentPhase.Skip();
            }

            // Update current phase
            currentPhase.Update(UnityEngine.Time.deltaTime);

            // Check for completion
            if (currentPhase.IsComplete)
            {
                DCamLog.Log($"[DCam] Phase {currentPhase.PhaseType} COMPLETE — exiting");
                currentPhase.Exit();
                _currentPhaseIndex++;

                if (_currentPhaseIndex < _phases.Count)
                {
                    var next = _phases[_currentPhaseIndex];
                    DCamLog.Log($"[DCam] Entering next phase: {next.PhaseType} (index {_currentPhaseIndex}/{_phases.Count})");
                    next.Enter(_context);
                }
                else
                {
                    DCamLog.Log("[DCam] All phases done — WaitingForRespawn");
                    _state = DeathCameraState.WaitingForRespawn;
                }
            }

            // Refresh alive players periodically for spectator phase
            if (UnityEngine.Time.frameCount % 30 == 0)
            {
                RefreshAlivePlayerList(localGhostId);
            }

        }

        private void UpdateWaitingForRespawn(ushort localGhostId)
        {
            // Keep refreshing alive players and updating respawn countdown
            float elapsed = (float)SystemAPI.Time.ElapsedTime - _context.DeathTime;
            _context.RespawnTimeRemaining = Mathf.Max(0f, _context.RespawnDelay - elapsed);

            if (UnityEngine.Time.frameCount % 30 == 0)
            {
                RefreshAlivePlayerList(localGhostId);
            }
        }

        private void BeginRespawnTransition()
        {
            // Exit current phase if running
            if (_state == DeathCameraState.RunningPhases && _currentPhaseIndex < _phases.Count)
            {
                _phases[_currentPhaseIndex].Exit();
            }

            // Run respawn transition phase
            _respawnTransitionPhase.Enter(_context);
            _state = DeathCameraState.Inactive;

            // The respawn transition handles its own async completion via CameraTransitionManager.
            // We release authority immediately since the transition manager blends smoothly.
            _respawnTransitionPhase.Exit();

            CameraAuthorityGate.Release(AuthorityOwner);

            // Pop DeathSpectator input context
            if (DIG.Core.Input.InputContextManager.Instance != null
                && DIG.Core.Input.InputContextManager.Instance.IsInContext(DIG.Core.Input.InputContext.DeathSpectator))
                DIG.Core.Input.InputContextManager.Instance.Pop();

            DCamLog.Log("[DCam] Respawn transition COMPLETE — death flow ended");
        }

        private void ForceExit()
        {
            if (_state == DeathCameraState.RunningPhases && _currentPhaseIndex < _phases.Count)
            {
                _phases[_currentPhaseIndex].Exit();
            }

            _state = DeathCameraState.Inactive;
            CameraAuthorityGate.Release(AuthorityOwner);

            // Pop DeathSpectator input context
            if (DIG.Core.Input.InputContextManager.Instance != null
                && DIG.Core.Input.InputContextManager.Instance.IsInContext(DIG.Core.Input.InputContext.DeathSpectator))
                DIG.Core.Input.InputContextManager.Instance.Pop();
        }

        private void RefreshAlivePlayerList(ushort localGhostId)
        {
            _context.AlivePlayerGhostIds.Clear();

            // Use SystemAPI.Query iteration — zero NativeArray allocations.
            foreach (var (ghost, death) in SystemAPI.Query<RefRO<GhostInstance>, RefRO<DeathState>>()
                .WithAll<GhostOwner>())
            {
                var ghostId = (ushort)ghost.ValueRO.ghostId;
                if (ghostId == localGhostId) continue;
                var phase = death.ValueRO.Phase;
                if (phase == DeathPhase.Dead || phase == DeathPhase.Downed)
                    continue;
                _context.AlivePlayerGhostIds.Add(ghostId);
            }
        }

        private static void CopyConfig(DeathCameraConfigSO source, DeathCameraConfigSO target)
        {
            target.ConfigName = source.ConfigName;
            target.PhaseSequence = (DeathCameraPhaseType[])source.PhaseSequence.Clone();
            target.SkipAllInput = source.SkipAllInput;

            target.KillCamEnabled = source.KillCamEnabled;
            target.KillCamDuration = source.KillCamDuration;
            target.KillCamOrbitRadius = source.KillCamOrbitRadius;
            target.KillCamOrbitHeight = source.KillCamOrbitHeight;
            target.KillCamOrbitSpeed = source.KillCamOrbitSpeed;
            target.KillCamEndRadius = source.KillCamEndRadius;
            target.KillCamEndHeight = source.KillCamEndHeight;
            target.KillCamSlowMotion = source.KillCamSlowMotion;
            target.KillCamTimeScale = source.KillCamTimeScale;
            target.KillCamTransitionIn = source.KillCamTransitionIn;

            target.DeathRecapEnabled = source.DeathRecapEnabled;
            target.DeathRecapDuration = source.DeathRecapDuration;
            target.ShowDamageBreakdown = source.ShowDamageBreakdown;
            target.ShowRespawnTimer = source.ShowRespawnTimer;

            target.SpectatorEnabled = source.SpectatorEnabled;
            target.AllowTPSOrbit = source.AllowTPSOrbit;
            target.AllowIsometric = source.AllowIsometric;
            target.AllowTopDown = source.AllowTopDown;
            target.AllowIsometricRotatable = source.AllowIsometricRotatable;
            target.AllowFreeCam = source.AllowFreeCam;
            target.ShowSpectatorHUD = source.ShowSpectatorHUD;
            target.TransitionBetweenPlayers = source.TransitionBetweenPlayers;
            target.SpectatorTransitionIn = source.SpectatorTransitionIn;

            target.FollowDistance = source.FollowDistance;
            target.FollowHeight = source.FollowHeight;
            target.FollowSmoothTime = source.FollowSmoothTime;
            target.LookAtHeight = source.LookAtHeight;
            target.DefaultPitch = source.DefaultPitch;
            target.OrbitSensitivity = source.OrbitSensitivity;

            target.ZoomDistanceMin = source.ZoomDistanceMin;
            target.ZoomDistanceMax = source.ZoomDistanceMax;
            target.ZoomScrollSensitivity = source.ZoomScrollSensitivity;

            target.EnableCollision = source.EnableCollision;
            target.CollisionLayers = source.CollisionLayers;
            target.CollisionRadius = source.CollisionRadius;

            target.IsometricAngle = source.IsometricAngle;
            target.IsometricRotation = source.IsometricRotation;
            target.IsometricHeight = source.IsometricHeight;
            target.TopDownAngle = source.TopDownAngle;
            target.TopDownHeight = source.TopDownHeight;

            target.FreeCamSpeed = source.FreeCamSpeed;
            target.FreeCamFastMultiplier = source.FreeCamFastMultiplier;
            target.FreeCamSensitivity = source.FreeCamSensitivity;

            target.RespawnTransitionDuration = source.RespawnTransitionDuration;
            target.FOV = source.FOV;
            target.NearClip = source.NearClip;
        }
    }
}
