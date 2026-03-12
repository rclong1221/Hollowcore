using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using DIG.Weapons;

namespace DIG.ProceduralMotion.Systems
{
    /// <summary>
    /// EPIC 15.25 Phase 3: All 8 weapon force providers computed in a single Burst pass.
    /// Sway + Bob + Inertia + Landing + IdleNoise + WallTuck + VisualRecoil + HitReaction.
    /// Early-outs when FPMotionWeight &lt; 0.001 (ARPG/MOBA/TwinStick).
    /// Wall probe is computed in WeaponMotionApplySystem (managed) and stored in WallTuckT.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ProceduralMotionStateSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct ProceduralWeaponForceSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WeaponSpringState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            if (dt <= 0f) return;

            // Read intensity singleton
            float globalIntensity = 1f;
            float weaponMotionScale = 1f;
            if (SystemAPI.HasSingleton<ProceduralMotionIntensity>())
            {
                var intensity = SystemAPI.GetSingleton<ProceduralMotionIntensity>();
                globalIntensity = intensity.GlobalIntensity;
                weaponMotionScale = intensity.WeaponMotionScale;
            }

            foreach (var (spring, motionState, config, playerInput, playerState, velocity, fireState,
                         entity) in
                     SystemAPI.Query<RefRW<WeaponSpringState>, RefRW<ProceduralMotionState>,
                             RefRO<ProceduralMotionConfig>, RefRO<PlayerInput>, RefRO<PlayerState>,
                             RefRO<PhysicsVelocity>, RefRO<WeaponFireState>>()
                         .WithAll<GhostOwnerIsLocal>()
                         .WithEntityAccess())
            {
                ref var ms = ref motionState.ValueRW;
                ref var ws = ref spring.ValueRW;

                // Early-out for non-FP paradigms
                if (ms.FPMotionWeight < 0.001f)
                {
                    ws.PositionVelocity = float3.zero;
                    ws.RotationVelocity = float3.zero;
                    ws.PositionValue = float3.zero;
                    ws.RotationValue = float3.zero;
                    continue;
                }

                if (!config.ValueRO.ProfileBlob.IsCreated) continue;
                ref var blob = ref config.ValueRO.ProfileBlob.Value;

                float masterScale = globalIntensity * weaponMotionScale * ms.FPMotionWeight;

                // Get blended state overrides
                int curIdx = (int)ms.CurrentState;
                int prevIdx = (int)ms.PreviousState;
                float t = ms.StateBlendT;

                ref var curOverride = ref blob.StateOverrides[math.clamp(curIdx, 0, blob.StateOverrides.Length - 1)];
                ref var prevOverride = ref blob.StateOverrides[math.clamp(prevIdx, 0, blob.StateOverrides.Length - 1)];

                float bobScale = math.lerp(prevOverride.BobScale, curOverride.BobScale, t);
                float swayScale = math.lerp(prevOverride.SwayScale, curOverride.SwayScale, t);
                float inertiaScale = math.lerp(prevOverride.InertiaScale, curOverride.InertiaScale, t);
                float noiseScale = math.lerp(prevOverride.IdleNoiseScale, curOverride.IdleNoiseScale, t);

                // Frozen state — hold static offset, no forces
                ws.IsFrozen = curOverride.IsFrozen && t >= 0.99f;
                if (ws.IsFrozen)
                {
                    float3 targetPos = math.lerp(prevOverride.PositionOffset, curOverride.PositionOffset, t);
                    float3 targetRot = math.lerp(prevOverride.RotationOffset, curOverride.RotationOffset, t);
                    ws.PositionValue = math.lerp(ws.PositionValue, targetPos, dt * 10f);
                    ws.RotationValue = math.lerp(ws.RotationValue, targetRot, dt * 10f);
                    ws.PositionVelocity = float3.zero;
                    ws.RotationVelocity = float3.zero;
                    continue;
                }

                // Apply blended spring parameters to weapon spring
                ApplySpringParameters(ref ws, ref blob, ref curOverride, ref prevOverride, t);

                // Apply static offset (blended between states)
                float3 blendedPosOffset = math.lerp(prevOverride.PositionOffset, curOverride.PositionOffset, t);
                float3 blendedRotOffset = math.lerp(prevOverride.RotationOffset, curOverride.RotationOffset, t);

                // ── Force 1: Sway ──────────────────────────────
                float2 rawLook = playerInput.ValueRO.LookDelta;
                ms.SmoothedLookDelta = math.lerp(ms.SmoothedLookDelta, rawLook, blob.SwayEMASmoothing);

                float swayFactor = swayScale * ms.SwayWeight * masterScale;
                ws.PositionVelocity.x += -ms.SmoothedLookDelta.x * blob.SwayPositionScale * swayFactor;
                ws.PositionVelocity.y += -ms.SmoothedLookDelta.y * blob.SwayPositionScale * swayFactor;
                ws.RotationVelocity.y += -ms.SmoothedLookDelta.x * blob.SwayRotationScale * swayFactor;
                ws.RotationVelocity.x += ms.SmoothedLookDelta.y * blob.SwayRotationScale * swayFactor;

                // Clamp sway angle
                ws.RotationValue = math.clamp(ws.RotationValue,
                    new float3(-blob.SwayMaxAngle), new float3(blob.SwayMaxAngle));

                // ── Force 2: Bob ───────────────────────────────
                float3 vel = velocity.ValueRO.Linear;
                float horizontalSpeed = math.length(new float3(vel.x, 0f, vel.z));
                float bobFactor = bobScale * ms.BobWeight * masterScale;

                if (horizontalSpeed > 0.5f && playerState.ValueRO.IsGrounded && bobFactor > 0.001f)
                {
                    float bobFreq = blob.BobFrequency;
                    if (ms.CurrentState == MotionState.Sprint)
                        bobFreq *= blob.BobSprintMultiplier;

                    ms.BobPhase += horizontalSpeed * bobFreq * dt;

                    float bobX = math.sin(ms.BobPhase) * blob.BobAmplitudeX * bobFactor;
                    float bobY = math.abs(math.cos(ms.BobPhase)) * blob.BobAmplitudeY * bobFactor;
                    float bobRoll = math.sin(ms.BobPhase) * blob.BobRotationScale * bobFactor;

                    // Apply bob as soft target (blend toward computed position)
                    ws.PositionVelocity.x += (bobX - ws.PositionValue.x) * 20f * dt;
                    ws.PositionVelocity.y += (bobY - ws.PositionValue.y) * 20f * dt;
                    ws.RotationVelocity.z += (bobRoll - ws.RotationValue.z) * 15f * dt;
                }

                // ── Force 3: Inertia ──────────────────────────
                float inertiaFactor = inertiaScale * masterScale;
                if (inertiaFactor > 0.001f)
                {
                    float3 velocityDelta = vel - ms.PreviousVelocity;
                    velocityDelta = math.clamp(velocityDelta,
                        new float3(-blob.InertiaMaxForce), new float3(blob.InertiaMaxForce));

                    ws.PositionVelocity -= velocityDelta * blob.InertiaPositionScale * inertiaFactor;
                }
                ms.PreviousVelocity = vel;

                // ── Force 4: Landing Impact ───────────────────
                bool isGrounded = playerState.ValueRO.IsGrounded;
                if (!ms.WasGrounded && isGrounded)
                {
                    float fallSpeed = math.abs(vel.y);
                    ms.LandingImpactSpeed = fallSpeed;
                    ms.TimeSinceLanding = 0f;

                    float impulseNorm = math.saturate(fallSpeed / math.max(blob.LandingSpeedThreshold, 0.1f));
                    float impulseClamp = math.min(impulseNorm, blob.LandingMaxImpulse / math.max(blob.LandingPositionImpulse, 0.001f));

                    ws.PositionVelocity.y -= blob.LandingPositionImpulse * impulseClamp * masterScale;
                    ws.RotationVelocity.x += blob.LandingRotationImpulse * impulseClamp * masterScale;
                }
                ms.WasGrounded = isGrounded;
                ms.TimeSinceLanding += dt;

                // ── Force 5: Idle Noise ───────────────────────
                float noiseFactor = noiseScale * masterScale;
                if (horizontalSpeed < 0.1f && noiseFactor > 0.001f)
                {
                    ms.IdleNoiseTime += dt * blob.IdleNoiseFrequency;
                    float noiseX = noise.snoise(new float2(ms.IdleNoiseTime, 0f)) * blob.IdleNoiseAmplitude * noiseFactor;
                    float noiseY = noise.snoise(new float2(ms.IdleNoiseTime, 100f)) * blob.IdleNoiseAmplitude * noiseFactor;
                    float noiseRotX = noise.snoise(new float2(ms.IdleNoiseTime, 200f)) * blob.IdleNoiseRotationScale * noiseFactor;

                    ws.PositionVelocity.x += (noiseX - ws.PositionValue.x * 0.5f) * dt * 5f;
                    ws.PositionVelocity.y += (noiseY - ws.PositionValue.y * 0.5f) * dt * 5f;
                    ws.RotationVelocity.x += noiseRotX * dt * 3f;
                }

                // ── Force 6: Wall Tuck ────────────────────────
                // WallTuckT is set by WeaponMotionApplySystem (managed, does SphereCast)
                if (ms.WallTuckT > 0.01f)
                {
                    float tuckScale = ms.WallTuckT * masterScale;
                    float targetZ = blob.WallTuckPositionZ * tuckScale;
                    float targetPitch = blob.WallTuckRotationPitch * tuckScale;

                    ws.PositionVelocity.z += (targetZ - ws.PositionValue.z) * blob.WallTuckBlendSpeed;
                    ws.RotationVelocity.x += (targetPitch - ws.RotationValue.x) * blob.WallTuckBlendSpeed;
                }

                // ── Force 7: Visual Recoil ────────────────────
                var fire = fireState.ValueRO;
                if (fire.IsFiring && fire.TimeSinceLastShot < dt * 1.5f)
                {
                    float recoilScale = masterScale;
                    ws.PositionVelocity.z += blob.VisualRecoilKickZ * blob.VisualRecoilPositionSnap * recoilScale;
                    ws.RotationVelocity.x += blob.VisualRecoilPitchUp * recoilScale;

                    // Random roll from entity hash (deterministic per entity)
                    var pos = SystemAPI.GetComponentRO<LocalTransform>(entity).ValueRO.Position;
                    uint seed = (uint)(state.GlobalSystemVersion + pos.GetHashCode());
                    var rng = Unity.Mathematics.Random.CreateFromIndex(seed);
                    ws.RotationVelocity.z += rng.NextFloat(-blob.VisualRecoilRollRange, blob.VisualRecoilRollRange) * recoilScale;
                }

                // ── Force 8: Hit Reaction ─────────────────────
                // Hit reactions are applied by ProceduralCameraForceSystem to camera,
                // and also to weapon spring here via damage direction (done in Phase 4).
                // Weapon hit reaction weight: ms.HitReactionWeight * ms.FPMotionWeight

                // Apply blended static offset as a soft spring target
                float offsetBlend = dt * 8f;
                ws.PositionVelocity += (blendedPosOffset - ws.PositionValue) * offsetBlend;
                ws.RotationVelocity += (blendedRotOffset - ws.RotationValue) * offsetBlend;
            }
        }

        private static void ApplySpringParameters(ref WeaponSpringState ws,
            ref ProceduralMotionBlob blob, ref MotionStateOverride cur, ref MotionStateOverride prev, float t)
        {
            // Compute blended frequency and damping
            float3 curFreq = cur.PositionFrequency;
            float3 prevFreq = prev.PositionFrequency;

            if (cur.FrequencyIsMultiplier)
                curFreq = math.select(blob.DefaultPositionFrequency, blob.DefaultPositionFrequency * curFreq, curFreq > float3.zero);
            else
                curFreq = math.select(blob.DefaultPositionFrequency, curFreq, curFreq > float3.zero);

            if (prev.FrequencyIsMultiplier)
                prevFreq = math.select(blob.DefaultPositionFrequency, blob.DefaultPositionFrequency * prevFreq, prevFreq > float3.zero);
            else
                prevFreq = math.select(blob.DefaultPositionFrequency, prevFreq, prevFreq > float3.zero);

            ws.PositionFrequency = math.lerp(prevFreq, curFreq, t);

            float3 curDamp = math.select(blob.DefaultPositionDampingRatio, cur.PositionDampingRatio, cur.PositionDampingRatio > float3.zero);
            float3 prevDamp = math.select(blob.DefaultPositionDampingRatio, prev.PositionDampingRatio, prev.PositionDampingRatio > float3.zero);
            ws.PositionDampingRatio = math.lerp(prevDamp, curDamp, t);

            // Rotation
            float3 curRotFreq = cur.RotationFrequency;
            float3 prevRotFreq = prev.RotationFrequency;
            curRotFreq = math.select(blob.DefaultRotationFrequency, curRotFreq, curRotFreq > float3.zero);
            prevRotFreq = math.select(blob.DefaultRotationFrequency, prevRotFreq, prevRotFreq > float3.zero);
            ws.RotationFrequency = math.lerp(prevRotFreq, curRotFreq, t);

            float3 curRotDamp = math.select(blob.DefaultRotationDampingRatio, cur.RotationDampingRatio, cur.RotationDampingRatio > float3.zero);
            float3 prevRotDamp = math.select(blob.DefaultRotationDampingRatio, prev.RotationDampingRatio, prev.RotationDampingRatio > float3.zero);
            ws.RotationDampingRatio = math.lerp(prevRotDamp, curRotDamp, t);
        }
    }
}
