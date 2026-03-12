using Unity.Entities;
using UnityEngine;

namespace DIG.Combat.Components
{
    /// <summary>
    /// Authoring component for CombatState.
    /// Add to player/enemy prefabs to enable combat state tracking.
    /// </summary>
    [DisallowMultipleComponent]
    public class CombatStateAuthoring : MonoBehaviour
    {
        [Header("Combat State Settings")]
        [Tooltip("How long (seconds) without combat before dropping out of combat state")]
        [Min(1f)]
        public float CombatDropTime = 5f;
        
        [Tooltip("Whether this entity can enter combat")]
        public bool CanEnterCombat = true;
        
        [Header("Initial State")]
        [Tooltip("Start in combat (for testing or spawned-in-combat scenarios)")]
        public bool StartInCombat = false;
        
        public class Baker : Baker<CombatStateAuthoring>
        {
            public override void Bake(CombatStateAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                
                // Add the main combat state component
                AddComponent(entity, new CombatState
                {
                    IsInCombat = authoring.StartInCombat,
                    TimeSinceLastCombatAction = authoring.StartInCombat ? 0f : 999f,
                    CombatDropTime = authoring.CombatDropTime,
                    CombatExitTime = authoring.StartInCombat ? float.NegativeInfinity : -999f
                });
                
                // Add settings for per-entity configuration
                AddComponent(entity, new CombatStateSettings
                {
                    CombatDropTime = authoring.CombatDropTime,
                    CanEnterCombat = authoring.CanEnterCombat
                });
                
                // Add enableable event tags (disabled by default)
                AddComponent<EnteredCombatTag>(entity);
                SetComponentEnabled<EnteredCombatTag>(entity, false);
                
                AddComponent<ExitedCombatTag>(entity);
                SetComponentEnabled<ExitedCombatTag>(entity, false);
            }
        }
    }
}
