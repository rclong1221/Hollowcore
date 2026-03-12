using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.NetCode;
using DIG.Combat.UI;
using DIG.Party;

namespace DIG.Combat.Systems
{
    /// <summary>
    /// EPIC 18.17 Phase 2: Server-side damage visibility filter.
    /// Rebuilds per-frame lookup tables mapping NetworkId → connection entity
    /// and connection → player world position. Provides CreateFilteredRpcs()
    /// for DamageApplicationSystem and DamageEventVisualBridgeSystem to send
    /// targeted RPCs based on the global DamageVisibilityConfig policy.
    ///
    /// Runs on ServerSimulation only — LocalSimulation doesn't need RPCs
    /// (shared static DamageVisualQueue handles single player).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(DamageApplicationSystem))]
    public partial class DamageVisibilityServerFilter : SystemBase
    {
        // Per-frame lookup tables
        private NativeHashMap<int, Entity> _networkIdToConnection;
        private NativeHashMap<int, float3> _networkIdToPosition;

        // Cached config values (loaded once, re-read on update)
        private DamageNumberVisibility _serverVisibility;
        private float _nearbyDistanceSq;

        // Component lookups
        private ComponentLookup<CommandTarget> _commandTargetLookup;
        private ComponentLookup<LocalToWorld> _localToWorldLookup;
        private ComponentLookup<PartyLink> _partyLinkLookup;
        private BufferLookup<PartyMemberElement> _partyMemberLookup;
        private ComponentLookup<GhostOwner> _ghostOwnerLookup;

        protected override void OnCreate()
        {
            _networkIdToConnection = new NativeHashMap<int, Entity>(16, Allocator.Persistent);
            _networkIdToPosition = new NativeHashMap<int, float3>(16, Allocator.Persistent);

            _commandTargetLookup = GetComponentLookup<CommandTarget>(true);
            _localToWorldLookup = GetComponentLookup<LocalToWorld>(true);
            _partyLinkLookup = GetComponentLookup<PartyLink>(true);
            _partyMemberLookup = GetBufferLookup<PartyMemberElement>(true);
            _ghostOwnerLookup = GetComponentLookup<GhostOwner>(true);
        }

        protected override void OnDestroy()
        {
            if (_networkIdToConnection.IsCreated) _networkIdToConnection.Dispose();
            if (_networkIdToPosition.IsCreated) _networkIdToPosition.Dispose();
        }

        protected override void OnUpdate()
        {
            // Reload config each frame (cheap — cached Resources.Load)
            var config = DamageVisibilityConfig.Instance;
            _serverVisibility = config != null ? config.DefaultVisibility : DamageNumberVisibility.All;
            float nearbyDist = config != null ? config.NearbyDistance : 50f;
            _nearbyDistanceSq = nearbyDist * nearbyDist;

            // Early out if broadcast mode — no need to rebuild maps
            if (_serverVisibility == DamageNumberVisibility.All)
                return;

            // Update lookups
            _commandTargetLookup.Update(this);
            _localToWorldLookup.Update(this);
            _partyLinkLookup.Update(this);
            _partyMemberLookup.Update(this);
            _ghostOwnerLookup.Update(this);

            // Rebuild connection maps
            _networkIdToConnection.Clear();
            _networkIdToPosition.Clear();

            foreach (var (netId, entity) in
                SystemAPI.Query<RefRO<NetworkId>>()
                    .WithAll<NetworkStreamInGame>()
                    .WithEntityAccess())
            {
                int id = netId.ValueRO.Value;
                _networkIdToConnection[id] = entity;

                // Resolve player entity position via CommandTarget
                if (_commandTargetLookup.HasComponent(entity))
                {
                    var playerEntity = _commandTargetLookup[entity].targetEntity;
                    if (playerEntity != Entity.Null && _localToWorldLookup.HasComponent(playerEntity))
                    {
                        _networkIdToPosition[id] = _localToWorldLookup[playerEntity].Position;
                    }
                }
            }
        }

