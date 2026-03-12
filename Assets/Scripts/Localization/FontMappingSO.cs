using UnityEngine;
using TMPro;

namespace DIG.Localization
{
    [CreateAssetMenu(menuName = "DIG/Localization/Font Mapping")]
    public class FontMappingSO : ScriptableObject
    {
        [Tooltip("Which locale this mapping applies to.")]
        public string LocaleCode;

        public TMP_FontAsset BodyFont;
        public TMP_FontAsset HeaderFont;
        public TMP_FontAsset TooltipFont;
        public TMP_FontAsset CombatFont;
        public TMP_FontAsset ButtonFont;
        public TMP_FontAsset MonoFont;

        public TMP_FontAsset GetFont(FontStyle style, TMP_FontAsset fallback)
        {
            var font = style switch
            {
                FontStyle.Body => BodyFont,
                FontStyle.Header => HeaderFont,
                FontStyle.Tooltip => TooltipFont,
                FontStyle.Combat => CombatFont,
                FontStyle.Button => ButtonFont,
                FontStyle.Mono => MonoFont,
                _ => null
            };
            return font != null ? font : fallback;
        }
    }
}
