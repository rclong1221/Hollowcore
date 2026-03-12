using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using DIG.Shared;
using DIG.Economy;

namespace DIG.Trading
{
    /// <summary>
    /// EPIC 17.3: Receives TradeOfferUpdateRpc, validates item/currency ownership,
    /// updates TradeOffer buffer, resets other player's confirmation, syncs to both clients.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TradeAcceptReceiveSystem))]
    public partial class TradeOfferReceiveSystem : SystemBase
    {
        private EntityQuery _rpcQuery;

        protected override void OnCreate()
        {
            _rpcQuery = GetEntityQuery(
                ComponentType.ReadOnly<TradeOfferUpdateRpc>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
            RequireForUpdate<TradeConfig>();
        }

        protected override void OnUpdate()
        {
            if (_rpcQuery.IsEmpty) return;

            var config = SystemAPI.GetSingleton<TradeConfig>();
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var netTime = SystemAPI.GetSingleton<NetworkTime>();
            uint currentTick = netTime.ServerTick.IsValid ? netTime.ServerTick.TickIndexForValidTick : 1;

            var entities = _rpcQuery.ToEntityArray(Allocator.Temp);
            var rpcs = _rpcQuery.ToComponentDataArray<TradeOfferUpdateRpc>(Allocator.Temp);
            var receives = _rpcQuery.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                ecb.DestroyEntity(entities[i]);

                var connection = receives[i].SourceConnection;
                var player = ResolvePlayer(connection);
                if (player == Entity.Null) continue;

                var rpc = rpcs[i];

                // Find active session for this player
                foreach (var (state, confirmState, sessionEntity) in
                         SystemAPI.Query<RefRW<TradeSessionState>, RefRW<TradeConfirmState>>()
                             .WithAll<TradeSessionTag>()
                             .WithEntityAccess())
                {
                    if (state.ValueRO.State != TradeState.Active) continue;

                    byte offerSide;
                    if (state.ValueRO.InitiatorEntity == player) offerSide = 0;
                    else if (state.ValueRO.TargetEntity == player) offerSide = 1;
                    else continue;

                    var offers = EntityManager.GetBuffer<TradeOffer>(sessionEntity);
                    bool success = false;

                    switch (rpc.Action)
                    {
                        case TradeOfferAction.Add:
                            success = HandleAddOffer(config, offers, offerSide, rpc, player);
                            break;
                        case TradeOfferAction.Remove:
                            success = HandleRemoveOffer(offers, offerSide, rpc);
                            break;
                        case TradeOfferAction.UpdateQty:
                            success = HandleUpdateQty(offers, offerSide, rpc, player);
                            break;
                    }

                    if (success)
                    {
                        state.ValueRW.LastModifiedTick = currentTick;

                        // Reset OTHER player's confirmation
                        if (offerSide == 0)
                            confirmState.ValueRW.TargetConfirmed = false;
                        else
                            confirmState.ValueRW.InitiatorConfirmed = false;

                        // Resolve actual ItemType from the offer buffer for sync
                        var itemType = ResolveItemType(offers, offerSide, rpc);

                        // Sync to both clients
                        SendOfferSync(ecb, state.ValueRO.InitiatorConnection, offerSide, rpc, itemType);
                        SendOfferSync(ecb, state.ValueRO.TargetConnection, offerSide, rpc, itemType);
                    }
                    break;
                }
            }

            entities.Dispose();
            rpcs.Dispose();
            receives.Dispose();
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private bool HandleAddOffer(TradeConfig config, DynamicBuffer<TradeOffer> offers, byte offerSide,
            TradeOfferUpdateRpc rpc, Entity player)
        {
            if (rpc.OfferType == TradeOfferType.Item)
            {
                // Count existing item offers for this side
                int itemCount = 0;
                for (int o = 0; o < offers.Length; o++)
                    if (offers[o].OfferSide == offerSide && offers[o].OfferType == TradeOfferType.Item) itemCount++;
                if (itemCount >= config.MaxItemsPerOffer) return false;

                // Check for duplicate slot
                for (int o = 0; o < offers.Length; o++)
                    if (offers[o].OfferSide == offerSide && offers[o].OfferType == TradeOfferType.Item &&
                        offers[o].ItemSlotIndex == rpc.ItemSlotIndex) return false;

                // Validate item exists in player's inventory
                if (!EntityManager.HasBuffer<InventoryItem>(player)) return false;
                var inventory = EntityManager.GetBuffer<InventoryItem>(player, true);
                if (rpc.ItemSlotIndex >= inventory.Length) return false;
                if (rpc.Quantity <= 0 || rpc.Quantity > inventory[rpc.ItemSlotIndex].Quantity) return false;

                offers.Add(new TradeOffer
                {
                    OfferSide = offerSide,
                    OfferType = TradeOfferType.Item,
                    ItemSlotIndex = rpc.ItemSlotIndex,
                    ItemType = inventory[rpc.ItemSlotIndex].ResourceType,
                    Quantity = rpc.Quantity,
                    CurrencyType = default,
                    CurrencyAmount = 0
                });
                return true;
            }
            else // Currency
            {
                // Count existing currency offers for this side
                int currCount = 0;
                for (int o = 0; o < offers.Length; o++)
                    if (offers[o].OfferSide == offerSide && offers[o].OfferType == TradeOfferType.Currency) currCount++;
                if (currCount >= config.MaxCurrencyPerOffer) return false;

                // Check Premium currency restriction
                if (rpc.CurrencyType == CurrencyType.Premium && !config.AllowPremiumCurrencyTrade) return false;

                // Check for duplicate currency type
                for (int o = 0; o < offers.Length; o++)
                    if (offers[o].OfferSide == offerSide && offers[o].OfferType == TradeOfferType.Currency &&
                        offers[o].CurrencyType == rpc.CurrencyType) return false;

                // Validate balance
                if (!EntityManager.HasComponent<CurrencyInventory>(player)) return false;
                var currency = EntityManager.GetComponentData<CurrencyInventory>(player);
                int balance = rpc.CurrencyType switch
                {
                    CurrencyType.Gold => currency.Gold,
                    CurrencyType.Premium => currency.Premium,
                    CurrencyType.Crafting => currency.Crafting,
                    _ => 0
                };
                if (rpc.CurrencyAmount <= 0 || rpc.CurrencyAmount > balance) return false;

                offers.Add(new TradeOffer
                {
                    OfferSide = offerSide,
                    OfferType = TradeOfferType.Currency,
                    ItemSlotIndex = 0,
                    ItemType = default,
                    Quantity = 0,
                    CurrencyType = rpc.CurrencyType,
                    CurrencyAmount = rpc.CurrencyAmount
                });
                return true;
            }
        }

        private bool HandleRemoveOffer(DynamicBuffer<TradeOffer> offers, byte offerSide, TradeOfferUpdateRpc rpc)
        {
            for (int o = offers.Length - 1; o >= 0; o--)
            {
                var offer = offers[o];
                if (offer.OfferSide != offerSide) continue;

                if (rpc.OfferType == TradeOfferType.Item && offer.OfferType == TradeOfferType.Item &&
                    offer.ItemSlotIndex == rpc.ItemSlotIndex)
                {
                    offers.RemoveAt(o);
                    return true;
                }
                if (rpc.OfferType == TradeOfferType.Currency && offer.OfferType == TradeOfferType.Currency &&
                    offer.CurrencyType == rpc.CurrencyType)
                {
                    offers.RemoveAt(o);
                    return true;
                }
            }
            return false;
        }

        private bool HandleUpdateQty(DynamicBuffer<TradeOffer> offers, byte offerSide, TradeOfferUpdateRpc rpc,
            Entity player)
        {
            for (int o = 0; o < offers.Length; o++)
            {
                var offer = offers[o];
                if (offer.OfferSide != offerSide) continue;

                if (rpc.OfferType == TradeOfferType.Item && offer.OfferType == TradeOfferType.Item &&
                    offer.ItemSlotIndex == rpc.ItemSlotIndex)
                {
                    // Validate new quantity
                    if (!EntityManager.HasBuffer<InventoryItem>(player)) return false;
                    var inventory = EntityManager.GetBuffer<InventoryItem>(player, true);
                    if (rpc.ItemSlotIndex >= inventory.Length) return false;
                    if (rpc.Quantity <= 0 || rpc.Quantity > inventory[rpc.ItemSlotIndex].Quantity) return false;

                    offer.Quantity = rpc.Quantity;
                    offers[o] = offer;
                    return true;
                }
                if (rpc.OfferType == TradeOfferType.Currency && offer.OfferType == TradeOfferType.Currency &&
                    offer.CurrencyType == rpc.CurrencyType)
                {
                    // Validate new amount
                    if (!EntityManager.HasComponent<CurrencyInventory>(player)) return false;
                    var currency = EntityManager.GetComponentData<CurrencyInventory>(player);
                    int balance = rpc.CurrencyType switch
                    {
                        CurrencyType.Gold => currency.Gold,
                        CurrencyType.Premium => currency.Premium,
                        CurrencyType.Crafting => currency.Crafting,
                        _ => 0
                    };
                    if (rpc.CurrencyAmount <= 0 || rpc.CurrencyAmount > balance) return false;

                    offer.CurrencyAmount = rpc.CurrencyAmount;
                    offers[o] = offer;
                    return true;
                }
            }
            return false;
        }

        private static ResourceType ResolveItemType(DynamicBuffer<TradeOffer> offers, byte offerSide,
            TradeOfferUpdateRpc rpc)
        {
            if (rpc.OfferType != TradeOfferType.Item) return default;
            for (int o = 0; o < offers.Length; o++)
            {
                if (offers[o].OfferSide == offerSide && offers[o].OfferType == TradeOfferType.Item &&
                    offers[o].ItemSlotIndex == rpc.ItemSlotIndex)
                    return offers[o].ItemType;
            }
            return default;
        }

        private void SendOfferSync(EntityCommandBuffer ecb, Entity connection, byte offerSide,
            TradeOfferUpdateRpc rpc, ResourceType itemType)
        {
            if (!EntityManager.Exists(connection)) return;
            var notify = ecb.CreateEntity();
            ecb.AddComponent(notify, new TradeOfferSyncRpc
            {
                OfferSide = offerSide,
                Action = rpc.Action,
                OfferType = rpc.OfferType,
                ItemSlotIndex = rpc.ItemSlotIndex,
                ItemType = itemType,
                Quantity = rpc.Quantity,
                CurrencyType = rpc.CurrencyType,
                CurrencyAmount = rpc.CurrencyAmount
            });
            ecb.AddComponent(notify, new SendRpcCommandRequest { TargetConnection = connection });
        }

        private Entity ResolvePlayer(Entity sourceConnection)
        {
            if (sourceConnection == Entity.Null) return Entity.Null;
            if (!SystemAPI.HasComponent<CommandTarget>(sourceConnection)) return Entity.Null;
            return SystemAPI.GetComponent<CommandTarget>(sourceConnection).targetEntity;
        }
    }
}
