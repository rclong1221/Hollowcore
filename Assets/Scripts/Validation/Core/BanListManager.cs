using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

namespace DIG.Validation
{
    /// <summary>
    /// EPIC 17.11: Ban entry for persistence.
    /// </summary>
    [Serializable]
    public class BanEntry
    {
        public int NetworkId;
        public byte BanType; // 0=Temp, 1=Permanent
        public long ExpiryUtcMs; // Unix epoch UTC ms (0 = permanent)
        public string Reason;
        public long IssuedUtcMs;
        public float ViolationScore;
    }

    /// <summary>
    /// EPIC 17.11: Serializable wrapper for ban list JSON.
    /// </summary>
    [Serializable]
    internal class BanListData
    {
        public List<BanEntry> Bans = new List<BanEntry>();
    }

    /// <summary>
    /// EPIC 17.11: Managed static class for ban list I/O.
    /// Reads/writes JSON file. NOT ECS — persists across server restarts.
    /// Uses deferred dirty-flag save to avoid main-thread disk I/O hitches.
    /// </summary>
    public static class BanListManager
    {
        private static readonly Dictionary<int, BanEntry> _bans = new Dictionary<int, BanEntry>();
        private static string _filePath;
        private static bool _loaded;
        private static volatile bool _dirty;
        private static volatile bool _saving;

        public static void Initialize(string filePath = null)
        {
            _filePath = filePath ?? Path.Combine(Application.persistentDataPath, "ban_list.json");
            Load();
        }

        /// <summary>
        /// O(1) int-based ban check. No managed allocation.
        /// Expired temp bans are marked dirty for deferred cleanup — no disk I/O here.
        /// </summary>
        public static bool IsBanned(int networkId)
        {
            if (!_loaded) return false;

            if (!_bans.TryGetValue(networkId, out var entry))
                return false;

            // Check temp ban expiry — defer removal instead of saving immediately
            if (entry.BanType == 0 && entry.ExpiryUtcMs > 0)
            {
                long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (nowMs >= entry.ExpiryUtcMs)
                {
                    _bans.Remove(networkId);
                    _dirty = true; // deferred save, no disk I/O on hot path
                    return false;
                }
            }

            return true;
        }

        public static void AddTempBan(int networkId, int durationMinutes, string reason, float violationScore = 0f)
        {
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _bans[networkId] = new BanEntry
            {
                NetworkId = networkId,
                BanType = 0,
                ExpiryUtcMs = nowMs + (long)durationMinutes * 60L * 1000L,
                Reason = reason ?? "Anti-cheat violation",
                IssuedUtcMs = nowMs,
                ViolationScore = violationScore
            };
            _dirty = true;
        }

        public static void AddPermaBan(int networkId, string reason, float violationScore = 0f)
        {
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _bans[networkId] = new BanEntry
            {
                NetworkId = networkId,
                BanType = 1,
                ExpiryUtcMs = 0,
                Reason = reason ?? "Permanent ban",
                IssuedUtcMs = nowMs,
                ViolationScore = violationScore
            };
            _dirty = true;
        }

        public static void RemoveBan(int networkId)
        {
            if (_bans.Remove(networkId))
                _dirty = true;
        }

        public static List<BanEntry> GetActiveBans()
        {
            var result = new List<BanEntry>();
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            foreach (var entry in _bans.Values)
            {
                if (entry.BanType == 1 || entry.ExpiryUtcMs > nowMs)
                    result.Add(entry);
            }
            return result;
        }

        public static int ActiveBanCount => _bans.Count;

        /// <summary>
        /// Call once per frame (or on interval) from a managed system to flush pending writes.
        /// Writes to disk on a background thread to avoid main-thread hitching.
        /// </summary>
        public static void SaveIfDirty()
        {
            if (!_dirty || _saving) return;
            _dirty = false;
            _saving = true;

            // Snapshot data on main thread (Dictionary is not thread-safe)
            var data = new BanListData { Bans = new List<BanEntry>(_bans.Values) };
            string path = _filePath;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    if (!string.IsNullOrEmpty(path))
                    {
                        string json = JsonUtility.ToJson(data, true);
                        File.WriteAllText(path, json);
                    }
                }
                catch (Exception e)
                {
                    // Log on next main-thread opportunity; Debug.Log is thread-safe in Unity
                    Debug.LogWarning($"[BanListManager] Background save failed: {e.Message}");
                }
                finally
                {
                    _saving = false;
                }
            });
        }

        /// <summary>
        /// Blocking save for shutdown. Call from OnDestroy or application quit.
        /// </summary>
        public static void FlushBlocking()
        {
            if (!_dirty) return;
            _dirty = false;
            SaveImmediate();
        }

        private static void Load()
        {
            _bans.Clear();
            _loaded = true;

            if (string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath))
                return;

            try
            {
                string json = File.ReadAllText(_filePath);
                var data = JsonUtility.FromJson<BanListData>(json);
                if (data?.Bans != null)
                {
                    foreach (var entry in data.Bans)
                        _bans[entry.NetworkId] = entry;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BanListManager] Failed to load ban list: {e.Message}");
            }
        }

        private static void SaveImmediate()
        {
            if (string.IsNullOrEmpty(_filePath)) return;

            try
            {
                var data = new BanListData { Bans = new List<BanEntry>(_bans.Values) };
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BanListManager] Failed to save ban list: {e.Message}");
            }
        }
    }
}
