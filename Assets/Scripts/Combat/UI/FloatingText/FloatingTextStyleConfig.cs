using UnityEngine;

namespace DIG.Combat.UI.FloatingText
{
    /// <summary>
    /// EPIC 15.9: Configuration for floating text visual styles.
    /// </summary>
    [CreateAssetMenu(fileName = "FloatingTextStyleConfig", menuName = "DIG/Combat/Floating Text Style Config")]
    public class FloatingTextStyleConfig : ScriptableObject
    {
        [System.Serializable]
        public class TextStyleData
        {
            public FloatingTextStyle Style;
            public Color Color = Color.white;
            public float FontSize = 24f;
            public float Duration = 1.5f;
            public float RiseSpeed = 1f;
            public float FadeStartTime = 0.8f;
            public AnimationCurve ScaleCurve = AnimationCurve.EaseInOut(0, 0.5f, 0.2f, 1f);
            public bool UseOutline = true;
            public Color OutlineColor = Color.black;
        }
        
        [Header("Style Definitions")]
        [SerializeField] private TextStyleData normalStyle = new TextStyleData
        {
            Style = FloatingTextStyle.Normal,
            Color = Color.white,
            FontSize = 24f,
            Duration = 1.5f
        };
        
        [SerializeField] private TextStyleData importantStyle = new TextStyleData
        {
            Style = FloatingTextStyle.Important,
            Color = Color.yellow,
            FontSize = 32f,
            Duration = 2f
        };
        
        [SerializeField] private TextStyleData warningStyle = new TextStyleData
        {
            Style = FloatingTextStyle.Warning,
            Color = new Color(1f, 0.5f, 0f), // Orange
            FontSize = 28f,
            Duration = 2f
        };
        
        [SerializeField] private TextStyleData successStyle = new TextStyleData
        {
            Style = FloatingTextStyle.Success,
            Color = Color.green,
            FontSize = 30f,
            Duration = 2f
        };
        
        [SerializeField] private TextStyleData failureStyle = new TextStyleData
        {
            Style = FloatingTextStyle.Failure,
            Color = Color.gray,
            FontSize = 22f,
            Duration = 1f
        };
        
        [Header("Status Effect Colors")]
        [SerializeField] private Color burnColor = new Color(1f, 0.4f, 0f);
        [SerializeField] private Color bleedColor = new Color(0.8f, 0f, 0f);
        [SerializeField] private Color poisonColor = new Color(0.5f, 0.8f, 0.2f);
        [SerializeField] private Color freezeColor = new Color(0.5f, 0.8f, 1f);
        [SerializeField] private Color stunColor = new Color(1f, 1f, 0.3f);
        [SerializeField] private Color buffColor = new Color(0.3f, 1f, 0.5f);
        [SerializeField] private Color debuffColor = new Color(0.8f, 0.3f, 0.8f);
        
        public TextStyleData GetStyle(FloatingTextStyle style)
        {
            return style switch
            {
                FloatingTextStyle.Important => importantStyle,
                FloatingTextStyle.Warning => warningStyle,
                FloatingTextStyle.Success => successStyle,
                FloatingTextStyle.Failure => failureStyle,
                _ => normalStyle
            };
        }
        
        public Color GetStatusEffectColor(StatusEffectType status)
        {
            return status switch
            {
                StatusEffectType.Burn => burnColor,
                StatusEffectType.Bleed => bleedColor,
                StatusEffectType.Poison => poisonColor,
                StatusEffectType.Frostbite => freezeColor,
                StatusEffectType.Freeze => freezeColor,
                StatusEffectType.Stun => stunColor,
                StatusEffectType.Slow => freezeColor,
                StatusEffectType.Root => debuffColor,
                StatusEffectType.Silence => debuffColor,
                StatusEffectType.Blind => debuffColor,
                StatusEffectType.Haste => buffColor,
                StatusEffectType.Shield => new Color(0.3f, 0.7f, 1f),
                StatusEffectType.Regen => buffColor,
                StatusEffectType.Strength => buffColor,
                StatusEffectType.Armor => buffColor,
                StatusEffectType.Weakness => debuffColor,
                StatusEffectType.Vulnerable => debuffColor,
                StatusEffectType.Exposed => debuffColor,
                StatusEffectType.Marked => new Color(1f, 0.3f, 0.3f),
                StatusEffectType.Fear => debuffColor,
                _ => Color.white
            };
        }
        
        public string GetStatusEffectName(StatusEffectType status)
        {
            return status switch
            {
                StatusEffectType.Burn => "BURNING",
                StatusEffectType.Bleed => "BLEEDING",
                StatusEffectType.Poison => "POISONED",
                StatusEffectType.Frostbite => "FROSTBITTEN",
                StatusEffectType.Freeze => "FROZEN",
                StatusEffectType.Stun => "STUNNED",
                StatusEffectType.Slow => "SLOWED",
                StatusEffectType.Root => "ROOTED",
                StatusEffectType.Silence => "SILENCED",
                StatusEffectType.Blind => "BLINDED",
                StatusEffectType.Haste => "HASTE",
                StatusEffectType.Shield => "SHIELDED",
                StatusEffectType.Regen => "REGENERATING",
                StatusEffectType.Strength => "EMPOWERED",
                StatusEffectType.Armor => "ARMORED",
                StatusEffectType.Weakness => "WEAKENED",
                StatusEffectType.Vulnerable => "VULNERABLE",
                StatusEffectType.Exposed => "EXPOSED",
                StatusEffectType.Marked => "MARKED",
                StatusEffectType.Fear => "FEARED",
                _ => status.ToString().ToUpper()
            };
        }
        
        public string GetCombatVerbText(CombatVerb verb)
        {
            return verb switch
            {
                CombatVerb.Parry => "PARRY!",
                CombatVerb.Counter => "COUNTER!",
                CombatVerb.PerfectBlock => "PERFECT BLOCK!",
                CombatVerb.Finisher => "FINISHER!",
                CombatVerb.Combo => "COMBO!",
                CombatVerb.Immune => "IMMUNE",
                CombatVerb.Resist => "RESIST",
                CombatVerb.Absorb => "ABSORBED",
                CombatVerb.Evade => "EVADE!",
                CombatVerb.Riposte => "RIPOSTE!",
                _ => verb.ToString().ToUpper()
            };
        }
    }
}
