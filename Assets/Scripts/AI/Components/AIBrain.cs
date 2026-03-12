using Unity.Entities;
using DIG.Targeting.Theming;

namespace DIG.AI.Components
{
    public enum AIBrainArchetype : byte
    {
        Melee = 0,
        Ranged = 1,
        Swarm = 2,
        Elite = 3,
        Boss = 4
    }

    public enum AIBehaviorState : byte
    {
        Idle = 0,
        Patrol = 1,
        Investigate = 2,
        Combat = 3,
        Flee = 4,
        ReturnHome = 5
    }

    public enum AICombatSubState : byte
    {
        Approach = 0,
        Attack = 1,
        CircleStrafe = 2,
        Retreat = 3
    }

    /// <summary>
    /// EPIC 15.31: Baked config for AI brain behavior.
    /// Designer-tunable per enemy type via AIBrainAuthoring.
    /// </summary>
    public struct AIBrain : IComponentData
    {
        public AIBrainArchetype Archetype;
        public float MeleeRange;
        public float ChaseSpeed;
        public float PatrolSpeed;
        public float PatrolRadius;
        public float AttackCooldown;
        public float AttackWindUp;
        public float AttackActiveDuration;
        public float AttackRecovery;
        public float FleeHealthPercent;
        public float BaseDamage;
        public float DamageVariance;
        public DamageType DamageType;

        public static AIBrain Default => new AIBrain
        {
            Archetype = AIBrainArchetype.Melee,
            MeleeRange = 2.5f,
            ChaseSpeed = 5.0f,
            PatrolSpeed = 1.5f,
            PatrolRadius = 8.0f,
            AttackCooldown = 1.5f,
            AttackWindUp = 0.4f,
            AttackActiveDuration = 0.15f,
            AttackRecovery = 0.5f,
            FleeHealthPercent = 0.2f,
            BaseDamage = 15f,
            DamageVariance = 5f,
            DamageType = DamageType.Physical
        };
    }
}
