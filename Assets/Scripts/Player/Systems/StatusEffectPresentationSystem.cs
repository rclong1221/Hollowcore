using Unity.Entities;
using Unity.NetCode;
using Player.Components;
using DIG.Combat.UI;

namespace Player.Systems
{
    /// <summary>
    /// Client-side system that reads active status effects and updates
    /// the Combat UI via CombatUIBootstrap.
    /// EPIC 15.9: Syncs ECS StatusEffect buffer to StatusEffectBarViewModel.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class StatusEffectPresentationSystem : SystemBase
    {
        private Entity _localPlayerEntity;
        
        protected override void OnUpdate()
        {
            var bootstrap = CombatUIBootstrap.Instance;
            if (bootstrap == null || bootstrap.StatusEffects == null)
                return;
            
            // Find local player if not cached
            if (_localPlayerEntity == Entity.Null)
            {
                foreach (var (_, entity) in SystemAPI.Query<RefRO<GhostOwnerIsLocal>>().WithEntityAccess())
                {
                    _localPlayerEntity = entity;
                    break;
                }
            }
            
            if (_localPlayerEntity == Entity.Null)
                return;
            
            // Check if player has status effect buffer
            if (!EntityManager.HasBuffer<StatusEffect>(_localPlayerEntity))
                return;
            
            var buffer = EntityManager.GetBuffer<StatusEffect>(_localPlayerEntity);
            var viewModel = bootstrap.StatusEffects;
            
            // Sync each status effect to the ViewModel
            for (int i = 0; i < buffer.Length; i++)
            {
                var effect = buffer[i];
                
                // Map Player.Components.StatusEffectType to DIG.Combat.UI.StatusEffectType
                var uiType = MapStatusEffectType(effect.Type);
                
                if (uiType != DIG.Combat.UI.StatusEffectType.None)
                {
                    // Stacks = severity * 10 for visual representation
                    int stacks = (int)(effect.Severity * 10);
                    if (stacks < 1) stacks = 1;
                    
                    viewModel.AddOrUpdateEffect(uiType, effect.TimeRemaining, stacks);
                }
            }
        }
        
        /// <summary>
        /// Maps Player.Components.StatusEffectType to DIG.Combat.UI.StatusEffectType
        /// </summary>
        private DIG.Combat.UI.StatusEffectType MapStatusEffectType(Player.Components.StatusEffectType type)
        {
            return type switch
            {
                Player.Components.StatusEffectType.Burn => DIG.Combat.UI.StatusEffectType.Burn,
                Player.Components.StatusEffectType.Frostbite => DIG.Combat.UI.StatusEffectType.Frostbite,
                Player.Components.StatusEffectType.Bleed => DIG.Combat.UI.StatusEffectType.Bleed,
                Player.Components.StatusEffectType.RadiationPoisoning => DIG.Combat.UI.StatusEffectType.Poison,
                Player.Components.StatusEffectType.Concussion => DIG.Combat.UI.StatusEffectType.Stun,
                _ => DIG.Combat.UI.StatusEffectType.None
            };
        }
        
        /// <summary>
        /// Set the local player entity explicitly.
        /// </summary>
        public void SetLocalPlayer(Entity player)
        {
            _localPlayerEntity = player;
        }
    }
}
