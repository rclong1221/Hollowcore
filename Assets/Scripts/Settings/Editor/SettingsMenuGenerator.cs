using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using DIG.Settings;
using DIG.Core.Input.Keybinds.UI;

namespace DIG.Settings.Editor
{
    /// <summary>
    /// Editor tool to generate the tabbed Settings / Escape menu.
    /// Creates a prefab with Graphics and Controls tabs.
    ///
    /// Menu: DIG/Settings/Create Settings Menu Prefabs
    /// Menu: DIG/Settings/Add Settings Menu to Scene
    /// </summary>
    public static class SettingsMenuGenerator
    {
        private const string PREFAB_FOLDER = "Assets/Prefabs/UI/Settings";
        private const string KEYBIND_PREFAB = "Assets/Prefabs/UI/Keybinds/KeybindPanel.prefab";

        // Color palette (matches KeybindUIGenerator)
        private static readonly Color BG_PANEL = new Color(0.10f, 0.10f, 0.12f, 0.98f);
        private static readonly Color BG_HEADER = new Color(0.08f, 0.08f, 0.10f, 1f);
        private static readonly Color ACCENT = new Color(0.30f, 0.55f, 0.80f, 1f);
        private static readonly Color DANGER = new Color(0.70f, 0.25f, 0.25f, 1f);
        private static readonly Color TEXT_WHITE = new Color(0.95f, 0.95f, 0.97f, 1f);
        private static readonly Color TEXT_GRAY = new Color(0.55f, 0.58f, 0.65f, 1f);
        private static readonly Color TEXT_CYAN = new Color(0.45f, 0.75f, 1f, 1f);
        private static readonly Color BTN_DARK = new Color(0.18f, 0.18f, 0.22f, 1f);
        private static readonly Color TAB_ACTIVE = new Color(0.20f, 0.22f, 0.28f, 1f);
        private static readonly Color TAB_INACTIVE = new Color(0.12f, 0.12f, 0.15f, 1f);
        private static readonly Color ROW_BG = new Color(0.12f, 0.12f, 0.15f, 1f);

        [MenuItem("DIG/Settings/Create Settings Menu Prefab")]
        public static void CreateSettingsMenuPrefab()
        {
            EnsureFolderExists("Assets/Prefabs");
            EnsureFolderExists("Assets/Prefabs/UI");
            EnsureFolderExists(PREFAB_FOLDER);

            CreatePanelPrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[SettingsMenuGenerator] Created settings menu prefab in " + PREFAB_FOLDER);
            EditorUtility.DisplayDialog("Settings Menu Created",
                "Press ESC in-game to toggle the settings menu.\n\n" +
                "Tabs: Graphics, Controls\n\n" +
                "Use 'DIG/Settings/Add Settings Menu to Scene' to add it.",
                "OK");
        }

        [MenuItem("DIG/Settings/Add Settings Menu to Scene")]
        public static void AddSettingsMenuToScene()
        {
            // Ensure EventSystem exists
            var eventSystem = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (eventSystem == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            // Find or create Canvas
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasGO = new GameObject("UICanvas");
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                var scaler = canvasGO.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
                canvasGO.AddComponent<GraphicRaycaster>();
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_FOLDER + "/SettingsMenu.prefab");
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("Error",
                    "Run 'DIG/Settings/Create Settings Menu Prefab' first.", "OK");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
            instance.name = "SettingsMenu";
            instance.transform.SetAsLastSibling();
            Selection.activeGameObject = instance;
        }

        // ========== MAIN PANEL ==========
        private static void CreatePanelPrefab()
        {
            // Root object
            var root = new GameObject("SettingsMenu");
            var rootRT = root.AddComponent<RectTransform>();
            rootRT.anchorMin = Vector2.zero;
            rootRT.anchorMax = Vector2.one;
            rootRT.offsetMin = Vector2.zero;
            rootRT.offsetMax = Vector2.zero;

            // Panel Root (toggled on/off)
            var panelRoot = new GameObject("PanelRoot");
            panelRoot.transform.SetParent(root.transform, false);
            var prRT = panelRoot.AddComponent<RectTransform>();
            prRT.anchorMin = Vector2.zero;
            prRT.anchorMax = Vector2.one;
            prRT.offsetMin = Vector2.zero;
            prRT.offsetMax = Vector2.zero;

            // Dim overlay
            var dim = new GameObject("Dim");
            dim.transform.SetParent(panelRoot.transform, false);
            var dimRT = dim.AddComponent<RectTransform>();
            dimRT.anchorMin = Vector2.zero;
            dimRT.anchorMax = Vector2.one;
            dimRT.offsetMin = Vector2.zero;
            dimRT.offsetMax = Vector2.zero;
            dim.AddComponent<Image>().color = new Color(0, 0, 0, 0.8f);

            // Main panel
            var panel = new GameObject("Panel");
            panel.transform.SetParent(panelRoot.transform, false);
            var panelRT = panel.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.15f, 0.08f);
            panelRT.anchorMax = new Vector2(0.85f, 0.92f);
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;
            panel.AddComponent<Image>().color = BG_PANEL;

