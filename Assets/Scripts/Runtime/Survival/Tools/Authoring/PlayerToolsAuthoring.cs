using UnityEngine;
using Unity.Entities;

namespace DIG.Survival.Tools.Authoring
{
    /// <summary>
    /// Authoring component for players that can use tools.
    /// Adds ActiveTool and ToolOwnership buffer to player entities.
    /// </summary>
    public class PlayerToolsAuthoring : MonoBehaviour
    {
        [Header("Tool Loadout")]
        [Tooltip("Starting tools to give the player (optional, can be empty)")]
        public GameObject[] StartingTools;

        [Header("Display")]
        [Tooltip("Add Geiger display state for HUD")]
        public bool HasGeigerDisplay = true;
    }

    /// <summary>
    /// Baker for PlayerToolsAuthoring.
    /// </summary>
    public class PlayerToolsBaker : Baker<PlayerToolsAuthoring>
    {
        public override void Bake(PlayerToolsAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Add active tool component (starts with no tool)
            AddComponent(entity, ActiveTool.Empty);

            // Add tool ownership buffer
            var buffer = AddBuffer<ToolOwnership>(entity);

            // Add starting tools to buffer
            // Note: Actual tool entities need to be spawned at runtime
            // This just sets up the slots for them
            if (authoring.StartingTools != null)
            {
                for (int i = 0; i < authoring.StartingTools.Length && i < 5; i++)
                {
                    if (authoring.StartingTools[i] != null)
                    {
                        // Tool entities will be spawned and linked at runtime
                        // For now just reserve the slot
                        buffer.Add(new ToolOwnership
                        {
                            ToolEntity = Entity.Null, // Set at runtime
                            SlotIndex = i
                        });
                    }
                }
            }

            // Add Geiger display state if enabled
            if (authoring.HasGeigerDisplay)
            {
                AddComponent(entity, new GeigerDisplayState
                {
                    DisplayLevel = 0f,
                    IsVisible = false
                });
            }
        }
    }
}
