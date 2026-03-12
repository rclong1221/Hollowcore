using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.Targeting.Theming
{
    /// <summary>
    /// Default theme resolver that evaluates IndicatorThemeContext
    /// against IndicatorThemeData to produce final indicator styling.
    /// </summary>
    public class DefaultThemeResolver : MonoBehaviour
    {
        private EntityManager _entityManager;
        private Entity _playerEntity;
        private bool _initialized;
        
        /// <summary>
        /// Initialize with player entity that has IndicatorThemeData.
        /// </summary>
        public void Initialize(EntityManager em, Entity playerEntity)
        {
            _entityManager = em;
            _playerEntity = playerEntity;
            _initialized = true;
        }
        
        /// <summary>
        /// Resolve theme context to final indicator color.
        /// </summary>
        public Color ResolveColor(IndicatorThemeContext context)
        {
            IndicatorThemeData themeData = GetThemeData();
            
            // Priority: Damage Type > Category > Faction
            float4 baseColor;
            
            // 1. If we just did combat, use damage type color briefly
            if (context.LastHitType != HitType.None && context.LastDamageType != DamageType.Physical)
            {
                baseColor = themeData.GetDamageTypeColor(context.LastDamageType);
            }
            // 2. Boss/Elite get special colors
            else if (context.TargetCategory == TargetCategory.Boss || 
                     context.TargetCategory == TargetCategory.Miniboss)
            {
                baseColor = themeData.BossColor;
            }
            else if (context.TargetCategory == TargetCategory.Elite)
            {
                baseColor = themeData.EliteColor;
            }
            // 3. Default to faction color
            else
            {
                baseColor = themeData.GetFactionColor(context.TargetFaction);
            }
            
            // Apply hit type alpha modifier
            float alpha = themeData.ApplyHitTypeAlpha(baseColor.w, context.LastHitType);
            
            return new Color(baseColor.x, baseColor.y, baseColor.z, alpha);
        }
        
        /// <summary>
        /// Resolve theme context to indicator size multiplier.
        /// </summary>
        public float ResolveSizeMultiplier(IndicatorThemeContext context)
        {
            IndicatorThemeData themeData = GetThemeData();
            
            float size = context.SizeScale;
            
            // Crits get bigger
            size = themeData.ApplyHitTypeScale(size, context.LastHitType);
            
            // Bosses slightly larger
            if (context.TargetCategory == TargetCategory.Boss)
            {
                size *= 1.25f;
            }
            
            return size;
        }
        
        /// <summary>
        /// Build context from current targeting state.
        /// </summary>
        public IndicatorThemeContext BuildContext(TargetData targetData)
        {
            var context = IndicatorThemeContext.Default;
            
            context.Mode = targetData.Mode;
            context.HasValidTarget = targetData.HasValidTarget;
            context.IsLocked = targetData.Mode == TargetingMode.LockOn && 
                               targetData.TargetEntity != Entity.Null;
            context.TargetPosition = targetData.TargetPoint;
            context.TargetEntity = targetData.TargetEntity;
            
            // Target info would need to be looked up from target entity components
            // Placeholder: default to enemy
            if (targetData.HasValidTarget)
            {
                context.TargetFaction = TargetFaction.Enemy;
                context.TargetCategory = TargetCategory.Normal;
            }
            
            return context;
        }
        
        private IndicatorThemeData GetThemeData()
        {
            if (!_initialized || _playerEntity == Entity.Null)
                return IndicatorThemeData.Default;
                
            if (!_entityManager.HasComponent<IndicatorThemeData>(_playerEntity))
                return IndicatorThemeData.Default;
                
            return _entityManager.GetComponentData<IndicatorThemeData>(_playerEntity);
        }
    }
}
