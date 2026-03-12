using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;
using DIG.Voxel.Components;
using DIG.Voxel.Core;
using DIG.Voxel.Interaction;

namespace DIG.Voxel.Systems.Interaction
{
    /// <summary>
    /// Local/Offline Loot System.
    /// Uses Burst to process events and filter drops, Main Thread only handles instantiation.
    /// 
    /// In NETWORKED games, LootSpawnServerSystem + LootSpawnClientSystem handle loot visibility.
    /// This system only runs in LocalSimulation (offline/single-player mode).
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    public partial class VoxelLootSystem : SystemBase
    {
        // Cache native rules for Burst
        private NativeHashMap<byte, LootRule> _lootRules;
        private VoxelMaterialRegistry _registry;
        private LootPhysicsSettings _physicsSettings;
        private Unity.Mathematics.Random _random;
        private System.Random _systemRandom;

        private struct LootRule
        {
            public float DropChance;
            public int Min;
            public int Max;
        }

        private struct LootSpawnCommand
        {
            public byte MaterialID;
            public float3 Position;
            public float3 Velocity;
            public float3 MinePoint; // For radial scatter calculation
        }

        private bool _registrySearchFailed;

        protected override void OnCreate()
        {
            _lootRules = new NativeHashMap<byte, LootRule>(256, Allocator.Persistent);
            _random = new Unity.Mathematics.Random((uint)System.DateTime.Now.Ticks);
            _systemRandom = new System.Random();

            // Load physics settings (Task 10.17.13)
            _physicsSettings = Resources.Load<LootPhysicsSettings>("LootPhysicsSettings");
            if (_physicsSettings == null)
            {
                _physicsSettings = LootPhysicsSettings.CreateDefault();
            }

            // Load registry once at creation
            _registry = Resources.Load<VoxelMaterialRegistry>("VoxelMaterialRegistry");
            if (_registry != null)
            {
                InitializeRegistry();
            }
        }

        protected override void OnDestroy()
        {
            if (_lootRules.IsCreated) _lootRules.Dispose();
        }

        protected override void OnUpdate()
        {
            if (!SystemAPI.TryGetSingletonEntity<VoxelEventsSingleton>(out Entity eventEntity))
                return;

            var buffer = EntityManager.GetBuffer<VoxelDestroyedEvent>(eventEntity);
            if (buffer.IsEmpty) return;

            // Lazy retry registry load (once only)
            if (_registry == null)
            {
                EnsureRegistryAndCache();
                if (_registry == null) return;
            }

                // 3. Run Job to process events -> Commands
            var commands = new NativeQueue<LootSpawnCommand>(Allocator.TempJob);
            
            // Advance RNG
            uint seed = _random.NextUInt();
            
            var job = new ProcessLootEventsJob
            {
                Events = buffer.AsNativeArray(),
                Rules = _lootRules,
                Commands = commands.AsParallelWriter(),
                RandomSeed = seed
            };
            
            job.Schedule(buffer.Length, 64).Complete(); // Sync processing (Presentation group)

            // Debug Logic
            // UnityEngine.Debug.Log($"[VoxelLootSystem] Processing {buffer.Length} events. Rules found: {_lootRules.Count}. Commands: {commands.Count}");

            // 4. Execute Spawns (Main Thread)
            int spawnCount = 0;
            const int MAX_SPAWNS = 50; 
            
            while (commands.TryDequeue(out var cmd))
            {
                if (spawnCount++ > MAX_SPAWNS) break;

                // Lookup Prefab (Fast managed lookup)
                var matDef = _registry.GetMaterial(cmd.MaterialID);
                if (matDef != null && matDef.LootPrefab != null)
                {
                    var go = Object.Instantiate(matDef.LootPrefab, cmd.Position, Quaternion.identity);
                    
                    // Task 10.17.11: Apply explosion scatter force
                    var rb = go.GetComponent<Rigidbody>();
                    
                    // Fallback: Add Rigidbody if prefab doesn't have one
                    if (rb == null)
                    {
                        rb = go.AddComponent<Rigidbody>();
                    }
                    
                    // Task 10.17.12: Configure physics weight
                    float massMultiplier = matDef.LootMassMultiplier > 0 ? matDef.LootMassMultiplier : 1f;
                    _physicsSettings.ConfigureRigidbody(rb, massMultiplier);
                    
                    // Calculate radial scatter velocity
                    Vector3 scatterVel = _physicsSettings.CalculateScatterVelocity(
                        cmd.MinePoint, cmd.Position, _systemRandom);
                    rb.AddForce(scatterVel, ForceMode.Impulse);
                    
                    // Add some random spin for visual interest
                    rb.AddTorque(new Vector3(
                        (float)(_systemRandom.NextDouble() * 2 - 1) * 5f,
                        (float)(_systemRandom.NextDouble() * 2 - 1) * 5f,
                        (float)(_systemRandom.NextDouble() * 2 - 1) * 5f
                    ), ForceMode.Impulse);
                    
                    // Task 10.17.15: Add lifetime component
                    var lifetime = go.GetComponent<LootLifetime>();
                    if (lifetime == null)
                    {
                        lifetime = go.AddComponent<LootLifetime>();
                    }
                    lifetime.Initialize(_physicsSettings.Lifetime, _physicsSettings.FadeDuration);
                }
                // else: material has no loot prefab configured — skip silently
            }
            
            commands.Dispose();
            buffer.Clear();
        }

        private void EnsureRegistryAndCache()
        {
            if (_registrySearchFailed) return;

            _registry = Resources.Load<VoxelMaterialRegistry>("VoxelMaterialRegistry");
            if (_registry != null)
            {
                InitializeRegistry();
                return;
            }

            // One-time fallback — never retry after this
            var allMats = Resources.LoadAll<VoxelMaterialDefinition>("VoxelMaterials");
            if (allMats.Length > 0)
            {
                var newReg = ScriptableObject.CreateInstance<VoxelMaterialRegistry>();
                newReg.Materials = new System.Collections.Generic.List<VoxelMaterialDefinition>(allMats);
                _registry = newReg;
                InitializeRegistry();
            }
            else
            {
                _registrySearchFailed = true;
            }
        }

        private void InitializeRegistry()
        {
            _registry.Initialize(); 
            _lootRules.Clear();
            foreach (var mat in _registry.Materials)
            {
                if (mat != null)
                {
                    _lootRules.TryAdd(mat.MaterialID, new LootRule
                    {
                        DropChance = mat.DropChance,
                        Min = mat.MinDropCount,
                        Max = mat.MaxDropCount
                    });
                }
            }
        }


        [BurstCompile]
        private struct ProcessLootEventsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<VoxelDestroyedEvent> Events;
            [ReadOnly] public NativeHashMap<byte, LootRule> Rules;
            [WriteOnly] public NativeQueue<LootSpawnCommand>.ParallelWriter Commands;
            public uint RandomSeed;

            public void Execute(int index)
            {
                var evt = Events[index];
                if (!Rules.TryGetValue(evt.MaterialID, out var rule)) return;

                // Independent RNG per index
                var rng = new Unity.Mathematics.Random(RandomSeed + (uint)index * 987654321);

                if (rng.NextFloat() > rule.DropChance) return;

                int count = rng.NextInt(rule.Min, rule.Max + 1);
                
                for (int i = 0; i < count; i++)
                {
                    // Spread spawn position around mine point
                    float3 pos = evt.Position + new float3(
                        rng.NextFloat(-0.25f, 0.25f),
                        rng.NextFloat(0.1f, 0.4f),
                        rng.NextFloat(-0.25f, 0.25f)
                    );
                    
                    // Velocity is now calculated on main thread using LootPhysicsSettings
                    // We pass the mine point for radial direction calculation
                    Commands.Enqueue(new LootSpawnCommand
                    {
                        MaterialID = evt.MaterialID,
                        Position = pos,
                        Velocity = float3.zero, // Calculated on main thread
                        MinePoint = evt.Position
                    });
                }
            }
        }
    }
}
