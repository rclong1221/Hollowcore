using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using DIG.Core.Input.Keybinds.UI;

namespace DIG.Core.Input.Keybinds.Editor
{
    /// <summary>
    /// Editor tool to generate premium AAA-quality Keybind UI prefabs.
    /// Fixed layout with proper anchoring and responsive design.
    /// 
    /// EPIC 15.21 Phase 6: Keybind UI
    /// </summary>
    public static class KeybindUIGenerator
    {
        private const string PREFAB_FOLDER = "Assets/Prefabs/UI/Keybinds";
        
        // Premium color palette
        private static readonly Color BG_PANEL = new Color(0.10f, 0.10f, 0.12f, 0.98f);
        private static readonly Color BG_HEADER = new Color(0.08f, 0.08f, 0.10f, 1f);
        private static readonly Color ACCENT = new Color(0.30f, 0.55f, 0.80f, 1f);
        private static readonly Color DANGER = new Color(0.70f, 0.25f, 0.25f, 1f);
        private static readonly Color TEXT_WHITE = new Color(0.95f, 0.95f, 0.97f, 1f);
        private static readonly Color TEXT_GRAY = new Color(0.55f, 0.58f, 0.65f, 1f);
        private static readonly Color TEXT_CYAN = new Color(0.45f, 0.75f, 1f, 1f);
        private static readonly Color ROW_BG = new Color(0.12f, 0.12f, 0.15f, 1f);
        private static readonly Color BTN_DARK = new Color(0.18f, 0.18f, 0.22f, 1f);
        private static readonly Color KEY_BG = new Color(0.08f, 0.12f, 0.16f, 1f);
        private static readonly Color KEY_BORDER = new Color(0.35f, 0.60f, 0.85f, 1f);
        
        [MenuItem("DIG/Input/Create Keybind UI Prefabs")]
        public static void CreateKeybindUIPrefabs()
        {
            EnsureFolderExists("Assets/Prefabs");
            EnsureFolderExists("Assets/Prefabs/UI");
            EnsureFolderExists(PREFAB_FOLDER);
            
            CreateRowPrefab();
            CreateCategoryHeaderPrefab();
            CreateOverlayPrefab();
            CreatePanelPrefab();
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log("[KeybindUIGenerator] Created keybind UI prefabs in " + PREFAB_FOLDER);
            EditorUtility.DisplayDialog("Keybind UI Created", 
                "Press ESC or F1 in-game to toggle the keybind panel.", 
                "OK");
        }
        
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
        
