using Unity.Entities;
using UnityEngine;

namespace DIG.VFX.Authoring
{
    /// <summary>
    /// EPIC 16.7 Phase 7: Sets the initial VFX quality preset.
    /// Place on a GameObject in a subscene.
    /// </summary>
    public class VFXQualityAuthoring : MonoBehaviour
    {
        public VFXQualityPreset InitialPreset = VFXQualityPreset.High;

        private class Baker : Baker<VFXQualityAuthoring>
        {
            public override void Bake(VFXQualityAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new VFXQualityState
                {
                    CurrentPreset = authoring.InitialPreset,
                    IsDirty = true
                });
            }
        }
    }
}
