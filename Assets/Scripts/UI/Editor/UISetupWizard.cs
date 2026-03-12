#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.IO;
using DIG.UI.Core.Input;

namespace DIG.UI.Editor
{
    /// <summary>
    /// Editor wizard for setting up EPIC 15.8 UI systems.
    /// Access via: Tools > DIG > UI Setup Wizard
    /// </summary>
    public class UISetupWizard : EditorWindow
    {
        [MenuItem("Tools/DIG/UI Setup Wizard")]
        public static void ShowWindow()
        {
            var window = GetWindow<UISetupWizard>();
            window.titleContent = new GUIContent("UI Setup Wizard");
            window.minSize = new Vector2(400, 500);
        }
        
        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.flexGrow = 1;
            
            // Main scroll view for all content
            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;
            scrollView.style.paddingTop = 10;
            scrollView.style.paddingBottom = 10;
            scrollView.style.paddingLeft = 15;
            scrollView.style.paddingRight = 15;
            root.Add(scrollView);
            
            // Header
            var header = new Label("DIG UI Setup Wizard");
            header.style.fontSize = 20;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 20;
            header.style.flexShrink = 0;
            scrollView.Add(header);
            
            // Section 1: Input Glyph System
            scrollView.Add(CreateSection("1. Input Glyph System", CreateGlyphSection()));
            
            // Section 2: Health Bar (ShaderHealthBarSync)
            scrollView.Add(CreateSection("2. Health Bar", CreateHealthBarSection()));
            
            // Section 3: Flashlight HUD
            scrollView.Add(CreateSection("3. Flashlight HUD", CreateFlashlightHUDSection()));
            
            // Section 4: Shader Effects
            scrollView.Add(CreateSection("4. Shader Effects", CreateShaderEffectsSection()));
            
            // Section 5: Quick Actions
            scrollView.Add(CreateSection("5. Quick Actions", CreateQuickActionsSection()));
            
            // Status
            scrollView.Add(CreateStatusSection());
        }
        
        private VisualElement CreateSection(string title, VisualElement content)
        {
            var section = new VisualElement();
            section.style.marginBottom = 20;
            section.style.paddingBottom = 10;
            section.style.borderBottomWidth = 1;
            section.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f);
            section.style.flexShrink = 0;
            
            var label = new Label(title);
            label.style.fontSize = 14;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginBottom = 10;
            label.style.flexShrink = 0;
            section.Add(label);
            
