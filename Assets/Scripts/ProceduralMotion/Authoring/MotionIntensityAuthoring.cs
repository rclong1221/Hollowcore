using Unity.Entities;
using UnityEngine;

namespace DIG.ProceduralMotion.Authoring
{
    /// <summary>
    /// EPIC 15.25 Phase 5: Baker for ProceduralMotionIntensity ECS singleton.
    /// Place in a subscene to bake default intensity settings.
    /// At runtime, MotionIntensitySlider writes to this singleton via EntityManager.
    /// </summary>
    public class MotionIntensityAuthoring : MonoBehaviour
    {
        [Header("Motion Intensity Defaults")]
        [Range(0f, 2f)]
        [Tooltip("Master scale (0=disabled, 1=normal, 2=exaggerated).")]
        public float GlobalIntensity = 1f;

        [Range(0f, 2f)]
        [Tooltip("Camera procedural force scale.")]
        public float CameraMotionScale = 1f;

        [Range(0f, 2f)]
        [Tooltip("Weapon procedural force scale.")]
        public float WeaponMotionScale = 1f;

        public class Baker : Baker<MotionIntensityAuthoring>
        {
            public override void Bake(MotionIntensityAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new ProceduralMotionIntensity
                {
                    GlobalIntensity = authoring.GlobalIntensity,
                    CameraMotionScale = authoring.CameraMotionScale,
                    WeaponMotionScale = authoring.WeaponMotionScale
                });
            }
        }
    }
}
