using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.NetCode.Hybrid;
using Unity.Transforms;
using UnityEngine;

namespace DIG.Combat.Knockback
{
    /// <summary>
    /// EPIC 16.9: Drives animator parameters for knockback animations.
    /// Sets KnockbackActive, KnockbackType, KnockbackSpeed, KnockbackDirX/Z on entity animators.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class KnockbackAnimationBridgeSystem : SystemBase
    {
        private GhostPresentationGameObjectSystem _ghostPresentationSystem;

        private static readonly int KnockbackActiveHash = Animator.StringToHash("KnockbackActive");
        private static readonly int KnockbackTypeHash = Animator.StringToHash("KnockbackType");
        private static readonly int KnockbackSpeedHash = Animator.StringToHash("KnockbackSpeed");
        private static readonly int KnockbackDirXHash = Animator.StringToHash("KnockbackDirX");
        private static readonly int KnockbackDirZHash = Animator.StringToHash("KnockbackDirZ");

        private Dictionary<Entity, bool> _wasActive = new();

        protected override void OnCreate()
        {
            _ghostPresentationSystem = World.GetExistingSystemManaged<GhostPresentationGameObjectSystem>();
            RequireForUpdate<KnockbackState>();
        }

        protected override void OnUpdate()
        {
            if (_ghostPresentationSystem == null) return;

            // Clean up destroyed entities
            var toRemove = new List<Entity>();
            foreach (var entity in _wasActive.Keys)
            {
                if (!EntityManager.Exists(entity))
                    toRemove.Add(entity);
            }
            foreach (var entity in toRemove)
                _wasActive.Remove(entity);

            foreach (var (knockbackState, transform, entity) in
                SystemAPI.Query<RefRO<KnockbackState>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                var presentationObject = _ghostPresentationSystem.GetGameObjectForEntity(EntityManager, entity);
                if (presentationObject == null) continue;

                var animator = presentationObject.GetComponentInChildren<Animator>();
                if (animator == null) continue;

                var kb = knockbackState.ValueRO;
                bool wasActive = _wasActive.TryGetValue(entity, out bool prev) && prev;

                if (kb.IsActive)
                {
                    animator.SetBool(KnockbackActiveHash, true);
                    animator.SetInteger(KnockbackTypeHash, (int)kb.Type);

                    // Normalized speed (0-1) for blend tree intensity
                    float maxVelocity = 25f; // Match KnockbackConfig.Default.MaxVelocity
                    float speed = math.saturate(kb.InitialSpeed / maxVelocity);
                    animator.SetFloat(KnockbackSpeedHash, speed);

                    // Local-space knockback direction for directional blend
                    float3 worldDir = math.normalizesafe(kb.Velocity, float3.zero);
                    worldDir.y = 0;
                    worldDir = math.normalizesafe(worldDir, new float3(0, 0, 1));

                    // Convert to local space
                    var entityRotation = transform.ValueRO.Rotation;
                    float3 localDir = math.mul(math.inverse(entityRotation), worldDir);

                    animator.SetFloat(KnockbackDirXHash, localDir.x);
                    animator.SetFloat(KnockbackDirZHash, localDir.z);
                }
                else if (wasActive)
                {
                    // Knockback just ended
                    animator.SetBool(KnockbackActiveHash, false);
                }

                _wasActive[entity] = kb.IsActive;
            }
        }
    }
}
