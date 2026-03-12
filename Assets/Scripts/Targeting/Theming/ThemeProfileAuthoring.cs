using Unity.Entities;
using UnityEngine;

namespace DIG.Targeting.Theming
{
    /// <summary>
    /// Authoring component for theme profile.
    /// Bakes ThemeProfile ScriptableObject to IndicatorThemeData ECS component.
    /// </summary>
    public class ThemeProfileAuthoring : MonoBehaviour
    {
        [Tooltip("Theme profile to use. If null, uses default.")]
        public ThemeProfile Profile;
        
        public class Baker : Baker<ThemeProfileAuthoring>
        {
            public override void Bake(ThemeProfileAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                
                if (authoring.Profile != null)
                {
                    AddComponent(entity, authoring.Profile.Bake());
                }
                else
                {
                    AddComponent(entity, IndicatorThemeData.Default);
                }
            }
        }
    }
}
