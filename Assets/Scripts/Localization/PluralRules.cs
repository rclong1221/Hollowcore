namespace DIG.Localization
{
    public enum PluralCategory : byte
    {
        One = 0,
        Few = 1,
        Many = 2,
        Other = 3
    }

    public enum PluralRuleSet : byte
    {
        English = 0,
        French = 1,
        Japanese = 2,
        Arabic = 3,
        Polish = 4,
        Russian = 5
    }

    public enum TextDirection : byte
    {
        LTR = 0,
        RTL = 1
    }

    public enum FontStyle : byte
    {
        Body = 0,
        Header = 1,
        Tooltip = 2,
        Combat = 3,
        Button = 4,
        Mono = 5
    }

    public interface IPluralRule
    {
        PluralCategory Evaluate(int count);
    }

    public class EnglishPluralRule : IPluralRule
    {
        public PluralCategory Evaluate(int count) =>
            count == 1 ? PluralCategory.One : PluralCategory.Other;
    }

    public class FrenchPluralRule : IPluralRule
    {
        public PluralCategory Evaluate(int count) =>
            count <= 1 ? PluralCategory.One : PluralCategory.Other;
    }

    public class JapanesePluralRule : IPluralRule
    {
        public PluralCategory Evaluate(int count) => PluralCategory.Other;
    }

    public class ArabicPluralRule : IPluralRule
    {
        public PluralCategory Evaluate(int count)
        {
            if (count == 0) return PluralCategory.Other;
            if (count == 1) return PluralCategory.One;
            if (count == 2) return PluralCategory.One;
            if (count >= 3 && count <= 10) return PluralCategory.Few;
            if (count >= 11 && count <= 99) return PluralCategory.Many;
            return PluralCategory.Other;
        }
    }

    public class PolishPluralRule : IPluralRule
    {
        public PluralCategory Evaluate(int count)
        {
            if (count == 1) return PluralCategory.One;
            int mod10 = count % 10;
            int mod100 = count % 100;
            if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14))
                return PluralCategory.Few;
            return PluralCategory.Many;
        }
    }

    public class RussianPluralRule : IPluralRule
    {
        public PluralCategory Evaluate(int count)
        {
            int mod10 = count % 10;
            int mod100 = count % 100;
            if (mod10 == 1 && mod100 != 11) return PluralCategory.One;
            if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14))
                return PluralCategory.Few;
            return PluralCategory.Many;
        }
    }

    public static class PluralRuleFactory
    {
        public static IPluralRule Create(PluralRuleSet ruleSet)
        {
            return ruleSet switch
            {
                PluralRuleSet.English => new EnglishPluralRule(),
                PluralRuleSet.French => new FrenchPluralRule(),
                PluralRuleSet.Japanese => new JapanesePluralRule(),
                PluralRuleSet.Arabic => new ArabicPluralRule(),
                PluralRuleSet.Polish => new PolishPluralRule(),
                PluralRuleSet.Russian => new RussianPluralRule(),
                _ => new EnglishPluralRule()
            };
        }
    }
}
