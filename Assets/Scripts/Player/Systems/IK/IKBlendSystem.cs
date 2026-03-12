using Unity.Burst;
using Unity.Entities;
using DIG.Player.IK;
using DIG.Player.Components; // PlayerState

namespace DIG.Player.Systems.IK
{
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(FootIKSystem))] // Run before specific IK to set global weights
    public partial struct IKBlendSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            
            // Example: Smoothly disable Foot IK if player is in air (handled individually in FootIKSystem), 
            // but this system could handle global toggles or cutscene overrides.
            
            // Currently, FootIKSystem handles its own grounded check.
            // LookAtIKSystem handles its own speed check.
            // This system remains as an extension point for global IK Fades (e.g. death, swimming).
        }
    }
}
