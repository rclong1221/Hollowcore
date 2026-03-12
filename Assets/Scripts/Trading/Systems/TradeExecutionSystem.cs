using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using DIG.Shared;
using DIG.Economy;
using DIG.Combat.Components;

namespace DIG.Trading
{
    /// <summary>
    /// EPIC 17.3: ATOMIC trade execution. Runs ONLY when State==Executing.
    /// Re-validates everything, then performs item+currency swap in a single pass.
    /// Uses direct buffer access (NOT ECB) for the swap to ensure atomicity.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TradeConfirmReceiveSystem))]
    public partial class TradeExecutionSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<TradeConfig>();
        }

        protected override void OnUpdate()
        {
            var config = SystemAPI.GetSingleton<TradeConfig>();
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            bool resourceWeightsAvailable = SystemAPI.HasSingleton<ResourceWeights>();
            ResourceWeights weights = resourceWeightsAvailable ? SystemAPI.GetSingleton<ResourceWeights>() : ResourceWeights.Default;

            foreach (var (state, sessionEntity) in
                     SystemAPI.Query<RefRW<TradeSessionState>>()
                         .WithAll<TradeSessionTag>()
                         .WithEntityAccess())
            {
                if (state.ValueRO.State != TradeState.Executing) continue;

                var initiator = state.ValueRO.InitiatorEntity;
                var target = state.ValueRO.TargetEntity;
                byte failReason = 0;

                // === VALIDATION PASS ===

                // 1. Entities still exist
                if (!EntityManager.Exists(initiator) || !EntityManager.Exists(target))
                { failReason = 1; goto Fail; }

                // 2. Proximity re-check
                if (EntityManager.HasComponent<LocalTransform>(initiator) &&
                    EntityManager.HasComponent<LocalTransform>(target))
                {
                    float distSq = math.distancesq(
                        EntityManager.GetComponentData<LocalTransform>(initiator).Position,
                        EntityManager.GetComponentData<LocalTransform>(target).Position);
                    if (distSq > config.ProximityRange * config.ProximityRange)
                    { failReason = (byte)TradeCancelReason.TooFar; goto Fail; }
                }

                // 3. Combat re-check
                if (EntityManager.HasComponent<CombatState>(initiator) &&
                    EntityManager.GetComponentData<CombatState>(initiator).IsInCombat)
                { failReason = (byte)TradeCancelReason.EnteredCombat; goto Fail; }
                if (EntityManager.HasComponent<CombatState>(target) &&
                    EntityManager.GetComponentData<CombatState>(target).IsInCombat)
                { failReason = (byte)TradeCancelReason.EnteredCombat; goto Fail; }

                // 4. Get buffers
                if (!EntityManager.HasBuffer<InventoryItem>(initiator) || !EntityManager.HasBuffer<InventoryItem>(target) ||
                    !EntityManager.HasBuffer<CurrencyTransaction>(initiator) || !EntityManager.HasBuffer<CurrencyTransaction>(target) ||
                    !EntityManager.HasComponent<CurrencyInventory>(initiator) || !EntityManager.HasComponent<CurrencyInventory>(target))
                { failReason = (byte)TradeCancelReason.InvalidSession; goto Fail; }

                var offers = EntityManager.GetBuffer<TradeOffer>(sessionEntity, true);
                var initInv = EntityManager.GetBuffer<InventoryItem>(initiator);
                var targetInv = EntityManager.GetBuffer<InventoryItem>(target);
                var initCurrency = EntityManager.GetComponentData<CurrencyInventory>(initiator);
                var targetCurrency = EntityManager.GetComponentData<CurrencyInventory>(target);

                // Calculate weight changes
                float initWeightDelta = 0f;
                float targetWeightDelta = 0f;

                // 5. Validate all item offers
                for (int o = 0; o < offers.Length; o++)
                {
                    var offer = offers[o];
                    if (offer.OfferType != TradeOfferType.Item) continue;

                    var senderInv = offer.OfferSide == 0 ? initInv : targetInv;
                    if (offer.ItemSlotIndex >= senderInv.Length)
                    { failReason = (byte)TradeCancelReason.InvalidSession; goto Fail; }
                    if (senderInv[offer.ItemSlotIndex].ResourceType != offer.ItemType)
                    { failReason = (byte)TradeCancelReason.InvalidSession; goto Fail; }
                    if (senderInv[offer.ItemSlotIndex].Quantity < offer.Quantity)
                    { failReason = (byte)TradeCancelReason.InvalidSession; goto Fail; }

                    float itemWeight = weights.GetWeight(offer.ItemType) * offer.Quantity;
                    if (offer.OfferSide == 0) { initWeightDelta -= itemWeight; targetWeightDelta += itemWeight; }
                    else { targetWeightDelta -= itemWeight; initWeightDelta += itemWeight; }
                }

                // 6. Validate all currency offers
                int initGoldOffer = 0, initPremiumOffer = 0, initCraftingOffer = 0;
                int targetGoldOffer = 0, targetPremiumOffer = 0, targetCraftingOffer = 0;
                for (int o = 0; o < offers.Length; o++)
                {
                    var offer = offers[o];
                    if (offer.OfferType != TradeOfferType.Currency) continue;
                    if (offer.CurrencyType == CurrencyType.Premium && !config.AllowPremiumCurrencyTrade)
                    { failReason = (byte)TradeCancelReason.InvalidSession; goto Fail; }

                    if (offer.OfferSide == 0)
                    {
                        switch (offer.CurrencyType)
                        {
                            case CurrencyType.Gold: initGoldOffer += offer.CurrencyAmount; break;
                            case CurrencyType.Premium: initPremiumOffer += offer.CurrencyAmount; break;
                            case CurrencyType.Crafting: initCraftingOffer += offer.CurrencyAmount; break;
                        }
                    }
                    else
                    {
                        switch (offer.CurrencyType)
                        {
                            case CurrencyType.Gold: targetGoldOffer += offer.CurrencyAmount; break;
                            case CurrencyType.Premium: targetPremiumOffer += offer.CurrencyAmount; break;
                            case CurrencyType.Crafting: targetCraftingOffer += offer.CurrencyAmount; break;
                        }
                    }
                }

                if (initGoldOffer > initCurrency.Gold || initPremiumOffer > initCurrency.Premium ||
                    initCraftingOffer > initCurrency.Crafting)
                { failReason = (byte)TradeCancelReason.InvalidSession; goto Fail; }
                if (targetGoldOffer > targetCurrency.Gold || targetPremiumOffer > targetCurrency.Premium ||
                    targetCraftingOffer > targetCurrency.Crafting)
                { failReason = (byte)TradeCancelReason.InvalidSession; goto Fail; }

                // 7. Weight capacity check
                if (EntityManager.HasComponent<InventoryCapacity>(initiator))
                {
                    var cap = EntityManager.GetComponentData<InventoryCapacity>(initiator);
                    if (cap.CurrentWeight + initWeightDelta > cap.MaxWeight)
                    { failReason = (byte)TradeCancelReason.InvalidSession; goto Fail; }
                }
                if (EntityManager.HasComponent<InventoryCapacity>(target))
                {
                    var cap = EntityManager.GetComponentData<InventoryCapacity>(target);
                    if (cap.CurrentWeight + targetWeightDelta > cap.MaxWeight)
                    { failReason = (byte)TradeCancelReason.InvalidSession; goto Fail; }
                }

                // === EXECUTION PASS (all validation passed) ===
                {
                    // Items: process in reverse to handle slot index shifts from removals
                    // First pass: remove items from senders
                    // Second pass: add items to receivers
                    // We process removals first to avoid capacity issues

                    // Collect item removals and additions
                    var removals = new NativeList<TradeOffer>(16, Allocator.Temp);
                    for (int o = 0; o < offers.Length; o++)
                    {
                        if (offers[o].OfferType == TradeOfferType.Item)
                            removals.Add(offers[o]);
                    }

                    // Sort removals by slot index descending to avoid index shifts
                    for (int a = 0; a < removals.Length - 1; a++)
                    {
                        for (int b = a + 1; b < removals.Length; b++)
                        {
                            if (removals[a].ItemSlotIndex < removals[b].ItemSlotIndex)
                            {
                                var tmp = removals[a];
                                removals[a] = removals[b];
                                removals[b] = tmp;
                            }
                        }
                    }

                    // Remove items from senders
                    for (int r = 0; r < removals.Length; r++)
                    {
                        var offer = removals[r];
                        var senderBuf = offer.OfferSide == 0 ? initInv : targetInv;
                        var item = senderBuf[offer.ItemSlotIndex];
                        item.Quantity -= offer.Quantity;
                        if (item.Quantity <= 0)
                            senderBuf.RemoveAt(offer.ItemSlotIndex);
                        else
                            senderBuf[offer.ItemSlotIndex] = item;
                    }

                    // Add items to receivers
                    for (int r = 0; r < removals.Length; r++)
                    {
                        var offer = removals[r];
                        var receiverBuf = offer.OfferSide == 0 ? targetInv : initInv;
                        AddItemToBuffer(receiverBuf, offer.ItemType, offer.Quantity);
                    }

                    removals.Dispose();

                    // Currency: write CurrencyTransaction entries
                    var initTx = EntityManager.GetBuffer<CurrencyTransaction>(initiator);
                    var targetTx = EntityManager.GetBuffer<CurrencyTransaction>(target);

                    // Initiator's currency offers → debit initiator, credit target
                    if (initGoldOffer > 0) { WriteTx(initTx, CurrencyType.Gold, -initGoldOffer, sessionEntity); WriteTx(targetTx, CurrencyType.Gold, initGoldOffer, sessionEntity); }
                    if (initPremiumOffer > 0) { WriteTx(initTx, CurrencyType.Premium, -initPremiumOffer, sessionEntity); WriteTx(targetTx, CurrencyType.Premium, initPremiumOffer, sessionEntity); }
                    if (initCraftingOffer > 0) { WriteTx(initTx, CurrencyType.Crafting, -initCraftingOffer, sessionEntity); WriteTx(targetTx, CurrencyType.Crafting, initCraftingOffer, sessionEntity); }

                    // Target's currency offers → debit target, credit initiator
                    if (targetGoldOffer > 0) { WriteTx(targetTx, CurrencyType.Gold, -targetGoldOffer, sessionEntity); WriteTx(initTx, CurrencyType.Gold, targetGoldOffer, sessionEntity); }
                    if (targetPremiumOffer > 0) { WriteTx(targetTx, CurrencyType.Premium, -targetPremiumOffer, sessionEntity); WriteTx(initTx, CurrencyType.Premium, targetPremiumOffer, sessionEntity); }
                    if (targetCraftingOffer > 0) { WriteTx(targetTx, CurrencyType.Crafting, -targetCraftingOffer, sessionEntity); WriteTx(initTx, CurrencyType.Crafting, targetCraftingOffer, sessionEntity); }

                    // Update weight
                    if (EntityManager.HasComponent<InventoryCapacity>(initiator) && initWeightDelta != 0f)
                    {
                        var cap = EntityManager.GetComponentData<InventoryCapacity>(initiator);
                        cap.CurrentWeight = math.max(0f, cap.CurrentWeight + initWeightDelta);
                        cap.IsOverencumbered = cap.CurrentWeight > cap.MaxWeight;
                        EntityManager.SetComponentData(initiator, cap);
                    }
                    if (EntityManager.HasComponent<InventoryCapacity>(target) && targetWeightDelta != 0f)
                    {
                        var cap = EntityManager.GetComponentData<InventoryCapacity>(target);
                        cap.CurrentWeight = math.max(0f, cap.CurrentWeight + targetWeightDelta);
                        cap.IsOverencumbered = cap.CurrentWeight > cap.MaxWeight;
                        EntityManager.SetComponentData(target, cap);
                    }

                    state.ValueRW.State = TradeState.Completed;

                    // Notify both
                    NotifyState(ecb, state.ValueRO.InitiatorConnection, TradeState.Completed, 0);
                    NotifyState(ecb, state.ValueRO.TargetConnection, TradeState.Completed, 0);

                    TradeVisualQueue.Enqueue(new TradeVisualQueue.TradeVisualEvent
                    {
                        Type = TradeVisualEventType.TradeCompleted,
                        Payload = 1
                    });
                    continue;
                }

                Fail:
                state.ValueRW.State = TradeState.Failed;
                NotifyState(ecb, state.ValueRO.InitiatorConnection, TradeState.Failed, failReason);
                NotifyState(ecb, state.ValueRO.TargetConnection, TradeState.Failed, failReason);

                TradeVisualQueue.Enqueue(new TradeVisualQueue.TradeVisualEvent
                {
                    Type = TradeVisualEventType.TradeFailed,
                    Payload = failReason
                });
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private static void AddItemToBuffer(DynamicBuffer<InventoryItem> buffer, ResourceType type, int quantity)
        {
            // Try to stack with existing entry
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].ResourceType == type)
                {
                    var item = buffer[i];
                    item.Quantity += quantity;
                    buffer[i] = item;
                    return;
                }
            }
            // Add new entry
            buffer.Add(new InventoryItem { ResourceType = type, Quantity = quantity });
        }

        private static void WriteTx(DynamicBuffer<CurrencyTransaction> buffer, CurrencyType type, int amount, Entity source)
        {
            buffer.Add(new CurrencyTransaction { Type = type, Amount = amount, Source = source });
        }

        private void NotifyState(EntityCommandBuffer ecb, Entity connection, TradeState newState, byte reason)
        {
            if (!EntityManager.Exists(connection)) return;
            var notify = ecb.CreateEntity();
            ecb.AddComponent(notify, new TradeStateNotifyRpc { NewState = newState, FailReason = reason });
            ecb.AddComponent(notify, new SendRpcCommandRequest { TargetConnection = connection });
        }
    }
}
