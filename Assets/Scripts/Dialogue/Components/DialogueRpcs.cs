using Unity.NetCode;

namespace DIG.Dialogue
{
    /// <summary>
    /// EPIC 16.16: Client-to-server RPC for selecting a dialogue choice.
    /// Server validates ChoiceIndex against ValidChoicesMask before advancing.
    /// </summary>
    public struct DialogueChoiceRpc : IRpcCommand
    {
        public int NpcGhostId;
        public int ChoiceIndex;
        public int CurrentNodeId;
    }

    /// <summary>
    /// EPIC 16.16: Client-to-server RPC to skip/close dialogue.
    /// </summary>
    public struct DialogueSkipRpc : IRpcCommand
    {
        public int NpcGhostId;
    }
}
