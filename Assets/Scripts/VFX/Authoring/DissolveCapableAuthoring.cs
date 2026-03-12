using Unity.Entities;
using UnityEngine;

namespace DIG.VFX.Authoring
{
    /// <summary>
    /// EPIC 16.7 Phase 5: Marks an entity as dissolve-capable.
    /// When present, CorpseSinkSystem skips position sinking for this entity
    /// and CorpseDissolveSystem drives _DissolveAmount on its renderers instead.
    ///
    /// Can be placed manually on prefabs, or auto-detected from material.
    /// </summary>
    public class DissolveCapableAuthoring : MonoBehaviour
    {
        [Tooltip("If true, automatically detects dissolve capability from renderer materials at bake time.")]
        public bool AutoDetect = true;

        private class Baker : Baker<DissolveCapableAuthoring>
        {
            public override void Bake(DissolveCapableAuthoring authoring)
            {
                bool hasDissolveMaterial = false;

                if (authoring.AutoDetect)
                {
                    // Check if any renderer on this GameObject uses the dissolve shader
                    var renderers = GetComponentsInChildren<Renderer>();
                    foreach (var renderer in renderers)
                    {
                        foreach (var mat in renderer.sharedMaterials)
                        {
                            if (mat != null && mat.shader != null &&
                                mat.shader.name == "DIG/URP/Dissolve")
                            {
                                hasDissolveMaterial = true;
                                break;
                            }
                        }
                        if (hasDissolveMaterial) break;
                    }
                }
                else
                {
                    hasDissolveMaterial = true;
                }

                if (hasDissolveMaterial)
                {
                    var entity = GetEntity(TransformUsageFlags.Dynamic);
                    AddComponent<DissolveCapable>(entity);
                }
            }
        }
    }
}