        /// <summary>
        /// Create filtered RPC entities based on server visibility policy.
        /// Called by DamageApplicationSystem and DamageEventVisualBridgeSystem
        /// instead of creating broadcast RPCs directly.
        /// </summary>
        public void CreateFilteredRpcs(
            EntityCommandBuffer ecb,
            DamageVisualRpc rpc,
            int sourceNetworkId,
            float3 hitPosition)
        {
            // Environment damage (SourceNetworkId == -1) always broadcasts to all
            if (sourceNetworkId == -1 || _serverVisibility == DamageNumberVisibility.All)
            {
                var e = ecb.CreateEntity();
                ecb.AddComponent(e, rpc);
                ecb.AddComponent(e, new SendRpcCommandRequest());
                return;
            }

            switch (_serverVisibility)
            {
                case DamageNumberVisibility.SelfOnly:
                    SendToNetworkId(ecb, rpc, sourceNetworkId);
                    break;

                case DamageNumberVisibility.Nearby:
                    SendToNearby(ecb, rpc, hitPosition);
                    break;

                case DamageNumberVisibility.Party:
                    SendToPartyMembers(ecb, rpc, sourceNetworkId);
                    break;

                case DamageNumberVisibility.None:
                    // No RPCs — listen server host still sees via shared DamageVisualQueue
                    break;
            }
        }

        private void SendToNetworkId(
            EntityCommandBuffer ecb,
            DamageVisualRpc rpc,
            int networkId)
        {
            if (!_networkIdToConnection.TryGetValue(networkId, out var connectionEntity))
                return;

            var e = ecb.CreateEntity();
            ecb.AddComponent(e, rpc);
            ecb.AddComponent(e, new SendRpcCommandRequest { TargetConnection = connectionEntity });
        }

        private void SendToNearby(
            EntityCommandBuffer ecb,
            DamageVisualRpc rpc,
            float3 hitPosition)
        {
            var enumerator = _networkIdToPosition.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var kvp = enumerator.Current;
                if (math.distancesq(hitPosition, kvp.Value) <= _nearbyDistanceSq)
                {
                    if (_networkIdToConnection.TryGetValue(kvp.Key, out var connectionEntity))
                    {
                        var e = ecb.CreateEntity();
                        ecb.AddComponent(e, rpc);
                        ecb.AddComponent(e, new SendRpcCommandRequest { TargetConnection = connectionEntity });
                    }
                }
            }
            enumerator.Dispose();
        }

        private void SendToPartyMembers(
            EntityCommandBuffer ecb,
            DamageVisualRpc rpc,
            int sourceNetworkId)
        {
            // Always send to the attacker themselves
            SendToNetworkId(ecb, rpc, sourceNetworkId);

            // Find the attacker's party
            if (!_networkIdToConnection.TryGetValue(sourceNetworkId, out var attackerConnection))
                return;

            if (!_commandTargetLookup.HasComponent(attackerConnection))
                return;

            var attackerPlayer = _commandTargetLookup[attackerConnection].targetEntity;
            if (attackerPlayer == Entity.Null || !_partyLinkLookup.HasComponent(attackerPlayer))
                return;

            var partyEntity = _partyLinkLookup[attackerPlayer].PartyEntity;
            if (partyEntity == Entity.Null || !_partyMemberLookup.HasBuffer(partyEntity))
                return;

            // Send to each party member (skip attacker — already sent above)
            var members = _partyMemberLookup[partyEntity];
            for (int i = 0; i < members.Length; i++)
            {
                var memberConnection = members[i].ConnectionEntity;
                if (memberConnection == Entity.Null || memberConnection == attackerConnection)
                    continue;

                var e = ecb.CreateEntity();
                ecb.AddComponent(e, rpc);
                ecb.AddComponent(e, new SendRpcCommandRequest { TargetConnection = memberConnection });
            }
        }
    }
}
