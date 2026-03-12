using Unity.Entities;
using Unity.Mathematics;

namespace Player.Components
{
    /// <summary>
    /// Component placed on spawn points.
    /// Used by RespawnSystem to select a location.
    /// </summary>
    public struct RespawnPoint : IComponentData
    {
        public int Priority; // Lower is better
        public bool Enabled;
    }
}
