using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace DIG.Targeting.UI
{
    /// <summary>
    /// Lock-on reticle UI for Souls-like targeting.
    /// Displays when locked onto a target with visual feedback.
    /// </summary>
    public class LockOnReticleUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image _reticleImage;
        [SerializeField] private Camera _camera;
        
        [Header("Visuals")]
        [SerializeField] private Sprite _lockedSprite;
        [SerializeField] private Color _lockedColor = Color.red;
        [SerializeField] private float _rotationSpeed = 45f;
        
        [Header("Settings")]
        [SerializeField] private Vector2 _screenOffset = Vector2.zero;
        
        // ECS link
        private EntityManager _entityManager;
        private Entity _playerEntity;
        private bool _initialized;
        
        private void Start()
        {
            if (_camera == null)
                _camera = Camera.main;
                
            if (_reticleImage != null)
                _reticleImage.gameObject.SetActive(false);
        }
        
        /// <summary>
        /// Initialize with player entity reference.
        /// </summary>
        public void Initialize(EntityManager em, Entity playerEntity)
        {
            _entityManager = em;
            _playerEntity = playerEntity;
            _initialized = true;
        }
        
        private void LateUpdate()
        {
            if (!_initialized || _reticleImage == null || _camera == null)
                return;
                
            if (_playerEntity == Entity.Null || !_entityManager.Exists(_playerEntity))
            {
                _reticleImage.gameObject.SetActive(false);
                return;
            }
            
            if (!_entityManager.HasComponent<TargetData>(_playerEntity))
            {
                _reticleImage.gameObject.SetActive(false);
                return;
            }
            
            var targetData = _entityManager.GetComponentData<TargetData>(_playerEntity);
            
            // Only show for lock-on mode with valid target
            if (targetData.Mode != TargetingMode.LockOn || 
                targetData.TargetEntity == Entity.Null ||
                !targetData.HasValidTarget)
            {
                _reticleImage.gameObject.SetActive(false);
                return;
            }
            
            // Show reticle
            _reticleImage.gameObject.SetActive(true);
            
            if (_lockedSprite != null)
                _reticleImage.sprite = _lockedSprite;
            _reticleImage.color = _lockedColor;
            
            // Get target screen position
            float3 targetPoint = targetData.TargetPoint;
            Vector3 screenPoint = _camera.WorldToScreenPoint(targetPoint);
            
            // Check if behind camera
            if (screenPoint.z < 0)
            {
                _reticleImage.gameObject.SetActive(false);
                return;
            }
            
            // Position on screen
            var rectTransform = _reticleImage.rectTransform;
            rectTransform.position = new Vector3(
                screenPoint.x + _screenOffset.x,
                screenPoint.y + _screenOffset.y,
                0f
            );
            
            // Rotate for visual effect
            rectTransform.Rotate(0, 0, _rotationSpeed * Time.deltaTime);
        }
    }
}
