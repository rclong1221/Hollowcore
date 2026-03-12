using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.VFX;

namespace DIG.Targeting.UI
{
    /// <summary>
    /// VFX Graph-based target highlight indicator.
    /// Uses Visual Effect Graph for particles, glow, animated rings.
    /// </summary>
    public class VFXTargetHighlight : MonoBehaviour, ITargetIndicator
    {
        [Header("VFX References")]
        [SerializeField] private VisualEffect _vfx;
        
        [Header("VFX Property Names")]
        [SerializeField] private string _positionProperty = "TargetPosition";
        [SerializeField] private string _colorProperty = "IndicatorColor";
        [SerializeField] private string _intensityProperty = "Intensity";
        
        [Header("Settings")]
        [SerializeField] private Color _softColor = Color.white;
        [SerializeField] private Color _lockedColor = Color.red;
        [SerializeField] private float _softIntensity = 0.5f;
        [SerializeField] private float _lockedIntensity = 1f;
        
        private EntityManager _entityManager;
        private Entity _playerEntity;
        private bool _initialized;
        private int _positionId;
        private int _colorId;
        private int _intensityId;
        
        private void Awake()
        {
            // Cache property IDs for performance
            _positionId = Shader.PropertyToID(_positionProperty);
            _colorId = Shader.PropertyToID(_colorProperty);
            _intensityId = Shader.PropertyToID(_intensityProperty);
            
            if (_vfx != null)
                _vfx.Stop();
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
            
            if (isLocked)
            {
                _vfx.SetVector4(_colorId, (Vector4)(Color)_lockedColor);
                _vfx.SetFloat(_intensityId, _lockedIntensity);
            }
            else
            {
                _vfx.SetVector4(_colorId, (Vector4)(Color)_softColor);
                _vfx.SetFloat(_intensityId, _softIntensity);
            }
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
            
            if (!targetData.HasValidTarget)
            {
                SetVisible(false);
                return;
            }
            
            bool isLocked = targetData.Mode == TargetingMode.LockOn && 
                            targetData.TargetEntity != Entity.Null;
            
            SetVisible(true);
            UpdateIndicator(targetData.TargetPoint, targetData.HasValidTarget, isLocked);
        }
    }
}
