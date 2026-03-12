using Unity.Entities;
using UnityEngine;

namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 16.15: Authoring component for player save state.
    /// Creates a child entity holding save metadata, linked via SaveStateLink.
    /// Place on the player prefab (e.g., Warrok_Server) root.
    /// </summary>
    [AddComponentMenu("DIG/Persistence/Save State")]
    public class SaveStateAuthoring : MonoBehaviour
    {
        private class Baker : Baker<SaveStateAuthoring>
        {
            public override void Bake(SaveStateAuthoring authoring)
            {
                var playerEntity = GetEntity(TransformUsageFlags.Dynamic);

                // Create child entity for save state metadata
                var childEntity = CreateAdditionalEntity(TransformUsageFlags.None, false, "SaveState");

                // Player gets the link (8 bytes)
                AddComponent(playerEntity, new SaveStateLink
                {
                    SaveChildEntity = childEntity
                });

                // Child entity gets metadata components
                AddComponent(childEntity, new SaveStateTag());
                AddComponent(childEntity, new SaveDirtyFlags());
                AddComponent(childEntity, new PlayerSaveId());
                AddComponent(childEntity, new SaveStateOwner { Owner = playerEntity });
            }
        }
    }
}
