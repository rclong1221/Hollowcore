using UnityEngine;
using Unity.Entities;
using Horror.Components;

namespace Horror.Authoring
{
    /// <summary>
    /// Authoring component that adds hallucination capability to a player.
    /// Add to the player prefab alongside StressAuthoring.
    /// </summary>
    [AddComponentMenu("DIG/Horror/Hallucination Receiver")]
    public class HallucinationReceiverAuthoring : MonoBehaviour
    {
        class Baker : Baker<HallucinationReceiverAuthoring>
        {
            public override void Bake(HallucinationReceiverAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                AddComponent(entity, new PlayerHallucinationState
                {
                    TimeSinceLastHallucination = 0f,
                    HallucinationIntensity = 0f,
                    IsHallucinating = false,
                    CurrentHallucinationType = HorrorEventType.Whispers,
                    HallucinationTimeRemaining = 0f
                });
            }
        }
    }
}
