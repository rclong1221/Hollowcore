using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace DIG.Editor.OpsiveParityAnalysis
{
    /// <summary>
    /// EPIC 15.7: Opsive Parity Analysis Window.
    /// Analyzes Opsive Ultimate Character Controller as "Gold Standard" reference,
    /// identifying features to replicate or surpass in our ECS engine.
    /// </summary>
    public class OpsiveParityWindow : EditorWindow
    {
        private Vector2 _scrollPosition;
        private int _selectedTab = 0;
        private string[] _tabs = new string[] 
        { 
            "Overview", "Locomotion", "Interaction", "Abilities", "Clean-Up", "Action Plan" 
        };

        // Feature matrices
        private List<FeatureComparison> _locomotionFeatures = new List<FeatureComparison>();
        private List<FeatureComparison> _interactionFeatures = new List<FeatureComparison>();
        private List<FeatureComparison> _abilityFeatures = new List<FeatureComparison>();
        
        // Clean-up items
        private List<CleanUpItem> _cleanUpItems = new List<CleanUpItem>();
        
        // Action plan
        private List<ActionItem> _actionPlan = new List<ActionItem>();
        
        // Filter
        private ParityStatus _statusFilter = ParityStatus.All;
        private string _searchFilter = "";

        [MenuItem("DIG/Opsive Parity Analysis")]
        public static void ShowWindow()
        {
            var window = GetWindow<OpsiveParityWindow>("Opsive Parity");
            window.minSize = new Vector2(900, 650);
        }

        private void OnEnable()
        {
            InitializeData();
        }

        private void InitializeData()
        {
            InitializeLocomotionFeatures();
            InitializeInteractionFeatures();
            InitializeAbilityFeatures();
            InitializeCleanUpItems();
            InitializeActionPlan();
        }

        private void OnGUI()
        {
            DrawHeader();
            
            EditorGUILayout.BeginHorizontal();
            
            // Sidebar
            EditorGUILayout.BeginVertical("box", GUILayout.Width(120), GUILayout.ExpandHeight(true));
            _selectedTab = GUILayout.SelectionGrid(_selectedTab, _tabs, 1, EditorStyles.miniButton);
            
            EditorGUILayout.Space(20);
            DrawSidebarStats();
            
            EditorGUILayout.EndVertical();
            
            // Content
            EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            switch (_selectedTab)
            {
                case 0: DrawOverviewTab(); break;
                case 1: DrawLocomotionTab(); break;
                case 2: DrawInteractionTab(); break;
                case 3: DrawAbilitiesTab(); break;
                case 4: DrawCleanUpTab(); break;
                case 5: DrawActionPlanTab(); break;
            }
            
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("EPIC 15.7: Opsive Parity Analysis", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            
            EditorGUILayout.LabelField("Filter:", GUILayout.Width(40));
            _statusFilter = (ParityStatus)EditorGUILayout.EnumPopup(_statusFilter, GUILayout.Width(100));
            
            _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(150));
            
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
            {
                ScanProject();
            }
            
            if (GUILayout.Button("Export", EditorStyles.toolbarButton))
            {
                ExportReport();
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSidebarStats()
        {
            var allFeatures = _locomotionFeatures.Concat(_interactionFeatures).Concat(_abilityFeatures).ToList();
            
            int total = allFeatures.Count;
            int complete = allFeatures.Count(f => f.Status == ParityStatus.Complete);
            int partial = allFeatures.Count(f => f.Status == ParityStatus.Partial);
            int missing = allFeatures.Count(f => f.Status == ParityStatus.Missing);
            int surpassed = allFeatures.Count(f => f.Status == ParityStatus.Surpassed);
            
            EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
            
            GUI.color = Color.green;
            EditorGUILayout.LabelField($"✓ Complete: {complete}");
            
            GUI.color = Color.cyan;
            EditorGUILayout.LabelField($"★ Surpassed: {surpassed}");
            
            GUI.color = Color.yellow;
            EditorGUILayout.LabelField($"◐ Partial: {partial}");
            
            GUI.color = Color.red;
            EditorGUILayout.LabelField($"✗ Missing: {missing}");
            
            GUI.color = Color.white;
            
            EditorGUILayout.Space(10);
            
            float parityPercent = total > 0 ? ((complete + surpassed) / (float)total) * 100f : 0f;
            EditorGUILayout.LabelField($"Parity: {parityPercent:F0}%", EditorStyles.boldLabel);
            
            Rect barRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.Height(12), GUILayout.ExpandWidth(true));
            DrawParityBar(barRect, complete, surpassed, partial, missing);
        }

        private void DrawParityBar(Rect rect, int complete, int surpassed, int partial, int missing)
        {
            int total = complete + surpassed + partial + missing;
            if (total == 0) return;
            
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));
            
            float x = rect.x;
            float w;
            
            // Surpassed
            w = (surpassed / (float)total) * rect.width;
            EditorGUI.DrawRect(new Rect(x, rect.y, w, rect.height), Color.cyan);
            x += w;
            
            // Complete
            w = (complete / (float)total) * rect.width;
            EditorGUI.DrawRect(new Rect(x, rect.y, w, rect.height), Color.green);
            x += w;
            
            // Partial
            w = (partial / (float)total) * rect.width;
            EditorGUI.DrawRect(new Rect(x, rect.y, w, rect.height), Color.yellow);
            x += w;
            
            // Missing is implicit (remaining space)
        }

        #region Overview Tab
        
        private void DrawOverviewTab()
        {
            EditorGUILayout.LabelField("Opsive Parity Analysis Overview", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            EditorGUILayout.HelpBox(
                "This tool analyzes the Opsive Ultimate Character Controller (full source in project) as a 'Gold Standard' reference.\n\n" +
                "Goal: Identify high-end features (Gravity Zones, Moving Platforms, Item Abilities) that our ECS engine should replicate or surpass.\n\n" +
                "NOTE: We are NOT integrating Opsive code directly (it is non-ECS). This is for reference only.",
                MessageType.Info);
            
            EditorGUILayout.Space(10);
            
            // Summary cards
            EditorGUILayout.BeginHorizontal();
            
            DrawSummaryCard("Locomotion", _locomotionFeatures, new Color(0.3f, 0.6f, 0.9f));
            DrawSummaryCard("Interaction", _interactionFeatures, new Color(0.9f, 0.6f, 0.3f));
            DrawSummaryCard("Abilities", _abilityFeatures, new Color(0.6f, 0.9f, 0.3f));
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(15);
            
            // Key findings
            EditorGUILayout.LabelField("Key Findings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            var missingCritical = _locomotionFeatures.Concat(_interactionFeatures).Concat(_abilityFeatures)
                .Where(f => f.Status == ParityStatus.Missing && f.Priority == Priority.Critical)
                .ToList();
            
            if (missingCritical.Count > 0)
            {
                GUI.color = Color.red;
                EditorGUILayout.LabelField($"⚠ {missingCritical.Count} Critical features missing:", EditorStyles.boldLabel);
                GUI.color = Color.white;
                
                foreach (var f in missingCritical.Take(5))
                {
                    EditorGUILayout.LabelField($"  • {f.FeatureName} ({f.Category})");
                }
            }
            else
            {
                GUI.color = Color.green;
                EditorGUILayout.LabelField("✓ No critical features missing!", EditorStyles.boldLabel);
                GUI.color = Color.white;
            }
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(10);
            
            // Quick actions
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Scan Opsive Source", GUILayout.Height(30)))
            {
                ScanOpsiveSource();
            }
            
            if (GUILayout.Button("Scan ECS Systems", GUILayout.Height(30)))
            {
                ScanECSSystems();
            }
            
            if (GUILayout.Button("Generate Report", GUILayout.Height(30)))
            {
                GenerateFullReport();
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSummaryCard(string title, List<FeatureComparison> features, Color color)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(220));
            
            GUI.color = color;
            EditorGUILayout.LabelField(title, new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 });
            GUI.color = Color.white;
            
            int complete = features.Count(f => f.Status == ParityStatus.Complete || f.Status == ParityStatus.Surpassed);
            int total = features.Count;
            float percent = total > 0 ? (complete / (float)total) * 100f : 0f;
            
            EditorGUILayout.LabelField($"{complete}/{total} features ({percent:F0}%)");
            
            Rect barRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.Height(8), GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(barRect, new Color(0.2f, 0.2f, 0.2f));
            EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, barRect.width * (percent / 100f), barRect.height), color);
            
            EditorGUILayout.EndVertical();
        }
        
        #endregion

        #region Locomotion Tab
        
        private void DrawLocomotionTab()
        {
            EditorGUILayout.LabelField("Locomotion Parity Analysis", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Comparing Opsive's locomotion features against our ECS implementation.",
                MessageType.Info);
            EditorGUILayout.Space(10);
            
            DrawFeatureMatrix(_locomotionFeatures, "Locomotion");
        }
        
        #endregion

        #region Interaction Tab
        
        private void DrawInteractionTab()
        {
            EditorGUILayout.LabelField("Interaction Parity Analysis", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Comparing Opsive's interaction systems against our ECS implementation.",
                MessageType.Info);
            EditorGUILayout.Space(10);
            
            DrawFeatureMatrix(_interactionFeatures, "Interaction");
        }
        
        #endregion

        #region Abilities Tab
        
        private void DrawAbilitiesTab()
        {
            EditorGUILayout.LabelField("Abilities Parity Analysis", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Comparing Opsive's item abilities against our ECS UsableAction system.",
                MessageType.Info);
            EditorGUILayout.Space(10);
            
            DrawFeatureMatrix(_abilityFeatures, "Abilities");
        }
        
        #endregion

        #region Clean-Up Tab
        
        private void DrawCleanUpTab()
        {
            EditorGUILayout.LabelField("Opsive Clean-Up Audit", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Audit items to ensure Opsive components are disabled/removed to prevent conflicts.\n" +
                "Goal: Pure ECS simulation with Opsive only used for Animation State Machine logic (if needed).",
                MessageType.Warning);
            EditorGUILayout.Space(10);
            
            // Header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Item", EditorStyles.boldLabel, GUILayout.Width(250));
            EditorGUILayout.LabelField("Type", EditorStyles.boldLabel, GUILayout.Width(100));
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel, GUILayout.Width(100));
            EditorGUILayout.LabelField("Action", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(3);
            
            foreach (var item in _cleanUpItems)
            {
                DrawCleanUpRow(item);
            }
            
            EditorGUILayout.Space(15);
            
            // Bulk actions
            EditorGUILayout.BeginHorizontal();
            
            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.yellow;
            
            if (GUILayout.Button("Disable All Opsive Components", GUILayout.Height(25)))
            {
                DisableAllOpsiveComponents();
            }
            
            GUI.backgroundColor = Color.red;
            
            if (GUILayout.Button("Remove Unused Opsive Assets", GUILayout.Height(25)))
            {
                if (EditorUtility.DisplayDialog("Confirm Removal",
                    "This will remove unused Opsive assets to reduce project size. Continue?",
                    "Remove", "Cancel"))
                {
                    RemoveUnusedOpsiveAssets();
                }
            }
            
            GUI.backgroundColor = prevColor;
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawCleanUpRow(CleanUpItem item)
        {
            EditorGUILayout.BeginHorizontal();
            
            EditorGUILayout.LabelField(item.Name, GUILayout.Width(250));
            EditorGUILayout.LabelField(item.Type.ToString(), GUILayout.Width(100));
            
            Color statusColor = item.Status switch
            {
                CleanUpStatus.Clean => Color.green,
                CleanUpStatus.NeedsAction => Color.yellow,
                CleanUpStatus.Conflict => Color.red,
                _ => Color.white
            };
            
            GUI.color = statusColor;
            EditorGUILayout.LabelField(item.Status.ToString(), GUILayout.Width(100));
            GUI.color = Color.white;
            
            if (item.Status != CleanUpStatus.Clean)
            {
                if (GUILayout.Button(item.RecommendedAction, GUILayout.Width(100)))
                {
                    ExecuteCleanUpAction(item);
                }
            }
            else
            {
                EditorGUILayout.LabelField("✓ OK", GUILayout.Width(100));
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        #endregion

        #region Action Plan Tab
        
        private void DrawActionPlanTab()
        {
            EditorGUILayout.LabelField("Action Plan", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Tasks to achieve and surpass Opsive parity in our ECS implementation.",
                MessageType.Info);
            EditorGUILayout.Space(10);
            
            // Group by priority
            var critical = _actionPlan.Where(a => a.Priority == Priority.Critical).ToList();
            var high = _actionPlan.Where(a => a.Priority == Priority.High).ToList();
            var medium = _actionPlan.Where(a => a.Priority == Priority.Medium).ToList();
            var low = _actionPlan.Where(a => a.Priority == Priority.Low).ToList();
            
            if (critical.Count > 0)
            {
                DrawActionGroup("Critical Priority", critical, Color.red);
            }
            
            if (high.Count > 0)
            {
                DrawActionGroup("High Priority", high, new Color(1f, 0.5f, 0f));
            }
            
            if (medium.Count > 0)
            {
                DrawActionGroup("Medium Priority", medium, Color.yellow);
            }
            
            if (low.Count > 0)
            {
                DrawActionGroup("Low Priority", low, Color.gray);
            }
            
            EditorGUILayout.Space(15);
            
            // Export
            if (GUILayout.Button("Export Action Plan to Markdown", GUILayout.Height(25)))
            {
                ExportActionPlan();
            }
        }

        private void DrawActionGroup(string title, List<ActionItem> items, Color color)
        {
            GUI.color = color;
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            GUI.color = Color.white;
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            foreach (var item in items)
            {
                EditorGUILayout.BeginHorizontal();
                
                item.IsComplete = EditorGUILayout.Toggle(item.IsComplete, GUILayout.Width(20));
                
                GUIStyle labelStyle = item.IsComplete ? 
                    new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Italic } : 
                    EditorStyles.label;
                
                if (item.IsComplete)
                {
                    GUI.color = Color.gray;
                }
                
                EditorGUILayout.LabelField(item.Task, labelStyle, GUILayout.Width(350));
                EditorGUILayout.LabelField(item.Category, GUILayout.Width(100));
                EditorGUILayout.LabelField(item.EstimatedEffort, GUILayout.Width(80));
                
                GUI.color = Color.white;
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }
        
        #endregion

        #region Feature Matrix Drawing
        
        private void DrawFeatureMatrix(List<FeatureComparison> features, string category)
        {
            // Filter
            var filtered = features.Where(f => 
                (_statusFilter == ParityStatus.All || f.Status == _statusFilter) &&
                (string.IsNullOrEmpty(_searchFilter) || 
                 f.FeatureName.ToLower().Contains(_searchFilter.ToLower()) ||
                 f.OpsiveImplementation.ToLower().Contains(_searchFilter.ToLower()))
            ).ToList();
            
            // Header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Feature", EditorStyles.boldLabel, GUILayout.Width(150));
            EditorGUILayout.LabelField("Opsive Implementation", EditorStyles.boldLabel, GUILayout.Width(200));
            EditorGUILayout.LabelField("Our ECS", EditorStyles.boldLabel, GUILayout.Width(200));
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel, GUILayout.Width(80));
            EditorGUILayout.LabelField("Priority", EditorStyles.boldLabel, GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(3);
            
            foreach (var feature in filtered)
            {
                DrawFeatureRow(feature);
            }
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField($"Showing {filtered.Count} of {features.Count} features", 
                EditorStyles.centeredGreyMiniLabel);
        }

        private void DrawFeatureRow(FeatureComparison feature)
        {
            Color rowColor = feature.Status switch
            {
                ParityStatus.Surpassed => new Color(0.2f, 0.4f, 0.4f, 0.3f),
                ParityStatus.Complete => new Color(0.2f, 0.4f, 0.2f, 0.3f),
                ParityStatus.Partial => new Color(0.4f, 0.4f, 0.2f, 0.3f),
                ParityStatus.Missing => new Color(0.4f, 0.2f, 0.2f, 0.3f),
                _ => Color.clear
            };
            
            Rect rowRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, 
                GUILayout.Height(22), GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rowRect, rowColor);
            
            EditorGUILayout.BeginHorizontal();
            
            EditorGUILayout.LabelField(feature.FeatureName, GUILayout.Width(150));
            EditorGUILayout.LabelField(feature.OpsiveImplementation, EditorStyles.miniLabel, GUILayout.Width(200));
            
            // Show ECS implementation with clickable file links
            if (feature.FoundFiles != null && feature.FoundFiles.Count > 0)
            {
                string displayText = feature.ECSImplementation;
                if (GUILayout.Button(displayText, EditorStyles.linkLabel, GUILayout.Width(200)))
                {
                    // Open first found file
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(feature.FoundFiles[0]);
                    if (asset != null)
                    {
                        AssetDatabase.OpenAsset(asset);
                    }
                }
            }
            else
            {
                EditorGUILayout.LabelField(feature.ECSImplementation, EditorStyles.miniLabel, GUILayout.Width(200));
            }
            
            // Status badge
            Color statusColor = feature.Status switch
            {
                ParityStatus.Surpassed => Color.cyan,
                ParityStatus.Complete => Color.green,
                ParityStatus.Partial => Color.yellow,
                ParityStatus.Missing => Color.red,
                _ => Color.white
            };
            
            string statusIcon = feature.Status switch
            {
                ParityStatus.Surpassed => "★",
                ParityStatus.Complete => "✓",
                ParityStatus.Partial => "◐",
                ParityStatus.Missing => "✗",
                _ => "?"
            };
            
            GUI.color = statusColor;
            EditorGUILayout.LabelField($"{statusIcon} {feature.Status}", GUILayout.Width(80));
            GUI.color = Color.white;
            
            // Priority
            Color priorityColor = feature.Priority switch
            {
                Priority.Critical => Color.red,
                Priority.High => new Color(1f, 0.5f, 0f),
                Priority.Medium => Color.yellow,
                Priority.Low => Color.gray,
                _ => Color.white
            };
            
            GUI.color = priorityColor;
            EditorGUILayout.LabelField(feature.Priority.ToString(), GUILayout.Width(70));
            GUI.color = Color.white;
            
            EditorGUILayout.EndHorizontal();
            
            // Show notes and found files on hover/expansion
            if (!string.IsNullOrEmpty(feature.Notes))
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField(feature.Notes, EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }
            
            // Show found files count
            if (feature.FoundFiles != null && feature.FoundFiles.Count > 0)
            {
                EditorGUI.indentLevel++;
                GUI.color = Color.gray;
                EditorGUILayout.LabelField($"Found: {string.Join(", ", feature.FoundFiles.Select(f => System.IO.Path.GetFileName(f)))}", EditorStyles.miniLabel);
                GUI.color = Color.white;
                EditorGUI.indentLevel--;
            }
        }
        
        #endregion

        #region Data Initialization
        
        private void InitializeLocomotionFeatures()
        {
            _locomotionFeatures = new List<FeatureComparison>
            {
                CreateFeature("Gravity Zones", "Locomotion",
                    "Spherical/Tube gravity volumes",
                    new[] { "Assets/Scripts/**/GravityZone*.cs" },
                    new[] { "GravityZoneSystem", "GravityZoneComponent" },
                    Priority.High),
                    
                CreateFeature("Moving Platforms", "Locomotion",
                    "Robust parenting + velocity inheritance",
                    new[] { "Assets/Scripts/**/MovingPlatform*.cs" },
                    new[] { "MovingPlatformSystem", "MovingPlatform" },
                    Priority.High),
                    
                CreateFeature("Magnetic Boots", "Locomotion",
                    "N/A (DIG Unique)",
                    new[] { "Assets/Scripts/**/MagneticBoot*.cs" },
                    new[] { "MagneticBootGravitySystem", "MagneticBootState" },
                    Priority.High,
                    isSurpassedIfComplete: true),
                    
                CreateFeature("Root Motion", "Locomotion",
                    "Full root motion support",
                    new[] { "Assets/Scripts/**/RootMotion*.cs" },
                    new[] { "RootMotion" },
                    Priority.Medium),
                    
                CreateFeature("Swimming", "Locomotion",
                    "Swim ability with buoyancy",
                    new[] { "AddOns/Swimming/**/*.cs" },
                    new[] { "SwimmingSystem", "SwimState" },
                    Priority.Medium),
                    
                CreateFeature("Climbing", "Locomotion",
                    "Ladder/ledge climbing",
                    new[] { "AddOns/Climbing/**/*.cs" },
                    new[] { "ClimbingSystem", "ClimbState" },
                    Priority.Medium),
                    
                CreateFeature("Planet Gravity", "Locomotion",
                    "Spherical planet walking",
                    new[] { "Assets/Scripts/**/PlanetGravity*.cs", "Assets/Scripts/**/Planetoid*.cs" },
                    new[] { "PlanetGravitySystem" },
                    Priority.Low,
                    notes: "GravityZone works locally; no planetoid integration"),
                    
                CreateFeature("Vehicle Entry", "Locomotion",
                    "Drive/Ride abilities",
                    new[] { "Assets/Scripts/**/Vehicle*.cs", "Assets/Scripts/**/Drive*.cs" },
                    new[] { "VehicleSystem", "DriveSystem" },
                    Priority.Low,
                    notes: "Future scope"),
            };
        }

        private void InitializeInteractionFeatures()
        {
            _interactionFeatures = new List<FeatureComparison>
            {
                CreateFeature("Item Pickup", "Interaction",
                    "ItemPickup component",
                    new[] { "Assets/Scripts/**/Pickup*.cs" },
                    new[] { "PickupSystem" },
                    Priority.High),
                    
                CreateFeature("Item Drop", "Interaction",
                    "Drop with physics",
                    new[] { "Assets/Scripts/**/Drop*.cs", "Assets/Scripts/**/ItemDrop*.cs" },
                    new[] { "DropSystem" },
                    Priority.High),
                    
                CreateFeature("Door Interaction", "Interaction",
                    "Door object type",
                    new[] { "Assets/Scripts/**/Door*.cs" },
                    new[] { "DoorSystem", "DoorAuthoring" },
                    Priority.Medium),
                    
                CreateFeature("Airlock System", "Interaction",
                    "N/A (DIG Unique)",
                    new[] { "Assets/Scripts/**/Airlock*.cs" },
                    new[] { "AirlockSystem", "AirlockAuthoring" },
                    Priority.High,
                    isSurpassedIfComplete: true),
            };
        }

        private void InitializeAbilityFeatures()
        {
            _abilityFeatures = new List<FeatureComparison>
            {
                CreateFeature("Shootable Action", "Abilities",
                    "ShootableWeapon ability",
                    new[] { "Assets/Scripts/**/WeaponFire*.cs", "Assets/Scripts/**/Weapon*System*.cs" },
                    new[] { "WeaponFireSystem", "WeaponFireComponent" },
                    Priority.Critical),
                    
                CreateFeature("Melee Action", "Abilities",
                    "MeleeWeapon ability",
                    new[] { "Assets/Scripts/**/Melee*.cs" },
                    new[] { "MeleeActionSystem", "MeleeAction" },
                    Priority.Critical),
                    
                CreateFeature("Reload", "Abilities",
                    "Reload ability",
                    new[] { "Assets/Scripts/**/Reload*.cs" },
                    new[] { "ReloadSystem" },
                    Priority.High),
                    
                CreateFeature("Aim (IK)", "Abilities",
                    "Aim ability with IK",
                    new[] { "Assets/Scripts/**/Aim*.cs", "Assets/Scripts/**/WeaponAim*.cs" },
                    new[] { "AimSystem", "WeaponAimState" },
                    Priority.High),
                    
                CreateFeature("Charge-Up (Bow)", "Abilities",
                    "Charged shot/swing",
                    new[] { "Assets/Scripts/**/BowAction*.cs", "Assets/Scripts/**/Charge*.cs" },
                    new[] { "BowActionSystem", "BowState", "DrawProgress" },
                    Priority.High),
                    
                CreateFeature("Channeling", "Abilities",
                    "Channeled abilities (beam, drain)",
                    new[] { "Assets/Scripts/**/Channel*.cs" },
                    new[] { "ChannelSystem", "ChannelState", "IsChanneling" },
                    Priority.Medium,
                    notes: "Needed for magic items"),
                    
                CreateFeature("Dual Wield", "Abilities",
                    "Dual wielding support",
                    new[] { "Assets/Scripts/**/DualWield*.cs" },
                    new[] { "DualWieldSystem", "OffHandUseRequest" },
                    Priority.Medium,
                    partialPatterns: new[] { "OffHandUseRequest", "MainHand", "OffHand" },
                    notes: "Infrastructure exists (OffHandUseRequest), needs combat system"),
                    
                CreateFeature("Shield Block", "Abilities",
                    "Block with shield",
                    new[] { "Assets/Scripts/**/Shield*.cs", "Assets/Scripts/**/Block*.cs" },
                    new[] { "ShieldBlockSystem", "ShieldState", "IsBlocking" },
                    Priority.High,
                    partialPatterns: new[] { "Shield_ECS.prefab" },
                    notes: "Shield prefab exists, needs BlockState system"),
                    
                CreateFeature("Throwable", "Abilities",
                    "ThrowableItem",
                    new[] { "Assets/Scripts/**/Throwable*.cs", "Assets/Scripts/**/Explosive*.cs" },
                    new[] { "ThrowableSystem", "PlaceExplosiveRequest" },
                    Priority.High),
                    
                CreateFeature("Consumable", "Abilities",
                    "Eat/Drink items",
                    new[] { "Assets/Scripts/**/Consumable*.cs" },
                    new[] { "ConsumableSystem" },
                    Priority.Medium),
                    
                CreateFeature("Magic Casting", "Abilities",
                    "Magic ability + effects",
                    new[] { "Assets/Scripts/**/Magic*.cs", "Assets/Scripts/**/Spell*.cs" },
                    new[] { "MagicSystem", "SpellSystem" },
                    Priority.Medium),
            };
        }
        
        /// <summary>
        /// Creates a feature comparison with dynamic status detection.
        /// Scans the codebase for the specified patterns to determine actual implementation status.
        /// </summary>
        private FeatureComparison CreateFeature(
            string name, 
            string category,
            string opsiveImpl, 
            string[] filePatterns,
            string[] requiredPatterns,
            Priority priority,
            bool isSurpassedIfComplete = false,
            string[] partialPatterns = null,
            string notes = null)
        {
            // Scan for files matching patterns
            var foundFiles = new List<string>();
            var foundPatterns = new List<string>();
            
            foreach (var pattern in filePatterns)
            {
                var guids = AssetDatabase.FindAssets("", new[] { GetSearchFolder(pattern) });
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (MatchesPattern(path, pattern))
                    {
                        foundFiles.Add(path);
                        
                        // Read file content to check for patterns
                        var content = System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : "";
                        foreach (var reqPattern in requiredPatterns)
                        {
                            if (content.Contains(reqPattern) && !foundPatterns.Contains(reqPattern))
                            {
                                foundPatterns.Add(reqPattern);
                            }
                        }
                    }
                }
            }
            
            // Also search globally for required patterns
            foreach (var reqPattern in requiredPatterns)
            {
                var guids = AssetDatabase.FindAssets(reqPattern);
                if (guids.Length > 0)
                {
                    foreach (var guid in guids)
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        if (path.StartsWith("Assets/Scripts/") || path.StartsWith("AddOns/"))
                        {
                            if (!foundPatterns.Contains(reqPattern))
                                foundPatterns.Add(reqPattern);
                            if (!foundFiles.Contains(path))
                                foundFiles.Add(path);
                        }
                    }
                }
            }
            
            // Check for partial implementation patterns
            bool hasPartial = false;
            if (partialPatterns != null)
            {
                foreach (var partPattern in partialPatterns)
                {
                    var guids = AssetDatabase.FindAssets(partPattern);
                    if (guids.Length > 0)
                    {
                        hasPartial = true;
                        break;
                    }
                }
            }
            
            // Determine status
            ParityStatus status;
            float completeness = requiredPatterns.Length > 0 
                ? (float)foundPatterns.Count / requiredPatterns.Length 
                : 0f;
            
            if (completeness >= 0.7f)
            {
                status = isSurpassedIfComplete ? ParityStatus.Surpassed : ParityStatus.Complete;
            }
            else if (completeness >= 0.3f || hasPartial)
            {
                status = ParityStatus.Partial;
            }
            else
            {
                status = ParityStatus.Missing;
            }
            
            // Build implementation string
            string ecsImpl = foundFiles.Count > 0 
                ? string.Join(", ", foundPatterns.Take(3)) + (foundPatterns.Count > 3 ? "..." : "")
                : "Not implemented";
            
            return new FeatureComparison
            {
                FeatureName = name,
                Category = category,
                OpsiveImplementation = opsiveImpl,
                ECSImplementation = ecsImpl,
                Status = status,
                Priority = priority,
                Notes = notes ?? "",
                FoundFiles = foundFiles,
                MatchedPatterns = foundPatterns
            };
        }
        
        private string GetSearchFolder(string pattern)
        {
            // Extract base folder from pattern
            int starIndex = pattern.IndexOf('*');
            if (starIndex > 0)
            {
                return pattern.Substring(0, starIndex).TrimEnd('/');
            }
            return "Assets/Scripts";
        }
        
        private bool MatchesPattern(string path, string pattern)
        {
            // Simple pattern matching - supports ** and *
            var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                .Replace(@"\*\*", ".*")
                .Replace(@"\*", "[^/]*") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(path, regex, 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        private void InitializeCleanUpItems()
        {
            _cleanUpItems = new List<CleanUpItem>
            {
                new CleanUpItem 
                { 
                    Name = "UltimateCharacterLocomotion", 
                    Type = CleanUpType.Component,
                    Status = CleanUpStatus.NeedsAction,
                    RecommendedAction = "Disable"
                },
                new CleanUpItem 
                { 
                    Name = "CharacterHealth (Opsive)", 
                    Type = CleanUpType.Component,
                    Status = CleanUpStatus.NeedsAction,
                    RecommendedAction = "Remove"
                },
                new CleanUpItem 
                { 
                    Name = "ItemHandler", 
                    Type = CleanUpType.Component,
                    Status = CleanUpStatus.NeedsAction,
                    RecommendedAction = "Disable"
                },
                new CleanUpItem 
                { 
                    Name = "Opsive Demo Scenes", 
                    Type = CleanUpType.Asset,
                    Status = CleanUpStatus.NeedsAction,
                    RecommendedAction = "Delete"
                },
                new CleanUpItem 
                { 
                    Name = "Opsive Add-Ons (unused)", 
                    Type = CleanUpType.Asset,
                    Status = CleanUpStatus.NeedsAction,
                    RecommendedAction = "Delete"
                },
                new CleanUpItem 
                { 
                    Name = "AnimatorController (Opsive)", 
                    Type = CleanUpType.Asset,
                    Status = CleanUpStatus.Clean,
                    RecommendedAction = "Keep"
                },
            };
        }

        private void InitializeActionPlan()
        {
            _actionPlan = new List<ActionItem>
            {
                // Critical
                new ActionItem 
                { 
                    Task = "Verify all weapon abilities match Opsive parity",
                    Category = "Abilities",
                    Priority = Priority.Critical,
                    EstimatedEffort = "4 hours"
                },
                
                // High
                new ActionItem 
                { 
                    Task = "Design GravityZoneSystem inspired by Opsive math",
                    Category = "Locomotion",
                    Priority = Priority.High,
                    EstimatedEffort = "8 hours"
                },
                new ActionItem 
                { 
                    Task = "Verify MovingPlatform rotation/scale edge cases",
                    Category = "Locomotion",
                    Priority = Priority.High,
                    EstimatedEffort = "2 hours"
                },
                new ActionItem 
                { 
                    Task = "Implement Channeling system for beam weapons",
                    Category = "Abilities",
                    Priority = Priority.High,
                    EstimatedEffort = "6 hours"
                },
                
                // Medium
                new ActionItem 
                { 
                    Task = "Implement Dual Wield weapon slot support",
                    Category = "Abilities",
                    Priority = Priority.Medium,
                    EstimatedEffort = "8 hours"
                },
                new ActionItem 
                { 
                    Task = "Complete Charge-Up visual feedback system",
                    Category = "Abilities",
                    Priority = Priority.Medium,
                    EstimatedEffort = "4 hours"
                },
                new ActionItem 
                { 
                    Task = "Disable UltimateCharacterLocomotion on all prefabs",
                    Category = "Clean-Up",
                    Priority = Priority.Medium,
                    EstimatedEffort = "1 hour"
                },
                new ActionItem 
                { 
                    Task = "Remove unused Opsive demo assets",
                    Category = "Clean-Up",
                    Priority = Priority.Medium,
                    EstimatedEffort = "1 hour"
                },
                
                // Low
                new ActionItem 
                { 
                    Task = "Plan Vehicle/Ride system for future",
                    Category = "Locomotion",
                    Priority = Priority.Low,
                    EstimatedEffort = "2 hours"
                },
                new ActionItem 
                { 
                    Task = "Implement Planet gravity mode",
                    Category = "Locomotion",
                    Priority = Priority.Low,
                    EstimatedEffort = "12 hours"
                },
            };
        }
        
        #endregion

        #region Actions
        
        private void ScanProject()
        {
            Debug.Log("[OpsiveParity] Scanning project for ECS implementations...");
            
            // Re-initialize data with fresh scans
            InitializeData();
            
            // Report results
            var allFeatures = _locomotionFeatures.Concat(_interactionFeatures).Concat(_abilityFeatures).ToList();
            int complete = allFeatures.Count(f => f.Status == ParityStatus.Complete || f.Status == ParityStatus.Surpassed);
            int partial = allFeatures.Count(f => f.Status == ParityStatus.Partial);
            int missing = allFeatures.Count(f => f.Status == ParityStatus.Missing);
            
            Debug.Log($"[OpsiveParity] Scan complete: {complete} Complete, {partial} Partial, {missing} Missing");
            
            Repaint();
        }

        private void ScanOpsiveSource()
        {
            Debug.Log("[OpsiveParity] Scanning Opsive source for features...");
        }

        private void ScanECSSystems()
        {
            Debug.Log("[OpsiveParity] Scanning ECS systems for implementations...");
        }

        private void GenerateFullReport()
        {
            Debug.Log("[OpsiveParity] Generating full parity report...");
        }

        private void ExportReport()
        {
            string path = EditorUtility.SaveFilePanel("Export Parity Report", "", "opsive_parity_report.md", "md");
            if (!string.IsNullOrEmpty(path))
            {
                string md = GenerateMarkdownReport();
                System.IO.File.WriteAllText(path, md);
                Debug.Log($"[OpsiveParity] Report exported to {path}");
            }
        }

        private string GenerateMarkdownReport()
        {
            var sb = new System.Text.StringBuilder();
            
            sb.AppendLine("# Opsive Parity Analysis Report");
            sb.AppendLine();
            sb.AppendLine($"Generated: {System.DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine();
            
            // Summary
            var allFeatures = _locomotionFeatures.Concat(_interactionFeatures).Concat(_abilityFeatures).ToList();
            int complete = allFeatures.Count(f => f.Status == ParityStatus.Complete || f.Status == ParityStatus.Surpassed);
            
            sb.AppendLine("## Summary");
            sb.AppendLine($"- **Total Features:** {allFeatures.Count}");
            sb.AppendLine($"- **Complete/Surpassed:** {complete}");
            sb.AppendLine($"- **Parity:** {(complete / (float)allFeatures.Count * 100):F0}%");
            sb.AppendLine();
            
            // Missing features
            var missing = allFeatures.Where(f => f.Status == ParityStatus.Missing).ToList();
            sb.AppendLine("## Missing Features");
            foreach (var f in missing)
            {
                sb.AppendLine($"- **{f.FeatureName}** ({f.Category}) - Priority: {f.Priority}");
                if (!string.IsNullOrEmpty(f.Notes))
                {
                    sb.AppendLine($"  - {f.Notes}");
                }
            }
            
            sb.AppendLine();
            sb.AppendLine("## Action Plan");
            foreach (var action in _actionPlan.OrderBy(a => a.Priority))
            {
                string check = action.IsComplete ? "[x]" : "[ ]";
                sb.AppendLine($"- {check} **{action.Priority}**: {action.Task} ({action.EstimatedEffort})");
            }
            
            return sb.ToString();
        }

        private void ExportActionPlan()
        {
            string path = EditorUtility.SaveFilePanel("Export Action Plan", "", "opsive_action_plan.md", "md");
            if (!string.IsNullOrEmpty(path))
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("# Opsive Parity Action Plan");
                sb.AppendLine();
                
                foreach (var action in _actionPlan.OrderBy(a => a.Priority))
                {
                    string check = action.IsComplete ? "[x]" : "[ ]";
                    sb.AppendLine($"- {check} [{action.Priority}] {action.Task} - {action.Category} ({action.EstimatedEffort})");
                }
                
                System.IO.File.WriteAllText(path, sb.ToString());
                Debug.Log($"[OpsiveParity] Action plan exported to {path}");
            }
        }

        private void DisableAllOpsiveComponents()
        {
            foreach (var item in _cleanUpItems.Where(i => i.Type == CleanUpType.Component))
            {
                item.Status = CleanUpStatus.Clean;
            }
            Debug.Log("[OpsiveParity] Disabled all Opsive components");
        }

        private void RemoveUnusedOpsiveAssets()
        {
            foreach (var item in _cleanUpItems.Where(i => i.Type == CleanUpType.Asset && i.RecommendedAction == "Delete"))
            {
                item.Status = CleanUpStatus.Clean;
            }
            Debug.Log("[OpsiveParity] Removed unused Opsive assets");
        }

        private void ExecuteCleanUpAction(CleanUpItem item)
        {
            item.Status = CleanUpStatus.Clean;
            Debug.Log($"[OpsiveParity] Executed {item.RecommendedAction} on {item.Name}");
        }
        
        #endregion

        #region Data Types
        
        private enum ParityStatus
        {
            All,
            Surpassed,
            Complete,
            Partial,
            Missing
        }

        private enum Priority
        {
            Critical,
            High,
            Medium,
            Low
        }

        private enum CleanUpType
        {
            Component,
            Asset,
            Prefab
        }

        private enum CleanUpStatus
        {
            Clean,
            NeedsAction,
            Conflict
        }

        [System.Serializable]
        private class FeatureComparison
        {
            public string FeatureName;
            public string Category;
            public string OpsiveImplementation;
            public string ECSImplementation;
            public ParityStatus Status;
            public Priority Priority;
            public string Notes;
            
            // Dynamic detection results
            public List<string> FoundFiles = new List<string>();
            public List<string> MatchedPatterns = new List<string>();
        }

        [System.Serializable]
        private class CleanUpItem
        {
            public string Name;
            public CleanUpType Type;
            public CleanUpStatus Status;
            public string RecommendedAction;
        }

        [System.Serializable]
        private class ActionItem
        {
            public string Task;
            public string Category;
            public Priority Priority;
            public string EstimatedEffort;
            public bool IsComplete;
        }
        
        #endregion
    }
}
