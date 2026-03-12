using Unity.Entities;
using UnityEngine;

namespace DIG.Progression
{
    /// <summary>
    /// EPIC 16.14: Authoring component for player progression.
    /// Adds PlayerProgression, LevelUpEvent (disabled), and StatAllocationRequest buffer.
    /// Place on player prefab alongside CharacterAttributesAuthoring.
    /// </summary>
    [AddComponentMenu("DIG/Progression/Player Progression")]
    public class ProgressionAuthoring : MonoBehaviour
    {
        [Header("Starting Values")]
        [Tooltip("Starting XP (usually 0)")]
        [Min(0)] public int StartingXP;

        [Tooltip("Starting stat points (usually 0)")]
        [Min(0)] public int StartingStatPoints;

        [Tooltip("Starting rested XP pool")]
        [Min(0)] public float StartingRestedXP;

        private class Baker : Baker<ProgressionAuthoring>
        {
            public override void Bake(ProgressionAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new PlayerProgression
                {
                    CurrentXP = authoring.StartingXP,
                    TotalXPEarned = authoring.StartingXP,
                    UnspentStatPoints = authoring.StartingStatPoints,
                    RestedXP = authoring.StartingRestedXP
                });

                // LevelUpEvent: baked disabled (IEnableableComponent)
                AddComponent(entity, new LevelUpEvent());
                SetComponentEnabled<LevelUpEvent>(entity, false);

                // StatAllocationRequest buffer (transient, not ghost-replicated)
                AddBuffer<StatAllocationRequest>(entity);
            }
        }
    }
}
