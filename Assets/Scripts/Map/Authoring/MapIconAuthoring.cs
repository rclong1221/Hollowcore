using UnityEngine;
using Unity.Entities;

namespace DIG.Map.Authoring
{
    /// <summary>
    /// EPIC 17.6: Places a MapIcon component on baked entities so they appear on the minimap
    /// and world map. Attach to enemy prefabs, NPC prefabs, crafting stations, vendors, loot
    /// containers, boss prefabs, etc. NOT on the player entity (player icon is injected by
    /// MapIconUpdateSystem at runtime).
    /// </summary>
    [AddComponentMenu("DIG/Map/Map Icon")]
    [DisallowMultipleComponent]
    public class MapIconAuthoring : MonoBehaviour
    {
        [Tooltip("Icon classification used by the map theme to pick sprite and color.")]
        public MapIconType IconType = MapIconType.Enemy;

        [Tooltip("Overlap priority: higher values render on top. 0=low, 255=highest.")]
        [Range(0, 255)]
        public int Priority = 100;

        [Tooltip("Show this entity on the minimap.")]
        public bool VisibleOnMinimap = true;

        [Tooltip("Show this entity on the fullscreen world map.")]
        public bool VisibleOnWorldMap = true;

        [Tooltip("Override the theme default color for this icon.")]
        public bool UseCustomColor;

        [Tooltip("Custom RGBA color (only used if UseCustomColor is true).")]
        public Color CustomColor = Color.white;

        class Baker : Baker<MapIconAuthoring>
        {
            public override void Bake(MapIconAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                uint colorPacked = 0;
                if (authoring.UseCustomColor)
                {
                    var c = authoring.CustomColor;
                    colorPacked = ((uint)(c.r * 255) << 24)
                                | ((uint)(c.g * 255) << 16)
                                | ((uint)(c.b * 255) << 8)
                                | (uint)(c.a * 255);
                }

                AddComponent(entity, new MapIcon
                {
                    IconType = authoring.IconType,
                    Priority = (byte)authoring.Priority,
                    VisibleOnMinimap = authoring.VisibleOnMinimap,
                    VisibleOnWorldMap = authoring.VisibleOnWorldMap,
                    CustomColorPacked = colorPacked
                });
            }
        }
    }
}