            var panelVLG = panel.AddComponent<VerticalLayoutGroup>();
            panelVLG.childAlignment = TextAnchor.UpperCenter;
            panelVLG.childControlWidth = true;
            panelVLG.childControlHeight = true;
            panelVLG.childForceExpandWidth = true;
            panelVLG.childForceExpandHeight = false;
            panelVLG.spacing = 0;

            // ===== HEADER =====
            var header = new GameObject("Header");
            header.transform.SetParent(panel.transform, false);
            header.AddComponent<RectTransform>();
            header.AddComponent<Image>().color = BG_HEADER;
            var headerLE = header.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 80;
            headerLE.flexibleHeight = 0;

            var headerHLG = header.AddComponent<HorizontalLayoutGroup>();
            headerHLG.childAlignment = TextAnchor.MiddleLeft;
            headerHLG.childControlWidth = false;
            headerHLG.childControlHeight = false;
            headerHLG.spacing = 20;
            headerHLG.padding = new RectOffset(32, 32, 0, 0);

            // Title
            var title = new GameObject("Title");
            title.transform.SetParent(header.transform, false);
            title.AddComponent<RectTransform>().sizeDelta = new Vector2(220, 80);
            var titleTMP = title.AddComponent<TextMeshProUGUI>();
            titleTMP.text = "SETTINGS";
            titleTMP.fontSize = 32;
            titleTMP.fontStyle = FontStyles.Bold;
            titleTMP.color = TEXT_WHITE;
            titleTMP.alignment = TextAlignmentOptions.MidlineLeft;
            titleTMP.characterSpacing = 2;
            titleTMP.raycastTarget = false;

            // Spacer
            var hSpacer = new GameObject("Spacer");
            hSpacer.transform.SetParent(header.transform, false);
            hSpacer.AddComponent<RectTransform>().sizeDelta = new Vector2(50, 80);
            hSpacer.AddComponent<LayoutElement>().flexibleWidth = 1;

            // Resume button
            var resumeBtn = CreateButton("ResumeButton", "RESUME", header.transform, ACCENT, 120, 40);

            // Quit button
            var quitBtn = CreateButton("QuitButton", "QUIT", header.transform, DANGER, 80, 40);

            // ===== TAB BAR =====
            var tabBar = new GameObject("TabBar");
            tabBar.transform.SetParent(panel.transform, false);
            tabBar.AddComponent<RectTransform>();
            tabBar.AddComponent<Image>().color = new Color(0.06f, 0.06f, 0.08f, 1f);
            var tabBarLE = tabBar.AddComponent<LayoutElement>();
            tabBarLE.preferredHeight = 48;
            tabBarLE.flexibleHeight = 0;

            var tabBarHLG = tabBar.AddComponent<HorizontalLayoutGroup>();
            tabBarHLG.childAlignment = TextAnchor.MiddleLeft;
            tabBarHLG.childControlWidth = false;
            tabBarHLG.childControlHeight = false;
            tabBarHLG.childForceExpandWidth = false;
            tabBarHLG.spacing = 2;
            tabBarHLG.padding = new RectOffset(32, 32, 0, 0);

            // Tab buttons
            var graphicsTabBtn = CreateTabButton("GraphicsTab", "GRAPHICS", tabBar.transform);
            var controlsTabBtn = CreateTabButton("ControlsTab", "CONTROLS", tabBar.transform);

            // ===== TAB CONTENT AREA =====
            var contentArea = new GameObject("ContentArea");
            contentArea.transform.SetParent(panel.transform, false);
            contentArea.AddComponent<RectTransform>();
            var contentLE = contentArea.AddComponent<LayoutElement>();
            contentLE.flexibleHeight = 1;

            // --- Graphics Tab Panel ---
            var graphicsPanel = CreateGraphicsTabPanel(contentArea.transform);

            // --- Controls Tab Panel ---
            var controlsPanel = CreateControlsTabPanel(contentArea.transform);
            controlsPanel.SetActive(false); // Start with Graphics tab active

