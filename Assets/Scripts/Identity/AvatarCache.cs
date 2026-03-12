using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace DIG.Identity
{
    /// <summary>
    /// EPIC 17.14: LRU texture cache for player avatars.
    /// Memory-aware, configurable capacity, O(1) promotion.
    /// </summary>
    public static class AvatarCache
    {
        private static readonly Dictionary<string, Texture2D> _cache = new();
        private static readonly LinkedList<string> _lruOrder = new();
        private static readonly Dictionary<string, LinkedListNode<string>> _nodeMap = new();
        private static readonly HashSet<string> _loading = new();
        private static int _maxEntries = 32;
        private static int _hits;
        private static int _misses;

        public static int Count => _cache.Count;
        public static int Hits => _hits;
        public static int Misses => _misses;
        public static float HitRate => (_hits + _misses) > 0 ? (float)_hits / (_hits + _misses) : 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Clear();
            _loading.Clear();
            _hits = 0;
            _misses = 0;
            _maxEntries = 32;
        }

        public static void SetCapacity(int maxEntries)
        {
            _maxEntries = Mathf.Clamp(maxEntries, 8, 128);
        }

        public static bool TryGet(string platformId, out Texture2D avatar)
        {
            if (_cache.TryGetValue(platformId, out var tex) && tex != null)
            {
                PromoteLru(platformId);
                _hits++;
                avatar = tex;
                return true;
            }

            _misses++;
            avatar = null;
            return false;
        }

        public static async Task<Texture2D> GetOrLoadAsync(
            IIdentityProvider provider, string platformId, AvatarSize size = AvatarSize.Medium)
        {
            if (TryGet(platformId, out var cached))
                return cached;

            if (provider == null || !provider.SupportsAvatars)
                return null;

            if (!_loading.Add(platformId))
                return null;

            Texture2D tex;
            try
            {
                tex = await provider.GetAvatarAsync(platformId, size);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[AvatarCache] Failed to load avatar for {platformId}: {e.Message}");
                _loading.Remove(platformId);
                return null;
            }

            _loading.Remove(platformId);

            if (tex == null) return null;

            tex.Compress(true);
            Store(platformId, tex);
            return tex;
        }

        public static void Invalidate(string platformId)
        {
            if (_cache.TryGetValue(platformId, out var tex))
            {
                if (tex != null)
                    Object.Destroy(tex);
                _cache.Remove(platformId);
                RemoveLruNode(platformId);
            }
        }

        public static void Clear()
        {
            foreach (var kv in _cache)
            {
                if (kv.Value != null)
                    Object.Destroy(kv.Value);
            }
            _cache.Clear();
            _lruOrder.Clear();
            _nodeMap.Clear();
        }

        private static void Store(string platformId, Texture2D texture)
        {
            if (_cache.TryGetValue(platformId, out var existing) && existing != null)
                Object.Destroy(existing);

            while (_cache.Count >= _maxEntries && _lruOrder.Count > 0)
            {
                string oldest = _lruOrder.Last.Value;
                _lruOrder.RemoveLast();
                _nodeMap.Remove(oldest);
                if (_cache.TryGetValue(oldest, out var evicted))
                {
                    if (evicted != null)
                        Object.Destroy(evicted);
                    _cache.Remove(oldest);
                }
            }

            _cache[platformId] = texture;
            PromoteLru(platformId);
        }

        private static void PromoteLru(string platformId)
        {
            if (_nodeMap.TryGetValue(platformId, out var existingNode))
            {
                _lruOrder.Remove(existingNode);
                _lruOrder.AddFirst(existingNode);
            }
            else
            {
                var node = _lruOrder.AddFirst(platformId);
                _nodeMap[platformId] = node;
            }
        }

        private static void RemoveLruNode(string platformId)
        {
            if (_nodeMap.TryGetValue(platformId, out var node))
            {
                _lruOrder.Remove(node);
                _nodeMap.Remove(platformId);
            }
        }
    }
}
