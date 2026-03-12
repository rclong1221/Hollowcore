using UnityEngine;
using Unity.Entities;
using Player.Components;

namespace Player.Authoring
{
    [DisallowMultipleComponent]
    public class ProneAuthoring : MonoBehaviour
    {
        [Header("Prone Settings")]
        public float transitionDuration = 0.5f;
        public float speedMultiplier = 0.5f;
        [Header("Stance Heights")]
        public float standingHeight = 2.0f;
        public float proneHeight = 0.5f;

        [Header("Safe-Stand / Sampling")]
        public float clearanceMargin = 0.05f;
        public int safeStandSteps = 4;
        public int safeStandRadialSamples = 8;

        [Header("Interpolation")]
        public float heightInterpSpeed = 8f;

        class Baker : Baker<ProneAuthoring>
        {
            public override void Bake(ProneAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new ProneStateComponent
                {
                    IsProne = 0,
                    IsCrawling = 0,
                    TransitionTimer = 0f,
                    TransitionDuration = authoring.transitionDuration,
                    SpeedMultiplier = authoring.speedMultiplier
                });

                // Bake or override the PlayerStanceConfig values so this prefab can tune stance heights
                AddComponent(entity, new PlayerStanceConfig
                {
                    StandingHeight = authoring.standingHeight,
                    CrouchingHeight = PlayerStanceConfig.Default.CrouchingHeight,
                    ProneHeight = authoring.proneHeight,
                    StandingSpeed = PlayerStanceConfig.Default.StandingSpeed,
                    CrouchingSpeed = PlayerStanceConfig.Default.CrouchingSpeed,
                    ProneSpeed = authoring.speedMultiplier * PlayerStanceConfig.Default.StandingSpeed
                });

                // Also bake tuning values used by the ProneSystem (sampling & interpolation) as component data
                // so systems can read per-entity tuning if needed in the future.
                AddComponent(entity, new ProneTuning
                {
                    ClearanceMargin = authoring.clearanceMargin,
                    SafeStandSteps = authoring.safeStandSteps,
                    SafeStandRadialSamples = authoring.safeStandRadialSamples,
                    HeightInterpSpeed = authoring.heightInterpSpeed
                });

                // Add CrawlState for Opsive animation integration
                // ProneCrawlAnimationBridgeSystem syncs prone+movement -> CrawlState
                AddComponent(entity, new CrawlState
                {
                    IsCrawling = false,
                    CrawlSubState = 0
                });
            }
        }
    }
}
