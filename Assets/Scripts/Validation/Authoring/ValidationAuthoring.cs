using Unity.Entities;
using UnityEngine;

namespace DIG.Validation
{
    /// <summary>
    /// EPIC 17.11: Authoring component for player validation.
    /// Place on player prefab (Warrok_Server).
    /// Baker creates child entity with all validation state (same pattern as PvPPlayerAuthoring).
    /// Only 8 bytes (ValidationLink) added to the player entity.
    /// </summary>
    [AddComponentMenu("DIG/Validation/Player Validation")]
    public class ValidationAuthoring : MonoBehaviour
    {
        [Tooltip("Reference to validation profile SO (loaded at runtime from Resources if null).")]
        public ValidationProfileSO Profile;

        public class Baker : Baker<ValidationAuthoring>
        {
            public override void Bake(ValidationAuthoring authoring)
            {
                var playerEntity = GetEntity(TransformUsageFlags.Dynamic);

                // Create child entity for validation state
                var childEntity = CreateAdditionalEntity(TransformUsageFlags.None, false, "ValidationChild");

                // Parent → child link (8 bytes on player)
                AddComponent(playerEntity, new ValidationLink { ValidationChild = childEntity });

                // Child entity components
                AddComponent(childEntity, new ValidationChildTag());
                AddComponent(childEntity, new ValidationOwner { Owner = playerEntity });
                AddComponent(childEntity, default(PlayerValidationState));
                AddComponent(childEntity, default(MovementValidationState));

                // Rate limit buffer
                var rateLimitBuffer = AddBuffer<RateLimitEntry>(childEntity);

                // Pre-populate from profile if available
                var profile = authoring.Profile;
                if (profile == null)
                    profile = Resources.Load<ValidationProfileSO>("ValidationProfile");

                if (profile != null && profile.RpcRateLimits != null)
                {
                    foreach (var limit in profile.RpcRateLimits)
                    {
                        rateLimitBuffer.Add(new RateLimitEntry
                        {
                            RpcTypeId = limit.RpcTypeId,
                            TokenCount = limit.MaxBurst, // Start at full capacity
                            LastRefillTick = 0,
                            BurstConsumed = 0
                        });
                    }
                }

                // Economy audit buffer
                AddBuffer<EconomyAuditEntry>(childEntity);
            }
        }
    }
}
