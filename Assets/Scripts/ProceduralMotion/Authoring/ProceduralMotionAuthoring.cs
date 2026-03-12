using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.ProceduralMotion.Authoring
{
    /// <summary>
    /// EPIC 15.25 Phase 1: Baker for player prefab.
    /// Adds WeaponSpringState, ProceduralMotionState, and ProceduralMotionConfig
    /// to the player entity. Bakes the assigned ProceduralMotionProfile to a BlobAsset.
    /// </summary>
    public class ProceduralMotionAuthoring : MonoBehaviour
    {
        [Header("Motion Profile")]
        [Tooltip("Assign a ProceduralMotionProfile ScriptableObject. Baked to BlobAsset at build time.")]
        public ProceduralMotionProfile Profile;

        [Header("Weapon Spring Defaults")]
        [Tooltip("Default position clamp (meters).")]
        public Vector3 PositionClampMin = new Vector3(-0.15f, -0.15f, -0.2f);
        public Vector3 PositionClampMax = new Vector3(0.15f, 0.15f, 0.1f);

        [Tooltip("Default rotation clamp (degrees).")]
        public Vector3 RotationClampMin = new Vector3(-15f, -15f, -20f);
        public Vector3 RotationClampMax = new Vector3(15f, 15f, 20f);

        public class Baker : Baker<ProceduralMotionAuthoring>
        {
            public override void Bake(ProceduralMotionAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Add weapon spring state with clamp bounds
                AddComponent(entity, new WeaponSpringState
                {
                    PositionMin = authoring.PositionClampMin,
                    PositionMax = authoring.PositionClampMax,
                    RotationMin = authoring.RotationClampMin,
                    RotationMax = authoring.RotationClampMax
                });

                // Add procedural motion tracking state
                AddComponent(entity, new ProceduralMotionState
                {
                    CurrentState = MotionState.Idle,
                    PreviousState = MotionState.Idle,
                    StateBlendT = 1f,
                    StateTransitionSpeed = 10f,
                    FPMotionWeight = 1f,
                    CameraMotionWeight = 1f,
                    WeaponMotionWeight = 1f,
                    HitReactionWeight = 1f,
                    BobWeight = 1f,
                    SwayWeight = 1f
                });

                // Bake profile to blob
                if (authoring.Profile != null)
                {
                    DependsOn(authoring.Profile);
                    var blobRef = authoring.Profile.BakeToBlob(this);
                    AddComponent(entity, new ProceduralMotionConfig
                    {
                        ProfileBlob = blobRef
                    });
                }
                else
                {
                    AddComponent(entity, new ProceduralMotionConfig());
                }
            }
        }
    }
}
