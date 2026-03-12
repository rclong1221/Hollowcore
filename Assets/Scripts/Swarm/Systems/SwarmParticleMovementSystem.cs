using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using DIG.Swarm.Components;
using DIG.Swarm.Profiling;

namespace DIG.Swarm.Systems
{
    /// <summary>
    /// EPIC 16.2 Phase 2: Moves swarm particles along flow field directions.
    /// Samples flow field at particle position, applies Perlin noise for organic movement.
    /// Uses Burst-compiled IJobEntity for per-particle work.
    /// OnUpdate (managed) reads static SwarmFlowFieldData, then passes the NativeArray to Burst jobs.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(FlowFieldBuildSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct SwarmParticleMovementSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FlowFieldGrid>();
            state.RequireForUpdate<SwarmConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            using (SwarmProfilerMarkers.ParticleMovement.Auto())
            {
                if (!SwarmFlowFieldData.IsInitialized || !SwarmFlowFieldData.Cells.IsCreated)
                    return;

                var grid = SystemAPI.GetSingleton<FlowFieldGrid>();
                if (!grid.IsBuilt)
                    return;

                var config = SystemAPI.GetSingleton<SwarmConfig>();
                float dt = SystemAPI.Time.DeltaTime;
                float time = (float)SystemAPI.Time.ElapsedTime;

                // Read static NativeArray handle here (managed code), pass to Burst jobs
                var cells = SwarmFlowFieldData.Cells;

                // Pure particles — flow field + Perlin noise
                state.Dependency = new MoveParticlesJob
                {
                    Cells = cells,
                    Grid = grid,
                    NoiseScale = config.NoiseScale,
                    NoiseStrength = config.NoiseStrength,
                    FlowFieldWeight = config.FlowFieldWeight,
                    DeltaTime = dt,
                    ElapsedTime = time,
                }.ScheduleParallel(state.Dependency);

                // Aware agents — flow field + individual target bias + reduced noise
                state.Dependency = new MoveAgentsJob
                {
                    Cells = cells,
                    Grid = grid,
                    NoiseScale = config.NoiseScale,
                    NoiseStrength = config.NoiseStrength,
                    FlowFieldWeight = config.FlowFieldWeight,
                    DeltaTime = dt,
                    ElapsedTime = time,
                }.ScheduleParallel(state.Dependency);
            }
        }

        /// <summary>
        /// Bilinear sample of flow field direction. Burst-compatible (no static field access).
        /// </summary>
        static float2 SampleFlowDirection(float3 worldPos, in FlowFieldGrid grid,
            in NativeArray<FlowFieldCell> cells)
        {
            float fx = (worldPos.x - grid.WorldOrigin.x) / grid.CellSize - 0.5f;
            float fz = (worldPos.z - grid.WorldOrigin.z) / grid.CellSize - 0.5f;

            int x0 = (int)math.floor(fx);
            int z0 = (int)math.floor(fz);
            int x1 = x0 + 1;
            int z1 = z0 + 1;

            x0 = math.clamp(x0, 0, grid.GridWidth - 1);
            x1 = math.clamp(x1, 0, grid.GridWidth - 1);
            z0 = math.clamp(z0, 0, grid.GridHeight - 1);
            z1 = math.clamp(z1, 0, grid.GridHeight - 1);

            float tx = math.frac(fx);
            float tz = math.frac(fz);

            var d00 = cells[z0 * grid.GridWidth + x0].Direction;
            var d10 = cells[z0 * grid.GridWidth + x1].Direction;
            var d01 = cells[z1 * grid.GridWidth + x0].Direction;
            var d11 = cells[z1 * grid.GridWidth + x1].Direction;

            var d0 = math.lerp(d00, d10, tx);
            var d1 = math.lerp(d01, d11, tx);
            var dir = math.lerp(d0, d1, tz);

            float len = math.length(dir);
            return len > 0.001f ? dir / len : float2.zero;
        }

        [BurstCompile]
        [WithNone(typeof(SwarmAgent))]
        partial struct MoveParticlesJob : IJobEntity
        {
            [ReadOnly] public NativeArray<FlowFieldCell> Cells;
            public FlowFieldGrid Grid;
            public float NoiseScale;
            public float NoiseStrength;
            public float FlowFieldWeight;
            public float DeltaTime;
            public float ElapsedTime;

            void Execute(ref SwarmParticle particle, ref SwarmAnimState animState)
            {
                float2 flowDir = SampleFlowDirection(particle.Position, in Grid, in Cells);

                // Perlin noise for organic movement
                float noiseX = noise.cnoise(new float2(
                    particle.Position.x * NoiseScale + ElapsedTime * 0.5f,
                    particle.Position.z * NoiseScale
                ));
                float noiseZ = noise.cnoise(new float2(
                    particle.Position.z * NoiseScale,
                    particle.Position.x * NoiseScale + ElapsedTime * 0.5f + 100f
                ));
                float2 noiseOffset = new float2(noiseX, noiseZ) * NoiseStrength;

                float2 desiredDir = flowDir * FlowFieldWeight + noiseOffset;
                float dirLen = math.length(desiredDir);
                if (dirLen > 0.001f)
                    desiredDir /= dirLen;

                float3 movement = new float3(desiredDir.x, 0f, desiredDir.y) * particle.Speed * DeltaTime;
                particle.Velocity = math.lerp(particle.Velocity, movement / math.max(DeltaTime, 0.001f), 5f * DeltaTime);
                particle.Position += movement;

                float speed = math.length(new float2(particle.Velocity.x, particle.Velocity.z));
                if (speed < 0.1f)
                    animState.AnimClipIndex = 0; // Idle
                else if (speed < particle.Speed * 0.6f)
                    animState.AnimClipIndex = 1; // Walk
                else
                    animState.AnimClipIndex = 2; // Run

                animState.AnimTime += DeltaTime * animState.AnimSpeed;
                if (animState.AnimTime >= 1f)
                    animState.AnimTime -= 1f;
            }
        }

        [BurstCompile]
        partial struct MoveAgentsJob : IJobEntity
        {
            [ReadOnly] public NativeArray<FlowFieldCell> Cells;
            public FlowFieldGrid Grid;
            public float NoiseScale;
            public float NoiseStrength;
            public float FlowFieldWeight;
            public float DeltaTime;
            public float ElapsedTime;

            void Execute(ref SwarmParticle particle, ref SwarmAnimState animState, in SwarmAgent agent)
            {
                float2 flowDir = SampleFlowDirection(particle.Position, in Grid, in Cells);

                // Bias toward individual flow target
                float3 toTarget = agent.FlowTarget - particle.Position;
                float2 targetDir = math.normalizesafe(new float2(toTarget.x, toTarget.z));

                // Blend flow field with individual target (aware agents are more directed)
                float2 desiredDir = flowDir * FlowFieldWeight * 0.5f + targetDir * 1.5f;
                float dirLen = math.length(desiredDir);
                if (dirLen > 0.001f)
                    desiredDir /= dirLen;

                // Noise (less than pure particles — more purposeful)
                float noiseX = noise.cnoise(new float2(
                    particle.Position.x * NoiseScale * 0.5f + ElapsedTime * 0.3f,
                    particle.Position.z * NoiseScale * 0.5f
                ));
                desiredDir += new float2(noiseX, -noiseX) * NoiseStrength * 0.3f;

                float3 movement = new float3(desiredDir.x, 0f, desiredDir.y) * particle.Speed * DeltaTime;
                particle.Velocity = math.lerp(particle.Velocity, movement / math.max(DeltaTime, 0.001f), 5f * DeltaTime);
                particle.Position += movement;

                float speed = math.length(new float2(particle.Velocity.x, particle.Velocity.z));
                animState.AnimClipIndex = speed < particle.Speed * 0.6f ? (byte)1 : (byte)2;
                animState.AnimTime += DeltaTime * animState.AnimSpeed;
                if (animState.AnimTime >= 1f)
                    animState.AnimTime -= 1f;
            }
        }
    }
}
