using Audio.Systems;
using DIG.Weapons.Audio;

namespace DIG.Surface
{
    /// <summary>
    /// EPIC 15.24: Maps existing SurfaceMaterial SOs and SurfaceMaterialType enum to SurfaceID.
    /// </summary>
    public static class SurfaceIdResolver
    {
        /// <summary>
        /// Resolve SurfaceID from a SurfaceMaterial ScriptableObject.
        /// Uses the explicit SurfaceId field if set, otherwise falls back to name heuristics.
        /// </summary>
        public static SurfaceID FromMaterial(SurfaceMaterial material)
        {
            if (material == null) return SurfaceID.Default;
            if (material.SurfaceId != SurfaceID.Default) return material.SurfaceId;

            // Name-based heuristic fallback for materials that haven't been tagged yet
            string name = material.DisplayName?.ToLowerInvariant() ?? "";
            if (name.Contains("metal")) return SurfaceID.Metal_Thin;
            if (name.Contains("concrete") || name.Contains("cement")) return SurfaceID.Concrete;
            if (name.Contains("wood")) return SurfaceID.Wood;
            if (name.Contains("dirt") || name.Contains("earth")) return SurfaceID.Dirt;
            if (name.Contains("sand")) return SurfaceID.Sand;
            if (name.Contains("grass")) return SurfaceID.Grass;
            if (name.Contains("gravel")) return SurfaceID.Gravel;
            if (name.Contains("snow")) return SurfaceID.Snow;
            if (name.Contains("ice")) return SurfaceID.Ice;
            if (name.Contains("water")) return SurfaceID.Water;
            if (name.Contains("mud")) return SurfaceID.Mud;
            if (name.Contains("glass")) return SurfaceID.Glass;
            if (name.Contains("flesh") || name.Contains("skin")) return SurfaceID.Flesh;
            if (name.Contains("armor") || name.Contains("armour")) return SurfaceID.Armor;
            if (name.Contains("cloth") || name.Contains("fabric")) return SurfaceID.Fabric;
            if (name.Contains("plastic")) return SurfaceID.Plastic;
            if (name.Contains("stone") || name.Contains("rock")) return SurfaceID.Stone;
            if (name.Contains("ceramic") || name.Contains("tile")) return SurfaceID.Ceramic;
            if (name.Contains("leaf") || name.Contains("foliage")) return SurfaceID.Foliage;
            if (name.Contains("bark") || name.Contains("tree")) return SurfaceID.Bark;
            if (name.Contains("rubber")) return SurfaceID.Rubber;
            if (name.Contains("shield") || name.Contains("energy")) return SurfaceID.Energy_Shield;

            return SurfaceID.Default;
        }

        /// <summary>
        /// Map existing SurfaceMaterialType enum to new SurfaceID.
        /// </summary>
        public static SurfaceID FromSurfaceMaterialType(SurfaceMaterialType type)
        {
            return type switch
            {
                SurfaceMaterialType.Concrete => SurfaceID.Concrete,
                SurfaceMaterialType.Metal => SurfaceID.Metal_Thin,
                SurfaceMaterialType.Wood => SurfaceID.Wood,
                SurfaceMaterialType.Dirt => SurfaceID.Dirt,
                SurfaceMaterialType.Grass => SurfaceID.Grass,
                SurfaceMaterialType.Sand => SurfaceID.Sand,
                SurfaceMaterialType.Water => SurfaceID.Water,
                SurfaceMaterialType.Glass => SurfaceID.Glass,
                SurfaceMaterialType.Flesh => SurfaceID.Flesh,
                SurfaceMaterialType.Cloth => SurfaceID.Fabric,
                SurfaceMaterialType.Plastic => SurfaceID.Plastic,
                _ => SurfaceID.Default
            };
        }
    }
}
