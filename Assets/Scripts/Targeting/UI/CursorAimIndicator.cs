using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace DIG.Targeting.UI
{
    /// <summary>
    /// Displays a ground indicator for cursor-aim targeting mode.
    /// Shows where abilities/attacks will land in isometric/ARPG style.
    /// </summary>
    public class CursorAimIndicator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _indicator;
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Camera _camera;
        
        [Header("Settings")]
        [SerializeField] private Color _validColor = Color.green;
        [SerializeField] private Color _invalidColor = Color.red;
        [SerializeField] private float _groundOffset = 0.05f;
        
        // ECS link
        private EntityManager _entityManager;
        private Entity _playerEntity;
        private bool _initialized;
        
        private void Start()
        {
            if (_camera == null)
                _camera = Camera.main;
                
            if (_indicator != null)
                _indicator.gameObject.SetActive(false);
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
            if (!_initialized || _indicator == null)
                return;
                
            if (_playerEntity == Entity.Null || !_entityManager.Exists(_playerEntity))
            {
                _indicator.gameObject.SetActive(false);
                return;
            }
            
            if (!_entityManager.HasComponent<TargetData>(_playerEntity))
            {
                _indicator.gameObject.SetActive(false);
                return;
            }
            
            var targetData = _entityManager.GetComponentData<TargetData>(_playerEntity);
            
            // Only show for cursor-aim mode
            if (targetData.Mode != TargetingMode.CursorAim)
            {
                _indicator.gameObject.SetActive(false);
                return;
            }
            
            // Show indicator at target point
            _indicator.gameObject.SetActive(true);
            
            float3 targetPoint = targetData.TargetPoint;
            _indicator.position = new Vector3(
                targetPoint.x,
                targetPoint.y + _groundOffset,
                targetPoint.z
            );
            
            // Color based on valid target
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = targetData.HasValidTarget ? _validColor : _invalidColor;
            }
        }
    }
}
