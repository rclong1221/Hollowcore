using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace DIG.Replay
{
    /// <summary>
    /// EPIC 18.10: Multi-mode spectator camera controller.
    /// MonoBehaviour that reads ghost entity positions from ECS
    /// and drives the camera directly.
    ///
    /// Modes: FreeCam, FollowPlayer, FirstPerson, Orbit, KillCam.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class SpectatorCamera : MonoBehaviour
    {
        public static SpectatorCamera Instance { get; private set; }

        [Header("Config")]
        [SerializeField] private CameraPresetSO _defaultPreset;
        [SerializeField] private Camera _camera;

        // State
        private SpectatorCameraMode _currentMode = SpectatorCameraMode.FreeCam;
        private CameraPresetSO _activePreset;
        private ushort _followedGhostId;
        private World _clientWorld;

        // Cached fallback preset (avoid ScriptableObject.CreateInstance per frame)
        private CameraPresetSO _fallbackPreset;

        // Cached ghost data — refreshed once per LateUpdate, shared by all mode updates
        private EntityQuery _ghostQuery;
        private bool _ghostQueryCreated;
        private NativeArray<GhostInstance> _cachedGhosts;
        private NativeArray<LocalTransform> _cachedTransforms;
        private bool _ghostDataValid;

        // Free cam state
        private float3 _freeCamPosition;
        private float _freeCamYaw;
        private float _freeCamPitch;

        // Follow cam state
        private float _followOrbitAngle;
        private Vector3 _followVelocity;

        // Orbit state
        private float _orbitAngle;
        private float3 _orbitCenter;

        // Kill cam state
        private bool _killCamActive;
        private float _killCamTimer;
        private float3 _killCamTarget;

        // Player list for cycling
        private readonly List<ushort> _playerGhostIds = new();
        private int _playerIndex;

        // Events
        public event System.Action<SpectatorCameraMode> OnModeChanged;
        public event System.Action<int> OnPlayerChanged;

        public SpectatorCameraMode CurrentMode => _currentMode;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _activePreset = _defaultPreset;
            if (_camera == null) _camera = Camera.main;

            // Create fallback preset once instead of per-frame
            _fallbackPreset = ScriptableObject.CreateInstance<CameraPresetSO>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() { Instance = null; }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            DisposeGhostData();
            if (_fallbackPreset != null) Destroy(_fallbackPreset);
        }

        /// <summary>
        /// Get the effective preset, using cached fallback if no preset assigned.
        /// </summary>
        private CameraPresetSO GetPreset() => _activePreset != null ? _activePreset : _fallbackPreset;

        public void SetMode(SpectatorCameraMode mode)
        {
            _currentMode = mode;
            OnModeChanged?.Invoke(mode);
        }

        public void CycleMode()
        {
            int next = ((int)_currentMode + 1) % 4; // Skip KillCam (auto-triggered)
            SetMode((SpectatorCameraMode)next);
        }

        public void FollowGhostId(ushort ghostId)
        {
            _followedGhostId = ghostId;
            if (_currentMode == SpectatorCameraMode.FreeCam)
                SetMode(SpectatorCameraMode.FollowPlayer);
        }

        public void FollowNextPlayer()
        {
            if (_playerGhostIds.Count == 0) return;
            _playerIndex = (_playerIndex + 1) % _playerGhostIds.Count;
            _followedGhostId = _playerGhostIds[_playerIndex];
            OnPlayerChanged?.Invoke(_playerIndex);
        }

        public void FollowPreviousPlayer()
        {
            if (_playerGhostIds.Count == 0) return;
            _playerIndex = (_playerIndex - 1 + _playerGhostIds.Count) % _playerGhostIds.Count;
            _followedGhostId = _playerGhostIds[_playerIndex];
            OnPlayerChanged?.Invoke(_playerIndex);
        }

        public void SetPlayerList(List<ushort> ghostIds)
        {
            _playerGhostIds.Clear();
            _playerGhostIds.AddRange(ghostIds);
            _playerIndex = 0;
        }

        /// <summary>
        /// Trigger a kill-cam at the given position for the configured duration.
        /// </summary>
        public void TriggerKillCam(float3 position, float duration)
        {
            _killCamTarget = position;
            _killCamActive = true;
            _killCamTimer = duration;
            SetMode(SpectatorCameraMode.KillCam);
        }

        private void LateUpdate()
        {
            if (_camera == null) return;

            if (_clientWorld == null || !_clientWorld.IsCreated)
            {
                _clientWorld = FindClientWorld();
                _ghostQueryCreated = false;
            }

            // Refresh ghost data once per frame (shared by all camera modes)
            RefreshGhostData();

            switch (_currentMode)
            {
                case SpectatorCameraMode.FreeCam:
                    UpdateFreeCam();
                    break;
                case SpectatorCameraMode.FollowPlayer:
                    UpdateFollowCam();
                    break;
                case SpectatorCameraMode.FirstPerson:
                    UpdateFirstPersonCam();
                    break;
                case SpectatorCameraMode.Orbit:
                    UpdateOrbitCam();
                    break;
                case SpectatorCameraMode.KillCam:
                    UpdateKillCam();
                    break;
            }

            // Dispose ghost data after all modes have used it
            DisposeGhostData();

            // Apply FOV
            var preset = GetPreset();
            if (preset != null)
                _camera.fieldOfView = preset.FOV;
        }

        /// <summary>
        /// Query ghost entities once per frame, cache results for all GetGhostPosition/Rotation calls.
        /// </summary>
        private void RefreshGhostData()
        {
            _ghostDataValid = false;
            if (_clientWorld == null || !_clientWorld.IsCreated) return;

            var em = _clientWorld.EntityManager;

            // Create query once and cache it
            if (!_ghostQueryCreated)
            {
                _ghostQueryCreated = true;
                _ghostQuery = em.CreateEntityQuery(
                    ComponentType.ReadOnly<GhostInstance>(),
                    ComponentType.ReadOnly<LocalTransform>()
                );
            }

            if (_ghostQuery.CalculateEntityCount() == 0) return;

            _cachedGhosts = _ghostQuery.ToComponentDataArray<GhostInstance>(Allocator.Temp);
            _cachedTransforms = _ghostQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            _ghostDataValid = true;
        }

        private void DisposeGhostData()
        {
            if (!_ghostDataValid) return;
            if (_cachedGhosts.IsCreated) _cachedGhosts.Dispose();
            if (_cachedTransforms.IsCreated) _cachedTransforms.Dispose();
            _ghostDataValid = false;
        }

        private void UpdateFreeCam()
        {
            var p = GetPreset();
            float speed = p.MoveSpeed * (Input.GetKey(KeyCode.LeftShift) ? p.FastMoveMultiplier : 1f);

            // Mouse look
            _freeCamYaw += Input.GetAxis("Mouse X") * p.MouseSensitivity;
            _freeCamPitch -= Input.GetAxis("Mouse Y") * p.MouseSensitivity;
            _freeCamPitch = Mathf.Clamp(_freeCamPitch, -89f, 89f);

            var rotation = Quaternion.Euler(_freeCamPitch, _freeCamYaw, 0f);

            // WASD movement
            float3 move = float3.zero;
            if (Input.GetKey(KeyCode.W)) move.z += 1f;
            if (Input.GetKey(KeyCode.S)) move.z -= 1f;
            if (Input.GetKey(KeyCode.A)) move.x -= 1f;
            if (Input.GetKey(KeyCode.D)) move.x += 1f;
            if (Input.GetKey(KeyCode.E)) move.y += 1f;
            if (Input.GetKey(KeyCode.Q)) move.y -= 1f;

            float3 worldMove = rotation * (Vector3)move;
            _freeCamPosition += worldMove * speed * Time.unscaledDeltaTime;

            _camera.transform.SetPositionAndRotation(_freeCamPosition, rotation);
        }

        private void UpdateFollowCam()
        {
            var p = GetPreset();
            float3 targetPos = GetGhostPosition(_followedGhostId);
            if (math.all(targetPos == float3.zero) && _playerGhostIds.Count > 0)
            {
                _followedGhostId = _playerGhostIds[0];
                targetPos = GetGhostPosition(_followedGhostId);
            }

            // Orbit angle from mouse
            _followOrbitAngle += Input.GetAxis("Mouse X") * 2f;

            float3 offset = new float3(
                math.sin(math.radians(_followOrbitAngle)) * p.FollowDistance,
                p.FollowHeight,
                math.cos(math.radians(_followOrbitAngle)) * p.FollowDistance
            );

            float3 desiredPos = targetPos + offset;
            float3 currentPos = (float3)(Vector3)_camera.transform.position;
            float3 smoothed = Vector3.SmoothDamp(currentPos, desiredPos, ref _followVelocity, p.FollowSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);

            _camera.transform.position = (Vector3)smoothed;
            _camera.transform.LookAt((Vector3)targetPos);
        }

        private void UpdateFirstPersonCam()
        {
            float3 targetPos = GetGhostPosition(_followedGhostId);
            quaternion targetRot = GetGhostRotation(_followedGhostId);

            // Offset up slightly for eye-level
            targetPos.y += 1.6f;

            _camera.transform.SetPositionAndRotation((Vector3)targetPos, targetRot);
        }

        private void UpdateOrbitCam()
        {
            var p = GetPreset();

            // Orbit around followed entity or world center
            float3 center = _followedGhostId != 0
                ? GetGhostPosition(_followedGhostId)
                : _orbitCenter;

            _orbitAngle += p.OrbitSpeed * Time.unscaledDeltaTime;

            float3 offset = new float3(
                math.sin(math.radians(_orbitAngle)) * p.OrbitRadius,
                p.OrbitHeight,
                math.cos(math.radians(_orbitAngle)) * p.OrbitRadius
            );

            _camera.transform.position = (Vector3)(center + offset);
            _camera.transform.LookAt((Vector3)center);
        }

        private void UpdateKillCam()
        {
            if (!_killCamActive)
            {
                SetMode(SpectatorCameraMode.FreeCam);
                return;
            }

            _killCamTimer -= Time.unscaledDeltaTime;
            if (_killCamTimer <= 0f)
            {
                _killCamActive = false;
                SetMode(SpectatorCameraMode.FollowPlayer);
                return;
            }

            // Slow orbit around kill position
            float angle = _killCamTimer * 30f;
            float3 offset = new float3(
                math.sin(math.radians(angle)) * 5f,
                3f,
                math.cos(math.radians(angle)) * 5f
            );

            _camera.transform.position = (Vector3)(_killCamTarget + offset);
            _camera.transform.LookAt((Vector3)_killCamTarget);
        }

        /// <summary>
        /// Look up ghost position from cached per-frame data. O(n) scan but no allocations.
        /// </summary>
        private float3 GetGhostPosition(ushort ghostId)
        {
            if (!_ghostDataValid || ghostId == 0) return float3.zero;

            for (int i = 0; i < _cachedGhosts.Length; i++)
            {
                if ((ushort)_cachedGhosts[i].ghostId == ghostId)
                    return _cachedTransforms[i].Position;
            }
            return float3.zero;
        }

        /// <summary>
        /// Look up ghost rotation from cached per-frame data. O(n) scan but no allocations.
        /// </summary>
        private quaternion GetGhostRotation(ushort ghostId)
        {
            if (!_ghostDataValid || ghostId == 0) return quaternion.identity;

            for (int i = 0; i < _cachedGhosts.Length; i++)
            {
                if ((ushort)_cachedGhosts[i].ghostId == ghostId)
                    return _cachedTransforms[i].Rotation;
            }
            return quaternion.identity;
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
    }
}
