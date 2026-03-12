using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DIG.CameraSystem;

namespace DIG.DeathCamera
{
    /// <summary>
    /// EPIC 18.13: Follow cam for death spectator. Paradigm-aware: adapts to the gameplay
    /// camera mode (ThirdPersonFollow, IsometricFixed, TopDownFixed, IsometricRotatable).
    ///
    /// TPS mode: third-person orbit around a ghost entity with mouse orbit, pitch control,
    /// collision avoidance, scroll zoom, chest-level LookAt.
    ///
    /// Isometric/TopDown mode: fixed-angle camera from above (mirrors gameplay IsometricFixedCamera),
    /// zoom via height multiplier, no mouse orbit. Q/E rotation for IsometricRotatable.
    ///
    /// Supports first-person mode (TPS only). Uses delegate-based ghost data access
    /// to stay decoupled from ECS queries (SpectatorPhase provides the delegates).
    /// </summary>
    public class DeathFollowCam : MonoBehaviour, ICameraMode
    {
        // Paradigm-aware mode (set by Configure)
        private CameraMode _reportedMode = CameraMode.ThirdPersonFollow;
        public CameraMode Mode => _reportedMode;

        // Ghost data delegates — set by SpectatorPhase
        public System.Func<ushort, float3> GetGhostPosition;
        public System.Func<ushort, quaternion> GetGhostRotation;
        public System.Func<ushort, PlayerCameraSettings?> GetGhostCameraSettings;
        public System.Func<ushort, float3?> GetGhostPivotOffset;

        private ushort _followedGhostId;
        public ushort FollowedGhostId => _followedGhostId;
        private float _followDistance = 8f;
        private float _followHeight = 1.6f;
        private float _followSmoothTime = 0.15f;
        private float _orbitAngle;
        private float _pitch = 25f;
        private float _fov = 60f;
        private bool _firstPerson;
        private Vector3 _velocity;

        // LookAt offset (chest level)
        private float _lookAtHeight = 1.6f;

        // Zoom state (0 = closest, 1 = farthest)
        private float _currentZoom = 0.5f;

        // Zoom range (TPS mode — distance-based)
        private float _zoomDistMin = 2f;
        private float _zoomDistMax = 15f;

        // Orbit sensitivity
        private float _orbitSensitivity = 0.15f;

        // Static orbit target (used when no alive players — orbit the kill position)
        private bool _hasStaticTarget;
        private float3 _staticTargetPos;
        private float _staticOrbitSpeed = 15f; // degrees/sec auto-orbit when following a static target

        // Smooth player-switch transition
        private bool _transitioning;
        private float _transitionDuration;
        private float _transitionElapsed;
        private Vector3 _transitionFromPos;
        private Quaternion _transitionFromRot;

        // Locked follow mode: orbit angle derived from ghost rotation
        private bool _lockedFollow;

        // Paradigm state
        private CameraMode _gameplayMode; // The gameplay paradigm at death time (for gameplayConfig validity)
        private bool _isIsometric;   // IsometricFixed or IsometricRotatable
        private bool _isTopDown;      // TopDownFixed
        private bool _isRotatable;    // IsometricRotatable (Q/E rotation allowed)

        // Isometric/top-down parameters (read from gameplay CameraConfig or fallback)
        private float _isoAngle;
        private float _isoRotation;
        private float _isoHeight;

        // Captured camera state from gameplay (Cinemachine output at death time)
        private float3 _capturedOffset;
        private quaternion _capturedRotation;
        private float _capturedFOV;
        private float _capturedZoom;
        private bool _hasCapturedState;
        private CameraMode _capturedParadigm; // Which paradigm the captured state is valid for

        public bool SupportsOrbitRotation => !_isIsometric && !_isTopDown;
        public bool UsesCursorAiming => _isIsometric || _isTopDown;

        /// <summary>
        /// Target FOV for CameraManager to apply. Returns captured gameplay FOV if available,
        /// otherwise the configured FOV, or 0 if neither is set.
        /// </summary>
        public float TargetFOV => _hasCapturedState ? _capturedFOV : _fov;

