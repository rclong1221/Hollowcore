using Unity.Entities;
using UnityEngine;
using DIG.Targeting.Core;

namespace DIG.Targeting.Systems
{
    /// <summary>
    /// EPIC 15.16 Task 12: Lock Feature Flags Integration
    /// 
    /// Manages which targeting features are active based on LockFeatureFlags.
    /// Individual systems check their own flags, but this system provides
    /// debug logging and validation.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(LockBehaviorDispatcherSystem))]
    public partial struct LockFeatureFlagsSystem : ISystem
    {
        private LockFeatureFlags _lastFlags;
        private LockBehaviorType _lastType;
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ActiveLockBehavior>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<ActiveLockBehavior>(out var behavior))
                return;
            
            // Only log when flags change
            if (behavior.Features != _lastFlags || behavior.BehaviorType != _lastType)
            {
                _lastFlags = behavior.Features;
                _lastType = behavior.BehaviorType;
                
                #if UNITY_EDITOR
                LogActiveFeatures(behavior);
                #endif
            }
        }
        
        private void LogActiveFeatures(ActiveLockBehavior behavior)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"[LockFeatures] Mode: {behavior.BehaviorType}, Active Features: ");
            
            if ((behavior.Features & LockFeatureFlags.MultiLock) != 0)
                sb.Append("MultiLock ");
            if ((behavior.Features & LockFeatureFlags.PartTargeting) != 0)
                sb.Append("PartTargeting ");
            if ((behavior.Features & LockFeatureFlags.PredictiveAim) != 0)
                sb.Append("PredictiveAim ");
            if ((behavior.Features & LockFeatureFlags.PriorityAutoSwitch) != 0)
                sb.Append("AutoSwitch ");
            if ((behavior.Features & LockFeatureFlags.StickyAim) != 0)
                sb.Append("StickyAim ");
            if ((behavior.Features & LockFeatureFlags.SnapAim) != 0)
                sb.Append("SnapAim ");
                
            if (behavior.Features == LockFeatureFlags.None)
                sb.Append("None");
                
            UnityEngine.Debug.Log(sb.ToString());
        }
    }
}
