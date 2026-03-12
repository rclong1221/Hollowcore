using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using DIG.AI.Components;
using DIG.Combat.Components;

namespace DIG.Combat.Systems
{
    /// <summary>
    /// EPIC 15.32: Static helper for spawning TelegraphZone entities from abilities.
    /// Used by AbilityExecutionSystem during the Active phase of AOE abilities.
    /// </summary>
    public static class TelegraphSpawnHelper
    {
        public static Entity SpawnTelegraph(
            EntityCommandBuffer ecb,
            in AbilityDefinition ability,
            float3 position,
            quaternion rotation,
            Entity owner,
            float damageMultiplier = 1f)
        {
            var entity = ecb.CreateEntity();

            ecb.AddComponent(entity, new TelegraphZone
            {
                Shape = ability.TelegraphShape,
                Position = position,
                Rotation = rotation,
                Radius = ability.Radius,
                InnerRadius = 0f,
                Angle = ability.Angle,
                Length = ability.Range,
                Width = ability.Radius * 0.5f,
                WarningDuration = ability.TelegraphDuration,
                DamageDelay = ability.TelegraphDuration + ability.CastTime,
                LingerDuration = ability.TickInterval > 0 ? ability.ActiveDuration : 0f,
                TickInterval = ability.TickInterval,
                Timer = 0f,
                LastTickTime = -1f,
                DamageBase = ability.DamageBase * damageMultiplier,
                DamageVariance = ability.DamageVariance * damageMultiplier,
                DamageType = ability.DamageType,
                OwnerEntity = owner,
                MaxTargets = ability.MaxTargets,
                ResolverType = ability.ResolverType,
                IsSafeZone = false,
                HasDealtDamage = false,
                Modifier0Type = ability.Modifier0Type,
                Modifier0Chance = ability.Modifier0Chance,
                Modifier0Duration = ability.Modifier0Duration,
                Modifier0Intensity = ability.Modifier0Intensity
            });

            ecb.AddComponent(entity, new LocalTransform
            {
                Position = position,
                Rotation = rotation,
                Scale = 1f
            });

            ecb.AddComponent(entity, new LocalToWorld());

            return entity;
        }
    }
}
