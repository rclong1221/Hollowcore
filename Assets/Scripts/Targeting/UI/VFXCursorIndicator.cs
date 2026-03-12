using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.VFX;

namespace DIG.Targeting.UI
{
    /// <summary>
    /// VFX Graph-based ground cursor indicator for ARPG.
    /// Uses Visual Effect Graph for animated ground circle, particles.
    /// </summary>
    public class VFXCursorIndicator : MonoBehaviour, ITargetIndicator
    {
        [Header("VFX References")]
        [SerializeField] private VisualEffect _vfx;
        
        [Header("VFX Property Names")]
        [SerializeField] private string _positionProperty = "GroundPosition";
        [SerializeField] private string _colorProperty = "CircleColor";
        [SerializeField] private string _radiusProperty = "Radius";
        
        [Header("Settings")]
        [SerializeField] private Color _validColor = Color.green;
        [SerializeField] private Color _invalidColor = Color.red;
        [SerializeField] private float _radius = 0.5f;
        
        private EntityManager _entityManager;
        private Entity _playerEntity;
        private bool _initialized;
        private int _positionId;
        private int _colorId;
        private int _radiusId;
        
        private void Awake()
        {
            _positionId = Shader.PropertyToID(_positionProperty);
            _colorId = Shader.PropertyToID(_colorProperty);
            _radiusId = Shader.PropertyToID(_radiusProperty);
            
            if (_vfx != null)
            {
                _vfx.SetFloat(_radiusId, _radius);
                _vfx.Stop();
            }
        }
        
        public void Initialize(EntityManager em, Entity playerEntity)
        {
            _entityManager = em;
            _playerEntity = playerEntity;
            _initialized = true;
        }
        
        public void UpdateIndicator(float3 worldPosition, bool hasValidTarget, bool isLocked)
        {
            if (_vfx == null) return;
            
            _vfx.SetVector3(_positionId, worldPosition);
            _vfx.SetVector4(_colorId, (Vector4)(Color)(hasValidTarget ? _validColor : _invalidColor));
        }
        
        public void SetVisible(bool visible)
        {
            if (_vfx == null) return;
            
            if (visible)
                _vfx.Play();
            else
                _vfx.Stop();
        }
        
        private void LateUpdate()
        {
            if (!_initialized || _vfx == null) return;
            
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
            
            // Only show for cursor-aim mode
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