        [MenuItem("DIG/Input/Add Keybind Panel to Scene")]
        public static void AddKeybindPanelToScene()
        {
            var eventSystem = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (eventSystem == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
            
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasGO = new GameObject("KeybindCanvas");
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                var scaler = canvasGO.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
                canvasGO.AddComponent<GraphicRaycaster>();
            }
            else if (canvas.GetComponent<GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            }
            
            var panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_FOLDER + "/KeybindPanel.prefab");
            if (panelPrefab == null)
            {
                EditorUtility.DisplayDialog("Error", "Run 'Create Keybind UI Prefabs' first.", "OK");
                return;
            }
            
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab, canvas.transform);
            instance.name = "KeybindPanel";
            instance.transform.SetAsLastSibling();
            Selection.activeGameObject = instance;
        }
        
        // ========== ROW PREFAB ==========
        private static void CreateRowPrefab()
        {
            var root = new GameObject("KeybindRow");
            var rootRT = root.AddComponent<RectTransform>();
            rootRT.sizeDelta = new Vector2(0, 52);
            
            // Make row stretch to fill width
            var rootLE = root.AddComponent<LayoutElement>();
            rootLE.minWidth = 600;
            rootLE.flexibleWidth = 1;
            rootLE.preferredHeight = 52;
            
            // Background
            var bg = root.AddComponent<Image>();
            bg.color = ROW_BG;
            
            // Horizontal layout
            var hlg = root.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.spacing = 16;
            hlg.padding = new RectOffset(32, 32, 0, 0);
            
            // Action Name
            var nameGO = new GameObject("ActionName");
            nameGO.transform.SetParent(root.transform, false);
            var nameRT = nameGO.AddComponent<RectTransform>();
            nameRT.sizeDelta = new Vector2(280, 52);
            var nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
            nameTMP.text = "Action Name";
            nameTMP.fontSize = 18;
            nameTMP.color = TEXT_WHITE;
            nameTMP.alignment = TextAlignmentOptions.MidlineLeft;
            nameTMP.raycastTarget = false;
            
            // Flexible spacer
            var spacer = new GameObject("Spacer");
            spacer.transform.SetParent(root.transform, false);
            var spacerRT = spacer.AddComponent<RectTransform>();
            spacerRT.sizeDelta = new Vector2(50, 52);
            var spacerLE = spacer.AddComponent<LayoutElement>();
            spacerLE.flexibleWidth = 1;
            
            // Key Badge
            var badge = new GameObject("KeyBadge");
            badge.transform.SetParent(root.transform, false);
            var badgeRT = badge.AddComponent<RectTransform>();
            badgeRT.sizeDelta = new Vector2(120, 36);
            var badgeBG = badge.AddComponent<Image>();
            badgeBG.color = KEY_BG;
            var badgeOutline = badge.AddComponent<Outline>();
            badgeOutline.effectColor = KEY_BORDER;
            badgeOutline.effectDistance = new Vector2(2, 2);
            
            var bindingText = new GameObject("BindingText");
            bindingText.transform.SetParent(badge.transform, false);
            var bindingRT = bindingText.AddComponent<RectTransform>();
            bindingRT.anchorMin = Vector2.zero;
            bindingRT.anchorMax = Vector2.one;
            bindingRT.offsetMin = Vector2.zero;
            bindingRT.offsetMax = Vector2.zero;
            var bindingTMP = bindingText.AddComponent<TextMeshProUGUI>();
            bindingTMP.text = "[W]";
            bindingTMP.fontSize = 16;
            bindingTMP.color = TEXT_CYAN;
            bindingTMP.alignment = TextAlignmentOptions.Center;
            bindingTMP.fontStyle = FontStyles.Bold;
            bindingTMP.raycastTarget = false;
            
            // Rebind Button
            var rebindBtn = CreateButton("RebindButton", "REBIND", root.transform, ACCENT, 100, 36);
            
            // Reset Button
            var resetBtn = CreateButton("ResetButton", "RESET", root.transform, BTN_DARK, 80, 36);
            resetBtn.GetComponentInChildren<TextMeshProUGUI>().color = TEXT_GRAY;
            
            // Add component and wire up
            var rowComp = root.AddComponent<KeybindRow>();
            var so = new SerializedObject(rowComp);
            so.FindProperty("_actionNameText").objectReferenceValue = nameTMP;
            so.FindProperty("_bindingText").objectReferenceValue = bindingTMP;
            so.FindProperty("_rebindButton").objectReferenceValue = rebindBtn.GetComponent<Button>();
            so.FindProperty("_resetButton").objectReferenceValue = resetBtn.GetComponent<Button>();
            so.ApplyModifiedPropertiesWithoutUndo();
            
            PrefabUtility.SaveAsPrefabAsset(root, PREFAB_FOLDER + "/KeybindRow.prefab");
            Object.DestroyImmediate(root);
        }
        
        // ========== CATEGORY HEADER ==========
        private static void CreateCategoryHeaderPrefab()
        {
            var root = new GameObject("CategoryHeader");
            var rootRT = root.AddComponent<RectTransform>();
            rootRT.sizeDelta = new Vector2(0, 40);
            
            // Make header stretch to fill width
            var rootLE = root.AddComponent<LayoutElement>();
            rootLE.minWidth = 600;
            rootLE.flexibleWidth = 1;
            rootLE.preferredHeight = 40;
            
            var hlg = root.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.padding = new RectOffset(32, 32, 12, 0);
            hlg.spacing = 16;
            hlg.childControlHeight = false;
            hlg.childControlWidth = false;
            
            var textGO = new GameObject("CategoryText");
            textGO.transform.SetParent(root.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            textRT.sizeDelta = new Vector2(200, 28);
            var textTMP = textGO.AddComponent<TextMeshProUGUI>();
            textTMP.text = "MOVEMENT";
            textTMP.fontSize = 13;
            textTMP.color = TEXT_CYAN;
            textTMP.fontStyle = FontStyles.Bold;
            textTMP.characterSpacing = 3;
            textTMP.alignment = TextAlignmentOptions.MidlineLeft;
            textTMP.raycastTarget = false;
            
            // Line
            var line = new GameObject("Line");
            line.transform.SetParent(root.transform, false);
            var lineRT = line.AddComponent<RectTransform>();
            lineRT.sizeDelta = new Vector2(500, 1);
            var lineImg = line.AddComponent<Image>();
            lineImg.color = new Color(TEXT_CYAN.r, TEXT_CYAN.g, TEXT_CYAN.b, 0.4f);
            var lineLE = line.AddComponent<LayoutElement>();
            lineLE.flexibleWidth = 1;
            lineLE.preferredHeight = 1;
            
            PrefabUtility.SaveAsPrefabAsset(root, PREFAB_FOLDER + "/CategoryHeader.prefab");
            Object.DestroyImmediate(root);
        }
        
        // ========== OVERLAY ==========
        private static void CreateOverlayPrefab()
        {
            var root = new GameObject("RebindOverlay");
            var rootRT = root.AddComponent<RectTransform>();
            rootRT.anchorMin = Vector2.zero;
            rootRT.anchorMax = Vector2.one;
            rootRT.offsetMin = Vector2.zero;
            rootRT.offsetMax = Vector2.zero;
            
            // Dim background
            var dim = new GameObject("Dim");
            dim.transform.SetParent(root.transform, false);
            var dimRT = dim.AddComponent<RectTransform>();
            dimRT.anchorMin = Vector2.zero;
            dimRT.anchorMax = Vector2.one;
            dimRT.offsetMin = Vector2.zero;
            dimRT.offsetMax = Vector2.zero;
            dim.AddComponent<Image>().color = new Color(0, 0, 0, 0.9f);
            
            // Modal
            var modal = new GameObject("Modal");
            modal.transform.SetParent(root.transform, false);
            var modalRT = modal.AddComponent<RectTransform>();
            modalRT.sizeDelta = new Vector2(480, 240);
            modalRT.anchoredPosition = Vector2.zero;
            modal.AddComponent<Image>().color = BG_PANEL;
            
            var vlg = modal.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.spacing = 16;
            vlg.padding = new RectOffset(40, 40, 40, 40);
            
            // Prompt
            var promptGO = new GameObject("PromptText");
            promptGO.transform.SetParent(modal.transform, false);
            promptGO.AddComponent<RectTransform>().sizeDelta = new Vector2(400, 50);
            var promptTMP = promptGO.AddComponent<TextMeshProUGUI>();
            promptTMP.text = "Press any key...";
            promptTMP.fontSize = 28;
            promptTMP.color = TEXT_WHITE;
            promptTMP.alignment = TextAlignmentOptions.Center;
            promptTMP.raycastTarget = false;
            promptGO.AddComponent<LayoutElement>().preferredHeight = 50;
            
            // Action name (hidden)
            var actionGO = new GameObject("ActionName");
            actionGO.transform.SetParent(modal.transform, false);
            actionGO.AddComponent<RectTransform>();
            var actionTMP = actionGO.AddComponent<TextMeshProUGUI>();
            actionTMP.fontSize = 16;
            actionTMP.color = TEXT_GRAY;
            actionGO.SetActive(false);
            
            // Hint
            var hintGO = new GameObject("Hint");
            hintGO.transform.SetParent(modal.transform, false);
            hintGO.AddComponent<RectTransform>().sizeDelta = new Vector2(400, 30);
            var hintTMP = hintGO.AddComponent<TextMeshProUGUI>();
            hintTMP.text = "Press ESC to cancel";
            hintTMP.fontSize = 14;
            hintTMP.color = TEXT_GRAY;
            hintTMP.alignment = TextAlignmentOptions.Center;
            hintTMP.raycastTarget = false;
            
            // Cancel button
            var cancelBtn = CreateButton("CancelButton", "CANCEL", modal.transform, BTN_DARK, 140, 40);
            cancelBtn.AddComponent<LayoutElement>().preferredHeight = 40;
            
            var comp = root.AddComponent<RebindOverlay>();
            var so = new SerializedObject(comp);
            so.FindProperty("_overlayRoot").objectReferenceValue = root;
            so.FindProperty("_promptText").objectReferenceValue = promptTMP;
            so.FindProperty("_actionNameText").objectReferenceValue = actionTMP;
            so.FindProperty("_cancelButton").objectReferenceValue = cancelBtn.GetComponent<Button>();
            so.ApplyModifiedPropertiesWithoutUndo();
            
            PrefabUtility.SaveAsPrefabAsset(root, PREFAB_FOLDER + "/RebindOverlay.prefab");
            Object.DestroyImmediate(root);
        }
        
        // ========== MAIN PANEL ==========
        private static void CreatePanelPrefab()
        {
            var rowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_FOLDER + "/KeybindRow.prefab");
            var headerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_FOLDER + "/CategoryHeader.prefab");
            var overlayPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_FOLDER + "/RebindOverlay.prefab");
            
            // Outer
            var outer = new GameObject("KeybindPanel");
            var outerRT = outer.AddComponent<RectTransform>();
            outerRT.anchorMin = Vector2.zero;
            outerRT.anchorMax = Vector2.one;
            outerRT.offsetMin = Vector2.zero;
            outerRT.offsetMax = Vector2.zero;
            
            // Panel Root (toggled)
            var panelRoot = new GameObject("PanelRoot");
            panelRoot.transform.SetParent(outer.transform, false);
            var prRT = panelRoot.AddComponent<RectTransform>();
            prRT.anchorMin = Vector2.zero;
            prRT.anchorMax = Vector2.one;
            prRT.offsetMin = Vector2.zero;
            prRT.offsetMax = Vector2.zero;
            
            // Dim
            var dim = new GameObject("Dim");
            dim.transform.SetParent(panelRoot.transform, false);
            var dimRT = dim.AddComponent<RectTransform>();
            dimRT.anchorMin = Vector2.zero;
            dimRT.anchorMax = Vector2.one;
            dimRT.offsetMin = Vector2.zero;
            dimRT.offsetMax = Vector2.zero;
            dim.AddComponent<Image>().color = new Color(0, 0, 0, 0.8f);
            
            // Main panel box
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
            panelVLG.childControlHeight = true;  // IMPORTANT: must be true for flexibleHeight to work
            panelVLG.childForceExpandWidth = true;
            panelVLG.childForceExpandHeight = false;  // Only expand items with flexibleHeight > 0
            panelVLG.spacing = 0;
            panelVLG.padding = new RectOffset(0, 0, 0, 0);
            
            // ===== HEADER =====
            var header = new GameObject("Header");
            header.transform.SetParent(panel.transform, false);
            header.AddComponent<RectTransform>();
            header.AddComponent<Image>().color = BG_HEADER;
            var headerLE = header.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 80;
            headerLE.flexibleHeight = 0;  // Don't expand
            
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
            titleTMP.text = "CONTROLS";
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
            
            // Dropdown
            var dropdown = CreateDropdown("Dropdown", header.transform);
            
            // Reset All
            var resetAll = CreateButton("ResetAllButton", "⚠ RESET ALL", header.transform, DANGER, 140, 40);
            
            // Close
            var close = CreateButton("CloseButton", "✕", header.transform, BTN_DARK, 48, 40);
            close.GetComponentInChildren<TextMeshProUGUI>().fontSize = 24;
            
            // ===== SCROLL VIEW =====
            var scroll = CreateScrollView("ScrollView", panel.transform);
            scroll.AddComponent<LayoutElement>().flexibleHeight = 1;
            var content = scroll.transform.Find("Viewport/Content");
            
            // ===== FOOTER =====
            var footer = new GameObject("Footer");
            footer.transform.SetParent(panel.transform, false);
            footer.AddComponent<RectTransform>();
            footer.AddComponent<Image>().color = BG_HEADER;
            var footerLE = footer.AddComponent<LayoutElement>();
            footerLE.preferredHeight = 40;
            footerLE.flexibleHeight = 0;  // Don't expand
            
            var footerText = new GameObject("FooterText");
            footerText.transform.SetParent(footer.transform, false);
            var ftRT = footerText.AddComponent<RectTransform>();
            ftRT.anchorMin = Vector2.zero;
            ftRT.anchorMax = Vector2.one;
            ftRT.offsetMin = new Vector2(32, 0);
            ftRT.offsetMax = new Vector2(-32, 0);
            var ftTMP = footerText.AddComponent<TextMeshProUGUI>();
            ftTMP.text = "Press ESC or F1 to close";
            ftTMP.fontSize = 13;
            ftTMP.color = TEXT_GRAY;
            ftTMP.alignment = TextAlignmentOptions.MidlineLeft;
            ftTMP.raycastTarget = false;
            
            // Overlay
            GameObject overlayInst = null;
            if (overlayPrefab != null)
            {
                overlayInst = (GameObject)PrefabUtility.InstantiatePrefab(overlayPrefab, panelRoot.transform);
                overlayInst.name = "RebindOverlay";
                overlayInst.SetActive(false);
            }
            
            // Wire up component
            var comp = outer.AddComponent<KeybindPanel>();
            var so = new SerializedObject(comp);
            so.FindProperty("_panelRoot").objectReferenceValue = panelRoot;
            so.FindProperty("_contentParent").objectReferenceValue = content;
            so.FindProperty("_rowPrefab").objectReferenceValue = rowPrefab;
            so.FindProperty("_categoryHeaderPrefab").objectReferenceValue = headerPrefab;
            so.FindProperty("_paradigmDropdown").objectReferenceValue = dropdown.GetComponent<TMP_Dropdown>();
            so.FindProperty("_resetAllButton").objectReferenceValue = resetAll.GetComponent<Button>();
            so.FindProperty("_closeButton").objectReferenceValue = close.GetComponent<Button>();
            so.FindProperty("_startHidden").boolValue = true;
            if (overlayInst != null)
                so.FindProperty("_rebindOverlay").objectReferenceValue = overlayInst.GetComponent<RebindOverlay>();
            so.ApplyModifiedPropertiesWithoutUndo();
            
            panelRoot.SetActive(false);
            
            PrefabUtility.SaveAsPrefabAsset(outer, PREFAB_FOLDER + "/KeybindPanel.prefab");
            Object.DestroyImmediate(outer);
        }
        
        // ========== HELPERS ==========
        
        private static GameObject CreateButton(string name, string text, Transform parent, Color bgColor, float w, float h)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);
            
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
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(180, 40);
            
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
            ltmp.text = "All Controls";
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
            
            // Template - minimal
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
            contentVLG.childControlHeight = false; // Items have fixed height (36)
            contentVLG.childForceExpandWidth = true;
            contentVLG.childForceExpandHeight = false;
            contentVLG.spacing = 0;
            
            var contentCSF = content.AddComponent<ContentSizeFitter>();
            contentCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            var item = new GameObject("Item");
            item.transform.SetParent(content.transform, false);
            var irt = item.AddComponent<RectTransform>();
            irt.sizeDelta = new Vector2(0, 36);
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
        
        private static GameObject CreateScrollView(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var sr = go.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.scrollSensitivity = 30f;
            sr.movementType = ScrollRect.MovementType.Clamped;
            go.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.10f, 1f);
            
            var vp = new GameObject("Viewport");
            vp.transform.SetParent(go.transform, false);
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
            
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 2;
            vlg.padding = new RectOffset(16, 16, 16, 16);
            
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            sr.content = crt;
            sr.viewport = vprt;
            
            return go;
        }
    }
}
