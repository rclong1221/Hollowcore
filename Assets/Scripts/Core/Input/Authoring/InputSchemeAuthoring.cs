using Unity.Entities;
using UnityEngine;
using DIG.Targeting;
using DIG.Player.Components;

namespace DIG.Core.Input
{
    /// <summary>
    /// Bakes InputSchemeState and CursorHoverResult onto the player entity.
    ///
    /// EPIC 15.18
    /// </summary>
    public class InputSchemeAuthoring : MonoBehaviour
    {
        [Tooltip("Default input scheme for this player prefab.")]
        public InputScheme DefaultScheme = InputScheme.ShooterDirect;

        class Baker : Baker<InputSchemeAuthoring>
        {
            public override void Bake(InputSchemeAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new InputSchemeState
                {
                    ActiveScheme = authoring.DefaultScheme,
                    IsTemporaryCursorActive = false,
                });

                // CursorHoverResult is the output written by CursorHoverSystem
                AddComponent(entity, new CursorHoverResult());
                
                // EPIC 15.20: Isometric facing lock for attack-toward-cursor
                AddComponent(entity, new IsometricFacingLock());
            }
        }
    }
}
