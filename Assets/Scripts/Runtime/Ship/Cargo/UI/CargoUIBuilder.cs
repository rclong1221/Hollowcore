using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DIG.Ship.Cargo.UI
{
    /// <summary>
    /// Automatically creates the cargo UI at runtime.
    /// Attach to a persistent GameObject in the scene.
    /// </summary>
    public class CargoUIBuilder : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private bool createOnStart = true;

        private CargoUIPanel uiPanel;
        private CargoUIDataBridge dataBridge;
        private GameObject itemRowPrefab;

        private void Start()
        {
            if (createOnStart)
            {
                CreateCargoUI();
            }
        }

        public void CreateCargoUI()
        {
            // Create data bridge
            dataBridge = gameObject.AddComponent<CargoUIDataBridge>();

            // Create item row prefab
            itemRowPrefab = CargoItemRow.CreateDefaultRowPrefab();
            itemRowPrefab.SetActive(false);
            DontDestroyOnLoad(itemRowPrefab);

            // Create Canvas
            var canvasGO = new GameObject("CargoUICanvas");
            DontDestroyOnLoad(canvasGO);

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasGO.AddComponent<GraphicRaycaster>();

            // Create main panel (hidden by default)
            var panelGO = CreatePanel(canvasGO.transform);
            panelGO.SetActive(false);

            // Create panel content
            var contentGO = CreatePanelContent(panelGO.transform);

            // Add CargoUIPanel component
            uiPanel = canvasGO.AddComponent<CargoUIPanel>();

            // Use reflection or serialize to set fields (simplified: public setters would be cleaner)
            SetPanelReferences(uiPanel, panelGO, contentGO);
        }

        private GameObject CreatePanel(Transform parent)
        {
            var panelGO = new GameObject("CargoPanel");
            panelGO.transform.SetParent(parent, false);

            var rect = panelGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.15f, 0.1f);
            rect.anchorMax = new Vector2(0.85f, 0.9f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = panelGO.AddComponent<Image>();
            image.color = new Color(0.08f, 0.08f, 0.12f, 0.98f);

            // Add rounded corners outline
            var outline = panelGO.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.5f, 0.7f, 0.8f);
            outline.effectDistance = new Vector2(2, -2);

            return panelGO;
        }

        private GameObject CreatePanelContent(Transform parent)
        {
            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(parent, false);

            var rect = contentGO.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(20, 20);
            rect.offsetMax = new Vector2(-20, -20);

            var layout = contentGO.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // Title bar
            CreateTitleBar(contentGO.transform);

            // Columns container
            CreateColumnsContainer(contentGO.transform);

            // Close button
            CreateCloseButton(parent);

            return contentGO;
        }

        private void CreateTitleBar(Transform parent)
        {
            var titleGO = new GameObject("TitleBar");
            titleGO.transform.SetParent(parent, false);

            var rect = titleGO.AddComponent<RectTransform>();
            var layoutElem = titleGO.AddComponent<LayoutElement>();
            layoutElem.preferredHeight = 50;

            var titleText = titleGO.AddComponent<TextMeshProUGUI>();
            titleText.text = "SHIP CARGO";
            titleText.fontSize = 28;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = new Color(0.9f, 0.9f, 0.95f, 1f);
        }

        private void CreateColumnsContainer(Transform parent)
        {
            var columnsGO = new GameObject("Columns");
            columnsGO.transform.SetParent(parent, false);

            var rect = columnsGO.AddComponent<RectTransform>();
            var layoutElem = columnsGO.AddComponent<LayoutElement>();
            layoutElem.flexibleHeight = 1;

            var layout = columnsGO.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 20;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            // Left column (Player Inventory)
            CreateColumn(columnsGO.transform, "Player Inventory", "InventoryColumn", true);

            // Right column (Ship Cargo)
            CreateColumn(columnsGO.transform, "Ship Cargo", "CargoColumn", false);
        }

        private void CreateColumn(Transform parent, string title, string name, bool isInventory)
        {
            var columnGO = new GameObject(name);
            columnGO.transform.SetParent(parent, false);

            var rect = columnGO.AddComponent<RectTransform>();
            var image = columnGO.AddComponent<Image>();
            image.color = new Color(0.12f, 0.12f, 0.18f, 0.9f);

            var layout = columnGO.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 5;
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // Column header
            var headerGO = new GameObject("Header");
            headerGO.transform.SetParent(columnGO.transform, false);
            var headerRect = headerGO.AddComponent<RectTransform>();
            var headerLayout = headerGO.AddComponent<LayoutElement>();
            headerLayout.preferredHeight = 35;

            var headerText = headerGO.AddComponent<TextMeshProUGUI>();
            headerText.text = title;
            headerText.fontSize = 20;
            headerText.fontStyle = FontStyles.Bold;
            headerText.alignment = TextAlignmentOptions.Center;
            headerText.color = isInventory ? new Color(0.4f, 0.7f, 1f) : new Color(0.9f, 0.7f, 0.3f);

            // Scroll view for items
            var scrollGO = CreateScrollView(columnGO.transform, isInventory ? "InventoryScroll" : "CargoScroll");

            // Weight text at bottom
            var weightGO = new GameObject("WeightText");
            weightGO.transform.SetParent(columnGO.transform, false);
            var weightRect = weightGO.AddComponent<RectTransform>();
            var weightLayout = weightGO.AddComponent<LayoutElement>();
            weightLayout.preferredHeight = 25;

            var weightText = weightGO.AddComponent<TextMeshProUGUI>();
            weightText.text = "Weight: 0 / 100 kg";
            weightText.fontSize = 14;
            weightText.alignment = TextAlignmentOptions.Center;
            weightText.color = new Color(0.7f, 0.7f, 0.7f);
        }

        private GameObject CreateScrollView(Transform parent, string name)
        {
            var scrollGO = new GameObject(name);
            scrollGO.transform.SetParent(parent, false);

            var scrollRect = scrollGO.AddComponent<RectTransform>();
            var scrollLayout = scrollGO.AddComponent<LayoutElement>();
            scrollLayout.flexibleHeight = 1;

            var scrollImage = scrollGO.AddComponent<Image>();
            scrollImage.color = new Color(0.08f, 0.08f, 0.1f, 0.8f);

            var scroll = scrollGO.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 20;

            // Viewport
            var viewportGO = new GameObject("Viewport");
            viewportGO.transform.SetParent(scrollGO.transform, false);
            var viewportRect = viewportGO.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;

            var viewportMask = viewportGO.AddComponent<Mask>();
            viewportMask.showMaskGraphic = false;
            var viewportImage = viewportGO.AddComponent<Image>();

            scroll.viewport = viewportRect;

            // Content
            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(viewportGO.transform, false);
            var contentRect = contentGO.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            var contentLayout = contentGO.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 5;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = false;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            var contentSizeFitter = contentGO.AddComponent<ContentSizeFitter>();
            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = contentRect;

            return scrollGO;
        }

        private void CreateCloseButton(Transform parent)
        {
            var btnGO = new GameObject("CloseButton");
            btnGO.transform.SetParent(parent, false);

            var rect = btnGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.anchoredPosition = new Vector2(-10, -10);
            rect.sizeDelta = new Vector2(40, 40);

            var image = btnGO.AddComponent<Image>();
            image.color = new Color(0.6f, 0.2f, 0.2f, 0.9f);

            var btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = image;

            var colors = btn.colors;
            colors.highlightedColor = new Color(0.8f, 0.3f, 0.3f, 1f);
            colors.pressedColor = new Color(0.5f, 0.15f, 0.15f, 1f);
            btn.colors = colors;

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(btnGO.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textGO.AddComponent<TextMeshProUGUI>();
            text.text = "X";
            text.fontSize = 24;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
        }

        private void SetPanelReferences(CargoUIPanel panel, GameObject panelRoot, GameObject content)
        {
            // Find components in hierarchy and assign via reflection
            // This is a workaround since we're creating at runtime
            // In production, you'd use SerializeField with prefabs

            var fields = typeof(CargoUIPanel).GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (field.Name == "panelRoot" && field.FieldType == typeof(GameObject))
                    field.SetValue(panel, panelRoot);
                else if (field.Name == "itemRowPrefab" && field.FieldType == typeof(GameObject))
                    field.SetValue(panel, itemRowPrefab);
                else if (field.Name == "inventoryListContent" && field.FieldType == typeof(Transform))
                {
                    var invScroll = panelRoot.transform.Find("Content/Columns/InventoryColumn/InventoryScroll/Viewport/Content");
                    if (invScroll != null) field.SetValue(panel, invScroll);
                }
                else if (field.Name == "cargoListContent" && field.FieldType == typeof(Transform))
                {
                    var cargoScroll = panelRoot.transform.Find("Content/Columns/CargoColumn/CargoScroll/Viewport/Content");
                    if (cargoScroll != null) field.SetValue(panel, cargoScroll);
                }
                else if (field.Name == "inventoryWeightText" && field.FieldType == typeof(TextMeshProUGUI))
                {
                    var weightText = panelRoot.transform.Find("Content/Columns/InventoryColumn/WeightText");
                    if (weightText != null) field.SetValue(panel, weightText.GetComponent<TextMeshProUGUI>());
                }
                else if (field.Name == "cargoWeightText" && field.FieldType == typeof(TextMeshProUGUI))
                {
                    var weightText = panelRoot.transform.Find("Content/Columns/CargoColumn/WeightText");
                    if (weightText != null) field.SetValue(panel, weightText.GetComponent<TextMeshProUGUI>());
                }
                else if (field.Name == "titleText" && field.FieldType == typeof(TextMeshProUGUI))
                {
                    var titleBar = panelRoot.transform.Find("Content/TitleBar");
                    if (titleBar != null) field.SetValue(panel, titleBar.GetComponent<TextMeshProUGUI>());
                }
                else if (field.Name == "closeButton" && field.FieldType == typeof(Button))
                {
                    var closeBtn = panelRoot.transform.Find("CloseButton");
                    if (closeBtn != null) field.SetValue(panel, closeBtn.GetComponent<Button>());
                }
            }
        }
    }
}
