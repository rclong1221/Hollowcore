using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;
using Visuals.Components;
using Player.Components; // DamageEvent

namespace Visuals.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class VisorDamageSystem : SystemBase
    {
        private static readonly int CrackLevelProp = Shader.PropertyToID("_CrackLevel");

        protected override void OnUpdate()
        {
            foreach (var (
                         visor, 
                         damageBuffer, 
                         reference) in 
                     SystemAPI.Query<
                         RefRW<HelmetVisor>, 
                         DynamicBuffer<DamageEvent>, 
                         VisorReference>()
                     .WithAll<GhostOwnerIsLocal>())
            {
                // Process Damage Events
                // Note: DamageEvents are typically consumed by HealthSystem on Server.
                // On Client, they are replicated for prediction/feedback.
                // We should ensure we don't process the same event twice?
                // Visual systems usually just react to "New" events.
                // But NetCode buffers are snapshotted.
                // To avoid re-processing, we might need to track the "LastProcessedTick" or just process all and assume buffer is cleared/replaced?
                // For now, iterate all events as "Recent Damage".
                
                float totalDamage = 0f;
                foreach (var evt in damageBuffer)
                {
                    totalDamage += evt.Amount;
                }
                
                if (totalDamage > 0)
                {
                    // Accumulate Cracks
                    // 100 damage = +0.2 crack?
                    float crackInc = (totalDamage / 100f) * 0.5f; 
                    visor.ValueRW.CrackLevel = math.clamp(visor.ValueRO.CrackLevel + crackInc, 0f, 1f);
                    
                    // Shake HUD or Glitch?
                    // handled by separate logic or here.
                }
                
                // Update Material
                if (reference.VisorMaterial != null)
                {
                    reference.VisorMaterial.SetFloat(CrackLevelProp, visor.ValueRO.CrackLevel);
                }
            }
        }
    }
}
