using Unity.NetCode;

namespace DIG.SkillTree
{
    /// <summary>
    /// EPIC 17.1: Client→Server RPC to allocate a talent point on a specific node.
    /// Server validates prerequisites, available points, and node existence.
    /// </summary>
    public struct TalentAllocationRpc : IRpcCommand
    {
        public ushort TreeId;
        public ushort NodeId;
    }

    /// <summary>
    /// EPIC 17.1: Client→Server RPC to respec a skill tree.
    /// TreeId=0 means respec all trees. Validates gold cost on server.
    /// </summary>
    public struct TalentRespecRpc : IRpcCommand
    {
        public ushort TreeId;
    }
}
