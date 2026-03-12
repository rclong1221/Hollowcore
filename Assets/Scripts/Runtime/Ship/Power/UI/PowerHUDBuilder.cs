using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DIG.Ship.Power.UI
{
    /// <summary>
    /// Automatically creates the Power HUD at runtime.
    /// Attach to a persistent GameObject in the scene.
    /// </summary>
    public class PowerHUDBuilder : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private bool createOnStart = true;

        private PowerHUD powerHUD;

        private void Start()
        {
            if (createOnStart)
            {
                CreatePowerHUD();
            }
        }

        public void CreatePowerHUD()
        {
            // Create Canvas
            var canvasGO = new GameObject("PowerHUDCanvas");
            DontDestroyOnLoad(canvasGO);

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90; // Below cargo UI

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasGO.AddComponent<GraphicRaycaster>();

            // Create HUD Container (top-left corner)
            var hudRoot = new GameObject("PowerHUD");
            hudRoot.transform.SetParent(canvasGO.transform, false);

            var hudRect = hudRoot.AddComponent<RectTransform>();
            hudRect.anchorMin = new Vector2(0, 1);
            hudRect.anchorMax = new Vector2(0, 1);
            hudRect.pivot = new Vector2(0, 1);
            hudRect.anchoredPosition = new Vector2(20, -20);
            hudRect.sizeDelta = new Vector2(300, 120);

            // Background
            var bgImage = hudRoot.AddComponent<Image>();
            bgImage.color = new Color(0.05f, 0.05f, 0.1f, 0.85f);

            // Add vertical layout
            var layout = hudRoot.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(15, 15, 10, 10);
            layout.spacing = 5;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // Title
            var titleGO = CreateTextElement(hudRoot.transform, "ShipStatusTitle", "SHIP STATUS", 16, FontStyles.Bold);
            var titleLayout = titleGO.AddComponent<LayoutElement>();
            titleLayout.preferredHeight = 22;
            var titleText = titleGO.GetComponent<TextMeshProUGUI>();
            titleText.color = new Color(0.7f, 0.8f, 0.9f);

            // Life Support Row
            var lifeSupportRow = CreateRow(hudRoot.transform, "LifeSupportRow");
            var lifeSupportIcon = CreateIcon(lifeSupportRow.transform, "LifeSupportIcon", new Color(0.2f, 0.9f, 0.3f));
            var lifeSupportTextGO = CreateTextElement(lifeSupportRow.transform, "LifeSupportText", "LIFE SUPPORT: ONLINE", 14, FontStyles.Normal);
            var lifeSupportText = lifeSupportTextGO.GetComponent<TextMeshProUGUI>();

            // Power Row
            var powerRow = CreateRow(hudRoot.transform, "PowerRow");
            var powerIcon = CreateIcon(powerRow.transform, "PowerIcon", new Color(1f, 0.8f, 0.2f));
            var powerTextGO = CreateTextElement(powerRow.transform, "PowerText", "POWER: 100/100W", 14, FontStyles.Normal);
            var powerText = powerTextGO.GetComponent<TextMeshProUGUI>();

            // Environment Row
            var envRow = CreateRow(hudRoot.transform, "EnvironmentRow");
            var warningIcon = CreateIcon(envRow.transform, "WarningIcon", new Color(0.9f, 0.3f, 0.2f));
            warningIcon.SetActive(false);
            var envTextGO = CreateTextElement(envRow.transform, "EnvironmentText", "PRESSURIZED", 14, FontStyles.Normal);
            var envText = envTextGO.GetComponent<TextMeshProUGUI>();

            // Add PowerHUD component
            powerHUD = canvasGO.AddComponent<PowerHUD>();

            // Set references via reflection
            SetHUDReferences(powerHUD, hudRoot, lifeSupportText, powerText, envText, 
                lifeSupportIcon.GetComponent<Image>(), warningIcon.GetComponent<Image>());
        }

        private GameObject CreateRow(Transform parent, string name)
        {
            var row = new GameObject(name);
            row.transform.SetParent(parent, false);

            var rect = row.AddComponent<RectTransform>();
            var layoutElem = row.AddComponent<LayoutElement>();
            layoutElem.preferredHeight = 24;

            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            return row;
        }

        private GameObject CreateIcon(Transform parent, string name, Color color)
        {
            var icon = new GameObject(name);
            icon.transform.SetParent(parent, false);

            var rect = icon.AddComponent<RectTransform>();
            var layoutElem = icon.AddComponent<LayoutElement>();
            layoutElem.preferredWidth = 16;
            layoutElem.preferredHeight = 16;

            var image = icon.AddComponent<Image>();
            image.color = color;

            return icon;
        }

        private GameObject CreateTextElement(Transform parent, string name, string text, int fontSize, FontStyles style)
        {
            var textGO = new GameObject(name);
            textGO.transform.SetParent(parent, false);

            var rect = textGO.AddComponent<RectTransform>();
            var layoutElem = textGO.AddComponent<LayoutElement>();
            layoutElem.flexibleWidth = 1;

            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Left;

            return textGO;
        }

        private void SetHUDReferences(PowerHUD hud, GameObject hudRoot, 
            TextMeshProUGUI lifeSupportText, TextMeshProUGUI powerText, TextMeshProUGUI envText,
            Image lifeSupportIcon, Image warningIcon)
        {
            var fields = typeof(PowerHUD).GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (field.Name == "hudRoot" && field.FieldType == typeof(GameObject))
                    field.SetValue(hud, hudRoot);
                else if (field.Name == "lifeSupportText" && field.FieldType == typeof(TextMeshProUGUI))
                    field.SetValue(hud, lifeSupportText);
                else if (field.Name == "powerText" && field.FieldType == typeof(TextMeshProUGUI))
                    field.SetValue(hud, powerText);
                else if (field.Name == "environmentText" && field.FieldType == typeof(TextMeshProUGUI))
                    field.SetValue(hud, envText);
                else if (field.Name == "lifeSupportIcon" && field.FieldType == typeof(Image))
                    field.SetValue(hud, lifeSupportIcon);
                else if (field.Name == "warningIcon" && field.FieldType == typeof(Image))
                    field.SetValue(hud, warningIcon);
            }
        }
    }
}
