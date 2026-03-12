using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;
using Visuals.Components;
using Player.Components; // StatusEffect

namespace Visuals.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class HelmetEnvironmentEffectSystem : SystemBase
    {
        private static readonly int IceLevelProp = Shader.PropertyToID("_IceLevel");

        protected override void OnUpdate()
        {
            float dt = SystemAPI.Time.DeltaTime;
            
            foreach (var (
                         visor, 
                         effects, 
                         reference) in 
                     SystemAPI.Query<
                         RefRW<HelmetVisor>, 
                         DynamicBuffer<StatusEffect>, 
                         VisorReference>()
                     .WithAll<GhostOwnerIsLocal>())
            {
                float targetIce = 0f;
                
                // Check effects
                foreach (var effect in effects)
                {
                    if (effect.Type == StatusEffectType.Hypoxia)
                    {
                        // Hypoxia -> Frost/fog on edges
                        targetIce = math.max(targetIce, effect.Severity * 0.8f);
                    }
                    if (effect.Type == StatusEffectType.Frostbite)
                    {
                        targetIce = math.max(targetIce, effect.Severity);
                    }
                }
                
                // Lerp towards target
                float current = visor.ValueRO.IceLevel;
                float newVal = math.lerp(current, targetIce, dt * 1.0f); // Slow accumulation
                visor.ValueRW.IceLevel = newVal;
                
                // Update Material
                if (reference.VisorMaterial != null)
                {
                    reference.VisorMaterial.SetFloat(IceLevelProp, newVal);
                }
            }
        }
    }
}
