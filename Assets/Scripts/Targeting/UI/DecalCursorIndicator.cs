#if UNITY_2021_2_OR_NEWER
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DIG.Targeting.UI
{
    /// <summary>
    /// URP Decal Projector-based cursor indicator.
    /// Projects a ground circle that conforms to terrain.
    /// </summary>
    public class DecalCursorIndicator : MonoBehaviour, ITargetIndicator
    {
        [Header("Decal References")]
        [SerializeField] private DecalProjector _decal;
        
        [Header("Materials")]
        [SerializeField] private Material _validMaterial;
        [SerializeField] private Material _invalidMaterial;
        
        [Header("Settings")]
        [SerializeField] private float _groundOffset = 0.1f;
        
        private EntityManager _entityManager;
        private Entity _playerEntity;
        private bool _initialized;
        
        private void Start()
        {
            if (_decal != null)
                _decal.gameObject.SetActive(false);
        }
        
        public void Initialize(EntityManager em, Entity playerEntity)
        {
            _entityManager = em;
            _playerEntity = playerEntity;
            _initialized = true;
        }
        
        public void UpdateIndicator(float3 worldPosition, bool hasValidTarget, bool isLocked)
        {
            if (_decal == null) return;
            
            transform.position = new Vector3(worldPosition.x, worldPosition.y + _groundOffset, worldPosition.z);
            
            if (_validMaterial != null && _invalidMaterial != null)
            {
                _decal.material = hasValidTarget ? _validMaterial : _invalidMaterial;
            }
        }
        
        public void SetVisible(bool visible)
        {
            if (_decal != null)
                _decal.gameObject.SetActive(visible);
        }
        
        private void LateUpdate()
        {
            if (!_initialized || _decal == null) return;
            
            if (_playerEntity == Entity.Null || !_entityManager.Exists(_playerEntity))
            {
                SetVisible(false);
                return;
            }
            
            if (!_entityManager.HasComponent<TargetData>(_playerEntity))
            {
                SetVisible(false);
                return;
            }
            
            var targetData = _entityManager.GetComponentData<TargetData>(_playerEntity);
            
            if (targetData.Mode != TargetingMode.CursorAim)
            {
                SetVisible(false);
                return;
            }
            
            SetVisible(true);
            UpdateIndicator(targetData.TargetPoint, targetData.HasValidTarget, false);
        }
    }
}
#endif
