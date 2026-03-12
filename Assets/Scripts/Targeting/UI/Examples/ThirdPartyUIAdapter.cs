using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DIG.Targeting.Theming;

namespace DIG.Targeting.UI.Examples
{
    /// <summary>
    /// Example adapter showing how to wrap a 3rd-party targeting UI asset.
    /// Replace the placeholder references with your actual 3rd-party asset.
    /// </summary>
    public class ThirdPartyUIAdapter : MonoBehaviour, IThemedTargetIndicator
    {
        [Header("3rd Party Asset References")]
        [Tooltip("Replace with your actual 3rd-party UI component")]
        [SerializeField] private GameObject _thirdPartyUIRoot;
        // Example: [SerializeField] private SomeAssetTargetUI _assetUI;
        
        private EntityManager _entityManager;
        private Entity _playerEntity;
        private IndicatorThemeData _themeData;
        
        // ========== ITargetIndicator Implementation ==========
        
        public void Initialize(EntityManager em, Entity playerEntity)
        {
            _entityManager = em;
            _playerEntity = playerEntity;
            
            // Get theme data
            if (em.HasComponent<IndicatorThemeData>(playerEntity))
            {
                _themeData = em.GetComponentData<IndicatorThemeData>(playerEntity);
            }
            else
            {
                _themeData = IndicatorThemeData.Default;
            }
        }
        
        public void UpdateIndicator(float3 worldPosition, bool hasValidTarget, bool isLocked)
        {
            // Basic update - forward position to 3rd party
            // Example: _assetUI.SetTargetPosition(worldPosition);
            // Example: _assetUI.SetLocked(isLocked);
            
            if (_thirdPartyUIRoot != null)
            {
                // Example: move the UI to track world position
                // Actual implementation depends on 3rd-party API
            }
        }
        
        public void SetVisible(bool visible)
        {
            if (_thirdPartyUIRoot != null)
                _thirdPartyUIRoot.SetActive(visible);
            
            // Example: _assetUI.gameObject.SetActive(visible);
        }
        
        // ========== IThemedTargetIndicator Implementation ==========
        
        public void UpdateIndicatorThemed(IndicatorThemeContext context)
        {
            // Forward full context to 3rd party with theming
            
            // Get color based on faction
            float4 color = _themeData.GetFactionColor(context.TargetFaction);
            
            // Apply damage type color if in combat
            if (context.LastDamageType != DamageType.Physical)
            {
                color = _themeData.GetDamageTypeColor(context.LastDamageType);
            }
            
            // Apply hit type modifiers
            float size = _themeData.ApplyHitTypeScale(1f, context.LastHitType);
            float alpha = _themeData.ApplyHitTypeAlpha(color.w, context.LastHitType);
            
            Color finalColor = new Color(color.x, color.y, color.z, alpha);
            
            // Forward to 3rd-party asset
            // Example: _assetUI.SetColor(finalColor);
            // Example: _assetUI.SetScale(size);
            // Example: _assetUI.SetTargetPosition(context.TargetPosition);
            // Example: _assetUI.SetLocked(context.IsLocked);
            
            // Boss/Elite special handling
            if (context.TargetCategory == TargetCategory.Boss)
            {
                // Example: _assetUI.ShowBossIndicator();
            }
        }
        
        public void OnThemeChanged(IndicatorThemeData themeData)
        {
            _themeData = themeData;
            
            // Refresh 3rd-party UI with new theme
            // Example: _assetUI.RefreshColors();
        }
    }
}
