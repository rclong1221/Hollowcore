using Unity.Entities;
using UnityEngine;
using DIG.Voxel.Components;

namespace DIG.Voxel.Authoring
{
    /// <summary>
    /// EPIC 15.10: Authoring component for tools that place explosives.
    /// </summary>
    [AddComponentMenu("DIG/Voxel/Explosive Placement Tool")]
    public class ExplosivePlacementTool : MonoBehaviour
    {
        [Header("Prefab Settings")]
        [Tooltip("The explosive entity prefab to spawn.")]
        public GameObject ExplosivePrefab;

        [Header("Placement Settings")]
        [Tooltip("Maximum distance for placement (meters).")]
        public float PlacementRange = 2.0f;

        [Tooltip("Can this be placed on walls/ceilings? (e.g. C4)")]
        public bool CanPlaceOnWalls = true;

        [Tooltip("Does this require a drilled hole? (e.g. Dynamite)")]
        public bool SubsurfacePlacement = false;

        [Header("Usage")]
        [Tooltip("Time between placements.")]
        public float Cooldown = 0.5f;
    }

    public class ExplosivePlacementToolBaker : Baker<ExplosivePlacementTool>
    {
        public override void Bake(ExplosivePlacementTool authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            var prefabEntity = GetEntity(authoring.ExplosivePrefab, TransformUsageFlags.Dynamic);

            AddComponent(entity, new ExplosivePlacementConfig
            {
                ExplosivePrefab = prefabEntity,
                PlacementRange = authoring.PlacementRange,
                CanPlaceOnWalls = authoring.CanPlaceOnWalls,
                SubsurfacePlacement = authoring.SubsurfacePlacement,
                CooldownTime = authoring.Cooldown
            });

            AddComponent(entity, new ExplosivePlacementState
            {
                CooldownTimer = 0f
            });
        }
    }
}
