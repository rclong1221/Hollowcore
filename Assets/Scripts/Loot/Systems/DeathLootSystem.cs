using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using DIG.Combat.Components;
using DIG.Loot.Components;
using Player.Components;
using DIG.Party;

namespace DIG.Loot.Systems
{
    /// <summary>
    /// EPIC 16.6: On enemy death, resolves loot table and writes PendingLootSpawn buffer.
    /// Runs after DeathTransitionSystem fires DiedEvent.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(global::Player.Systems.DeathTransitionSystem))]
    public partial class DeathLootSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<NetworkTime>();
        }

        protected override void OnUpdate()
        {
            // Need registry to resolve tables
            if (!SystemAPI.ManagedAPI.HasSingleton<LootTableRegistryManaged>())
                return;

            var registry = SystemAPI.ManagedAPI.GetSingleton<LootTableRegistryManaged>();
            var netTime = SystemAPI.GetSingleton<NetworkTime>();
            uint serverTick = netTime.ServerTick.IsValid ? netTime.ServerTick.TickIndexForValidTick : 1;

            foreach (var (lootRef, transform, pendingBuffer, entity) in
                     SystemAPI.Query<RefRW<LootTableRef>, RefRO<LocalTransform>, DynamicBuffer<PendingLootSpawn>>()
                     .WithAll<DiedEvent>()
                     .WithEntityAccess())
            {
                // Skip if already dropped
                if (lootRef.ValueRO.HasDropped)
                    continue;

                // Lookup the table SO
                if (!registry.Tables.TryGetValue(lootRef.ValueRO.LootTableId, out var table))
                    continue;

                // EPIC 16.14: Resolve killer's level for loot context.
                // The dying entity's CombatState.LastAttacker holds the killer entity.
                // Use killer's CharacterAttributes.Level for loot table MinLevel/MaxLevel gating.
                int killerLevel = 1;
                if (SystemAPI.HasComponent<global::Player.Components.CombatState>(entity))
                {
                    var killer = SystemAPI.GetComponent<global::Player.Components.CombatState>(entity).LastAttacker;
                    if (killer != Entity.Null && SystemAPI.HasComponent<CharacterAttributes>(killer))
                        killerLevel = math.max(1, SystemAPI.GetComponent<CharacterAttributes>(killer).Level);
                }

                // Build context with deterministic seed
                var context = new LootContext
                {
                    Level = killerLevel,
                    DifficultyMultiplier = 1f,
                    LuckModifier = 0f,
                    RandomSeed = (uint)(entity.Index + 1) ^ serverTick
                };

                // Resolve drops
                var drops = new NativeList<LootDrop>(8, Allocator.Temp);
                LootTableResolver.Resolve(table, context, ref drops);

                // Apply quantity multiplier and write to buffer
                float3 spawnPos = transform.ValueRO.Position;
                float qtyMul = lootRef.ValueRO.QuantityMultiplier;

                for (int i = 0; i < drops.Length; i++)
                {
                    var drop = drops[i];
                    int adjustedQty = (int)math.max(1, math.round(drop.Quantity * qtyMul));

                    pendingBuffer.Add(new PendingLootSpawn
                    {
                        ItemTypeId = drop.ItemTypeId,
                        Quantity = adjustedQty,
                        Rarity = drop.Rarity,
                        Type = drop.Type,
                        Currency = drop.Currency,
                        Resource = drop.Resource,
                        SpawnPosition = spawnPos
                    });
                }

                drops.Dispose();

                // EPIC 17.2: Apply party loot designation if present
                if (SystemAPI.HasComponent<LootDesignation>(entity))
                {
                    var designation = SystemAPI.GetComponent<LootDesignation>(entity);
                    // The LootDesignation on the dying entity signals that loot should
                    // be restricted to a specific player (RoundRobin/MasterLoot/NeedGreed winner).
                    // LootSpawnSystem downstream will read this and apply pickup restriction.
                    // Note: The designation is already on the entity from PartyLootSystem.
                }

                // Mark as dropped
                lootRef.ValueRW.HasDropped = true;

                // EPIC 16.3 Phase 4.3: Enforce minimum corpse lifetime for loot pickup window.
                // Corpse must persist at least as long as the shortest loot lifetime (Common = 60s).
                if (drops.Length > 0 && SystemAPI.HasComponent<CorpseState>(entity))
                {
                    const float MinCorpseLifetimeForLoot = 60f;
                    var corpse = SystemAPI.GetComponentRW<CorpseState>(entity);
                    if (corpse.ValueRO.CorpseLifetime < MinCorpseLifetimeForLoot)
                    {
                        corpse.ValueRW.CorpseLifetime = MinCorpseLifetimeForLoot;
                    }
                }
            }
        }
    }
}