            // ===== FOOTER =====
            var footer = new GameObject("Footer");
            footer.transform.SetParent(panel.transform, false);
            footer.AddComponent<RectTransform>();
            footer.AddComponent<Image>().color = BG_HEADER;
            var footerLE = footer.AddComponent<LayoutElement>();
            footerLE.preferredHeight = 40;
            footerLE.flexibleHeight = 0;

            var footerText = new GameObject("FooterText");
            footerText.transform.SetParent(footer.transform, false);
            var ftRT = footerText.AddComponent<RectTransform>();
            ftRT.anchorMin = Vector2.zero;
            ftRT.anchorMax = Vector2.one;
            ftRT.offsetMin = new Vector2(32, 0);
            ftRT.offsetMax = new Vector2(-32, 0);
            var ftTMP = footerText.AddComponent<TextMeshProUGUI>();
            ftTMP.text = "Press ESC to close";
            ftTMP.fontSize = 13;
            ftTMP.color = TEXT_GRAY;
            ftTMP.alignment = TextAlignmentOptions.MidlineLeft;
            ftTMP.raycastTarget = false;

            // ===== WIRE UP PauseMenu COMPONENT =====
            var pauseMenu = root.AddComponent<PauseMenu>();
            var so = new SerializedObject(pauseMenu);
            so.FindProperty("_panelRoot").objectReferenceValue = panelRoot;
            so.FindProperty("_resumeButton").objectReferenceValue = resumeBtn.GetComponent<Button>();
            so.FindProperty("_quitButton").objectReferenceValue = quitBtn.GetComponent<Button>();

            // Tab buttons array
            var tabButtonsProp = so.FindProperty("_tabButtons");
            tabButtonsProp.arraySize = 2;
            tabButtonsProp.GetArrayElementAtIndex(0).objectReferenceValue = graphicsTabBtn.GetComponent<Button>();
            tabButtonsProp.GetArrayElementAtIndex(1).objectReferenceValue = controlsTabBtn.GetComponent<Button>();

            // Tab panels array
            var tabPanelsProp = so.FindProperty("_tabPanels");
            tabPanelsProp.arraySize = 2;
            tabPanelsProp.GetArrayElementAtIndex(0).objectReferenceValue = graphicsPanel;
            tabPanelsProp.GetArrayElementAtIndex(1).objectReferenceValue = controlsPanel;

            // Tab colors
            so.FindProperty("_activeTabColor").colorValue = TAB_ACTIVE;
            so.FindProperty("_inactiveTabColor").colorValue = TAB_INACTIVE;

            so.ApplyModifiedPropertiesWithoutUndo();

            panelRoot.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(root, PREFAB_FOLDER + "/SettingsMenu.prefab");
            Object.DestroyImmediate(root);
        }

        // ========== GRAPHICS TAB ==========
        private static GameObject CreateGraphicsTabPanel(Transform parent)
        {
            var panel = new GameObject("GraphicsPanel");
            panel.transform.SetParent(parent, false);
            var rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 8;
            vlg.padding = new RectOffset(48, 48, 32, 32);

            // Section header
            var sectionHeader = new GameObject("SectionHeader");
            sectionHeader.transform.SetParent(panel.transform, false);
            sectionHeader.AddComponent<RectTransform>().sizeDelta = new Vector2(0, 30);
            var shLE = sectionHeader.AddComponent<LayoutElement>();
            shLE.preferredHeight = 30;
            var shTMP = sectionHeader.AddComponent<TextMeshProUGUI>();
            shTMP.text = "DISPLAY";
            shTMP.fontSize = 13;
            shTMP.color = TEXT_CYAN;
            shTMP.fontStyle = FontStyles.Bold;
            shTMP.characterSpacing = 3;
            shTMP.alignment = TextAlignmentOptions.MidlineLeft;
            shTMP.raycastTarget = false;

            // Window Mode row
            var windowModeRow = new GameObject("WindowModeRow");
            windowModeRow.transform.SetParent(panel.transform, false);
            windowModeRow.AddComponent<RectTransform>();
            windowModeRow.AddComponent<Image>().color = ROW_BG;
            var wmLE = windowModeRow.AddComponent<LayoutElement>();
            wmLE.preferredHeight = 52;

            var wmHLG = windowModeRow.AddComponent<HorizontalLayoutGroup>();
            wmHLG.childAlignment = TextAnchor.MiddleLeft;
            wmHLG.childControlWidth = false;
            wmHLG.childControlHeight = false;
            wmHLG.childForceExpandWidth = false;
            wmHLG.spacing = 16;
            wmHLG.padding = new RectOffset(32, 32, 0, 0);

            // Label
            var wmLabel = new GameObject("Label");
            wmLabel.transform.SetParent(windowModeRow.transform, false);
            wmLabel.AddComponent<RectTransform>().sizeDelta = new Vector2(200, 52);
            var wmTMP = wmLabel.AddComponent<TextMeshProUGUI>();
            wmTMP.text = "Window Mode";
            wmTMP.fontSize = 18;
            wmTMP.color = TEXT_WHITE;
            wmTMP.alignment = TextAlignmentOptions.MidlineLeft;
            wmTMP.raycastTarget = false;

            // Spacer
            var wmSpacer = new GameObject("Spacer");
            wmSpacer.transform.SetParent(windowModeRow.transform, false);
            wmSpacer.AddComponent<RectTransform>().sizeDelta = new Vector2(50, 52);
            wmSpacer.AddComponent<LayoutElement>().flexibleWidth = 1;

            // Dropdown
            var windowModeDropdown = CreateDropdown("WindowModeDropdown", windowModeRow.transform);

            // Wire up GraphicsSettingsPanel
            var gsp = panel.AddComponent<GraphicsSettingsPanel>();
            var gspSO = new SerializedObject(gsp);
            gspSO.FindProperty("_windowModeDropdown").objectReferenceValue = windowModeDropdown.GetComponent<TMP_Dropdown>();
            gspSO.ApplyModifiedPropertiesWithoutUndo();

            return panel;
        }

