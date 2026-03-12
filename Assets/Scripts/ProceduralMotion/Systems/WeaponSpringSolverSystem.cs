using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.ProceduralMotion.Systems
{
    /// <summary>
    /// EPIC 15.25 Phase 1: Analytical second-order spring solver for WeaponSpringState.
    /// Frame-rate independent by construction — identical behavior at 30fps and 240fps.
    /// Runs after force providers have accumulated impulses into velocity fields.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ProceduralWeaponForceSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct WeaponSpringSolverSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            if (dt <= 0f) return;

            foreach (var spring in SystemAPI.Query<RefRW<WeaponSpringState>>()
                         .WithAll<GhostOwnerIsLocal>())
            {
                if (spring.ValueRO.IsFrozen) continue;

                SolveAnalytical(ref spring.ValueRW.PositionValue, ref spring.ValueRW.PositionVelocity,
                    spring.ValueRO.PositionFrequency, spring.ValueRO.PositionDampingRatio, dt);

                SolveAnalytical(ref spring.ValueRW.RotationValue, ref spring.ValueRW.RotationVelocity,
                    spring.ValueRO.RotationFrequency, spring.ValueRO.RotationDampingRatio, dt);

                // Clamp
                spring.ValueRW.PositionValue = math.clamp(spring.ValueRW.PositionValue,
                    spring.ValueRO.PositionMin, spring.ValueRO.PositionMax);
                spring.ValueRW.RotationValue = math.clamp(spring.ValueRW.RotationValue,
                    spring.ValueRO.RotationMin, spring.ValueRO.RotationMax);

                // Near-zero stop to prevent micro-jitter
                if (math.lengthsq(spring.ValueRO.PositionVelocity) < 0.000001f &&
                    math.lengthsq(spring.ValueRO.PositionValue) < 0.000001f)
                {
                    spring.ValueRW.PositionVelocity = float3.zero;
                    spring.ValueRW.PositionValue = float3.zero;
                }

                if (math.lengthsq(spring.ValueRO.RotationVelocity) < 0.0001f &&
                    math.lengthsq(spring.ValueRO.RotationValue) < 0.0001f)
                {
                    spring.ValueRW.RotationVelocity = float3.zero;
                    spring.ValueRW.RotationValue = float3.zero;
                }
            }
        }

        /// <summary>
        /// Analytical second-order spring solver. Solves each axis independently.
        /// Parameters: frequency (Hz) and damping ratio (zeta).
        /// z &lt; 1: underdamped (overshoot), z = 1: critically damped, z &gt; 1: overdamped.
        /// </summary>
        public static void SolveAnalytical(ref float3 value, ref float3 velocity,
            in float3 frequency, in float3 dampingRatio, float dt)
        {
            for (int i = 0; i < 3; i++)
            {
                float f = frequency[i];
                if (f <= 0f) continue; // Skip axes with no spring

                float z = dampingRatio[i];
                float v = value[i];
                float vel = velocity[i];

                SolveAxis(ref v, ref vel, f, z, dt);

                value[i] = v;
                velocity[i] = vel;
            }
        }

        private static void SolveAxis(ref float value, ref float velocity, float f, float z, float dt)
        {
            float omega = 2f * math.PI * f;

            if (z < 0.999f)
            {
                // Underdamped
                float zClamped = math.max(z, 0.001f);
                float omega_d = omega * math.sqrt(1f - zClamped * zClamped);
                float e = math.exp(-zClamped * omega * dt);
                float c = math.cos(omega_d * dt);
                float s = math.sin(omega_d * dt);

                float newValue = e * (value * c + (velocity + zClamped * omega * value) / omega_d * s);
                float newVelocity = e * (velocity * (c - zClamped * omega / omega_d * s) - value * omega * omega / omega_d * s);

                value = newValue;
                velocity = newVelocity;
            }
            else if (z < 1.001f)
            {
                // Critically damped
                float e = math.exp(-omega * dt);
                float newValue = e * (value * (1f + omega * dt) + velocity * dt);
                float newVelocity = e * (velocity * (1f - omega * dt) - value * omega * omega * dt);

                value = newValue;
                velocity = newVelocity;
            }
            else
            {
                // Overdamped
                float sq = math.sqrt(z * z - 1f);
                float r1 = -omega * (z - sq);
                float r2 = -omega * (z + sq);
                float denom = r1 - r2;
                if (math.abs(denom) < 0.0001f) denom = 0.0001f;

                float c1 = (velocity - r2 * value) / denom;
                float c2 = value - c1;
                float e1 = math.exp(r1 * dt);
                float e2 = math.exp(r2 * dt);

                float newValue = c1 * e1 + c2 * e2;
                float newVelocity = c1 * r1 * e1 + c2 * r2 * e2;

                value = newValue;
                velocity = newVelocity;
            }
        }
    }
}
