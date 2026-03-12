using UnityEngine;
using Unity.Entities;
using DIG.Player.Components;

namespace DIG.Player.Authoring
{
    /// <summary>
    /// Authoring component for CollisionGameSettings singleton.
    /// Place on a GameObject in your scene or subscene to configure game-mode collision settings.
    /// Epic 7.6.3: Friendly fire toggle for PvP game modes.
    /// </summary>
    [DisallowMultipleComponent]
    public class CollisionGameSettingsAuthoring : MonoBehaviour
    {
        [Header("Friendly Fire")]
        [Tooltip("When enabled, player-player collisions cause stagger/knockdown. When disabled, players pass through each other.")]
        public bool FriendlyFireEnabled = true;
        
        [Header("Team Settings")]
        [Tooltip("When enabled, same-team players can stagger/knockdown each other. When disabled, team members ignore each other's collision.")]
        public bool TeamCollisionEnabled = false;
        
        [Header("Soft Collision")]
        [Tooltip("When enabled, players still experience gentle push forces even when friendly fire is disabled.")]
        public bool SoftCollisionWhenDisabled = true;
        
        [Tooltip("Multiplier for push forces when friendly fire is disabled (0 = no push, 1 = full push).")]
        [Range(0f, 1f)]
        public float SoftCollisionForceMultiplier = 0.3f;
        
        class Baker : Baker<CollisionGameSettingsAuthoring>
        {
            public override void Bake(CollisionGameSettingsAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                
                AddComponent(entity, new CollisionGameSettings
                {
                    FriendlyFireEnabled = authoring.FriendlyFireEnabled,
                    TeamCollisionEnabled = authoring.TeamCollisionEnabled,
                    SoftCollisionWhenDisabled = authoring.SoftCollisionWhenDisabled,
                    SoftCollisionForceMultiplier = authoring.SoftCollisionForceMultiplier
                });
            }
        }
    }
}