        // ========== CONTROLS TAB ==========
        private static GameObject CreateControlsTabPanel(Transform parent)
        {
            var panel = new GameObject("ControlsPanel");
            panel.transform.SetParent(parent, false);
            var rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Try to embed existing KeybindPanel prefab
            var keybindPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(KEYBIND_PREFAB);
            if (keybindPrefab != null)
            {
                var keybindInstance = (GameObject)PrefabUtility.InstantiatePrefab(keybindPrefab, panel.transform);
                keybindInstance.name = "KeybindPanel";

                // Get the KeybindPanel component and disable its own ESC/F1 toggle
                // (PauseMenu handles ESC now)
                var kp = keybindInstance.GetComponent<KeybindPanel>();
                if (kp != null)
                {
                    var kpSO = new SerializedObject(kp);
                    kpSO.FindProperty("_startHidden").boolValue = false;
                    kpSO.FindProperty("_enableKeyboardToggle").boolValue = false;
                    kpSO.ApplyModifiedPropertiesWithoutUndo();
                }

                // Make the keybind panel's own _panelRoot start active
                // (visibility is controlled by PauseMenu tab switching)
                var panelRootTransform = keybindInstance.transform.Find("PanelRoot");
                if (panelRootTransform != null)
                    panelRootTransform.gameObject.SetActive(true);
            }
            else
            {
                // Fallback: create placeholder text
                var placeholder = new GameObject("Placeholder");
                placeholder.transform.SetParent(panel.transform, false);
                var phRT = placeholder.AddComponent<RectTransform>();
                phRT.anchorMin = Vector2.zero;
                phRT.anchorMax = Vector2.one;
                phRT.offsetMin = Vector2.zero;
                phRT.offsetMax = Vector2.zero;
                var phTMP = placeholder.AddComponent<TextMeshProUGUI>();
                phTMP.text = "Run 'DIG/Input/Create Keybind UI Prefabs' first,\nthen recreate this menu.";
                phTMP.fontSize = 18;
                phTMP.color = TEXT_GRAY;
                phTMP.alignment = TextAlignmentOptions.Center;
                phTMP.raycastTarget = false;
            }

            return panel;
        }

        // ========== HELPERS ==========

