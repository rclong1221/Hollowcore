using Unity.Burst;
using Unity.Entities;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
#endif

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.2: Processes MetaUnlockRequest — validates cost and prerequisites,
    /// deducts meta-currency, marks the unlock as purchased.
    /// Burst-compiled. Runs during MetaScreen phase (between runs).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct MetaUnlockPurchaseSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MetaBank>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var bankEntity = SystemAPI.GetSingletonEntity<MetaBank>();

            // Check if a purchase request is pending
            if (!state.EntityManager.IsComponentEnabled<MetaUnlockRequest>(bankEntity))
                return;

            var request = SystemAPI.GetSingleton<MetaUnlockRequest>();
            var bank = SystemAPI.GetSingleton<MetaBank>();
            var unlocks = SystemAPI.GetBuffer<MetaUnlockEntry>(bankEntity);

            // Consume the request immediately
            state.EntityManager.SetComponentEnabled<MetaUnlockRequest>(bankEntity, false);

            // Find the requested unlock
            int targetIndex = -1;
            for (int i = 0; i < unlocks.Length; i++)
            {
                if (unlocks[i].UnlockId == request.UnlockId)
                {
                    targetIndex = i;
                    break;
                }
            }

            if (targetIndex < 0)
            {
                LogPurchaseResult(request.UnlockId, PurchaseResult.NotFound);
                return;
            }

            var entry = unlocks[targetIndex];

            // Already unlocked
            if (entry.IsUnlocked)
            {
                LogPurchaseResult(request.UnlockId, PurchaseResult.AlreadyUnlocked);
                return;
            }

            // Check prerequisite
            if (entry.PrerequisiteId >= 0)
            {
                bool prereqMet = false;
                for (int i = 0; i < unlocks.Length; i++)
                {
                    if (unlocks[i].UnlockId == entry.PrerequisiteId && unlocks[i].IsUnlocked)
                    {
                        prereqMet = true;
                        break;
                    }
                }
                if (!prereqMet)
                {
                    LogPurchaseResult(request.UnlockId, PurchaseResult.PrerequisiteNotMet);
                    return;
                }
            }

            // Check cost
            if (bank.MetaCurrency < entry.Cost)
            {
                LogPurchaseResult(request.UnlockId, PurchaseResult.InsufficientFunds);
                return;
            }

            // Deduct currency and unlock
            bank.MetaCurrency -= entry.Cost;
            SystemAPI.SetSingleton(bank);

            entry.IsUnlocked = true;
            unlocks[targetIndex] = entry;

            LogPurchaseResult(request.UnlockId, PurchaseResult.Success);
        }

        private enum PurchaseResult : byte
        {
            Success,
            NotFound,
            AlreadyUnlocked,
            PrerequisiteNotMet,
            InsufficientFunds
        }

        [BurstDiscard]
        private static void LogPurchaseResult(int unlockId, PurchaseResult result)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[MetaUnlock] Purchase UnlockId={unlockId}: {result}");
#endif
        }
    }
}
