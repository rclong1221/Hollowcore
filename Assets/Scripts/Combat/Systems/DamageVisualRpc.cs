using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Combat.Systems
{
    /// <summary>
    /// Server → all clients RPC carrying per-damage-event visual data
    /// with full-fidelity damage visuals: correct element colors, DOT flags, crit/headshot indicators.
    ///
    /// Sent by DamageEventVisualBridgeSystem (DamageEvent pipeline) and
    /// DamageApplicationSystem (CRE pipeline, DamagePreApplied=false).
    /// Received by DamageVisualRpcReceiveSystem on ClientSimulation.
    /// </summary>
    public struct DamageVisualRpc : IRpcCommand
    {
        public float Damage;
        public float3 HitPosition;
        public byte HitType;     // DIG.Targeting.Theming.HitType
        public byte DamageType;  // DIG.Targeting.Theming.DamageType
        public byte Flags;       // DIG.Targeting.Theming.ResultFlags
        public byte IsDOT;       // 1 = DOT tick, 0 = direct hit
        public int SourceNetworkId; // Attacker's GhostOwner.NetworkId (-1 = environment/unknown)
    }
}