        private static void EnsureFolderExists(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parts = path.Split('/');
                var current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    var next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }
        }

        private static GameObject CreateTabButton(string name, string text, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(140, 48);

            var img = go.AddComponent<Image>();
            img.color = TAB_INACTIVE;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            btn.colors = colors;

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;
            var textTMP = textGO.AddComponent<TextMeshProUGUI>();
            textTMP.text = text;
            textTMP.fontSize = 15;
            textTMP.fontStyle = FontStyles.Bold;
            textTMP.color = TEXT_WHITE;
            textTMP.characterSpacing = 2;
            textTMP.alignment = TextAlignmentOptions.Center;
            textTMP.raycastTarget = false;

            return go;
        }

        private static GameObject CreateButton(string name, string text, Transform parent, Color bgColor, float w, float h)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>().sizeDelta = new Vector2(w, h);

            var img = go.AddComponent<Image>();
            img.color = bgColor;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = colors;

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;
            var textTMP = textGO.AddComponent<TextMeshProUGUI>();
            textTMP.text = text;
            textTMP.fontSize = 14;
            textTMP.fontStyle = FontStyles.Bold;
            textTMP.color = TEXT_WHITE;
            textTMP.alignment = TextAlignmentOptions.Center;
            textTMP.raycastTarget = false;

            return go;
        }

        private static GameObject CreateDropdown(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>().sizeDelta = new Vector2(220, 40);

            go.AddComponent<Image>().color = BTN_DARK;
            var dd = go.AddComponent<TMP_Dropdown>();

            var label = new GameObject("Label");
            label.transform.SetParent(go.transform, false);
            var lrt = label.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(16, 0);
            lrt.offsetMax = new Vector2(-32, 0);
            var ltmp = label.AddComponent<TextMeshProUGUI>();
            ltmp.text = "Fullscreen";
            ltmp.fontSize = 14;
            ltmp.color = TEXT_WHITE;
            ltmp.alignment = TextAlignmentOptions.MidlineLeft;
            dd.captionText = ltmp;

            var arrow = new GameObject("Arrow");
            arrow.transform.SetParent(go.transform, false);
            var art = arrow.AddComponent<RectTransform>();
            art.anchorMin = new Vector2(1, 0);
            art.anchorMax = Vector2.one;
            art.offsetMin = new Vector2(-28, 0);
            art.offsetMax = new Vector2(-8, 0);
            var atmp = arrow.AddComponent<TextMeshProUGUI>();
            atmp.text = "▼";
            atmp.fontSize = 10;
            atmp.color = TEXT_GRAY;
            atmp.alignment = TextAlignmentOptions.Center;

            // Template
            var template = new GameObject("Template");
            template.transform.SetParent(go.transform, false);
            var trt = template.AddComponent<RectTransform>();
            trt.anchorMin = new Vector2(0, 0);
            trt.anchorMax = new Vector2(1, 0);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.sizeDelta = new Vector2(0, 150);
            template.AddComponent<Image>().color = BG_PANEL;
            var sr = template.AddComponent<ScrollRect>();

            var vp = new GameObject("Viewport");
            vp.transform.SetParent(template.transform, false);
            var vprt = vp.AddComponent<RectTransform>();
            vprt.anchorMin = Vector2.zero;
            vprt.anchorMax = Vector2.one;
            vprt.offsetMin = Vector2.zero;
            vprt.offsetMax = Vector2.zero;
            vp.AddComponent<Mask>().showMaskGraphic = false;
            vp.AddComponent<Image>();

            var content = new GameObject("Content");
            content.transform.SetParent(vp.transform, false);
            var crt = content.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(0, 1);
            crt.anchorMax = Vector2.one;
            crt.pivot = new Vector2(0.5f, 1f);
            crt.sizeDelta = new Vector2(0, 0);

            var contentVLG = content.AddComponent<VerticalLayoutGroup>();
            contentVLG.childAlignment = TextAnchor.UpperCenter;
            contentVLG.childControlWidth = true;
            contentVLG.childControlHeight = false;
            contentVLG.childForceExpandWidth = true;
            contentVLG.childForceExpandHeight = false;
            contentVLG.spacing = 0;

            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var item = new GameObject("Item");
            item.transform.SetParent(content.transform, false);
            item.AddComponent<RectTransform>().sizeDelta = new Vector2(0, 36);
            item.AddComponent<Toggle>();
            item.AddComponent<Image>().color = ROW_BG;

            var itemLabel = new GameObject("Item Label");
            itemLabel.transform.SetParent(item.transform, false);
            var ilrt = itemLabel.AddComponent<RectTransform>();
            ilrt.anchorMin = Vector2.zero;
            ilrt.anchorMax = Vector2.one;
            ilrt.offsetMin = new Vector2(16, 0);
            ilrt.offsetMax = Vector2.zero;
            var iltmp = itemLabel.AddComponent<TextMeshProUGUI>();
            iltmp.text = "Option";
            iltmp.fontSize = 14;
            iltmp.color = TEXT_WHITE;
            iltmp.alignment = TextAlignmentOptions.MidlineLeft;

            sr.content = crt;
            sr.viewport = vprt;
            dd.template = trt;
            dd.itemText = iltmp;

            template.SetActive(false);

            return go;
        }
    }
}
