using Unity.Entities;
using Unity.NetCode;

namespace DIG.Loot.Network
{
    /// <summary>
    /// EPIC 16.6: Client-to-server pickup request.
    /// Sent when a client wants to pick up a loot entity.
    /// </summary>
    public struct PickupRequestRpc : IRpcCommand
    {
        public Entity LootEntity;
        public int ItemTypeId;
    }

    /// <summary>
    /// EPIC 16.6: Server-to-client pickup result.
    /// Sent back to confirm or deny a pickup request.
    /// </summary>
    public struct PickupResultRpc : IRpcCommand
    {
        public bool Success;
        public int ItemTypeId;
        public int Quantity;
        public byte FailReason; // 0=None, 1=AlreadyPickedUp, 2=InventoryFull, 3=TooFar, 4=Owned
    }
}
