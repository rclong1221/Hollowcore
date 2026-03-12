using UnityEngine;
using Unity.Entities;
using DIG.Aggro.Components;

namespace DIG.Aggro.Authoring
{
    /// <summary>
    /// EPIC 15.33: Authoring component for social/group aggro behaviors.
    /// Add to enemy prefabs that need linked pull, call-for-help, ally death reactions,
    /// pack hierarchy, guard communication, body discovery, or defender aggro.
    ///
    /// Solo enemies should NOT have this component — they use only AggroAuthoring.
    /// </summary>
    [AddComponentMenu("DIG/Aggro/Social Aggro Authoring")]
    public class SocialAggroAuthoring : MonoBehaviour
    {
        [Header("Group")]
        [Tooltip("Group ID for linked pull. 0 = no group. Same ID across prefabs = linked pull.")]
        public int EncounterGroupId = 0;

        [Tooltip("Aggro one = aggro all in the same EncounterGroupId")]
        public bool LinkedPull = false;

        [Header("Call For Help")]
        [Tooltip("Emit call-for-help when taking damage while aggroed")]
        public bool CallForHelp = true;

        [Tooltip("React to nearby ally call-for-help")]
        public bool RespondToHelp = true;

        [Tooltip("Radius within which call-for-help is heard (meters)")]
        public float CallForHelpRadius = 25f;

        [Tooltip("Minimum seconds between call-for-help emissions")]
        public float CallForHelpCooldown = 3f;

        [Tooltip("Fraction of own threat shared in call-for-help (0-1)")]
        [Range(0f, 1f)]
        public float CallForHelpThreatShare = 0.5f;

        [Header("Ally Reactions")]
        [Tooltip("Add bonus threat on killer when an ally dies")]
        public bool AllyDeathAvenge = true;

        [Tooltip("Flat threat bonus added to killer on ally death")]
        public float AllyDeathThreatBonus = 50f;

        [Tooltip("Multiply all threat entries by this when ally dies. 1.0 = no change.")]
        public bool AllyDeathEnrage = false;

        [Tooltip("Rage multiplier applied on ally death")]
        [Range(1f, 3f)]
        public float AllyDeathRageMultiplier = 1.5f;

        [Tooltip("Continuously share damage threat to nearby allies")]
        public bool ShareDamageInfo = false;

        [Tooltip("Fraction of damage threat shared per frame")]
        [Range(0f, 1f)]
        public float AllyDamagedThreatShare = 0f;

        [Tooltip("Flee when ally or alpha dies")]
        public bool AllyDeathFlee = false;

        [Header("Pack")]
        [Tooltip("Pack hierarchy role")]
        public PackRole Role = PackRole.None;

        [Tooltip("Follow alpha, react specially to alpha death")]
        public bool PackBehavior = false;

        [Header("Stealth")]
        [Tooltip("Relay alert states to other guards")]
        public bool GuardCommunication = false;

        [Tooltip("Discover dead allies, raise alert")]
        public bool BodyDiscovery = false;

        [Header("Defender")]
        [Tooltip("Prioritize entities attacking allied entities (MOBA tower behavior)")]
        public bool DefenderAggro = false;

        class Baker : Baker<SocialAggroAuthoring>
        {
            public override void Bake(SocialAggroAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Build flags
                SocialAggroFlags flags = SocialAggroFlags.None;
                if (authoring.LinkedPull) flags |= SocialAggroFlags.LinkedPull;
                if (authoring.CallForHelp) flags |= SocialAggroFlags.CallForHelp;
                if (authoring.RespondToHelp) flags |= SocialAggroFlags.RespondToHelp;
                if (authoring.ShareDamageInfo) flags |= SocialAggroFlags.ShareDamageInfo;
                if (authoring.AllyDeathAvenge) flags |= SocialAggroFlags.AllyDeathAvenge;
                if (authoring.AllyDeathEnrage) flags |= SocialAggroFlags.AllyDeathEnrage;
                if (authoring.AllyDeathFlee) flags |= SocialAggroFlags.AllyDeathFlee;
                if (authoring.PackBehavior) flags |= SocialAggroFlags.PackBehavior;
                if (authoring.GuardCommunication) flags |= SocialAggroFlags.GuardCommunication;
                if (authoring.BodyDiscovery) flags |= SocialAggroFlags.BodyDiscovery;
                if (authoring.DefenderAggro) flags |= SocialAggroFlags.DefenderAggro;

                AddComponent(entity, new SocialAggroConfig
                {
                    EncounterGroupId = authoring.EncounterGroupId,
                    Flags = flags,
                    CallForHelpRadius = authoring.CallForHelpRadius,
                    CallForHelpCooldown = authoring.CallForHelpCooldown,
                    CallForHelpThreatShare = authoring.CallForHelpThreatShare,
                    AllyDeathThreatBonus = authoring.AllyDeathThreatBonus,
                    AllyDeathRageMultiplier = authoring.AllyDeathRageMultiplier,
                    AllyDamagedThreatShare = authoring.AllyDamagedThreatShare,
                    Role = authoring.Role
                });

                AddComponent(entity, SocialAggroState.Default);
            }
        }
    }
}
