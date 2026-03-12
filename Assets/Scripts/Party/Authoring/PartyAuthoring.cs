using Unity.Entities;
using UnityEngine;

namespace DIG.Party
{
    /// <summary>
    /// EPIC 17.2: Baker adds PartyLink to player entity.
    /// Minimal — only 8 bytes added to player archetype.
    /// Party data lives on separate party entities, NOT on the player.
    /// Place on player prefab alongside PlayerAuthoring, ProgressionAuthoring, etc.
    /// </summary>
    [AddComponentMenu("DIG/Party/Party Member")]
    public class PartyAuthoring : MonoBehaviour
    {
        public class Baker : Baker<PartyAuthoring>
        {
            public override void Bake(PartyAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new PartyLink { PartyEntity = Entity.Null });
            }
        }
    }
}
