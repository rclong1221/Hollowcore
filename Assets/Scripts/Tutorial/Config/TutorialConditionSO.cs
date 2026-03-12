using UnityEngine;

namespace DIG.Tutorial.Config
{
    /// <summary>
    /// EPIC 18.4: Condition definition for tutorial prerequisites and branching.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Tutorial/Condition", fileName = "NewTutorialCondition")]
    public class TutorialConditionSO : ScriptableObject
    {
        public enum ConditionType : byte
        {
            TutorialCompleted = 0,
            SettingEquals = 1,
            PlayerLevelAbove = 2,
        }

        public enum Comparator : byte
        {
            Equals = 0,
            NotEquals = 1,
            GreaterThan = 2,
            LessThan = 3,
        }

        public ConditionType Type;
        public string TutorialId;
        public string SettingKey;
        public int IntValue;
        public string StringValue;
        public Comparator Compare;
        public bool Invert;

        public bool Evaluate(TutorialService service)
        {
            bool result = Type switch
            {
                ConditionType.TutorialCompleted => service != null && service.IsTutorialCompleted(TutorialId),
                ConditionType.SettingEquals => PlayerPrefs.GetString(SettingKey, "") == StringValue,
                ConditionType.PlayerLevelAbove => PlayerPrefs.GetInt("PlayerLevel", 1) > IntValue,
                _ => false,
            };
            return Invert ? !result : result;
        }
    }
}
