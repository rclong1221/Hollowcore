using UnityEngine;
using Unity.Entities;
using DIG.Player.Abilities;

namespace DIG.Player.Bridges
{
    /// <summary>
    /// Listens for "OnAnimatorJump" animation event and sets the JumpEventTrigger ECS component.
    /// This allows the JumpSystem to synchronize the physical jump impulse with the animation.
    /// </summary>
    public class JumpAnimationBridge : MonoBehaviour
    {
        private Entity _entity;
        private EntityManager _entityManager;
        
        public void Initialize(Entity entity, EntityManager entityManager)
        {
            _entity = entity;
            _entityManager = entityManager;
        }

        // Called by Animation Event: Function="OnAnimatorJump"
        public void OnAnimatorJump()
        {
            if (_entityManager != default && _entityManager.Exists(_entity))
            {
                if (_entityManager.HasComponent<JumpEventTrigger>(_entity))
                {
                    var trigger = _entityManager.GetComponentData<JumpEventTrigger>(_entity);
                    trigger.Triggered = true;
                    _entityManager.SetComponentData(_entity, trigger);
                }
            }
        }
    }
}
