using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using DIG.CameraSystem;
using Player.Components;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DIG.DeathCamera
{
    /// <summary>
    /// EPIC 18.13: Spectator phase. Multi-mode camera following alive teammates.
    /// Supports Follow, FirstPerson, and FreeCam modes — all implemented
    /// as ICameraMode for CameraModeProvider integration.
    /// Never self-completes — runs until orchestrator detects respawn.
    /// </summary>
    public class SpectatorPhase : IDeathCameraPhase
    {
        public DeathCameraPhaseType PhaseType => DeathCameraPhaseType.Spectator;
        public bool IsComplete => false; // Never self-completes
        public bool CanSkip => false;

        private DeathCameraContext _context;
        private DeathSpectatorMode _currentMode;
        private List<DeathSpectatorMode> _availableModes;
        private int _playerIndex;

        // Camera mode instances
        private DeathFollowCam _followCam;
        private DeathFreeCam _freeCam;

        // Reusable list for player list UI — avoids allocation per call
        private readonly List<PlayerListEntry> _playerListCache = new();

        // Ghost data query
        private World _clientWorld;
        private EntityQuery _ghostQuery;
        private bool _ghostQueryCreated;

        // Ghost data cache — refreshed once per frame, read multiple times.
        // Uses persistent NativeList + Dictionary for O(1) ghost lookups.
        private int _cachedFrame = -1;
        private NativeList<GhostInstance> _cachedGhosts;
        private NativeList<Entity> _cachedEntities;
        private readonly Dictionary<ushort, int> _ghostIdToIndex = new();
        private bool _persistentAllocated;

        public void Enter(DeathCameraContext context)
        {
            _context = context;
            var config = context.Config;

            // Build available mode list from config per-style toggles
            _availableModes = config.GetAvailableModes();

            // Default to the mode matching gameplay paradigm at death time
            var idealInitial = DeathSpectatorModeExtensions.FromGameplayMode(context.GameplayMode);
            _currentMode = _availableModes.Contains(idealInitial)
                ? idealInitial
                : (_availableModes.Count > 0 ? _availableModes[0] : DeathSpectatorMode.TPSOrbit);
            _playerIndex = 0;
            _cachedFrame = -1;

            // Find client world for ghost data
            _clientWorld = FindClientWorld();
            _ghostQueryCreated = false;

            // Create camera modes
            var followGO = new GameObject("[DeathFollowCam]");
            Object.DontDestroyOnLoad(followGO);
            _followCam = followGO.AddComponent<DeathFollowCam>();
            _followCam.Configure(config, context.GameplayMode, context.GameplayCameraConfig, context.GameplayZoomLevel);
            _followCam.GetGhostPosition = GetGhostPosition;
            _followCam.GetGhostRotation = GetGhostRotation;
            _followCam.GetGhostCameraSettings = GetGhostCameraSettings;
            _followCam.GetGhostPivotOffset = GetGhostPivotOffset;

            // Pass captured camera state so isometric death cam matches Cinemachine output exactly
            if (context.HasCapturedCameraState)
            {
                _followCam.SetCapturedCameraState(
                    context.CapturedCameraOffset,
                    context.CapturedCameraRotation,
                    context.CapturedFOV,
                    context.GameplayZoomLevel >= 0f ? context.GameplayZoomLevel : 0.5f
                );
            }

            var freeGO = new GameObject("[DeathFreeCam]");
            Object.DontDestroyOnLoad(freeGO);
            _freeCam = freeGO.AddComponent<DeathFreeCam>();
            _freeCam.Configure(config.FreeCamSpeed, config.FreeCamFastMultiplier, config.FreeCamSensitivity, config.FOV);
            _freeCam.SetReportedMode(context.GameplayMode);

            // Set initial follow target
            if (context.AlivePlayerGhostIds.Count > 0)
            {
                var targetGhostId = context.AlivePlayerGhostIds[0];
                _followCam.SetFollowTarget(targetGhostId);
            }
            else
            {
                // No alive teammates — orbit the kill position (dead player's body)
                _followCam.SetStaticTarget(context.KillPosition);
            }

            // Skip CameraTransitionManager — the previous ActiveCamera (kill cam) is already
            // destroyed, which causes TransitionToCamera to set up a broken blend that
            // fights with CameraManager.LateUpdate over Camera.main's transform.
            // Instead, directly activate the mode via CameraModeProvider.
            ActivateMode(_currentMode);

            DCamLog.Log($"[DCam] Spectator ENTER — mode={_currentMode}, alivePlayers={context.AlivePlayerGhostIds.Count}, clientWorld={(_clientWorld != null ? _clientWorld.Name : "NULL")}, followTarget={(context.AlivePlayerGhostIds.Count > 0 ? context.AlivePlayerGhostIds[0].ToString() : "static@" + context.KillPosition)}");

            // Show spectator HUD
            if (config.ShowSpectatorHUD)
            {
                var hud = SpectatorHUDView.Instance;
                if (hud != null)
                {
                    var entries = BuildPlayerList();
                    hud.Show(entries, GetFollowedPlayerName(), 0f, 0f);
                    hud.UpdateCameraMode(_currentMode);
                }
            }
        }

        public void Update(float deltaTime)
        {
            // Handle input
            HandleInput();

            var activeCam = GetActiveCameraMode();

            // Update active camera — but skip if CameraTransitionManager is mid-blend,
            // because it also calls UpdateCamera inside UpdateTransition, and double
            // SmoothDamp calls per frame corrupt the velocity state.
            bool transitionActive = CameraTransitionManager.HasInstance && CameraTransitionManager.Instance.IsTransitioning;
            if (!transitionActive)
            {
                activeCam?.UpdateCamera(deltaTime);
            }

            // Update HUD — throttled to every 6 frames to avoid per-frame string allocations.
            // Respawn timer shows 0.1s precision, so 10fps updates are visually identical.
            if (_context.Config.ShowSpectatorHUD && UnityEngine.Time.frameCount % 6 == 0)
            {
                var hud = SpectatorHUDView.Instance;
                if (hud != null)
                {
                    hud.UpdateFollowedPlayer(GetFollowedPlayerName(), 0f, 100f);
                    hud.UpdateRespawnCountdown(_context.RespawnTimeRemaining);
                }
            }

            // Persistent ghost cache is reused across frames — no per-frame disposal needed.
        }

        public void Skip() { }

        public void Exit()
        {
            DCamLog.Log("[DCam] Spectator EXIT");
            // Hide HUD
            var hud = SpectatorHUDView.Instance;
            if (hud != null)
                hud.Hide();

            // Dispose ghost cache
            DisposeGhostCache();

            // Destroy camera modes
            if (_followCam != null)
            {
                Object.Destroy(_followCam.gameObject);
                _followCam = null;
            }
            if (_freeCam != null)
            {
                Object.Destroy(_freeCam.gameObject);
                _freeCam = null;
            }
        }

        private void HandleInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            // Tab to cycle camera mode
            if (kb.tabKey.wasPressedThisFrame)
            {
                DCamLog.Log($"[DCam] Spectator: TAB pressed — cycling from {_currentMode}");
                CycleMode();
            }

            // Number keys (1-9) to follow specific player (all non-FreeCam modes)
            if (!_currentMode.IsFreeCam())
            {
                for (int i = 0; i < 9 && i < _context.AlivePlayerGhostIds.Count; i++)
                {
                    var key = (Key)((int)Key.Digit1 + i);
                    if (kb[key].wasPressedThisFrame)
                    {
                        DCamLog.Log($"[DCam] Spectator: key {i + 1} pressed — switching to ghostId={_context.AlivePlayerGhostIds[i]}");
                        _playerIndex = i;
                        SwitchFollowTarget(_context.AlivePlayerGhostIds[i]);

                        var hud = SpectatorHUDView.Instance;
                        if (hud != null)
                        {
                            hud.UpdateFollowedPlayer(GetFollowedPlayerName(), 0f, 100f);
                            hud.UpdatePlayerList(BuildPlayerList());
                        }
                    }
                }
            }

            // Q/E rotation — only for IsometricRotatable and IsometricRotLocked
            if (_followCam != null
                && (_currentMode == DeathSpectatorMode.IsometricRotatable
                 || _currentMode == DeathSpectatorMode.IsometricRotLocked))
            {
                if (kb.qKey.wasPressedThisFrame) _followCam.RotateIsometric(-45f);
                if (kb.eKey.wasPressedThisFrame) _followCam.RotateIsometric(45f);
            }

            // Mouse orbit control — only for TPSOrbit
            var mouse = Mouse.current;
            if (mouse != null && _followCam != null && _currentMode == DeathSpectatorMode.TPSOrbit)
            {
                var delta = mouse.delta.ReadValue();
                if (math.lengthsq(new float2(delta.x, delta.y)) > 0.01f)
                {
                    _followCam.HandleRotationInput(new float2(delta.x, delta.y));
                }
            }

            // Scroll wheel zoom — all non-FreeCam modes
            if (mouse != null && _followCam != null && !_currentMode.IsFreeCam())
            {
                float scrollValue = mouse.scroll.ReadValue().y;
                if (math.abs(scrollValue) > 0.01f)
                {
                    float currentZoom = _followCam.GetZoom();
                    float sensitivity = _context.Config.ZoomScrollSensitivity;
                    float scrollNormalized = scrollValue / 120f;
                    // Scroll up = zoom in (decrease zoom level → closer)
                    float newZoom = math.saturate(currentZoom - scrollNormalized * sensitivity);
                    _followCam.SetZoom(newZoom);
                }
            }
        }

        /// <summary>
        /// Switch to a new follow target with a smooth camera transition.
        /// </summary>
        private void SwitchFollowTarget(ushort ghostId)
        {
            if (_followCam == null)
                return;

            float transitionDuration = _context.Config.TransitionBetweenPlayers;
            if (transitionDuration <= 0.01f)
            {
                _followCam.SetFollowTarget(ghostId);
                return;
            }

            // Capture current camera pose before switching
            var camTransform = _followCam.GetCameraTransform();
            if (camTransform == null)
            {
                _followCam.SetFollowTarget(ghostId);
                return;
            }

            Vector3 fromPos = camTransform.position;
            Quaternion fromRot = camTransform.rotation;

            // Set new target (computes new desired orbit position)
            _followCam.SetFollowTarget(ghostId);

            // Smoothly blend from old pose to new orbit position
            _followCam.SetTransitionFrom(fromPos, fromRot, transitionDuration);
        }

        private void CycleMode()
        {
            if (_availableModes == null || _availableModes.Count == 0) return;

            int currentIndex = _availableModes.IndexOf(_currentMode);
            int nextIndex = (currentIndex + 1) % _availableModes.Count;
            _currentMode = _availableModes[nextIndex];

            // Ensure static target when switching to a follow mode with no alive players
            if (!_currentMode.IsFreeCam() && _context.AlivePlayerGhostIds.Count == 0)
                _followCam.SetStaticTarget(_context.KillPosition);

            DCamLog.Log($"[DCam] Spectator: mode cycled to {_currentMode}");
            ActivateMode(_currentMode);

            var hud = SpectatorHUDView.Instance;
            if (hud != null)
                hud.UpdateCameraMode(_currentMode);
        }

        private void ActivateMode(DeathSpectatorMode mode)
        {
            ICameraMode cam;

            if (mode.IsFreeCam())
            {
                // Initialize free cam from current follow cam position (smooth handoff)
                var followT = _followCam?.GetCameraTransform();
                if (followT != null && followT.position.sqrMagnitude > 0.01f)
                    _freeCam.SetInitialPose(followT.position, followT.eulerAngles.y, followT.eulerAngles.x);
                else if (_context.TargetCamera != null)
                {
                    var t = _context.TargetCamera.transform;
                    _freeCam.SetInitialPose(t.position, t.eulerAngles.y, t.eulerAngles.x);
                }
                else
                    _freeCam.SetInitialPose((Vector3)_context.KillPosition + Vector3.up * 5f, 0f, 30f);

                cam = _freeCam;
            }
            else
            {
                // All follow modes: reconfigure camera style + locked state
                _followCam.SetCameraStyle(mode.ToCameraMode(), _context.GameplayCameraConfig, _context.Config);
                _followCam.SetLockedFollow(mode.IsLocked());
                _followCam.SetFirstPerson(false);
                cam = _followCam;
            }

            if (CameraModeProvider.HasInstance)
                CameraModeProvider.Instance.SetActiveCamera(cam, force: true);
        }

        private ICameraMode GetActiveCameraMode()
        {
            return _currentMode.IsFreeCam() ? (ICameraMode)_freeCam : _followCam;
        }

        private ICameraMode GetActiveCameraModeForMode(DeathSpectatorMode mode)
        {
            return mode.IsFreeCam() ? (ICameraMode)_freeCam : _followCam;
        }

        // ====================================================================
        // Ghost data access (for DeathFollowCam delegates)
        // ====================================================================

        /// <summary>
        /// Refresh ghost data once per frame into persistent NativeLists + Dictionary.
        /// Subsequent lookups are O(1) via _ghostIdToIndex instead of O(N) linear scan.
        /// </summary>
        private void RefreshGhostCache()
        {
            int currentFrame = UnityEngine.Time.frameCount;
            if (currentFrame == _cachedFrame) return;
            _cachedFrame = currentFrame;

            if (!EnsureGhostQuery()) return;

            // Ensure persistent containers exist
            if (!_persistentAllocated)
            {
                _cachedGhosts = new NativeList<GhostInstance>(32, Allocator.Persistent);
                _cachedEntities = new NativeList<Entity>(32, Allocator.Persistent);
                _persistentAllocated = true;
            }

            // Copy query results into persistent lists (avoids per-frame alloc/dealloc)
            _cachedGhosts.Clear();
            _cachedEntities.Clear();
            _ghostIdToIndex.Clear();

            using var tempGhosts = _ghostQuery.ToComponentDataArray<GhostInstance>(Allocator.Temp);
            using var tempEntities = _ghostQuery.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < tempGhosts.Length; i++)
            {
                _cachedGhosts.Add(tempGhosts[i]);
                _cachedEntities.Add(tempEntities[i]);
                var gid = (ushort)tempGhosts[i].ghostId;
                // First match wins (root entities, no duplicates expected)
                _ghostIdToIndex.TryAdd(gid, i);
            }
        }

        private void DisposeGhostCache()
        {
            if (_persistentAllocated)
            {
                if (_cachedGhosts.IsCreated) _cachedGhosts.Dispose();
                if (_cachedEntities.IsCreated) _cachedEntities.Dispose();
                _persistentAllocated = false;
            }
            _ghostIdToIndex.Clear();
            _cachedFrame = -1;
        }

        /// <summary>
        /// Resolve the entity for a ghost ID from the cached data. O(1) via Dictionary.
        /// </summary>
        private Entity ResolveGhostEntity(ushort ghostId)
        {
            if (ghostId == 0) return Entity.Null;
            RefreshGhostCache();
            if (!_persistentAllocated) return Entity.Null;
            return _ghostIdToIndex.TryGetValue(ghostId, out int idx) ? _cachedEntities[idx] : Entity.Null;
        }

        private float3 GetGhostPosition(ushort ghostId)
        {
            var entity = ResolveGhostEntity(ghostId);
            if (entity == Entity.Null) return float3.zero;

            var em = _clientWorld.EntityManager;
            if (em.HasComponent<LocalToWorld>(entity))
                return em.GetComponentData<LocalToWorld>(entity).Position;
            if (em.HasComponent<LocalTransform>(entity))
                return em.GetComponentData<LocalTransform>(entity).Position;
            return float3.zero;
        }

        private quaternion GetGhostRotation(ushort ghostId)
        {
            var entity = ResolveGhostEntity(ghostId);
            if (entity == Entity.Null) return quaternion.identity;

            var em = _clientWorld.EntityManager;
            if (em.HasComponent<LocalToWorld>(entity))
                return em.GetComponentData<LocalToWorld>(entity).Rotation;
            if (em.HasComponent<LocalTransform>(entity))
                return em.GetComponentData<LocalTransform>(entity).Rotation;
            return quaternion.identity;
        }

        /// <summary>
        /// Read the replicated PlayerCameraSettings (Yaw, Pitch, CurrentDistance) from a ghost entity.
        /// Returns null if the entity doesn't have the component (e.g., non-player ghost).
        /// </summary>
        private PlayerCameraSettings? GetGhostCameraSettings(ushort ghostId)
        {
            var entity = ResolveGhostEntity(ghostId);
            if (entity == Entity.Null) return null;

            var em = _clientWorld.EntityManager;
            if (em.HasComponent<PlayerCameraSettings>(entity))
                return em.GetComponentData<PlayerCameraSettings>(entity);
            return null;
        }

        /// <summary>
        /// Read the replicated CameraViewConfig.CombatPivotOffset from a ghost entity.
        /// This is the point around which the gameplay camera orbits (e.g., shoulder height).
        /// Returns null if the entity doesn't have the component.
        /// </summary>
        private float3? GetGhostPivotOffset(ushort ghostId)
        {
            var entity = ResolveGhostEntity(ghostId);
            if (entity == Entity.Null) return null;

            var em = _clientWorld.EntityManager;
            if (em.HasComponent<CameraViewConfig>(entity))
                return em.GetComponentData<CameraViewConfig>(entity).CombatPivotOffset;
            return null;
        }

        private bool EnsureGhostQuery()
        {
            if (_clientWorld == null || !_clientWorld.IsCreated)
            {
                _clientWorld = FindClientWorld();
                _ghostQueryCreated = false;
            }
            if (_clientWorld == null) return false;

            if (!_ghostQueryCreated)
            {
                // Exclude Parent to only match ROOT ghost entities.
                // Ghost prefab children (e.g. HitboxOwnerMarker child) share the same
                // ghostId but have LocalTransform in LOCAL space (0,0,0 relative to parent).
                _ghostQuery = _clientWorld.EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<GhostInstance>(),
                    ComponentType.Exclude<Unity.Transforms.Parent>()
                );
                _ghostQueryCreated = true;
            }

            return true;
        }

        private World FindClientWorld()
        {
            foreach (var world in World.All)
            {
                if (world.IsCreated && world.Name == "ClientWorld")
                    return world;
            }
            return null;
        }

        private string GetFollowedPlayerName()
        {
            if (_context.AlivePlayerGhostIds.Count == 0) return "No players";
            if (_playerIndex >= _context.AlivePlayerGhostIds.Count) _playerIndex = 0;
            return $"Player {_context.AlivePlayerGhostIds[_playerIndex]}";
        }

        private List<PlayerListEntry> BuildPlayerList()
        {
            _playerListCache.Clear();
            for (int i = 0; i < _context.AlivePlayerGhostIds.Count; i++)
            {
                _playerListCache.Add(new PlayerListEntry
                {
                    GhostId = _context.AlivePlayerGhostIds[i],
                    Name = $"Player {_context.AlivePlayerGhostIds[i]}",
                    IsAlive = true
                });
            }
            return _playerListCache;
        }
    }
}
