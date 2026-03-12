using Unity.Mathematics;
using Unity.Collections;
using Unity.Entities;

namespace DIG.Widgets.Systems
{
    /// <summary>
    /// EPIC 15.26 Phase 5: Screen-space overlap detection and displacement for widgets.
    /// Called by WidgetBridgeSystem when StackingEnabled in the active paradigm profile.
    ///
    /// Algorithm:
    ///   1. Sort visible widgets by screen Y (top to bottom)
    ///   2. For each pair within proximity: compute overlap area
    ///   3. If overlap > 30% of either widget rect: displace lower-importance widget
    ///   4. Displacement direction: push vertically away from overlap center
    ///
    /// Rules:
    ///   - Targeted widget is never displaced
    ///   - Boss widgets are never displaced
    ///   - Lower importance yields to higher
    /// </summary>
    public static class WidgetStackingResolver
    {
        // Estimated widget rect size in pixels (used for overlap detection)
        private const float DefaultWidgetWidth = 120f;
        private const float DefaultWidgetHeight = 20f;
        private const float OverlapThreshold = 0.3f; // 30% overlap triggers displacement
        private const float DisplacementStep = 22f;   // Pixels to push per displacement

        /// <summary>
        /// Resolve overlapping widgets by displacing screen positions.
        /// Modifies ScreenPos in the projection entries in-place.
        /// </summary>
        /// <param name="projections">HashMap of projected widgets (modified in-place).</param>
        public static void Resolve(ref NativeHashMap<Entity, WidgetProjection> projections)
        {
            if (!projections.IsCreated || projections.Count < 2) return;

            // Extract visible widgets into sortable list
            var entries = projections.GetKeyValueArrays(Allocator.Temp);
            using var visible = new NativeList<SortEntry>(entries.Keys.Length, Allocator.Temp);

            for (int i = 0; i < entries.Keys.Length; i++)
            {
                if (entries.Values[i].IsVisible)
                {
                    visible.Add(new SortEntry
                    {
                        Entity = entries.Keys[i],
                        ScreenY = entries.Values[i].ScreenPos.y,
                        Importance = entries.Values[i].Importance,
                        Index = i
                    });
                }
            }

            if (visible.Length < 2)
            {
                entries.Dispose();
                return;
            }

            // Sort by screen Y descending (top of screen first)
            visible.Sort(new ScreenYComparer());

            // Check pairs within proximity and displace
            for (int i = 0; i < visible.Length; i++)
            {
                var a = entries.Values[visible[i].Index];
                if (!a.IsVisible) continue;

                float2 rectA = GetWidgetRect(a);

                for (int j = i + 1; j < visible.Length; j++)
                {
                    var b = entries.Values[visible[j].Index];
                    if (!b.IsVisible) continue;

                    // Quick screen-Y distance check (skip if too far apart)
                    if (math.abs(a.ScreenPos.y - b.ScreenPos.y) > DefaultWidgetHeight * 2f)
                        break; // sorted, so all subsequent are further

                    float2 rectB = GetWidgetRect(b);
                    float overlap = ComputeOverlap(a.ScreenPos, rectA, b.ScreenPos, rectB);

                    if (overlap > OverlapThreshold)
                    {
                        // Displace the lower-importance widget
                        bool displaceB = b.Importance <= a.Importance;

                        // Boss/targeted exemption (importance >= 200)
                        if (a.Importance >= 200f) displaceB = true;
                        if (b.Importance >= 200f) displaceB = false;

                        if (displaceB)
                        {
                            b.ScreenPos.y -= DisplacementStep;
                            entries.Values[visible[j].Index] = b;
                            projections[b.Entity] = b;
                        }
                        else
                        {
                            a.ScreenPos.y += DisplacementStep;
                            entries.Values[visible[i].Index] = a;
                            projections[a.Entity] = a;
                        }
                    }
                }
            }

            entries.Dispose();
        }

        private static float2 GetWidgetRect(in WidgetProjection proj)
        {
            float scale = math.max(proj.Scale, 0.1f);
            return new float2(DefaultWidgetWidth * scale, DefaultWidgetHeight * scale);
        }

        private static float ComputeOverlap(float2 posA, float2 sizeA, float2 posB, float2 sizeB)
        {
            // Compute AABB overlap ratio
            float2 minA = posA - sizeA * 0.5f;
            float2 maxA = posA + sizeA * 0.5f;
            float2 minB = posB - sizeB * 0.5f;
            float2 maxB = posB + sizeB * 0.5f;

            float overlapX = math.max(0f, math.min(maxA.x, maxB.x) - math.max(minA.x, minB.x));
            float overlapY = math.max(0f, math.min(maxA.y, maxB.y) - math.max(minA.y, minB.y));

            float overlapArea = overlapX * overlapY;
            float areaA = sizeA.x * sizeA.y;
            float areaB = sizeB.x * sizeB.y;
            float minArea = math.min(areaA, areaB);

            return minArea > 0f ? overlapArea / minArea : 0f;
        }

        private struct SortEntry
        {
            public Entity Entity;
            public float ScreenY;
            public float Importance;
            public int Index;
        }

        private struct ScreenYComparer : System.Collections.Generic.IComparer<SortEntry>
        {
            public int Compare(SortEntry a, SortEntry b)
            {
                return b.ScreenY.CompareTo(a.ScreenY); // descending (top first)
            }
        }
    }
}
