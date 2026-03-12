using System;
using Unity.Entities;

namespace DIG.Player.Components
{
    /// <summary>
    /// Epic 7.7.6: Temporal Coherence - Hashmap key for collision pair caching.
    /// 
    /// Entities are ordered (lower index first) to ensure A-B and B-A produce
    /// the same key, avoiding duplicate cache entries.
    /// 
    /// Implements IEquatable for NativeHashMap compatibility.
    /// </summary>
    public readonly struct CollisionPairKey : IEquatable<CollisionPairKey>
    {
        /// <summary>
        /// First entity (always the one with lower index).
        /// </summary>
        public readonly Entity EntityA;
        
        /// <summary>
        /// Second entity (always the one with higher index).
        /// </summary>
        public readonly Entity EntityB;
        
        /// <summary>
        /// Private constructor - use Create() to ensure ordering.
        /// </summary>
        private CollisionPairKey(Entity entityA, Entity entityB)
        {
            EntityA = entityA;
            EntityB = entityB;
        }
        
        /// <summary>
        /// Create a collision pair key with entities ordered by index.
        /// This ensures (A, B) and (B, A) produce the same key.
        /// </summary>
        public static CollisionPairKey Create(Entity a, Entity b)
        {
            // Order by entity index for uniqueness
            if (a.Index <= b.Index)
                return new CollisionPairKey(a, b);
            else
                return new CollisionPairKey(b, a);
        }
        
        /// <summary>
        /// Check if this key contains the specified entity.
        /// Used for cache cleanup when an entity is destroyed.
        /// </summary>
        public bool Contains(Entity entity)
        {
            return EntityA == entity || EntityB == entity;
        }
        
        /// <summary>
        /// Hash code for NativeHashMap compatibility.
        /// Uses both entity indices combined with XOR and bit rotation.
        /// </summary>
        public override int GetHashCode()
        {
            // Combine both entity indices with bit mixing for good distribution
            // Entity.Index is the primary differentiator; Version rarely changes
            unchecked
            {
                int hash = EntityA.Index;
                hash = (hash << 5) | (hash >> 27); // Rotate left 5 bits
                hash ^= EntityB.Index;
                hash = (hash * 397) ^ EntityA.Version;
                hash = (hash * 397) ^ EntityB.Version;
                return hash;
            }
        }
        
        /// <summary>
        /// Equality check for NativeHashMap compatibility.
        /// </summary>
        public bool Equals(CollisionPairKey other)
        {
            return EntityA == other.EntityA && EntityB == other.EntityB;
        }
        
        public override bool Equals(object obj)
        {
            return obj is CollisionPairKey other && Equals(other);
        }
        
        public static bool operator ==(CollisionPairKey left, CollisionPairKey right)
        {
            return left.Equals(right);
        }
        
        public static bool operator !=(CollisionPairKey left, CollisionPairKey right)
        {
            return !left.Equals(right);
        }
        
        public override string ToString()
        {
            return $"CollisionPairKey({EntityA.Index}:{EntityA.Version}, {EntityB.Index}:{EntityB.Version})";
        }
    }
}
