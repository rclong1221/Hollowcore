using System.IO;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;

namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 17.6: Serializes fog-of-war texture (RLE compressed) and discovered POI list.
    /// TypeId=13. Fog data is async GPU readback → RLE encode → binary. On load, RLE decode → texture.
    /// Uses pre-cached fog snapshot from AsyncGPUReadback to avoid blocking main thread during save.
    /// </summary>
    public class MapSaveModule : ISaveModule
    {
        public int TypeId => SaveModuleTypeIds.Map;
        public string DisplayName => "Map";
        public int ModuleVersion => 1;

        private int _lastSavedRevealCount;
        // Cached fog pixels from async readback — updated periodically, used during save
        private byte[] _cachedFogPixels;
        private int _cachedFogWidth;
        private int _cachedFogHeight;
        private bool _readbackPending;

        public bool IsDirty(in SaveContext ctx)
        {
            var em = ctx.EntityManager;
            if (!HasMapState(em)) return false;

            var reveal = GetRevealState(em);

            // Kick off async readback when dirty, so pixels are ready when Serialize is called
            if (reveal.TotalRevealed != _lastSavedRevealCount && !_readbackPending)
                RequestAsyncReadback(em, reveal);

            return reveal.TotalRevealed != _lastSavedRevealCount;
        }

        private void RequestAsyncReadback(EntityManager em, DIG.Map.MapRevealState reveal)
        {
            var managed = GetManagedState(em);
            if (managed?.FogOfWarTexture == null) return;

            _readbackPending = true;
            AsyncGPUReadback.Request(managed.FogOfWarTexture, 0, TextureFormat.R8, (request) =>
            {
                _readbackPending = false;
                if (request.hasError) return;
                var data = request.GetData<byte>();
                _cachedFogPixels = data.ToArray();
                _cachedFogWidth = reveal.FogTextureWidth;
                _cachedFogHeight = reveal.FogTextureHeight;
            });
        }

        public int Serialize(in SaveContext ctx, BinaryWriter w)
        {
            var em = ctx.EntityManager;
            long start = w.BaseStream.Position;

            if (!HasMapState(em))
            {
                // Write empty placeholder
                w.Write(0); // FogTextureWidth
                w.Write(0); // FogTextureHeight
                w.Write((byte)0); // Compression: raw
                w.Write(0); // FogPixelCount
                w.Write((short)0); // DiscoveredPOICount
                w.Write(0); // TotalRevealed
                return (int)(w.BaseStream.Position - start);
            }

            var reveal = GetRevealState(em);
            var managed = GetManagedState(em);

            // Fog texture dimensions
            w.Write(reveal.FogTextureWidth);
            w.Write(reveal.FogTextureHeight);

            // Use cached async readback pixels if available and dimensions match
            byte[] fogPixels = null;
            if (_cachedFogPixels != null &&
                _cachedFogWidth == reveal.FogTextureWidth &&
                _cachedFogHeight == reveal.FogTextureHeight)
            {
                fogPixels = _cachedFogPixels;
            }
            else if (managed.FogOfWarTexture != null)
            {
                // Fallback: synchronous readback if async cache not yet ready
                var prevRT = RenderTexture.active;
                RenderTexture.active = managed.FogOfWarTexture;
                var tmp = new Texture2D(reveal.FogTextureWidth, reveal.FogTextureHeight, TextureFormat.R8, false);
                tmp.ReadPixels(new Rect(0, 0, reveal.FogTextureWidth, reveal.FogTextureHeight), 0, 0);
                tmp.Apply();
                fogPixels = tmp.GetRawTextureData();
                RenderTexture.active = prevRT;
                Object.Destroy(tmp);
            }

            // RLE compress fog data
            if (fogPixels != null && fogPixels.Length > 0)
            {
                w.Write((byte)1); // Compression: RLE
                byte[] rle = RLEEncode(fogPixels);
                w.Write(rle.Length);
                w.Write(rle);
            }
            else
            {
                w.Write((byte)0); // Compression: raw
                w.Write(0); // zero pixels
            }

            // Discovered POIs
            int poiCount = 0;
            var poiQuery = em.CreateEntityQuery(ComponentType.ReadOnly<DIG.Map.PointOfInterest>());
            if (poiQuery.CalculateEntityCount() > 0)
            {
                var pois = poiQuery.ToComponentDataArray<DIG.Map.PointOfInterest>(Unity.Collections.Allocator.Temp);
                // Count discovered
                for (int i = 0; i < pois.Length; i++)
                    if (pois[i].DiscoveredByPlayer) poiCount++;

                w.Write((short)poiCount);
                for (int i = 0; i < pois.Length; i++)
                {
                    if (!pois[i].DiscoveredByPlayer) continue;
                    w.Write(pois[i].POIId);
                    w.Write(ctx.ElapsedPlaytime);
                }
                pois.Dispose();
            }
            else
            {
                w.Write((short)0);
            }

            // Total revealed
            w.Write(reveal.TotalRevealed);
            _lastSavedRevealCount = reveal.TotalRevealed;

            return (int)(w.BaseStream.Position - start);
        }

        public void Deserialize(in LoadContext ctx, BinaryReader r, int blockVersion)
        {
            var em = ctx.EntityManager;

            int fogW = r.ReadInt32();
            int fogH = r.ReadInt32();
            byte compression = r.ReadByte();
            int dataLen = r.ReadInt32();

            byte[] fogPixels = null;
            if (dataLen > 0)
            {
                byte[] rawData = r.ReadBytes(dataLen);
                fogPixels = compression == 1 ? RLEDecode(rawData, fogW * fogH) : rawData;
            }

            short poiCount = r.ReadInt16();
            var discoveredPOIs = new int[poiCount];
            for (int i = 0; i < poiCount; i++)
            {
                discoveredPOIs[i] = r.ReadInt32();
                r.ReadSingle(); // DiscoverTimestamp (stored but not used on load currently)
            }

            int totalRevealed = r.ReadInt32();

            // Apply fog texture
            if (HasMapState(em) && fogPixels != null && fogPixels.Length == fogW * fogH)
            {
                var managed = GetManagedState(em);
                if (managed.FogOfWarTexture != null &&
                    managed.FogOfWarTexture.width == fogW &&
                    managed.FogOfWarTexture.height == fogH)
                {
                    var tmp = new Texture2D(fogW, fogH, TextureFormat.R8, false);
                    tmp.LoadRawTextureData(fogPixels);
                    tmp.Apply();
                    Graphics.Blit(tmp, managed.FogOfWarTexture);
                    Object.Destroy(tmp);
                }

                // Restore reveal count
                var reveal = GetRevealState(em);
                reveal.TotalRevealed = totalRevealed;
                SetRevealState(em, reveal);
            }

            // Restore discovered POIs
            if (discoveredPOIs.Length > 0)
            {
                var poiQuery = em.CreateEntityQuery(ComponentType.ReadWrite<DIG.Map.PointOfInterest>());
                if (poiQuery.CalculateEntityCount() > 0)
                {
                    var entities = poiQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                    var pois = poiQuery.ToComponentDataArray<DIG.Map.PointOfInterest>(Unity.Collections.Allocator.Temp);

                    for (int i = 0; i < pois.Length; i++)
                    {
                        for (int j = 0; j < discoveredPOIs.Length; j++)
                        {
                            if (pois[i].POIId == discoveredPOIs[j])
                            {
                                var poi = pois[i];
                                poi.DiscoveredByPlayer = true;
                                em.SetComponentData(entities[i], poi);
                                break;
                            }
                        }
                    }

                    entities.Dispose();
                    pois.Dispose();
                }
            }

            _lastSavedRevealCount = totalRevealed;
        }

        // ==================== Helpers ====================

        private static bool HasMapState(EntityManager em)
        {
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<DIG.Map.MapRevealState>());
            return query.CalculateEntityCount() > 0;
        }

        private static DIG.Map.MapRevealState GetRevealState(EntityManager em)
        {
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<DIG.Map.MapRevealState>());
            return query.GetSingleton<DIG.Map.MapRevealState>();
        }

        private static void SetRevealState(EntityManager em, DIG.Map.MapRevealState state)
        {
            var query = em.CreateEntityQuery(ComponentType.ReadWrite<DIG.Map.MapRevealState>());
            query.SetSingleton(state);
        }

        private static DIG.Map.MapManagedState GetManagedState(EntityManager em)
        {
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<DIG.Map.MapManagedState>());
            var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            var result = em.GetComponentObject<DIG.Map.MapManagedState>(entities[0]);
            entities.Dispose();
            return result;
        }

        // ==================== RLE Compression ====================

        private static byte[] RLEEncode(byte[] data)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                int i = 0;
                while (i < data.Length)
                {
                    byte val = data[i];
                    int run = 1;
                    while (i + run < data.Length && data[i + run] == val && run < 255)
                        run++;

                    bw.Write((byte)run);
                    bw.Write(val);
                    i += run;
                }
                return ms.ToArray();
            }
        }

        private static byte[] RLEDecode(byte[] rle, int expectedLength)
        {
            byte[] result = new byte[expectedLength];
            int outIdx = 0;

            using (var ms = new MemoryStream(rle))
            using (var br = new BinaryReader(ms))
            {
                while (ms.Position < ms.Length && outIdx < expectedLength)
                {
                    byte run = br.ReadByte();
                    byte val = br.ReadByte();
                    for (int j = 0; j < run && outIdx < expectedLength; j++)
                        result[outIdx++] = val;
                }
            }
            return result;
        }
    }
}
