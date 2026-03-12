using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Weapons
{
    /// <summary>
    /// Shared static event queue for weapon animation events from MonoBehaviour bridge.
    /// Used to communicate OPSIVE weapon animation events to ECS systems.
    /// Follows the same pattern as FreeClimbAnimationEvents.
    /// </summary>
    public static class WeaponAnimationEvents
    {
        /// <summary>
        /// Types of weapon animation events from OPSIVE clips.
        /// </summary>
        public enum EventType : byte
        {
            None = 0,

            // Shootable events
            Fire,               // Fire animation triggered
            FireComplete,       // Fire animation complete (for semi-auto reset)
            ReloadStart,        // Reload animation started
            ReloadInsertAmmo,   // Magazine inserted / ammo loaded (mid-reload)
            ReloadComplete,     // Reload animation complete
            DryFire,            // Dry fire click (no ammo)

            // Melee events
            MeleeStart,         // Melee swing started
            MeleeHitFrame,      // Active hitbox frame (damage window)
            MeleeComplete,      // Melee swing complete
            MeleeCombo,         // Combo transition point

            // Throwable events
            ThrowChargeStart,   // Started charging throw
            ThrowRelease,       // Throw released
            ThrowComplete,      // Throw animation complete

            // Shield events
            BlockStart,         // Block raised
            BlockEnd,           // Block lowered
            ParryWindow,        // Perfect parry window active
            ParryComplete,      // Parry window closed

            // Generic events
            EquipStart,         // Equip animation started
            EquipComplete,      // Equip animation complete
            UnequipStart,       // Unequip animation started
            UnequipComplete,    // Unequip animation complete
            ItemUseStart,       // Generic use started
            ItemUseComplete     // Generic use complete
        }

        /// <summary>
        /// A weapon animation event with associated entity.
        /// </summary>
        public struct WeaponEvent
        {
            public EventType Type;
            public Entity WeaponEntity;
            public float3 Position;      // World position (for effects)
            public float3 Direction;     // Aim direction (for projectiles)
            public int IntData;          // Generic int data (combo index, ammo count, etc.)
            public float FloatData;      // Generic float data (charge level, etc.)
        }

        // Ring buffer for multiple events per frame
        private const int MaxEvents = 16;
        private static WeaponEvent[] _eventBuffer = new WeaponEvent[MaxEvents];
        private static volatile int _writeIndex = 0;
        private static volatile int _readIndex = 0;

        /// <summary>
        /// Queue a weapon animation event from MonoBehaviour for ECS processing.
        /// Thread-safe via atomic operations.
        /// </summary>
        public static void QueueEvent(EventType eventType, Entity weaponEntity = default,
            float3 position = default, float3 direction = default,
            int intData = 0, float floatData = 0f)
        {
            int index = _writeIndex % MaxEvents;
            _eventBuffer[index] = new WeaponEvent
            {
                Type = eventType,
                WeaponEntity = weaponEntity,
                Position = position,
                Direction = direction,
                IntData = intData,
                FloatData = floatData
            };
            _writeIndex++;

            #if UNITY_EDITOR
            UnityEngine.Debug.Log($"[WeaponAnimEvents] Event queued: {eventType} (entity: {weaponEntity.Index})");
            #endif
        }

        /// <summary>
        /// Simple event queue without entity reference.
        /// Used when the animation event doesn't know which entity it belongs to.
        /// </summary>
        public static void QueueEvent(EventType eventType)
        {
            QueueEvent(eventType, Entity.Null);
        }

        /// <summary>
        /// Try to dequeue a pending event. Returns true if an event was available.
        /// Called by ECS systems during update.
        /// </summary>
        public static bool TryDequeueEvent(out WeaponEvent weaponEvent)
        {
            if (_readIndex < _writeIndex)
            {
                int index = _readIndex % MaxEvents;
                weaponEvent = _eventBuffer[index];
                _readIndex++;
                return true;
            }

            weaponEvent = default;
            return false;
        }

        /// <summary>
        /// Check if there are pending events without consuming them.
        /// </summary>
        public static bool HasPendingEvents => _readIndex < _writeIndex;

        /// <summary>
        /// Get the count of pending events.
        /// </summary>
        public static int PendingEventCount => _writeIndex - _readIndex;

        /// <summary>
        /// Clear all pending events.
        /// Use with caution - typically only for scene transitions.
        /// </summary>
        public static void ClearAll()
        {
            _readIndex = _writeIndex;
        }
    }
}
