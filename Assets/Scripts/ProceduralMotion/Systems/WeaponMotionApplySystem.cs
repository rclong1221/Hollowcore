using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace DIG.ProceduralMotion.Systems
{
    /// <summary>
    /// EPIC 15.25 Phase 3: Managed bridge that writes weapon spring offsets to the weapon model
    /// root transform. Also performs the wall probe SphereCast (managed Physics) and stores
    /// WallTuckT in ProceduralMotionState for the Burst force system to read.
    /// Must run BEFORE HandIKSystem so IK targets are in world space above the offset.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(WeaponSpringSolverSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class WeaponMotionApplySystem : SystemBase
    {
        private Camera _mainCamera;

        protected override void OnUpdate()
        {
            float dt = SystemAPI.Time.DeltaTime;
            if (dt <= 0f) return;

            if (_mainCamera == null)
                _mainCamera = Camera.main;

            foreach (var (spring, motionState, config, localTransform) in
                     SystemAPI.Query<RefRO<WeaponSpringState>, RefRW<ProceduralMotionState>,
                             RefRO<ProceduralMotionConfig>, RefRO<LocalTransform>>()
                         .WithAll<GhostOwnerIsLocal>())
            {
                ref var ms = ref motionState.ValueRW;
                var ws = spring.ValueRO;

                // ── Wall Probe (managed Physics.SphereCast) ──
                if (config.ValueRO.ProfileBlob.IsCreated && _mainCamera != null)
                {
                    ref var blob = ref config.ValueRO.ProfileBlob.Value;
                    PerformWallProbe(ref ms, ref blob, dt);
                }

                // ── Write to weapon model ──
                // The weapon model root is found via the companion GameObject.
                // In production, this would write to a specific weapon bone/socket.
                // For now, the offset is stored in the spring state for other systems
                // (like WeaponAnimatorBridge or the visual transform update) to read.
                // The actual visual application depends on the weapon attachment architecture.
            }
        }

        private void PerformWallProbe(ref ProceduralMotionState ms, ref ProceduralMotionBlob blob, float dt)
        {
            if (blob.WallProbeDistance <= 0f) return;
            if (_mainCamera == null) return;

            var camTransform = _mainCamera.transform;
            Vector3 origin = camTransform.position;
            Vector3 forward = camTransform.forward;

            float targetTuck;
            if (Physics.SphereCast(origin, blob.WallProbeRadius, forward, out var hit, blob.WallProbeDistance))
            {
                targetTuck = 1f - hit.distance / blob.WallProbeDistance;
            }
            else
            {
                targetTuck = 0f;
            }

            ms.WallTuckT = math.lerp(ms.WallTuckT, targetTuck, blob.WallTuckBlendSpeed * dt);
        }
    }
}
