using UnityEngine;
using Unity.Collections;
using Unity.Entities;

namespace DIG.Map.Authoring
{
    /// <summary>
    /// EPIC 17.6: Marks a landmark entity as a Point of Interest for compass, world map,
    /// and discovery tracking. Baker also adds a MapIcon component automatically.
    /// Attach to town centers, dungeon entrances, boss arena markers, fast travel shrines, etc.
    /// </summary>
    [AddComponentMenu("DIG/Map/Point of Interest")]
    [DisallowMultipleComponent]
    public class PointOfInterestAuthoring : MonoBehaviour
    {
        [Tooltip("Stable unique ID matching the POIRegistrySO entry.")]
        public int POIId;

        [Tooltip("POI classification for icon theming and discovery behavior.")]
        public POIType Type = POIType.Landmark;

        [Tooltip("Display name shown on compass and world map (max 29 characters).")]
        public string Label = "Unnamed POI";

        [Tooltip("If true, this POI starts discovered (e.g. towns). If false, requires player proximity.")]
        public bool DiscoveredByDefault;

        class Baker : Baker<PointOfInterestAuthoring>
        {
            public override void Bake(PointOfInterestAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new PointOfInterest
                {
                    POIId = authoring.POIId,
                    Type = authoring.Type,
                    Label = new FixedString32Bytes(authoring.Label),
                    DiscoveredByPlayer = authoring.DiscoveredByDefault
                });

                // Auto-add MapIcon so POIs appear on minimap/world map without separate MapIconAuthoring
                var iconType = authoring.Type == POIType.FastTravel ? MapIconType.FastTravel : MapIconType.POI;
                AddComponent(entity, new MapIcon
                {
                    IconType = iconType,
                    Priority = (byte)(authoring.Type == POIType.FastTravel ? 180 : 150),
                    VisibleOnMinimap = true,
                    VisibleOnWorldMap = true,
                    CustomColorPacked = 0
                });
            }
        }
    }
}
