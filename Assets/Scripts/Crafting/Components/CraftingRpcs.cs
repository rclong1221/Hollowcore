using Unity.NetCode;

namespace DIG.Crafting
{
    /// <summary>
    /// EPIC 16.13: Client-to-server request to start crafting a recipe.
    /// </summary>
    public struct CraftRequestRpc : IRpcCommand
    {
        public int RecipeId;
        public int StationGhostId;
    }

    /// <summary>
    /// EPIC 16.13: Client-to-server request to collect a completed craft output.
    /// </summary>
    public struct CollectCraftRpc : IRpcCommand
    {
        public int OutputIndex;
        public int StationGhostId;
    }

    /// <summary>
    /// EPIC 16.13: Client-to-server request to cancel a queued craft.
    /// </summary>
    public struct CancelCraftRpc : IRpcCommand
    {
        public int QueueIndex;
        public int StationGhostId;
    }
}
