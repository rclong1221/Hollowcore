using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Player.Components;

namespace Player.Components
{
    // T6: Heal Station
    public struct HealStation : IComponentData
    {
        public float HealAmount;
        public float HealInterval;
        public float Timer;
        public float Radius;
    }

    // T7: God Mode / Death Prevention
    public struct GodMode : IComponentData
    {
        public bool Enabled;
    }

    // T4/T7: Spawn on Death
    [InternalBufferCapacity(4)]
    public struct DeathSpawnElement : IBufferElementData
    {
        public Entity Prefab;
        public float3 PositionOffset;
        public bool ApplyExplosiveForce;
    }

    // T5: Damage Popups
    public struct DamagePopupConfig : IComponentData
    {
        public Entity PopupPrefab;
        public float SpawnHeightOffset;
        public float RandomJitter;
    }

    // T3: Death Layer
    public struct DeathLayerSettings : IComponentData
    {
        public int DeadLayer;
        public uint DeadCollisionMask; // For Physics
    }
}
