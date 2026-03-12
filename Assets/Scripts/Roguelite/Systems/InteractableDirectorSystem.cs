using Unity.Entities;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace DIG.Roguelite.Zones
{
    /// <summary>
    /// Places interactables in the zone on activation. Reads InteractablePoolSO, selects
    /// items by weight within budget, and calls IInteractableHandler with resolved positions
    /// and type IDs. Runs once per zone activation (called by ZoneTransitionSystem).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ZoneTransitionSystem))]
    public partial class InteractableDirectorSystem : SystemBase
    {
        protected override void OnCreate()
        {
            Enabled = false; // Only runs when explicitly called
        }

        protected override void OnUpdate() { }

        /// <summary>
        /// Select and place interactables from the pool at the given nodes.
        /// Called by ZoneTransitionSystem during zone activation.
        /// </summary>
        public void PlaceInteractables(
            float3[] nodes,
            InteractablePoolSO pool,
            uint seed,
            float difficulty,
            int budget,
            IInteractableHandler handler)
        {
            if (nodes == null || nodes.Length == 0 || pool == null
                || pool.Entries == null || pool.Entries.Count == 0
                || handler == null || budget <= 0)
                return;

            var rng = new Random(seed | 1);
            int entryCount = pool.Entries.Count;

            // Resolve how many interactables to place (min of budget and available nodes)
            int count = math.min(budget, nodes.Length);

            // Flat array for placed counts per pool entry index — avoids Dictionary alloc
            var placedCounts = new int[entryCount];

            // Select type IDs from pool by weight, respecting difficulty and MaxPerZone
            var typeIds = new int[count];

            for (int i = 0; i < count; i++)
            {
                int typeId = SelectFromPool(pool, ref rng, difficulty, placedCounts);
                typeIds[i] = typeId;

                // Increment placed count for the selected entry
                for (int e = 0; e < entryCount; e++)
                {
                    if (pool.Entries[e].InteractableTypeId == typeId)
                    {
                        placedCounts[e]++;
                        break;
                    }
                }
            }

            // Shuffle node assignment for variety
            var selectedNodes = new float3[count];
            for (int i = 0; i < count; i++)
                selectedNodes[i] = nodes[i % nodes.Length];

            // Fisher-Yates shuffle
            for (int i = count - 1; i > 0; i--)
            {
                int j = rng.NextInt(0, i + 1);
                (selectedNodes[i], selectedNodes[j]) = (selectedNodes[j], selectedNodes[i]);
            }

            handler.PlaceInteractables(selectedNodes, typeIds, seed, difficulty);
        }

        private static int SelectFromPool(
            InteractablePoolSO pool,
            ref Random rng,
            float difficulty,
            int[] placedCounts)
        {
            // Single pass: compute totalWeight and select in one go
            float totalWeight = 0f;
            int entryCount = pool.Entries.Count;

            for (int i = 0; i < entryCount; i++)
            {
                var e = pool.Entries[i];
                if (e.MinDifficulty > 0 && difficulty < e.MinDifficulty) continue;
                if (e.MaxPerZone > 0 && placedCounts[i] >= e.MaxPerZone) continue;
                totalWeight += e.Weight;
            }

            if (totalWeight <= 0f && entryCount > 0)
                return pool.Entries[0].InteractableTypeId;

            float roll = rng.NextFloat() * totalWeight;
            float acc = 0f;

            for (int i = 0; i < entryCount; i++)
            {
                var e = pool.Entries[i];
                if (e.MinDifficulty > 0 && difficulty < e.MinDifficulty) continue;
                if (e.MaxPerZone > 0 && placedCounts[i] >= e.MaxPerZone) continue;

                acc += e.Weight;
                if (roll <= acc)
                    return e.InteractableTypeId;
            }

            return pool.Entries[0].InteractableTypeId;
        }
    }
}
