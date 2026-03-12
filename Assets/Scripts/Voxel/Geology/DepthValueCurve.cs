using UnityEngine;

namespace DIG.Voxel.Geology
{
    /// <summary>
    /// Defines depth-based probability curves for ore rarity tiers.
    /// Deeper mining = access to rarer and more valuable resources.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/World/Depth Value Curve")]
    public class DepthValueCurve : ScriptableObject
    {
        [Header("Probability Curves")]
        [Tooltip("Common ores - peak probability at shallow depths")]
        public AnimationCurve CommonOreCurve = AnimationCurve.Linear(0, 1, 200, 0.3f);
        
        [Tooltip("Uncommon ores - peak at mid depths")]
        public AnimationCurve UncommonOreCurve = AnimationCurve.EaseInOut(0, 0, 50, 1);
        
        [Tooltip("Rare ores - peak at deep levels")]
        public AnimationCurve RareOreCurve = AnimationCurve.EaseInOut(0, 0, 100, 1);
        
        [Tooltip("Legendary ores - only spawn very deep")]
        public AnimationCurve LegendaryOreCurve = AnimationCurve.EaseInOut(0, 0, 150, 1);
        
        [Header("Depth Normalization")]
        [Tooltip("Maximum depth for curve evaluation (deepest expected mining)")]
        public float MaxDepthReference = 200f;
        
        /// <summary>
        /// Get spawn probability multiplier based on depth and rarity tier.
        /// Returns 0-1 value that multiplies the base ore threshold.
        /// </summary>
        public float GetProbability(OreRarity rarity, float depth)
        {
            float normalizedDepth = Mathf.Clamp01(depth / MaxDepthReference) * MaxDepthReference;
            
            return rarity switch
            {
                OreRarity.Common => CommonOreCurve.Evaluate(normalizedDepth),
                OreRarity.Uncommon => UncommonOreCurve.Evaluate(normalizedDepth),
                OreRarity.Rare => RareOreCurve.Evaluate(normalizedDepth),
                OreRarity.Legendary => LegendaryOreCurve.Evaluate(normalizedDepth),
                _ => 1f
            };
        }
        
        /// <summary>
        /// Modify an ore's threshold based on depth.
        /// Lower threshold = more likely to spawn.
        /// </summary>
        public float GetAdjustedThreshold(OreDefinition ore, float depth)
        {
            float probability = GetProbability(ore.Rarity, depth);
            
            // Higher probability = lower threshold
            // If probability is 1, threshold stays as defined
            // If probability is 0.5, threshold increases (harder to spawn)
            if (probability <= 0) return 1f; // Never spawn
            
            return ore.Threshold / probability;
        }
        
        /// <summary>
        /// Create default curves optimized for typical mining gameplay.
        /// </summary>
        public void ResetToDefaults()
        {
            // Common: abundant shallow, diminishes deep
            CommonOreCurve = new AnimationCurve(
                new Keyframe(0, 1f),
                new Keyframe(50, 0.8f),
                new Keyframe(100, 0.5f),
                new Keyframe(200, 0.2f)
            );
            
            // Uncommon: starts at 20m, peaks at 60m
            UncommonOreCurve = new AnimationCurve(
                new Keyframe(0, 0f),
                new Keyframe(20, 0.1f),
                new Keyframe(60, 1f),
                new Keyframe(120, 0.6f),
                new Keyframe(200, 0.4f)
            );
            
            // Rare: starts at 50m, peaks at 120m
            RareOreCurve = new AnimationCurve(
                new Keyframe(0, 0f),
                new Keyframe(50, 0f),
                new Keyframe(80, 0.3f),
                new Keyframe(120, 1f),
                new Keyframe(200, 0.8f)
            );
            
            // Legendary: only deep, starts at 100m
            LegendaryOreCurve = new AnimationCurve(
                new Keyframe(0, 0f),
                new Keyframe(100, 0f),
                new Keyframe(150, 0.5f),
                new Keyframe(200, 1f)
            );
        }
        
        private void OnValidate()
        {
            if (MaxDepthReference <= 0) MaxDepthReference = 200f;
        }
    }
}
