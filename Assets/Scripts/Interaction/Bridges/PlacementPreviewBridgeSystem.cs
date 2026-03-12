using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Interaction.Systems
{
    /// <summary>
    /// EPIC 16.1 Phase 6: Managed bridge system for placement preview visuals.
    ///
    /// Handles:
    /// - Detecting PlacementState.IsPlacing transitions on local player
    /// - Spawning/destroying preview GameObjects via PlacementPreviewLink
    /// - Updating preview position/rotation/validity each frame
    ///
    /// Follows the MinigameBridgeSystem / InteractableHybridBridgeSystem pattern.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class PlacementPreviewBridgeSystem : SystemBase
    {
        /// <summary>
        /// Static registry for preview links.
        /// Key: arbitrary ID (typically 0 for default, or a specific item type).
        /// </summary>
        private static readonly Dictionary<int, Bridges.PlacementPreviewLink> s_Registry = new();

        /// <summary>
        /// Default preview link (ID=0). Most setups use a single preview link.
        /// </summary>
        private static Bridges.PlacementPreviewLink s_DefaultLink;

        /// <summary>
        /// Track which entities were placing last frame for transition detection.
        /// </summary>
        private readonly HashSet<Entity> _previouslyPlacing = new();

        // --- Static API for PlacementPreviewLink ---

        public static void RegisterLink(int id, Bridges.PlacementPreviewLink link)
        {
            if (link != null)
            {
                s_Registry[id] = link;
                if (id == 0) s_DefaultLink = link;
            }
        }

        public static void UnregisterLink(int id)
        {
            if (id == 0) s_DefaultLink = null;
            s_Registry.Remove(id);
        }

        protected override void OnUpdate()
        {
            var currentlyPlacing = new HashSet<Entity>();

            foreach (var (placementState, entity) in
                     SystemAPI.Query<RefRO<PlacementState>>()
                     .WithEntityAccess())
            {
                if (placementState.ValueRO.IsPlacing)
                {
                    currentlyPlacing.Add(entity);

                    var link = s_DefaultLink;
                    if (link == null) continue;

                    if (!_previouslyPlacing.Contains(entity))
                    {
                        // Just entered placement mode — show preview
                        link.ShowPreview(
                            placementState.ValueRO.PreviewPosition,
                            placementState.ValueRO.PreviewRotation,
                            placementState.ValueRO.IsValid);
                    }
                    else
                    {
                        // Update preview position/rotation/validity
                        link.UpdatePreview(
                            placementState.ValueRO.PreviewPosition,
                            placementState.ValueRO.PreviewRotation,
                            placementState.ValueRO.IsValid);
                    }
                }
            }

            // Check for exit transitions
            foreach (var previousEntity in _previouslyPlacing)
            {
                if (!currentlyPlacing.Contains(previousEntity))
                {
                    // Exited placement mode — hide preview
                    var link = s_DefaultLink;
                    if (link != null)
                    {
                        link.HidePreview();
                    }
                }
            }

            _previouslyPlacing.Clear();
            foreach (var e in currentlyPlacing)
            {
                _previouslyPlacing.Add(e);
            }
        }
    }
}