        public void Configure(DeathCameraConfigSO config, CameraMode gameplayMode, CameraConfig gameplayConfig, float gameplayZoom = -1f)
        {
            // TPS config (always read — used as fallback and for TPS mode)
            _followDistance = config.FollowDistance;
            _followHeight = config.FollowHeight;
            _followSmoothTime = config.FollowSmoothTime;
            _fov = config.FOV;
            _lookAtHeight = config.LookAtHeight;
            _pitch = config.DefaultPitch;
            _zoomDistMin = config.ZoomDistanceMin;
            _zoomDistMax = config.ZoomDistanceMax;
            _orbitSensitivity = config.OrbitSensitivity;
            // Match gameplay camera zoom level instead of hardcoded default
            if (gameplayZoom >= 0f)
                _currentZoom = gameplayZoom;
            else if (gameplayConfig != null)
                _currentZoom = gameplayConfig.DefaultZoom;
            else
                _currentZoom = 0.5f;

            // Paradigm detection
            _gameplayMode = gameplayMode;
            _isIsometric = gameplayMode == CameraMode.IsometricFixed || gameplayMode == CameraMode.IsometricRotatable;
            _isTopDown = gameplayMode == CameraMode.TopDownFixed;
            _isRotatable = gameplayMode == CameraMode.IsometricRotatable;

            if (_isIsometric)
            {
                _reportedMode = _isRotatable ? CameraMode.IsometricRotatable : CameraMode.IsometricFixed;
                _isoAngle = gameplayConfig != null ? gameplayConfig.IsometricAngle : config.IsometricAngle;
                _isoRotation = gameplayConfig != null ? gameplayConfig.IsometricRotation : config.IsometricRotation;
                _isoHeight = gameplayConfig != null ? gameplayConfig.IsometricHeight : config.IsometricHeight;
            }
            else if (_isTopDown)
            {
                _reportedMode = CameraMode.TopDownFixed;
                _isoAngle = gameplayConfig != null ? gameplayConfig.TopDownAngle : config.TopDownAngle;
                _isoRotation = 0f;
                _isoHeight = gameplayConfig != null ? gameplayConfig.TopDownHeight : config.TopDownHeight;
            }
            else
            {
                _reportedMode = CameraMode.ThirdPersonFollow;
            }
        }

        /// <summary>
        /// Set a fixed world position to orbit around (e.g., kill position when no alive players).
        /// Clears any ghost follow target.
        /// </summary>
        public void SetStaticTarget(float3 position)
        {
            _followedGhostId = 0;
            _hasStaticTarget = true;
            _staticTargetPos = position;
            _velocity = Vector3.zero;
            UpdateCamera(0f);
        }

        public void SetFollowTarget(ushort ghostId)
        {
            _followedGhostId = ghostId;
            _hasStaticTarget = false;
            _velocity = Vector3.zero;

            // Initialize orbit parameters for TPS mode from the ALIVE player's
            // replicated camera settings (Yaw, Pitch, CurrentDistance are [GhostField]).
            // The captured offset approach only works for isometric (fixed angle),
            // not TPS where the orbit angle is player-facing-relative.
            if (!_isIsometric && !_isTopDown)
            {
                bool initialized = false;

                if (ghostId != 0)
                {
                    var camSettings = GetGhostCameraSettings?.Invoke(ghostId);
                    // Check for live (non-default) values. Defaults are Yaw=0, Pitch=25, Dist=8.
                    // If all match defaults exactly, the values likely haven't replicated yet.
                    bool isStale = !camSettings.HasValue
                        || (camSettings.Value.Yaw == 0f
                            && camSettings.Value.Pitch == 25f
                            && camSettings.Value.CurrentDistance == 8f);

                    if (camSettings.HasValue && !isStale)
                    {
                        var cs = camSettings.Value;
                        // Orbit convention: _orbitAngle is where the CAMERA sits.
                        // Yaw is where the camera LOOKS. Camera sits opposite (+180°).
                        _orbitAngle = cs.Yaw + 180f;
                        _pitch = cs.Pitch;
                        // Use alive player's distance (skip if FPS mode / < 1m)
                        if (cs.CurrentDistance > 1f)
                        {
                            _followDistance = cs.CurrentDistance;
                            float range = _zoomDistMax - _zoomDistMin;
                            if (range > 0.001f)
                                _currentZoom = math.saturate((_followDistance - _zoomDistMin) / range);
                        }
                        initialized = true;
                    }
                    else
                    {
                        // Camera settings are stale/default — fall through to ghost rotation fallback
                    }
                }

                if (!initialized && GetGhostRotation != null && ghostId != 0)
                {
                    // Fallback: derive orbit angle from ghost body rotation (behind the character)
                    quaternion rot = GetGhostRotation(ghostId);
                    float3 forward = math.forward(rot);
                    _orbitAngle = math.degrees(math.atan2(-forward.x, -forward.z));
                }
            }

            // Immediately position so the very first frame reads a valid transform
            UpdateCamera(0f);
        }

