using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Cinemachine;

namespace DIG.CameraSystem.Cinemachine
{
    /// <summary>
    /// EPIC 14.18 - Cinemachine Shake Bridge
    /// Converts ECS CameraShake component to Cinemachine Impulse system.
    /// 
    /// Attach to the same GameObject as CinemachineCameraController or camera rig.
    /// Reads CameraShake from player entity and generates Cinemachine impulses.
    /// </summary>
    [RequireComponent(typeof(CinemachineImpulseSource))]
    public class CinemachineShakeBridge : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Multiplier for shake amplitude when converting to impulse")]
        [SerializeField] private float _amplitudeMultiplier = 1f;
        
        [Tooltip("Default impulse velocity direction")]
        [SerializeField] private Vector3 _defaultDirection = new Vector3(0f, 1f, 0f);
        
        [Tooltip("Minimum amplitude to trigger impulse (avoids micro-shakes)")]
        [SerializeField] private float _minAmplitudeThreshold = 0.01f;
        
        // State
        private CinemachineImpulseSource _impulseSource;
        private World _clientWorld;
        private Entity _playerEntity;
        private float _lastShakeAmplitude;
        private bool _isInitialized;
        
        private void Awake()
        {
            _impulseSource = GetComponent<CinemachineImpulseSource>();
            
            if (_impulseSource == null)
            {
                _impulseSource = gameObject.AddComponent<CinemachineImpulseSource>();
            }
            
            // Configure impulse source for camera shake
            ConfigureImpulseSource();
        }
        
        private void ConfigureImpulseSource()
        {
            if (_impulseSource == null) return;
            
            // Set up default impulse definition for shake
            _impulseSource.ImpulseDefinition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
            _impulseSource.ImpulseDefinition.ImpulseDuration = 0.2f;
        }
        
        private void LateUpdate()
        {
            if (!_isInitialized)
            {
                TryInitialize();
                return;
            }
            
            CheckAndTriggerShake();
        }
        
        private void TryInitialize()
        {
            // Find client world
            if (_clientWorld == null || !_clientWorld.IsCreated)
            {
                foreach (var world in World.All)
                {
                    if (world.IsCreated && world.Name == "ClientWorld")
                    {
                        _clientWorld = world;
                        break;
                    }
                }
                
                if (_clientWorld == null) return;
            }
            
            // Find local player entity
            var query = _clientWorld.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<Unity.NetCode.GhostOwnerIsLocal>()
            );
            
            if (!query.IsEmpty)
            {
                var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
                if (entities.Length > 0)
                {
                    _playerEntity = entities[0];
                    _isInitialized = true;
                }
                entities.Dispose();
            }
            query.Dispose();
        }
        
        private void CheckAndTriggerShake()
        {
            if (_clientWorld == null || !_clientWorld.IsCreated) return;
            if (_playerEntity == Entity.Null || !_clientWorld.EntityManager.Exists(_playerEntity)) 
            {
                _isInitialized = false;
                return;
            }
            
            var em = _clientWorld.EntityManager;
            
            if (!em.HasComponent<CameraShake>(_playerEntity)) return;
            
            var shake = em.GetComponentData<CameraShake>(_playerEntity);
            
            // Check if this is a new shake (amplitude increased)
            if (shake.Amplitude > _lastShakeAmplitude + _minAmplitudeThreshold)
            {
                // New shake triggered - generate impulse
                float impulseForce = shake.Amplitude * _amplitudeMultiplier;
                
                // Use shake frequency to influence direction variation
                Vector3 direction = _defaultDirection;
                if (shake.Frequency > 0)
                {
                    // Add some randomness based on frequency
                    direction += UnityEngine.Random.insideUnitSphere * 0.3f;
                    direction.Normalize();
                }
                
                _impulseSource.GenerateImpulse(direction * impulseForce);
                
                DebugLog.LogCamera($"[CinemachineShakeBridge] Generated impulse: force={impulseForce:F2}");
            }
            
            _lastShakeAmplitude = shake.Amplitude;
        }
        
        /// <summary>
        /// Manually trigger a shake impulse (for non-ECS sources).
        /// </summary>
        public void TriggerShake(float amplitude, float frequency = 25f, Vector3? direction = null)
        {
            if (_impulseSource == null) return;
            
            Vector3 dir = direction ?? _defaultDirection;
            _impulseSource.GenerateImpulse(dir * amplitude * _amplitudeMultiplier);
        }
        
        /// <summary>
        /// Trigger directional shake (e.g., from explosion position).
        /// </summary>
        public void TriggerDirectionalShake(Vector3 sourcePosition, float amplitude)
        {
            if (_impulseSource == null) return;
            if (Camera.main == null) return;
            
            // Calculate direction from source to camera
            Vector3 toCamera = Camera.main.transform.position - sourcePosition;
            Vector3 direction = toCamera.normalized;
            
            _impulseSource.GenerateImpulse(direction * amplitude * _amplitudeMultiplier);
        }
    }
}
