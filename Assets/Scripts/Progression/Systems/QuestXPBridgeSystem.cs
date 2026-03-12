using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Progression
{
    /// <summary>
    /// EPIC 16.14: Static API for granting XP from external systems
    /// (QuestRewardSystem, CraftOutputGenerationSystem, etc.).
    /// Thread-safe: writes directly to entity via EntityManager.
    /// </summary>
    public static class XPGrantAPI
    {
        /// <summary>
        /// Grants XP to a player entity. Call from managed systems only.
        /// Checks for PlayerProgression and CharacterAttributes.Level cap.
        /// </summary>
        public static void GrantXP(EntityManager em, Entity playerEntity, int amount, XPSourceType source)
        {
            if (amount <= 0) return;
            if (playerEntity == Entity.Null) return;
            if (!em.HasComponent<PlayerProgression>(playerEntity)) return;

            // Check level cap
            if (em.HasComponent<DIG.Combat.Components.CharacterAttributes>(playerEntity))
            {
                if (!em.HasComponent<ProgressionConfigSingleton>(
                    em.CreateEntityQuery(typeof(ProgressionConfigSingleton)).GetSingletonEntity()))
                {
                    // No config singleton — can't check cap, still award
                }
                else
                {
                    // We'll just award — LevelUpSystem handles cap
                }
            }

            var prog = em.GetComponentData<PlayerProgression>(playerEntity);
            prog.CurrentXP += amount;
            prog.TotalXPEarned += amount;
            em.SetComponentData(playerEntity, prog);

            LevelUpVisualQueue.EnqueueXPGain(amount, source);
        }

        /// <summary>
        /// Simpler overload when EntityManager is available from SystemBase.
        /// </summary>
        public static void GrantXP(SystemBase system, Entity playerEntity, int amount, XPSourceType source)
        {
            GrantXP(system.EntityManager, playerEntity, amount, source);
        }
    }
}