        /// <summary>
        /// Start a smooth transition from the given camera pose (used for player switching).
        /// </summary>
        public void SetTransitionFrom(Vector3 fromPos, Quaternion fromRot, float duration)
        {
            if (duration <= 0f) return;
            _transitioning = true;
            _transitionDuration = duration;
            _transitionElapsed = 0f;
            _transitionFromPos = fromPos;
            _transitionFromRot = fromRot;
        }

        public void SetFirstPerson(bool enabled)
        {
            _firstPerson = enabled;
        }

        public void SetLockedFollow(bool locked)
        {
            _lockedFollow = locked;
        }

        /// <summary>
        /// Reconfigure the camera style at runtime for spectator mode switching.
        /// Does NOT reset follow target, ghost delegates, or transition state.
        /// Preserves zoom level. Clears captured camera state when switching away
        /// from the originally captured paradigm (restores if switching back).
        /// </summary>
        public void SetCameraStyle(CameraMode newMode, CameraConfig gameplayConfig, DeathCameraConfigSO config)
        {
            _isIsometric = newMode == CameraMode.IsometricFixed || newMode == CameraMode.IsometricRotatable;
            _isTopDown = newMode == CameraMode.TopDownFixed;
            _isRotatable = newMode == CameraMode.IsometricRotatable;

            // Only use gameplayConfig for iso/topdown params when the gameplay paradigm
            // actually matches — otherwise the gameplayConfig's values are placeholder defaults
            // for a different paradigm (e.g., TPS config's IsometricAngle is meaningless).
            bool gameplayWasIso = _gameplayMode == CameraMode.IsometricFixed || _gameplayMode == CameraMode.IsometricRotatable;
            bool gameplayWasTopDown = _gameplayMode == CameraMode.TopDownFixed;

            if (_isIsometric)
            {
                _reportedMode = _isRotatable ? CameraMode.IsometricRotatable : CameraMode.IsometricFixed;
                bool useGameplay = gameplayWasIso && gameplayConfig != null;
                _isoAngle = useGameplay ? gameplayConfig.IsometricAngle : config.IsometricAngle;
                _isoRotation = useGameplay ? gameplayConfig.IsometricRotation : config.IsometricRotation;
                _isoHeight = useGameplay ? gameplayConfig.IsometricHeight : config.IsometricHeight;
            }
            else if (_isTopDown)
            {
                _reportedMode = CameraMode.TopDownFixed;
                bool useGameplay = gameplayWasTopDown && gameplayConfig != null;
                _isoAngle = useGameplay ? gameplayConfig.TopDownAngle : config.TopDownAngle;
                _isoRotation = 0f;
                _isoHeight = useGameplay ? gameplayConfig.TopDownHeight : config.TopDownHeight;
            }
            else
            {
                _reportedMode = CameraMode.ThirdPersonFollow;
            }

            // Captured camera state is paradigm-specific (Cinemachine output at death time).
            // Restore it when switching back to the original paradigm; clear it otherwise.
            if (_reportedMode == _capturedParadigm)
                _hasCapturedState = _capturedFOV > 0f; // Restore if we have valid data
            else
                _hasCapturedState = false;

            // Reset velocity for smooth transition to new camera position
            _velocity = Vector3.zero;
        }

        /// <summary>
        /// Set captured camera state from gameplay (Cinemachine output at death time).
        /// This allows the death cam to match the exact camera position/rotation/FOV
        /// that was visible during gameplay, instead of recomputing from CameraConfig.
        /// </summary>
        public void SetCapturedCameraState(float3 offset, quaternion rotation, float fov, float zoomLevel)
        {
            _capturedOffset = offset;
            _capturedRotation = rotation;
            _capturedFOV = fov;
            _capturedZoom = zoomLevel;
            _hasCapturedState = true;
            _capturedParadigm = _reportedMode;
        }

        /// <summary>
        /// Rotate the isometric camera by the given degrees (Q/E rotation for IsometricRotatable).
        /// </summary>
        public void RotateIsometric(float degrees)
        {
            if (!_isRotatable) return;
            _isoRotation = (_isoRotation + degrees) % 360f;
            if (_isoRotation < 0f) _isoRotation += 360f;
        }

        public void Initialize(CameraConfig config) { }

