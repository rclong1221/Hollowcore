using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace DIG.Trading
{
    /// <summary>
    /// EPIC 17.3: Managed SystemBase that reads trade notification RPCs on client,
    /// maintains a client-side state mirror, and pushes updates to ITradeUIProvider.
    /// Follows PartyUIBridgeSystem pattern.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class TradeUIBridgeSystem : SystemBase
    {
        private EntityQuery _sessionNotifyQuery;
        private EntityQuery _offerSyncQuery;
        private EntityQuery _stateNotifyQuery;

        private int _noProviderFrameCount;

        // Client-side state mirror
        private bool _inTrade;
        private TradeOfferSnapshot[] _myOffers;
        private TradeOfferSnapshot[] _theirOffers;
        private int _myOfferCount;
        private int _theirOfferCount;
        private bool _iConfirmed;
        private bool _theyConfirmed;

        protected override void OnCreate()
        {
            _sessionNotifyQuery = GetEntityQuery(
                ComponentType.ReadOnly<TradeSessionNotifyRpc>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
            _offerSyncQuery = GetEntityQuery(
                ComponentType.ReadOnly<TradeOfferSyncRpc>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
            _stateNotifyQuery = GetEntityQuery(
                ComponentType.ReadOnly<TradeStateNotifyRpc>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

            _myOffers = new TradeOfferSnapshot[16];
            _theirOffers = new TradeOfferSnapshot[16];
        }

        protected override void OnUpdate()
        {
            if (!TradeUIRegistry.HasTradeUI)
            {
                _noProviderFrameCount++;
                if (_noProviderFrameCount == 120)
                    UnityEngine.Debug.LogWarning("[TradeUIBridge] No ITradeUIProvider registered after 120 frames.");
                DrainAllRpcs();
                DrainVisualQueue();
                return;
            }

            var provider = TradeUIRegistry.TradeUI;

            // Process trade session notifications (incoming request)
            ProcessSessionNotify(provider);

            // Process state changes (Active, Executing, Completed, Failed, Cancelled)
            ProcessStateNotify(provider);

            // Process offer syncs
            ProcessOfferSync(provider);

            // Drain visual queue
            while (TradeVisualQueue.TryDequeue(out _)) { }
        }

        private void ProcessSessionNotify(ITradeUIProvider provider)
        {
            if (_sessionNotifyQuery.IsEmpty) return;

            var entities = _sessionNotifyQuery.ToEntityArray(Allocator.Temp);
            var rpcs = _sessionNotifyQuery.ToComponentDataArray<TradeSessionNotifyRpc>(Allocator.Temp);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                provider.OnTradeRequested(rpcs[i].InitiatorGhostId);
                ecb.DestroyEntity(entities[i]);
            }

            entities.Dispose();
            rpcs.Dispose();
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private void ProcessStateNotify(ITradeUIProvider provider)
        {
            if (_stateNotifyQuery.IsEmpty) return;

            var entities = _stateNotifyQuery.ToEntityArray(Allocator.Temp);
            var rpcs = _stateNotifyQuery.ToComponentDataArray<TradeStateNotifyRpc>(Allocator.Temp);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var rpc = rpcs[i];
                ecb.DestroyEntity(entities[i]);

                switch (rpc.NewState)
                {
                    case TradeState.Active:
                        _inTrade = true;
                        _myOfferCount = 0;
                        _theirOfferCount = 0;
                        _iConfirmed = false;
                        _theyConfirmed = false;
                        provider.OnTradeSessionStarted();
                        break;

                    case TradeState.Executing:
                        // Both confirmed — UI can show "Trading..." state
                        break;

                    case TradeState.Completed:
                        _inTrade = false;
                        provider.OnTradeCompleted(true);
                        break;

                    case TradeState.Failed:
                        _inTrade = false;
                        provider.OnTradeCompleted(false);
                        break;

                    case TradeState.Cancelled:
                        _inTrade = false;
                        provider.OnTradeSessionCancelled((TradeCancelReason)rpc.FailReason);
                        break;
                }
            }

            entities.Dispose();
            rpcs.Dispose();
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private void ProcessOfferSync(ITradeUIProvider provider)
        {
            if (_offerSyncQuery.IsEmpty) return;

            var entities = _offerSyncQuery.ToEntityArray(Allocator.Temp);
            var rpcs = _offerSyncQuery.ToComponentDataArray<TradeOfferSyncRpc>(Allocator.Temp);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            bool changed = false;
            for (int i = 0; i < entities.Length; i++)
            {
                var rpc = rpcs[i];
                ecb.DestroyEntity(entities[i]);
                changed = true;

                // Determine which side this is for the local player
                // Side 0 = initiator, Side 1 = target
                // The UI bridge doesn't know which side we are, so we store both
                // The MonoBehaviour will determine which is "my" side
                var snapshot = new TradeOfferSnapshot
                {
                    OfferType = rpc.OfferType,
                    ItemType = rpc.ItemType,
                    Quantity = rpc.Quantity,
                    CurrencyType = rpc.CurrencyType,
                    CurrencyAmount = rpc.CurrencyAmount
                };

                ref var offers = ref (rpc.OfferSide == 0 ? ref _myOffers : ref _theirOffers);
                ref int count = ref (rpc.OfferSide == 0 ? ref _myOfferCount : ref _theirOfferCount);

                switch (rpc.Action)
                {
                    case TradeOfferAction.Add:
                        if (count < offers.Length)
                            offers[count++] = snapshot;
                        break;
                    case TradeOfferAction.Remove:
                        RemoveOffer(offers, ref count, rpc);
                        break;
                    case TradeOfferAction.UpdateQty:
                        UpdateOffer(offers, count, rpc);
                        break;
                }
            }

            if (changed)
            {
                provider.OnOfferUpdated(_myOffers, _myOfferCount, _theirOffers, _theirOfferCount);
            }

            entities.Dispose();
            rpcs.Dispose();
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private static void RemoveOffer(TradeOfferSnapshot[] offers, ref int count, TradeOfferSyncRpc rpc)
        {
            for (int o = 0; o < count; o++)
            {
                if (rpc.OfferType == TradeOfferType.Item && offers[o].OfferType == TradeOfferType.Item &&
                    offers[o].ItemType == rpc.ItemType)
                {
                    offers[o] = offers[count - 1];
                    count--;
                    return;
                }
                if (rpc.OfferType == TradeOfferType.Currency && offers[o].OfferType == TradeOfferType.Currency &&
                    offers[o].CurrencyType == rpc.CurrencyType)
                {
                    offers[o] = offers[count - 1];
                    count--;
                    return;
                }
            }
        }

        private static void UpdateOffer(TradeOfferSnapshot[] offers, int count, TradeOfferSyncRpc rpc)
        {
            for (int o = 0; o < count; o++)
            {
                if (rpc.OfferType == TradeOfferType.Item && offers[o].OfferType == TradeOfferType.Item &&
                    offers[o].ItemType == rpc.ItemType)
                {
                    offers[o].Quantity = rpc.Quantity;
                    return;
                }
                if (rpc.OfferType == TradeOfferType.Currency && offers[o].OfferType == TradeOfferType.Currency &&
                    offers[o].CurrencyType == rpc.CurrencyType)
                {
                    offers[o].CurrencyAmount = rpc.CurrencyAmount;
                    return;
                }
            }
        }

        private void DrainAllRpcs()
        {
            if (_sessionNotifyQuery.IsEmpty && _offerSyncQuery.IsEmpty && _stateNotifyQuery.IsEmpty) return;

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (_, e) in SystemAPI.Query<RefRO<TradeSessionNotifyRpc>>().WithAll<ReceiveRpcCommandRequest>().WithEntityAccess())
                ecb.DestroyEntity(e);
            foreach (var (_, e) in SystemAPI.Query<RefRO<TradeOfferSyncRpc>>().WithAll<ReceiveRpcCommandRequest>().WithEntityAccess())
                ecb.DestroyEntity(e);
            foreach (var (_, e) in SystemAPI.Query<RefRO<TradeStateNotifyRpc>>().WithAll<ReceiveRpcCommandRequest>().WithEntityAccess())
                ecb.DestroyEntity(e);
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private void DrainVisualQueue()
        {
            while (TradeVisualQueue.TryDequeue(out _)) { }
        }
    }
}
