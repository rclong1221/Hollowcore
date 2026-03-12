using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace DIG.Targeting.UI
{
    /// <summary>
    /// Displays a target highlight icon that follows the current target on screen.
    /// Based on OPSIVE AimTargetMonitor pattern, adapted to read from ECS TargetData.
    /// </summary>
    public class TargetHighlightUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image _highlightImage;
        [SerializeField] private Camera _camera;
        
        [Header("Icons")]
        [SerializeField] private Sprite _softTargetIcon;
        [SerializeField] private Sprite _lockedTargetIcon;
        
        [Header("Colors")]
        [SerializeField] private Color _softTargetColor = Color.white;
        [SerializeField] private Color _lockedTargetColor = Color.red;
        
        [Header("Settings")]
        [SerializeField] private Vector2 _screenOffset = Vector2.zero;
        [SerializeField] private float _hideDistance = 100f;
        
        // ECS link
        private EntityManager _entityManager;
        private Entity _playerEntity;
        private bool _initialized;
        
        private void Start()
        {
            if (_camera == null)
                _camera = Camera.main;
                
            if (_highlightImage != null)
                _highlightImage.gameObject.SetActive(false);
        }
        
        /// <summary>
        /// Initialize with player entity reference.
        /// Call this after player spawns.
        /// </summary>
        public void Initialize(EntityManager em, Entity playerEntity)
        {
            _entityManager = em;
            _playerEntity = playerEntity;
            _initialized = true;
        }
        
        private void LateUpdate()
        {
            if (!_initialized || _highlightImage == null || _camera == null)
                return;
                
            if (_playerEntity == Entity.Null || !_entityManager.Exists(_playerEntity))
            {
                _highlightImage.gameObject.SetActive(false);
                return;
            }
            
            if (!_entityManager.HasComponent<TargetData>(_playerEntity))
            {
                _highlightImage.gameObject.SetActive(false);
                return;
            }
            
            var targetData = _entityManager.GetComponentData<TargetData>(_playerEntity);
            
            if (!targetData.HasValidTarget)
            {
                _highlightImage.gameObject.SetActive(false);
                return;
            }
            
            // Get target screen position
            float3 targetPoint = targetData.TargetPoint;
            Vector3 screenPoint = _camera.WorldToScreenPoint(targetPoint);
            
            // Check if behind camera
            if (screenPoint.z < 0 || screenPoint.z > _hideDistance)
            {
                _highlightImage.gameObject.SetActive(false);
                return;
            }
            
            // Show and position
            _highlightImage.gameObject.SetActive(true);
            
            // Determine if locked (LockOn mode has a target entity)
            bool isLocked = targetData.Mode == TargetingMode.LockOn && 
                            targetData.TargetEntity != Entity.Null;
            
            if (isLocked)
            {
                _highlightImage.sprite = _lockedTargetIcon ?? _softTargetIcon;
                _highlightImage.color = _lockedTargetColor;
            }
            else
            {
                _highlightImage.sprite = _softTargetIcon;
                _highlightImage.color = _softTargetColor;
            }
            
            // Position on screen
            var rectTransform = _highlightImage.rectTransform;
            rectTransform.position = new Vector3(
                screenPoint.x + _screenOffset.x,
                screenPoint.y + _screenOffset.y,
                0f
            );
        }
    }
}