            content.style.flexShrink = 0;
            section.Add(content);
            return section;
        }
        
        private VisualElement CreateGlyphSection()
        {
            var container = new VisualElement();
            
            // Status check
            var status = new Label(InputGlyphDatabaseExists() 
                ? "✅ InputGlyphDatabase found" 
                : "❌ InputGlyphDatabase not found");
            status.style.marginBottom = 10;
            container.Add(status);
            
            // Create button
            var createBtn = new Button(() => CreateInputGlyphDatabase())
            {
                text = "Create Input Glyph Database"
            };
            createBtn.style.height = 30;
            createBtn.SetEnabled(!InputGlyphDatabaseExists());
            container.Add(createBtn);
            
            // Add common actions button
            var addActionsBtn = new Button(() => AddCommonActionsToDatabase())
            {
                text = "Add Common Actions (Jump, Interact, etc.)"
            };
            addActionsBtn.style.height = 30;
            addActionsBtn.style.marginTop = 5;
            addActionsBtn.SetEnabled(InputGlyphDatabaseExists());
            container.Add(addActionsBtn);
            
            // Select button
            var selectBtn = new Button(() => SelectInputGlyphDatabase())
            {
                text = "Select Database in Project"
            };
            selectBtn.style.height = 25;
            selectBtn.style.marginTop = 5;
            selectBtn.SetEnabled(InputGlyphDatabaseExists());
            container.Add(selectBtn);
            
            return container;
        }
        
        private VisualElement CreateHealthBarSection()
        {
            var container = new VisualElement();

            bool syncExists = File.Exists("Assets/Scripts/Combat/UI/ShaderHealthBarSync.cs");
            var status = new Label(syncExists
                ? "ShaderHealthBarSync found"
                : "ShaderHealthBarSync not found");
            status.style.marginBottom = 10;
            container.Add(status);

            var hint = new Label("Health bar uses ShaderHealthBarSync on PlayerUI > ShaderHealthBar");
            hint.style.fontSize = 10;
            hint.style.color = Color.gray;
            hint.style.marginTop = 3;
            container.Add(hint);

            return container;
        }
        
        private VisualElement CreateFlashlightHUDSection()
        {
            var container = new VisualElement();
            
            // Check if template exists
            bool templateExists = File.Exists("Assets/UI/Templates/FlashlightHUD.uxml");
            var status = new Label(templateExists 
                ? "✅ FlashlightHUD.uxml found" 
                : "❌ FlashlightHUD.uxml not found");
            status.style.marginBottom = 10;
            container.Add(status);
            
            // Create in scene button
            var createBtn = new Button(() => CreateFlashlightHUDInScene())
            {
                text = "Create Flashlight HUD in Scene"
            };
            createBtn.style.height = 30;
            createBtn.SetEnabled(templateExists);
            container.Add(createBtn);
            
            var hint = new Label("Creates UI Document + FlashlightHUDView component");
            hint.style.fontSize = 10;
            hint.style.color = Color.gray;
            hint.style.marginTop = 3;
            container.Add(hint);
            
            return container;
        }
        
        private VisualElement CreateShaderEffectsSection()
        {
            var container = new VisualElement();
            
            // Core bars
            var coreLabel = new Label("Core Bars");
            coreLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            coreLabel.style.marginBottom = 5;
            container.Add(coreLabel);
            
            // Check if shaders exist
            bool healthShaderExists = File.Exists("Assets/UI/Shaders/UI_ProceduralHealthBar.shader");
            bool batteryShaderExists = File.Exists("Assets/UI/Shaders/UI_ProceduralBatteryBar.shader");
            bool staminaShaderExists = File.Exists("Assets/UI/Shaders/UI_ProceduralStaminaBar.shader");
            bool shieldShaderExists = File.Exists("Assets/UI/Shaders/UI_ProceduralShieldBar.shader");
            
            container.Add(CreateShaderStatusRow("Health", healthShaderExists));
            container.Add(CreateShaderStatusRow("Battery", batteryShaderExists));
            container.Add(CreateShaderStatusRow("Stamina", staminaShaderExists));
            container.Add(CreateShaderStatusRow("Shield", shieldShaderExists));
            
            // Survival bars
            var survivalLabel = new Label("Survival Bars");
            survivalLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            survivalLabel.style.marginTop = 10;
            survivalLabel.style.marginBottom = 5;
            container.Add(survivalLabel);
            
            bool oxygenShaderExists = File.Exists("Assets/UI/Shaders/UI_ProceduralOxygenBar.shader");
            bool hungerShaderExists = File.Exists("Assets/UI/Shaders/UI_ProceduralHungerBar.shader");
            bool thirstShaderExists = File.Exists("Assets/UI/Shaders/UI_ProceduralThirstBar.shader");
            bool sanityShaderExists = File.Exists("Assets/UI/Shaders/UI_ProceduralSanityBar.shader");
            bool infectionShaderExists = File.Exists("Assets/UI/Shaders/UI_ProceduralInfectionBar.shader");
            
            container.Add(CreateShaderStatusRow("Oxygen", oxygenShaderExists));
            container.Add(CreateShaderStatusRow("Hunger", hungerShaderExists));
            container.Add(CreateShaderStatusRow("Thirst", thirstShaderExists));
            container.Add(CreateShaderStatusRow("Sanity", sanityShaderExists));
            container.Add(CreateShaderStatusRow("Infection", infectionShaderExists));
            
            // Combat/Ability bars
            var combatLabel = new Label("Combat/Ability Bars");
            combatLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            combatLabel.style.marginTop = 10;
            combatLabel.style.marginBottom = 5;
            container.Add(combatLabel);
            
            bool cooldownShaderExists = File.Exists("Assets/UI/Shaders/UI_ProceduralCooldownBar.shader");
            bool chargesShaderExists = File.Exists("Assets/UI/Shaders/UI_ProceduralChargesBar.shader");
            bool durabilityShaderExists = File.Exists("Assets/UI/Shaders/UI_ProceduralDurabilityBar.shader");
            
            container.Add(CreateShaderStatusRow("Cooldown (circular)", cooldownShaderExists));
            container.Add(CreateShaderStatusRow("Charges (segmented)", chargesShaderExists));
            container.Add(CreateShaderStatusRow("Durability", durabilityShaderExists));
            
            // Stealth/Contextual bars
            var stealthLabel = new Label("Stealth/Contextual Bars");
            stealthLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            stealthLabel.style.marginTop = 10;
            stealthLabel.style.marginBottom = 5;
            container.Add(stealthLabel);
            
            bool noiseShaderExists = File.Exists("Assets/UI/Shaders/UI_ProceduralNoiseMeter.shader");
            bool detectionShaderExists = File.Exists("Assets/UI/Shaders/UI_ProceduralDetectionBar.shader");
            bool interactionShaderExists = File.Exists("Assets/UI/Shaders/UI_ProceduralInteractionBar.shader");
            
            container.Add(CreateShaderStatusRow("Noise Meter", noiseShaderExists));
            container.Add(CreateShaderStatusRow("Detection (eye)", detectionShaderExists));
            container.Add(CreateShaderStatusRow("Interaction (ring)", interactionShaderExists));
            
            // Foldout for creation buttons
            var foldout = new Foldout { text = "Create Bars in Scene", value = false };
            foldout.style.marginTop = 15;
            
            // Core bar buttons
            AddBarCreationButton(foldout, "Health Bar", healthShaderExists, CreateShaderHealthBarInScene);
            AddBarCreationButton(foldout, "Battery Bar", batteryShaderExists, CreateShaderBatteryBarInScene);
            AddBarCreationButton(foldout, "Stamina Bar", staminaShaderExists, CreateShaderStaminaBarInScene);
            AddBarCreationButton(foldout, "Shield Bar", shieldShaderExists, () => CreateGenericShaderBar("Shield", "DIG/UI/ProceduralShieldBar", "Player.UI.Views.ShieldShaderView", new Vector2(300, 35)));
            
            // Survival bar buttons
            AddBarCreationButton(foldout, "Oxygen Bar", oxygenShaderExists, () => CreateGenericShaderBar("Oxygen", "DIG/UI/ProceduralOxygenBar", "Player.UI.Views.OxygenShaderView", new Vector2(180, 25)));
            AddBarCreationButton(foldout, "Hunger Bar", hungerShaderExists, () => CreateGenericShaderBar("Hunger", "DIG/UI/ProceduralHungerBar", "Player.UI.Views.HungerShaderView", new Vector2(150, 20)));
            AddBarCreationButton(foldout, "Thirst Bar", thirstShaderExists, () => CreateGenericShaderBar("Thirst", "DIG/UI/ProceduralThirstBar", "Player.UI.Views.ThirstShaderView", new Vector2(150, 20)));
            AddBarCreationButton(foldout, "Sanity Bar", sanityShaderExists, () => CreateGenericShaderBar("Sanity", "DIG/UI/ProceduralSanityBar", "Player.UI.Views.SanityShaderView", new Vector2(200, 25)));
            AddBarCreationButton(foldout, "Infection Bar", infectionShaderExists, () => CreateGenericShaderBar("Infection", "DIG/UI/ProceduralInfectionBar", "Player.UI.Views.InfectionShaderView", new Vector2(200, 25)));
            
            // Combat bar buttons
            AddBarCreationButton(foldout, "Dodge Cooldown", cooldownShaderExists, () => CreateGenericShaderBar("DodgeCooldown", "DIG/UI/ProceduralCooldownBar", "Player.UI.Views.DodgeCooldownShaderView", new Vector2(60, 60)));
            AddBarCreationButton(foldout, "Ability Charges", chargesShaderExists, () => CreateGenericShaderBar("AbilityCharges", "DIG/UI/ProceduralChargesBar", "Player.UI.Views.AbilityChargesShaderView", new Vector2(120, 30)));
            AddBarCreationButton(foldout, "Weapon Durability", durabilityShaderExists, () => CreateGenericShaderBar("WeaponDurability", "DIG/UI/ProceduralDurabilityBar", "Player.UI.Views.WeaponDurabilityShaderView", new Vector2(150, 20)));
            
            // Stealth bar buttons
            AddBarCreationButton(foldout, "Noise Meter", noiseShaderExists, () => CreateGenericShaderBar("NoiseMeter", "DIG/UI/ProceduralNoiseMeter", "Player.UI.Views.NoiseShaderView", new Vector2(40, 80)));
            AddBarCreationButton(foldout, "Detection Eye", detectionShaderExists, () => CreateGenericShaderBar("Detection", "DIG/UI/ProceduralDetectionBar", "Player.UI.Views.DetectionShaderView", new Vector2(80, 40)));
            AddBarCreationButton(foldout, "Interaction Ring", interactionShaderExists, () => CreateGenericShaderBar("Interaction", "DIG/UI/ProceduralInteractionBar", "Player.UI.Views.InteractionShaderView", new Vector2(80, 80)));
            
            container.Add(foldout);
            
            // Open shaders folder
            var openShadersBtn = new Button(() => OpenFolder("Assets/UI/Shaders"))
            {
                text = "Open Shaders Folder"
            };
            openShadersBtn.style.height = 25;
            openShadersBtn.style.marginTop = 10;
            container.Add(openShadersBtn);
            
            var hint = new Label("GPU-accelerated procedural bars with glow, shine & animations");
            hint.style.fontSize = 10;
            hint.style.color = Color.gray;
            hint.style.marginTop = 5;
            container.Add(hint);
            
            return container;
        }
        
        private VisualElement CreateShaderStatusRow(string name, bool exists)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginLeft = 10;
            row.style.marginBottom = 2;
            
            var icon = new Label(exists ? "✅" : "❌");
            icon.style.width = 20;
            row.Add(icon);
            
            var label = new Label(name);
            label.style.fontSize = 11;
            row.Add(label);
            
            return row;
        }
        
        private void AddBarCreationButton(Foldout foldout, string label, bool enabled, System.Action onClick)
        {
            var btn = new Button(onClick) { text = $"Create {label}" };
            btn.style.height = 25;
            btn.style.marginTop = 3;
            btn.SetEnabled(enabled);
            foldout.Add(btn);
        }
        
        private void CreateGenericShaderBar(string name, string shaderPath, string viewType, Vector2 size)
        {
            // Find or create Canvas
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasGo = new GameObject("Canvas");
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");
            }
            
            // Create bar object
            var go = new GameObject($"Shader{name}Bar");
            go.transform.SetParent(canvas.transform, false);
            
            // Add RectTransform
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(20, -20);
            rect.sizeDelta = size;
            
            // Add RawImage (preferred for shader materials)
            var rawImage = go.AddComponent<UnityEngine.UI.RawImage>();
            rawImage.color = Color.white;
            
            // Add view component
            var type = FindType(viewType);
            if (type != null)
            {
                go.AddComponent(type);
            }
            else
            {
                Debug.LogWarning($"[UI Setup] {viewType} not found. Add ViewModel + View components manually.");
            }
            
            Selection.activeGameObject = go;
            Undo.RegisterCreatedObjectUndo(go, $"Create {name} Bar");
            
            Debug.Log($"[UI Setup] Created Shader{name}Bar - material auto-assigned by ShaderView at runtime");
        }
        
        private VisualElement CreateQuickActionsSection()
        {
            var container = new VisualElement();
            
            // Create custom ViewModel/View
            var customBtn = new Button(() => CreateCustomViewModelViewWindow.ShowWindow())
            {
                text = "Create New ViewModel + View..."
            };
            customBtn.style.height = 30;
            container.Add(customBtn);
            
            // Open styles folder
            var stylesBtn = new Button(() => OpenFolder("Assets/UI/Styles"))
            {
                text = "Open Styles Folder"
            };
            stylesBtn.style.height = 25;
            stylesBtn.style.marginTop = 5;
            container.Add(stylesBtn);
            
            // Open templates folder
            var templatesBtn = new Button(() => OpenFolder("Assets/UI/Templates"))
            {
                text = "Open Templates Folder"
            };
            templatesBtn.style.height = 25;
            templatesBtn.style.marginTop = 5;
            container.Add(templatesBtn);
            
            return container;
        }
        
        private VisualElement CreateStatusSection()
        {
            var container = new VisualElement();
            container.style.marginTop = 20;
            container.style.paddingTop = 10;
            container.style.borderTopWidth = 1;
            container.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f);
            
            var title = new Label("System Status");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 10;
            container.Add(title);
            
            // Core systems
            container.Add(CreateStatusRow("MVVM Framework", 
                File.Exists("Assets/Scripts/UI/Core/MVVM/BindableProperty.cs")));
            container.Add(CreateStatusRow("Navigation System", 
                File.Exists("Assets/Scripts/UI/Core/Navigation/NavigationManager.cs")));
            container.Add(CreateStatusRow("Input Glyph System", 
                File.Exists("Assets/Scripts/UI/Core/Input/InputGlyphProvider.cs")));
            container.Add(CreateStatusRow("USS Variables", 
                File.Exists("Assets/UI/Styles/Variables.uss")));
            container.Add(CreateStatusRow("USS Components", 
                File.Exists("Assets/UI/Styles/Components.uss")));
            container.Add(CreateStatusRow("InputGlyphDatabase", 
                InputGlyphDatabaseExists()));
            container.Add(CreateStatusRow("ShaderHealthBarSync",
                File.Exists("Assets/Scripts/Combat/UI/ShaderHealthBarSync.cs")));
            container.Add(CreateStatusRow("Flashlight HUD Template", 
                File.Exists("Assets/UI/Templates/FlashlightHUD.uxml")));
            
            // Shader count
            var shaderCount = CountShadersInFolder("Assets/UI/Shaders");
            var shaderLabel = new Label($"📊 {shaderCount} procedural shaders installed");
            shaderLabel.style.marginTop = 10;
            shaderLabel.style.fontSize = 11;
            shaderLabel.style.color = shaderCount >= 12 ? new Color(0.4f, 0.8f, 0.4f) : Color.gray;
            container.Add(shaderLabel);
            
            // ViewModel count
            var vmCount = CountFilesInFolder("Assets/Scripts/Player/UI/ViewModels", "*.cs");
            var vmLabel = new Label($"📊 {vmCount} ViewModels created");
            vmLabel.style.fontSize = 11;
            vmLabel.style.color = vmCount >= 10 ? new Color(0.4f, 0.8f, 0.4f) : Color.gray;
            container.Add(vmLabel);
            
            // View count
            var viewCount = CountFilesInFolder("Assets/Scripts/Player/UI/Views", "*.cs");
            var viewLabel = new Label($"📊 {viewCount} ShaderViews created");
            viewLabel.style.fontSize = 11;
            viewLabel.style.color = viewCount >= 10 ? new Color(0.4f, 0.8f, 0.4f) : Color.gray;
            container.Add(viewLabel);
            
            return container;
        }
        
        private int CountShadersInFolder(string path)
        {
            if (!Directory.Exists(path)) return 0;
            return Directory.GetFiles(path, "*.shader").Length;
        }
        
        private int CountFilesInFolder(string path, string pattern)
        {
            if (!Directory.Exists(path)) return 0;
            return Directory.GetFiles(path, pattern).Length;
        }
        
        private VisualElement CreateStatusRow(string name, bool exists)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 3;
            
            var icon = new Label(exists ? "✅" : "❌");
            icon.style.width = 20;
            row.Add(icon);
            
            var label = new Label(name);
            row.Add(label);
            
            return row;
        }
        
        #region Actions
        
        /// <summary>
        /// Find a type by full name across all loaded assemblies.
        /// </summary>
        private System.Type FindType(string fullName)
        {
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName);
                if (type != null) return type;
            }
            return null;
        }
        
        private bool InputGlyphDatabaseExists()
        {
            return File.Exists("Assets/Resources/InputGlyphDatabase.asset");
        }
        
        private void CreateInputGlyphDatabase()
        {
            // Ensure Resources folder exists
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
            
            // Create the database
            var database = ScriptableObject.CreateInstance<InputGlyphDatabase>();
            AssetDatabase.CreateAsset(database, "Assets/Resources/InputGlyphDatabase.asset");
            AssetDatabase.SaveAssets();
            
            // Select it
            Selection.activeObject = database;
            EditorGUIUtility.PingObject(database);
            
            Debug.Log("[UI Setup] Created InputGlyphDatabase at Assets/Resources/InputGlyphDatabase.asset");
            
            // Refresh the window
            rootVisualElement.Clear();
            CreateGUI();
        }
        
        private void AddCommonActionsToDatabase()
        {
            var database = AssetDatabase.LoadAssetAtPath<InputGlyphDatabase>(
                "Assets/Resources/InputGlyphDatabase.asset");
            
            if (database == null)
            {
                Debug.LogError("[UI Setup] Database not found!");
                return;
            }
            
            // Use SerializedObject to modify
            var so = new SerializedObject(database);
            var entriesProp = so.FindProperty("_entries");
            
            var commonActions = new[]
            {
                ("Jump", "[Space]", "(A)"),
                ("Interact", "[F]", "(X)"),
                ("Fire", "[LMB]", "(RT)"),
                ("Aim", "[RMB]", "(LT)"),
                ("Reload", "[R]", "(X)"),
                ("Dodge", "[Shift]", "(B)"),
                ("Sprint", "[Shift]", "(LS)"),
                ("Crouch", "[C]", "(B)"),
                ("Use", "[E]", "(Y)"),
                ("Inventory", "[I]", "(Back)"),
                ("Map", "[M]", "(View)"),
                ("Pause", "[Esc]", "(Start)"),
                ("Confirm", "[Enter]", "(A)"),
                ("Cancel", "[Esc]", "(B)"),
                ("NextWeapon", "[Q]", "(RB)"),
                ("PrevWeapon", "[Tab]", "(LB)")
            };
            
            foreach (var (action, keyboard, xbox) in commonActions)
            {
                // Check if already exists
                bool exists = false;
                for (int i = 0; i < entriesProp.arraySize; i++)
                {
                    var entry = entriesProp.GetArrayElementAtIndex(i);
                    if (entry.FindPropertyRelative("ActionName").stringValue == action)
                    {
                        exists = true;
                        break;
                    }
                }
                
                if (!exists)
                {
                    entriesProp.InsertArrayElementAtIndex(entriesProp.arraySize);
                    var newEntry = entriesProp.GetArrayElementAtIndex(entriesProp.arraySize - 1);
                    newEntry.FindPropertyRelative("ActionName").stringValue = action;
                    newEntry.FindPropertyRelative("KeyboardText").stringValue = keyboard;
                    newEntry.FindPropertyRelative("XboxText").stringValue = xbox;
                    newEntry.FindPropertyRelative("PlayStationText").stringValue = xbox.Replace("A", "×").Replace("B", "○").Replace("X", "□").Replace("Y", "△");
                    newEntry.FindPropertyRelative("SwitchText").stringValue = xbox;
                }
            }
            
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"[UI Setup] Added {commonActions.Length} common actions to database");
            Selection.activeObject = database;
        }
        
        private void SelectInputGlyphDatabase()
        {
            var database = AssetDatabase.LoadAssetAtPath<InputGlyphDatabase>(
                "Assets/Resources/InputGlyphDatabase.asset");
            if (database != null)
            {
                Selection.activeObject = database;
                EditorGUIUtility.PingObject(database);
            }
        }
        
        private void CreateFlashlightHUDInScene()
        {
            // Create GameObject
            var go = new GameObject("FlashlightHUD");
            
            // Add UIDocument
            var uiDoc = go.AddComponent<UnityEngine.UIElements.UIDocument>();
            
            // Load and assign UXML
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Templates/FlashlightHUD.uxml");
            if (uxml != null)
            {
                uiDoc.visualTreeAsset = uxml;
            }
            
            // Try to find a PanelSettings asset
            var panelSettingsGuids = AssetDatabase.FindAssets("t:PanelSettings");
            if (panelSettingsGuids.Length > 0)
            {
                var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(
                    AssetDatabase.GUIDToAssetPath(panelSettingsGuids[0]));
                uiDoc.panelSettings = panelSettings;
            }
            
            // Add FlashlightHUDView component
            var viewType = System.Type.GetType("FlashlightHUDView, Assembly-CSharp");
            if (viewType != null)
            {
                go.AddComponent(viewType);
            }
            else
            {
                Debug.LogWarning("[UI Setup] FlashlightHUDView component not found. Add it manually.");
            }
            
            // Select the new object
            Selection.activeGameObject = go;
            Undo.RegisterCreatedObjectUndo(go, "Create Flashlight HUD");
            
            Debug.Log("[UI Setup] Created FlashlightHUD GameObject in scene");
        }
        
        private void CreateShaderMaterials()
        {
            // Ensure Materials folder exists
            if (!AssetDatabase.IsValidFolder("Assets/UI/Materials"))
            {
                AssetDatabase.CreateFolder("Assets/UI", "Materials");
            }
            
            // Create Health Bar material
            var healthShader = Shader.Find("DIG/UI/ProceduralHealthBar");
            if (healthShader != null && !File.Exists("Assets/UI/Materials/HealthBar.mat"))
            {
                var healthMat = new Material(healthShader);
                healthMat.name = "HealthBar";
                AssetDatabase.CreateAsset(healthMat, "Assets/UI/Materials/HealthBar.mat");
                Debug.Log("[UI Setup] Created HealthBar material");
            }
            
            // Create Battery Bar material
            var batteryShader = Shader.Find("DIG/UI/ProceduralBatteryBar");
            if (batteryShader != null && !File.Exists("Assets/UI/Materials/BatteryBar.mat"))
            {
                var batteryMat = new Material(batteryShader);
                batteryMat.name = "BatteryBar";
                AssetDatabase.CreateAsset(batteryMat, "Assets/UI/Materials/BatteryBar.mat");
                Debug.Log("[UI Setup] Created BatteryBar material");
            }
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            // Select materials folder
            OpenFolder("Assets/UI/Materials");
            
            Debug.Log("[UI Setup] Shader materials created in Assets/UI/Materials/");
        }
        
        private void CreateShaderHealthBarInScene()
        {
            // Find or create Canvas
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasGo = new GameObject("Canvas");
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");
            }
            
            // Create health bar object
            var go = new GameObject("ShaderHealthBar");
            go.transform.SetParent(canvas.transform, false);
            
            // Add RectTransform
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(20, -20);
            rect.sizeDelta = new Vector2(300, 40);
            
            // Add Image - sync component will auto-assign the shader material at runtime
            var image = go.AddComponent<UnityEngine.UI.Image>();
            image.color = Color.white;
            
            // Add sync component - this now auto-creates and assigns the material from the shader
            var syncType = FindType("Combat.UI.ShaderHealthBarSync");
            if (syncType != null)
            {
                var sync = go.AddComponent(syncType);
                // Enable debug logs by default so user can verify it's working
                var debugField = syncType.GetField("_showDebugLogs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (debugField != null)
                    debugField.SetValue(sync, true);
            }
            else
            {
                Debug.LogError("[UI Setup] ShaderHealthBarSync component not found! Make sure the script exists in Combat.UI namespace.");
            }
            
            Selection.activeGameObject = go;
            Undo.RegisterCreatedObjectUndo(go, "Create Shader Health Bar");
            
            Debug.Log("[UI Setup] Created ShaderHealthBar - material will be auto-assigned at runtime by ShaderHealthBarSync");
        }
        
        private void CreateShaderBatteryBarInScene()
        {
            // Find or create Canvas
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasGo = new GameObject("Canvas");
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");
            }
            
            // Create battery bar object
            var go = new GameObject("ShaderBatteryBar");
            go.transform.SetParent(canvas.transform, false);
            
            // Add RectTransform
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(20, -70);
            rect.sizeDelta = new Vector2(200, 30);
            
            // Add Image - sync component will auto-assign the shader material at runtime
            var image = go.AddComponent<UnityEngine.UI.Image>();
            image.color = Color.white;
            
            // Add sync component - this now auto-creates and assigns the material from the shader
            var syncType = FindType("Visuals.UI.ShaderBatteryBarSync");
            if (syncType != null)
            {
                var sync = go.AddComponent(syncType);
                // Enable debug logs by default so user can verify it's working
                var debugField = syncType.GetField("_showDebugLogs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (debugField != null)
                    debugField.SetValue(sync, true);
            }
            else
            {
                Debug.LogError("[UI Setup] ShaderBatteryBarSync component not found! Make sure the script exists in Visuals.UI namespace.");
            }
            
            Selection.activeGameObject = go;
            Undo.RegisterCreatedObjectUndo(go, "Create Shader Battery Bar");
            
            Debug.Log("[UI Setup] Created ShaderBatteryBar - material will be auto-assigned at runtime by ShaderBatteryBarSync");
        }
        
        private void CreateShaderStaminaBarInScene()
        {
            // Find or create Canvas
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasGo = new GameObject("Canvas");
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");
            }
            
            // Create stamina bar object
            var go = new GameObject("ShaderStaminaBar");
            go.transform.SetParent(canvas.transform, false);
            
            // Add RectTransform - position below battery bar
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(20, -110);
            rect.sizeDelta = new Vector2(200, 25);
            
            // Add Image - sync component will auto-assign the shader material at runtime
            var image = go.AddComponent<UnityEngine.UI.Image>();
            image.color = Color.white;
            
            // Add sync component - this now auto-creates and assigns the material from the shader
            var syncType = FindType("Player.UI.ShaderStaminaBarSync");
            if (syncType != null)
            {
                var sync = go.AddComponent(syncType);
                // Enable debug logs by default so user can verify it's working
                var debugField = syncType.GetField("_showDebugLogs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (debugField != null)
                    debugField.SetValue(sync, true);
            }
            else
            {
                Debug.LogError("[UI Setup] ShaderStaminaBarSync component not found! Make sure the script exists in Player.UI namespace.");
            }
            
            Selection.activeGameObject = go;
            Undo.RegisterCreatedObjectUndo(go, "Create Shader Stamina Bar");
            
            Debug.Log("[UI Setup] Created ShaderStaminaBar - material will be auto-assigned at runtime by ShaderStaminaBarSync");
        }
        
        private void OpenFolder(string path)
        {
            var obj = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (obj != null)
            {
                Selection.activeObject = obj;
                EditorGUIUtility.PingObject(obj);
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Window for creating custom ViewModel + View pairs.
    /// </summary>
    public class CreateCustomViewModelViewWindow : EditorWindow
    {
        private string _name = "MyFeature";
        private bool _createUXML = true;
        private bool _createViewModel = true;
        private bool _createView = true;
        
        public static void ShowWindow()
        {
            var window = GetWindow<CreateCustomViewModelViewWindow>();
            window.titleContent = new GUIContent("Create ViewModel + View");
            window.minSize = new Vector2(350, 250);
        }
        
        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Create New UI Component", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);
            
            _name = EditorGUILayout.TextField("Name", _name);
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Components to Create:", EditorStyles.boldLabel);
            
            _createViewModel = EditorGUILayout.Toggle("ViewModel", _createViewModel);
            _createView = EditorGUILayout.Toggle("View", _createView);
            _createUXML = EditorGUILayout.Toggle("UXML Template", _createUXML);
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("Will Create:", EditorStyles.miniLabel);
            if (_createViewModel)
                EditorGUILayout.LabelField($"  • Assets/Scripts/Player/UI/ViewModels/{_name}ViewModel.cs", EditorStyles.miniLabel);
            if (_createView)
                EditorGUILayout.LabelField($"  • Assets/Scripts/Player/UI/Views/{_name}View.cs", EditorStyles.miniLabel);
            if (_createUXML)
                EditorGUILayout.LabelField($"  • Assets/UI/Templates/{_name}.uxml", EditorStyles.miniLabel);
            
            EditorGUILayout.Space(20);
            
            if (GUILayout.Button("Create", GUILayout.Height(30)))
            {
                CreateFiles();
                Close();
            }
        }
        
        private void CreateFiles()
        {
            if (string.IsNullOrWhiteSpace(_name))
            {
                Debug.LogError("[UI Setup] Name cannot be empty!");
                return;
            }
            
            // Ensure directories exist
            EnsureDirectory("Assets/Scripts/Player/UI/ViewModels");
            EnsureDirectory("Assets/Scripts/Player/UI/Views");
            EnsureDirectory("Assets/UI/Templates");
            
            if (_createViewModel)
            {
                CreateViewModelFile();
            }
            
            if (_createView)
            {
                CreateViewFile();
            }
            
            if (_createUXML)
            {
                CreateUXMLFile();
            }
            
            AssetDatabase.Refresh();
            Debug.Log($"[UI Setup] Created {_name} UI components");
        }
        
        private void EnsureDirectory(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parts = path.Split('/');
                var current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    var next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        AssetDatabase.CreateFolder(current, parts[i]);
                    }
                    current = next;
                }
            }
        }
        
        private void CreateViewModelFile()
        {
            var content = $@"using DIG.UI.Core.MVVM;

/// <summary>
/// ViewModel for {_name} UI.
/// </summary>
public class {_name}ViewModel : ViewModelBase
{{
    // Add your bindable properties here
    public BindableProperty<string> Title {{ get; }} = new(""{_name}"");
    public BindableProperty<bool> IsVisible {{ get; }} = new(true);
    
    // Add your commands/methods here
    public void OnButtonClicked()
    {{
        // Handle button click
    }}
    
    protected override void OnDispose()
    {{
        Title.ClearListeners();
        IsVisible.ClearListeners();
    }}
}}
";
            File.WriteAllText($"Assets/Scripts/Player/UI/ViewModels/{_name}ViewModel.cs", content);
        }
        
        private void CreateViewFile()
        {
            var content = $@"using UnityEngine;
using DIG.UI.Core.MVVM;

/// <summary>
/// View for {_name} UI.
/// </summary>
public class {_name}View : UIView<{_name}ViewModel>
{{
    [Header(""Settings"")]
    [SerializeField] private bool _autoCreateViewModel = true;
    
    protected override void Start()
    {{
        base.Start();
        
        if (_autoCreateViewModel && !IsBound)
        {{
            Bind(new {_name}ViewModel());
        }}
    }}
    
    protected override void OnBind()
    {{
        // Bind UI elements to ViewModel properties
        // Example:
        // BindLabel(""title-label"", ViewModel.Title);
        // BindVisibility(""main-container"", ViewModel.IsVisible);
        // BindButton(""action-button"", ViewModel.OnButtonClicked);
    }}
    
    protected override void OnUnbind()
    {{
        // Cleanup if needed
    }}
}}
";
            File.WriteAllText($"Assets/Scripts/Player/UI/Views/{_name}View.cs", content);
        }
        
        private void CreateUXMLFile()
        {
            var content = $@"<ui:UXML xmlns:ui=""UnityEngine.UIElements"" 
         xmlns:uie=""UnityEditor.UIElements"" 
         xsi=""http://www.w3.org/2001/XMLSchema-instance"" 
         engine=""UnityEngine.UIElements"" 
         editor=""UnityEditor.UIElements"" 
         noNamespaceSchemaLocation=""../../UIElementsSchema/UIElements.xsd"" 
         editor-extension-mode=""False"">
    
    <!-- {_name} UI Template -->
    
    <Style src=""project://database/Assets/UI/Styles/Variables.uss"" />
    <Style src=""project://database/Assets/UI/Styles/Components.uss"" />
    
    <ui:VisualElement name=""main-container"" class=""panel"" 
                      style=""padding: 20px;"">
        
        <ui:Label name=""title-label"" 
                  text=""{_name}"" 
                  class=""text-header"" />
        
        <ui:VisualElement style=""height: 20px;"" />
        
        <ui:Label name=""content-label"" 
                  text=""Content goes here"" 
                  class=""text-body"" />
        
        <ui:VisualElement style=""flex-grow: 1;"" />
        
        <ui:Button name=""action-button"" 
                   text=""Action"" 
                   class=""pro-button"" />
        
    </ui:VisualElement>
    
</ui:UXML>
";
            File.WriteAllText($"Assets/UI/Templates/{_name}.uxml", content);
        }
    }
}
#endif
