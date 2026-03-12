using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.Voxel.Core
{
    /// <summary>
    /// Tracks all voxel modifications for late-join synchronization.
    /// Server-authoritative history of all terrain changes.
    /// </summary>
    public class VoxelModificationHistory
    {
        [Serializable]
        public struct ModificationRecord
        {
            public int3 ChunkPos;
            public int3 LocalPos;
            public byte NewDensity;
            public byte NewMaterial;
            public uint ServerTick;
            public float Timestamp;
        }

        // Per-chunk modification history
        private readonly Dictionary<int3, List<ModificationRecord>> _chunkHistory = new();
        
        // Global ordered list for sync
        private readonly List<ModificationRecord> _allRecords = new();
        
        // Stats
        private int _totalModifications;
        private float _lastPruneTime;
        
        /// <summary>Total modifications recorded.</summary>
        public int TotalModifications => _totalModifications;
        
        /// <summary>Number of chunks with modifications.</summary>
        public int ModifiedChunkCount => _chunkHistory.Count;
        
        /// <summary>
        /// Record a new modification.
        /// </summary>
        public void RecordModification(int3 chunkPos, int3 localPos, byte density, byte material, uint serverTick)
        {
            var record = new ModificationRecord
            {
                ChunkPos = chunkPos,
                LocalPos = localPos,
                NewDensity = density,
                NewMaterial = material,
                ServerTick = serverTick,
                Timestamp = Time.time
            };
            
            // Add to chunk-specific list
            if (!_chunkHistory.TryGetValue(chunkPos, out var chunkList))
            {
                chunkList = new List<ModificationRecord>();
                _chunkHistory[chunkPos] = chunkList;
            }
            chunkList.Add(record);
            
            // Add to global list
            _allRecords.Add(record);
            _totalModifications++;
        }
        
        /// <summary>
        /// Get all modifications for a specific chunk.
        /// </summary>
        public List<ModificationRecord> GetChunkModifications(int3 chunkPos)
        {
            if (_chunkHistory.TryGetValue(chunkPos, out var list))
                return list;
            return new List<ModificationRecord>();
        }
        
        /// <summary>
        /// Get all modifications since a specific tick (for late-join sync).
        /// </summary>
        public List<ModificationRecord> GetModificationsSinceTick(uint sinceTick)
        {
            var result = new List<ModificationRecord>();
            foreach (var record in _allRecords)
            {
                if (record.ServerTick > sinceTick)
                    result.Add(record);
            }
            return result;
        }
        
        /// <summary>
        /// Get all modifications (for full late-join sync).
        /// </summary>
        public List<ModificationRecord> GetAllModifications()
        {
            return new List<ModificationRecord>(_allRecords);
        }
        
        /// <summary>
        /// Get modified chunk positions (for late-join awareness).
        /// </summary>
        public List<int3> GetModifiedChunks()
        {
            return new List<int3>(_chunkHistory.Keys);
        }
        
        /// <summary>
        /// Prune old modifications to save memory.
        /// </summary>
        public void PruneOlderThan(uint maxTick)
        {
            int removed = 0;
            
            // Prune from global list
            _allRecords.RemoveAll(r => 
            {
                if (r.ServerTick < maxTick)
                {
                    removed++;
                    return true;
                }
                return false;
            });
            
            // Prune from chunk lists
            var emptyChunks = new List<int3>();
            foreach (var kvp in _chunkHistory)
            {
                kvp.Value.RemoveAll(r => r.ServerTick < maxTick);
                if (kvp.Value.Count == 0)
                    emptyChunks.Add(kvp.Key);
            }
            
            // Remove empty chunk entries
            foreach (var chunk in emptyChunks)
            {
                _chunkHistory.Remove(chunk);
            }
            
            if (removed > 0)
            {
                UnityEngine.Debug.Log($"[ModificationHistory] Pruned {removed} old records");
            }
        }
        
        /// <summary>
        /// Clear all history (for server restart).
        /// </summary>
        public void Clear()
        {
            _chunkHistory.Clear();
            _allRecords.Clear();
            _totalModifications = 0;
        }
        
        /// <summary>
        /// Get estimated memory usage in bytes.
        /// </summary>
        public int GetEstimatedMemoryBytes()
        {
            // ModificationRecord is ~32 bytes
            return _allRecords.Count * 32;
        }
    }
}
