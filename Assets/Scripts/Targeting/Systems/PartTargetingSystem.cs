using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Player.Components;
using DIG.Player.Components;
using DIG.Targeting.Components;
using DIG.Targeting.Core;

namespace DIG.Targeting.Systems
{
    /// <summary>
    /// EPIC 15.16 Task 5: Part Targeting System
    /// 
    /// Target specific body parts on enemies. Different damage multipliers per part.
    /// Cycle between parts with input (Q/E or stick).
    /// 
    /// NOTE: PartTargetingState is on the TargetingModule child entity, accessed via TargetingModuleLink.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(global::Player.Systems.CameraLockOnSystem))]
    public partial struct PartTargetingSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ActiveLockBehavior>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Check if part targeting is enabled
            if (!SystemAPI.TryGetSingleton<ActiveLockBehavior>(out var behavior))
                return;
                
            if ((behavior.Features & LockFeatureFlags.PartTargeting) == 0)
                return;
            
            var em = state.EntityManager;
            
            foreach (var (moduleLink, lockState, camSettings, input, entity) in 
                SystemAPI.Query<RefRO<TargetingModuleLink>, RefRO<CameraTargetLockState>, RefRW<PlayerCameraSettings>, RefRO<PlayerInput>>()
                .WithEntityAccess())
            {
                // Get the targeting module entity
                Entity moduleEntity = moduleLink.ValueRO.TargetingModule;
                if (moduleEntity == Entity.Null || !SystemAPI.HasComponent<PartTargetingState>(moduleEntity))
                    continue;
                
                var partState = SystemAPI.GetComponentRW<PartTargetingState>(moduleEntity);
                
                if (!lockState.ValueRO.IsLocked) 
                {
                    // Clear part targeting when not locked
                    partState.ValueRW.CurrentPartIndex = 0;
                    partState.ValueRW.CurrentDamageMultiplier = 1f;
                    continue;
                }
                
                Entity target = lockState.ValueRO.TargetEntity;
                if (!em.Exists(target)) continue;
                
                // Check if target has parts
                if (!em.HasBuffer<TargetablePartElement>(target)) continue;
                
                var parts = em.GetBuffer<TargetablePartElement>(target);
                if (parts.Length == 0) continue;
                
                // Handle part cycling input
                // Using horizontal look input for cycling (right stick left/right or Q/E)
                float cycleInput = input.ValueRO.LookDelta.x;
                float cycleThreshold = 0.5f;
                
                bool cycleNext = cycleInput > cycleThreshold;
                bool cyclePrev = cycleInput < -cycleThreshold;
                
                // Simple edge detection (would need proper state tracking in production)
                if (cycleNext)
                {
                    int newIndex = partState.ValueRO.CurrentPartIndex + 1;
                    if (newIndex >= parts.Length) newIndex = 0;
                    
                    // Find next exposed part
                    for (int i = 0; i < parts.Length; i++)
                    {
                        int checkIndex = (newIndex + i) % parts.Length;
                        if (parts[checkIndex].IsExposed)
                        {
                            partState.ValueRW.CurrentPartIndex = checkIndex;
                            break;
                        }
                    }
                }
                else if (cyclePrev)
                {
                    int newIndex = partState.ValueRO.CurrentPartIndex - 1;
                    if (newIndex < 0) newIndex = parts.Length - 1;
                    
                    // Find previous exposed part
                    for (int i = 0; i < parts.Length; i++)
                    {
                        int checkIndex = (newIndex - i + parts.Length) % parts.Length;
                        if (parts[checkIndex].IsExposed)
                        {
                            partState.ValueRW.CurrentPartIndex = checkIndex;
                            break;
                        }
                    }
                }
                
                // Update current part info
                int currentIndex = partState.ValueRO.CurrentPartIndex;
                if (currentIndex >= 0 && currentIndex < parts.Length)
                {
                    var part = parts[currentIndex];
                    partState.ValueRW.CurrentDamageMultiplier = part.DamageMultiplier;
                    
                    // Calculate world offset for the part
                    if (em.HasComponent<LocalTransform>(target))
                    {
                        var targetTransform = em.GetComponentData<LocalTransform>(target);
                        // Rotate local offset by target rotation
                        float3 worldOffset = math.mul(targetTransform.Rotation, part.LocalOffset);
                        partState.ValueRW.PartOffset = worldOffset;
                        
                        // Optionally adjust camera to look at the part instead of center
                        float3 partWorldPos = targetTransform.Position + worldOffset;
                        // This could be used by the lock system to offset aim
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Authoring component for adding targetable parts to enemies.
    /// </summary>
    public class TargetablePartsAuthoring : MonoBehaviour
    {
        [System.Serializable]
        public struct PartDefinition
        {
            public string Name;
            public Vector3 LocalOffset;
            public float DamageMultiplier;
            public float HitRadius;
            public bool IsExposed;
        }
        
        public PartDefinition[] Parts = new PartDefinition[]
        {
            new PartDefinition { Name = "Body", LocalOffset = Vector3.zero, DamageMultiplier = 1f, HitRadius = 0.5f, IsExposed = true },
            new PartDefinition { Name = "Head", LocalOffset = new Vector3(0, 1.7f, 0), DamageMultiplier = 2f, HitRadius = 0.2f, IsExposed = true },
            new PartDefinition { Name = "Legs", LocalOffset = new Vector3(0, 0.3f, 0), DamageMultiplier = 0.75f, HitRadius = 0.3f, IsExposed = true }
        };
        
        public class Baker : Baker<TargetablePartsAuthoring>
        {
            public override void Bake(TargetablePartsAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                var buffer = AddBuffer<TargetablePartElement>(entity);
                for (int i = 0; i < authoring.Parts.Length; i++)
                {
                    var part = authoring.Parts[i];
                    buffer.Add(new TargetablePartElement
                    {
                        PartId = i,
                        LocalOffset = part.LocalOffset,
                        DamageMultiplier = part.DamageMultiplier,
                        HitRadius = part.HitRadius,
                        IsExposed = part.IsExposed
                    });
                }
            }
        }
    }
}
