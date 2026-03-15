using Hollowcore.Chassis.Definitions;
using Hollowcore.Chassis.Systems;
using Unity.Entities;
using UnityEngine;

namespace Hollowcore.Chassis.Authoring
{
    /// <summary>
    /// Added to the player prefab. Creates a child entity with ChassisState during baking
    /// and sets up ChassisLink on the player entity.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hollowcore/Chassis/Chassis Authoring")]
    public class ChassisAuthoring : MonoBehaviour
    {
        [Header("Starting Loadout")]
        [Tooltip("Limb definitions to equip on spawn. Leave empty for no starting limbs.")]
        public LimbDefinitionSO[] StartingLimbs;

        class Baker : Baker<ChassisAuthoring>
        {
            public override void Bake(ChassisAuthoring authoring)
            {
                var playerEntity = GetEntity(TransformUsageFlags.Dynamic);

                // Add ChassisLink (will be populated at runtime by ChassisBootstrapSystem)
                AddComponent(playerEntity, new ChassisLink
                {
                    ChassisEntity = Entity.Null
                });

                // Add ChassisAggregateStats for stat aggregation
                AddComponent(playerEntity, new ChassisAggregateStats());

                // Bake starting limb references into a buffer
                if (authoring.StartingLimbs != null && authoring.StartingLimbs.Length > 0)
                {
                    var buffer = AddBuffer<StartingLimbElement>(playerEntity);
                    foreach (var limb in authoring.StartingLimbs)
                    {
                        if (limb == null) continue;
                        buffer.Add(new StartingLimbElement
                        {
                            LimbDefinitionId = limb.LimbId
                        });
                    }
                }
            }
        }
    }
}
