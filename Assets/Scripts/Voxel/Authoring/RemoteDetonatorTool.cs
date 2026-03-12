using Unity.Entities;
using UnityEngine;
using DIG.Voxel.Components;

namespace DIG.Voxel.Authoring
{
    /// <summary>
    /// EPIC 15.10: Authoring component for remote detonator tools.
    /// Add this to a tool to allow it to trigger explosives owned by the player.
    /// </summary>
    [AddComponentMenu("DIG/Voxel/Remote Detonator Tool")]
    public class RemoteDetonatorTool : MonoBehaviour
    {
        [Tooltip("Max range to find explosives.")]
        public float Range = 100f;

        [Tooltip("Cooldown between uses.")]
        public float Cooldown = 1.0f;
    }

    public class RemoteDetonatorToolBaker : Baker<RemoteDetonatorTool>
    {
        public override void Bake(RemoteDetonatorTool authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new RemoteDetonator
            {
                Range = authoring.Range,
                Cooldown = authoring.Cooldown
            });
            
            AddComponent(entity, new ExplosivePlacementState 
            {
                CooldownTimer = 0f 
            });
        }
    }
}