        public void UpdateCamera(float deltaTime)
        {
            // Resolve target position: ghost follow target, or static orbit target
            float3 targetPos;
            if (_followedGhostId != 0 && GetGhostPosition != null)
            {
                targetPos = GetGhostPosition(_followedGhostId);
            }
            else if (_hasStaticTarget)
            {
                targetPos = _staticTargetPos;
            }
            else
            {
                return; // No target at all
            }

            if (_firstPerson && _followedGhostId != 0 && !_isIsometric && !_isTopDown)
            {
                // First person: lock to entity position at eye level (TPS only)
                quaternion targetRot = GetGhostRotation != null
                    ? GetGhostRotation(_followedGhostId)
                    : quaternion.identity;

                float3 eyePos = targetPos;
                eyePos.y += 1.6f;

                transform.SetPositionAndRotation((Vector3)eyePos, targetRot);
                _transitioning = false;
            }
            else if (_isIsometric || _isTopDown)
            {
                UpdateCameraFixedAngle(targetPos, deltaTime);
            }
            else
            {
                UpdateCameraThirdPerson(targetPos, deltaTime);
            }

        }

        /// <summary>
        /// Isometric/TopDown: fixed-angle camera from above.
        /// When captured camera state is available (from Cinemachine at death time),
        /// uses that for pixel-perfect matching. Otherwise falls back to CameraConfig math.
        /// </summary>
        private void UpdateCameraFixedAngle(float3 targetPos, float deltaTime)
        {
            float3 offset;
            Quaternion desiredRot;

            if (_hasCapturedState)
            {
                // Use captured Cinemachine camera state for exact matching.
                // Scale offset by zoom ratio to support scroll-wheel zoom.
                float capturedZoomMul = math.lerp(0.5f, 1.5f, _capturedZoom);
                float currentZoomMul = math.lerp(0.5f, 1.5f, _currentZoom);
                float zoomScale = capturedZoomMul > 0.001f ? currentZoomMul / capturedZoomMul : 1f;
                offset = _capturedOffset * zoomScale;
                desiredRot = _capturedRotation;
            }
            else
            {
                // Fallback: compute from CameraConfig values
                float pitchRad = math.radians(_isoAngle);
                float yawRad = math.radians(_isoRotation);
                float horizontalDistance = _isoHeight / math.tan(pitchRad);

                offset = new float3(
                    -math.sin(yawRad) * horizontalDistance,
                    _isoHeight,
                    -math.cos(yawRad) * horizontalDistance
                );

                float zoomMultiplier = math.lerp(0.5f, 1.5f, _currentZoom);
                offset *= zoomMultiplier;
                desiredRot = Quaternion.Euler(_isoAngle, _isoRotation, 0f);
            }

            float3 desiredPos = targetPos + offset;

            // Snap immediately when deltaTime is 0
            Vector3 smoothed;
            Quaternion smoothedRot;
            if (deltaTime <= 0f)
            {
                smoothed = (Vector3)desiredPos;
                smoothedRot = desiredRot;
                _velocity = Vector3.zero;
            }
            else
            {
                smoothed = Vector3.SmoothDamp(
                    transform.position,
                    (Vector3)desiredPos,
                    ref _velocity,
                    _followSmoothTime,
                    Mathf.Infinity,
                    deltaTime
                );
                smoothedRot = Quaternion.Slerp(transform.rotation, desiredRot, deltaTime * 10f);
            }

            // Player-switch transition blend
            if (_transitioning)
            {
                _transitionElapsed += deltaTime;
                float progress = Mathf.Clamp01(_transitionElapsed / _transitionDuration);
                float t = progress * progress * (3f - 2f * progress); // smoothstep

                smoothed = Vector3.Lerp(_transitionFromPos, smoothed, t);
                smoothedRot = Quaternion.Slerp(_transitionFromRot, smoothedRot, t);

                transform.SetPositionAndRotation(smoothed, smoothedRot);

                if (progress >= 1f)
                    _transitioning = false;
                return;
            }

            transform.SetPositionAndRotation(smoothed, smoothedRot);
        }

