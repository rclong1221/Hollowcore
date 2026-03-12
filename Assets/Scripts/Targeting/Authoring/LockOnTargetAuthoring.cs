using UnityEngine;
using Unity.Entities;

namespace DIG.Targeting.Authoring
{
    /// <summary>
    /// EPIC 15.16: Marks this entity as a valid lock-on target.
    /// Add to enemy prefabs, bosses, destructible objects, etc.
    /// </summary>
    [AddComponentMenu("DIG/Targeting/Lock-On Target Authoring")]
    public class LockOnTargetAuthoring : MonoBehaviour
    {
        [Header("Target Settings")]
        [Tooltip("Priority for targeting. Higher = preferred. 0=normal, 10=elite, 100=boss")]
        [SerializeField] private int _priority = 0;
        
        [Tooltip("Vertical offset for lock-on indicator (meters above entity origin)")]
        [SerializeField] private float _indicatorHeightOffset = 1.5f;
        
        [Tooltip("Whether this target starts enabled")]
        [SerializeField] private bool _startEnabled = true;
        
        public class Baker : Baker<LockOnTargetAuthoring>
        {
            public override void Bake(LockOnTargetAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                AddComponent(entity, new Components.LockOnTarget
                {
                    Priority = authoring._priority,
                    IndicatorHeightOffset = authoring._indicatorHeightOffset
                });
                
                // Set initial enabled state
                SetComponentEnabled<Components.LockOnTarget>(entity, authoring._startEnabled);
            }
        }
    }
}