        /// <summary>
        /// TPS: third-person orbit with pitch control.
        /// </summary>
        private void UpdateCameraThirdPerson(float3 targetPos, float deltaTime)
        {
            // Auto-orbit when following a static target (no alive players) so the camera
            // doesn't freeze in place. Users can still orbit manually with mouse.
            if (_hasStaticTarget && _followedGhostId == 0 && deltaTime > 0f)
            {
                _orbitAngle += _staticOrbitSpeed * deltaTime;
            }

            // Locked follow: use replicated camera settings from watched player
            if (_lockedFollow && _followedGhostId != 0 && deltaTime > 0f)
            {
                var camSettings = GetGhostCameraSettings?.Invoke(_followedGhostId);
                // Detect stale defaults (Yaw=0, Pitch=25, Dist=8 = never replicated)
                bool isStale = !camSettings.HasValue
                    || (camSettings.Value.Yaw == 0f
                        && camSettings.Value.Pitch == 25f
                        && camSettings.Value.CurrentDistance == 8f);

                if (camSettings.HasValue && !isStale)
                {
                    // Use the watched player's exact camera Yaw, Pitch, and Distance.
                    // Yaw is where the camera LOOKS; orbit angle is where it SITS (+180°).
                    float targetOrbitAngle = camSettings.Value.Yaw + 180f;
                    float targetPitch = camSettings.Value.Pitch;
                    float targetDist = camSettings.Value.CurrentDistance;

                    // Smooth transitions to avoid snapping
                    _orbitAngle = Mathf.LerpAngle(_orbitAngle, targetOrbitAngle, deltaTime * 8f);
                    _pitch = Mathf.Lerp(_pitch, targetPitch, deltaTime * 8f);
                    if (targetDist > 0.01f)
                        _followDistance = Mathf.Lerp(_followDistance, targetDist, deltaTime * 8f);
                }
                else
                {
                    // Fallback: derive from ghost body rotation (stale settings or bot/AI player)
                    if (GetGhostRotation != null)
                    {
                        quaternion rot = GetGhostRotation(_followedGhostId);
                        float3 forward = math.forward(rot);
                        float targetAngle = math.degrees(math.atan2(-forward.x, -forward.z));
                        _orbitAngle = Mathf.LerpAngle(_orbitAngle, targetAngle, deltaTime * 8f);
                    }
                }
            }

            // Pivot offset: gameplay camera orbits around playerPos + CombatPivotOffset (e.g. 0, 2.2, 0).
            // In locked mode, read the watched player's replicated CameraViewConfig.CombatPivotOffset
            // so the spectator camera orbits around the same point as their gameplay camera.
            float3 pivotOffset = float3.zero;
            if (_lockedFollow && _followedGhostId != 0)
            {
                var offset = GetGhostPivotOffset?.Invoke(_followedGhostId);
                if (offset.HasValue)
                    pivotOffset = offset.Value;
            }

            float3 pivotPos = targetPos + pivotOffset;
            float3 lookAtTarget = _lockedFollow
                ? pivotPos  // Match gameplay camera: look at the pivot point
                : targetPos + new float3(0f, _lookAtHeight, 0f);

            float3 desiredPos;
            if (_lockedFollow)
            {
                // Replicate Cinemachine's ThirdPersonFollow formula exactly.
                // At runtime: ShoulderOffset = (0,0,0) (scene serialized _shoulderOffset is zero,
                // and CameraSide=0.5 lerps any X to 0 via Lerp(-x, x, 0.5)).
                // VerticalArmLength = 0.4 (scene serialized on CinemachineThirdPersonFollow).
                // So the Cinemachine local offset from pivot is (0, 0.4, -distance).
                // pivotPos = playerPos + CombatPivotOffset (replicated from watched player).
                // _orbitAngle = Yaw + 180, so Yaw = _orbitAngle - 180.
                float yaw = _orbitAngle - 180f;
                Quaternion rot = Quaternion.Euler(_pitch, yaw, 0f);
                Vector3 localOffset = new Vector3(0f, 0.4f, -_followDistance);
                desiredPos = pivotPos + (float3)(rot * localOffset);
            }
            else
            {
                // Standard orbit formula for unlocked TPS (spectator-friendly, with elevated pivot)
                float pitchRad = math.radians(_pitch);
                float horizontalDist = _followDistance * math.cos(pitchRad);
                float verticalOffset = _followHeight + _followDistance * math.sin(pitchRad);

                float3 offset = new float3(
                    math.sin(math.radians(_orbitAngle)) * horizontalDist,
                    verticalOffset,
                    math.cos(math.radians(_orbitAngle)) * horizontalDist
                );
                desiredPos = targetPos + offset;
            }

            // Snap immediately when deltaTime is 0 (initial positioning from
            // SetFollowTarget/SetStaticTarget) — SmoothDamp returns current pos when dt=0.
            Vector3 smoothed;
            if (deltaTime <= 0f)
            {
                smoothed = (Vector3)desiredPos;
                _velocity = Vector3.zero;
            }
            else
            {
                smoothed = Vector3.SmoothDamp(
                    transform.position,
                    (Vector3)desiredPos,
                    ref _velocity,
                    _followSmoothTime,
                    Mathf.Infinity,
                    deltaTime
                );
            }

            // Collision avoidance disabled for spectator — the basic SphereCast approach
            // oscillates with SmoothDamp (snap-in vs pull-out each frame = flicker).
            // The gameplay camera (Cinemachine) has its own deoccluder; spectator cam
            // allows minor wall clipping rather than constant bouncing.

            // Player-switch transition blend
            if (_transitioning)
            {
                _transitionElapsed += deltaTime;
                float progress = Mathf.Clamp01(_transitionElapsed / _transitionDuration);
                float t = progress * progress * (3f - 2f * progress); // smoothstep

                smoothed = Vector3.Lerp(_transitionFromPos, smoothed, t);
                Quaternion targetRot = _lockedFollow
                    ? Quaternion.Euler(_pitch, _orbitAngle - 180f, 0f)
                    : Quaternion.LookRotation((Vector3)lookAtTarget - smoothed, Vector3.up);
                Quaternion blendedRot = Quaternion.Slerp(_transitionFromRot, targetRot, t);

                transform.SetPositionAndRotation(smoothed, blendedRot);

                if (progress >= 1f)
                    _transitioning = false;
                return;
            }

            // Rotation: locked mode uses the same rotation as the gameplay camera (Euler(pitch, yaw, 0))
            // rather than LookAt, because Cinemachine sets RawOrientation = targetRot directly
            // (no LookAt target). Using LookAt would shift the character's screen position.
            if (_lockedFollow)
            {
                float yawForRot = _orbitAngle - 180f;
                Quaternion directRot = Quaternion.Euler(_pitch, yawForRot, 0f);
                Quaternion smoothedRot = deltaTime > 0f
                    ? Quaternion.Slerp(transform.rotation, directRot, deltaTime * 15f)
                    : directRot;
                transform.SetPositionAndRotation(smoothed, smoothedRot);
                return;
            }

            // Unlocked mode: LookAt-based rotation toward the lookAt target.
            // Smooth to avoid stutter from ghost position jitter
            // (NetCode interpolation + SmoothDamp double-smoothing causes rotation snaps
            // when using raw LookAt on un-smoothed target position)
            Vector3 lookDir = (Vector3)lookAtTarget - smoothed;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion desiredRot = Quaternion.LookRotation(lookDir, Vector3.up);
                Quaternion smoothedRot = deltaTime > 0f
                    ? Quaternion.Slerp(transform.rotation, desiredRot, deltaTime * 15f)
                    : desiredRot;
                transform.SetPositionAndRotation(smoothed, smoothedRot);
            }
            else
            {
                transform.position = smoothed;
            }
        }

        public Transform GetCameraTransform() => transform;

        public Plane GetAimPlane()
        {
            float3 pos = GetGhostPosition != null ? GetGhostPosition(_followedGhostId) : float3.zero;
            return new Plane(Vector3.up, (Vector3)pos);
        }

        public float3 TransformMovementInput(float2 input) => float3.zero;

        public float3 TransformAimInput(float2 cursorScreenPos)
        {
            return GetGhostPosition != null ? GetGhostPosition(_followedGhostId) : float3.zero;
        }

        public void SetTarget(Entity entity, Transform visualTransform = null) { }

        public void SetZoom(float zoomLevel)
        {
            _currentZoom = math.saturate(zoomLevel);

            // For TPS mode, also update followDistance for backward compat
            if (!_isIsometric && !_isTopDown)
            {
                _followDistance = math.lerp(_zoomDistMin, _zoomDistMax, _currentZoom);
            }
        }

        public float GetZoom()
        {
            if (_isIsometric || _isTopDown)
            {
                return _currentZoom;
            }

            // TPS: derive from distance
            float range = _zoomDistMax - _zoomDistMin;
            return range > 0.001f ? math.saturate((_followDistance - _zoomDistMin) / range) : 0.5f;
        }

        public void Shake(float intensity, float duration) { }

        public void HandleRotationInput(float2 rotationInput)
        {
            if (_isIsometric || _isTopDown) return; // No mouse orbit for fixed-angle paradigms
            _orbitAngle += rotationInput.x * _orbitSensitivity;
            _pitch = Mathf.Clamp(_pitch - rotationInput.y * _orbitSensitivity, -20f, 60f);
        }
    }
}
