using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DIG.Lobby.Editor
{
    /// <summary>
    /// EPIC 17.4: One-click lobby system setup module for the Lobby Workstation.
    /// Creates all ScriptableObject assets, LobbyManager singleton,
    /// full UI Canvas hierarchy, and wires all serialized field references.
    /// </summary>
    public class LobbySetupModule : ILobbyWorkstationModule
    {
        public string ModuleName => "Setup Wizard";

        private bool _overwriteExisting;
        private bool _hasConfig, _hasDefaultMap, _hasDefaultDifficulty;
        private bool _hasLobbyManager, _hasLobbyCanvas, _hasEntryPrefab;
        private bool _statusDirty = true;

        // ── Color Palette ──────────────────────────────
        static readonly Color ColOverlay       = new Color(0.00f, 0.00f, 0.02f, 0.95f);
        static readonly Color ColPanelBg       = new Color(0.07f, 0.08f, 0.12f, 0.98f);
        static readonly Color ColTitleBar      = new Color(0.10f, 0.12f, 0.20f, 1.00f);
        static readonly Color ColSection       = new Color(0.09f, 0.10f, 0.15f, 1.00f);
        static readonly Color ColRow           = new Color(0.11f, 0.12f, 0.17f, 0.95f);
        static readonly Color ColRowAlt        = new Color(0.13f, 0.14f, 0.20f, 0.95f);
        static readonly Color ColDivider       = new Color(0.25f, 0.27f, 0.35f, 0.40f);
        static readonly Color ColInputBg       = new Color(0.05f, 0.06f, 0.10f, 1.00f);
        static readonly Color ColBtnNormal     = new Color(0.24f, 0.27f, 0.38f, 1.00f);
        static readonly Color ColBtnPrimary    = new Color(0.15f, 0.58f, 0.35f, 1.00f);
        static readonly Color ColBtnDanger     = new Color(0.62f, 0.18f, 0.18f, 1.00f);
        static readonly Color ColTextPrimary   = new Color(0.90f, 0.91f, 0.94f, 1.00f);
        static readonly Color ColTextSecondary = new Color(0.55f, 0.57f, 0.64f, 1.00f);
        static readonly Color ColTextAccent    = new Color(0.45f, 0.65f, 0.88f, 1.00f);
        static readonly Color ColGreen         = new Color(0.25f, 0.75f, 0.30f, 1.00f);
        static readonly Color ColRed           = new Color(0.75f, 0.25f, 0.25f, 1.00f);
        static readonly Color ColGold          = new Color(1.00f, 0.82f, 0.20f, 1.00f);

        // ── Editor GUI ─────────────────────────────────

        private void RefreshStatus()
        {
            _hasConfig = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Resources/LobbyConfig.asset") != null;
            _hasDefaultMap = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Resources/Maps/DefaultMap.asset") != null;
            _hasDefaultDifficulty = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Resources/Difficulties/Normal.asset") != null;
            _hasLobbyManager = GameObject.FindFirstObjectByType<LobbyManager>() != null;
            _hasLobbyCanvas = GameObject.Find("LobbyCanvas") != null;
            _hasEntryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Lobby/LobbyListEntry.prefab") != null;
            _statusDirty = false;
        }

        public void OnGUI()
        {
            if (_statusDirty) RefreshStatus();

            EditorGUILayout.LabelField("Lobby Setup Wizard", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Creates all assets, GameObjects, and UI needed for the lobby system. " +
                "Run once in your main scene. Existing assets are skipped unless 'Overwrite' is checked.",
                MessageType.Info);
            EditorGUILayout.Space(4);

            _overwriteExisting = EditorGUILayout.Toggle("Overwrite Existing", _overwriteExisting);
            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            StatusRow("LobbyConfig SO", _hasConfig);
            StatusRow("Default Map SO", _hasDefaultMap);
            StatusRow("Default Difficulty SO", _hasDefaultDifficulty);
            StatusRow("LobbyManager (Scene)", _hasLobbyManager);
            StatusRow("LobbyCanvas (Scene)", _hasLobbyCanvas);
            StatusRow("LobbyListEntry Prefab", _hasEntryPrefab);
            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("Individual Steps", EditorStyles.boldLabel);
            if (GUILayout.Button("1. Create ScriptableObject Assets"))
                EditorApplication.delayCall += () => { CreateConfigAsset(); CreateDefaultMapAsset(); CreateDefaultDifficultyAssets(); _statusDirty = true; };
            if (GUILayout.Button("2. Create LobbyManager GameObject"))
                EditorApplication.delayCall += () => { CreateLobbyManager(); _statusDirty = true; };
            if (GUILayout.Button("3. Create Lobby UI Canvas"))
                EditorApplication.delayCall += () => { CreateLobbyCanvas(); _statusDirty = true; };

            EditorGUILayout.Space(12);
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("Setup Everything", GUILayout.Height(36)))
                EditorApplication.delayCall += () => { CreateConfigAsset(); CreateDefaultMapAsset(); CreateDefaultDifficultyAssets(); CreateLobbyManager(); CreateLobbyCanvas(); _statusDirty = true; Debug.Log("[LobbySetup] Setup complete."); };
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Refresh Status")) _statusDirty = true;
        }

        public void OnSceneGUI(SceneView sceneView) { }

        static void StatusRow(string label, bool ok)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(ok ? "\u2713" : "\u2717", GUILayout.Width(20));
            EditorGUILayout.LabelField(label);
            EditorGUILayout.EndHorizontal();
        }

        // ── 1. ScriptableObject Assets ─────────────────

        void CreateConfigAsset()
        {
            const string path = "Assets/Resources/LobbyConfig.asset";
            if (!_overwriteExisting && AssetDatabase.LoadAssetAtPath<ScriptableObject>(path) != null) return;
            EnsureDirectory("Assets/Resources");
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<LobbyConfigSO>(), path);
            Debug.Log($"[LobbySetup] Created {path}");
        }

        void CreateDefaultMapAsset()
        {
            const string path = "Assets/Resources/Maps/DefaultMap.asset";
            if (!_overwriteExisting && AssetDatabase.LoadAssetAtPath<ScriptableObject>(path) != null) return;
            EnsureDirectory("Assets/Resources/Maps");
            var map = ScriptableObject.CreateInstance<MapDefinitionSO>();
            map.MapId = 0; map.DisplayName = "Default Map"; map.Description = "The default multiplayer map.";
            map.MinPlayers = 1; map.MaxPlayers = 4; map.EstimatedMinutes = 30;
            map.SpawnPositions = new Vector3[4]; map.SpawnRotations = new Quaternion[4];
            for (int i = 0; i < 4; i++) { map.SpawnPositions[i] = new Vector3(i * 3f, 1f, 0f); map.SpawnRotations[i] = Quaternion.identity; }
            AssetDatabase.CreateAsset(map, path);
            Debug.Log($"[LobbySetup] Created {path}");
        }

        void CreateDefaultDifficultyAssets()
        {
            EnsureDirectory("Assets/Resources/Difficulties");
            MakeDifficulty("Assets/Resources/Difficulties/Normal.asset", 0, "Normal", 1f, 1f, 1f, 1f, 0f, 1f, 1f);
            MakeDifficulty("Assets/Resources/Difficulties/Hard.asset", 1, "Hard", 1.5f, 1.3f, 1.2f, 1.2f, 0.1f, 1.5f, 1.25f);
            MakeDifficulty("Assets/Resources/Difficulties/Nightmare.asset", 2, "Nightmare", 2.5f, 2f, 1.5f, 1.5f, 0.25f, 2.5f, 2f);
        }

        void MakeDifficulty(string path, int id, string name, float hp, float dmg, float spawn, float lootQ, float lootB, float xp, float cur)
        {
            if (!_overwriteExisting && AssetDatabase.LoadAssetAtPath<ScriptableObject>(path) != null) return;
            var d = ScriptableObject.CreateInstance<DifficultyDefinitionSO>();
            d.DifficultyId = id; d.DisplayName = name; d.Description = $"{name} difficulty.";
            d.EnemyHealthScale = hp; d.EnemyDamageScale = dmg; d.EnemySpawnRateScale = spawn;
            d.LootQuantityScale = lootQ; d.LootQualityBonus = lootB; d.XPMultiplier = xp; d.CurrencyMultiplier = cur;
            AssetDatabase.CreateAsset(d, path);
            Debug.Log($"[LobbySetup] Created {path}");
        }

        // ── 2. LobbyManager ────────────────────────────

        void CreateLobbyManager()
        {
            var existing = GameObject.FindFirstObjectByType<LobbyManager>();
            if (existing != null && !_overwriteExisting) return;
            if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);
            var go = new GameObject("LobbyManager");
            Undo.RegisterCreatedObjectUndo(go, "Create LobbyManager");
            go.AddComponent<LobbyManager>();
            Debug.Log("[LobbySetup] Created LobbyManager.");
        }

        // ── 3. Lobby UI Canvas ─────────────────────────

        void CreateLobbyCanvas()
        {
            var old = GameObject.Find("LobbyCanvas");
            if (old != null && !_overwriteExisting) return;
            if (old != null) Undo.DestroyObjectImmediate(old);

            // Canvas
            var canvasGo = new GameObject("LobbyCanvas");
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create LobbyCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // LobbyRoot
            var lobbyRoot = Child(canvasGo.transform, "LobbyRoot");
            Stretch(lobbyRoot);

            // Full-screen dark overlay (inside LobbyRoot so it hides with panels)
            var overlay = Child(lobbyRoot.transform, "Overlay");
            Stretch(overlay);
            overlay.AddComponent<Image>().color = ColOverlay;

            var uiManager = canvasGo.AddComponent<LobbyUIManager>();

            // ════════ BROWSER PANEL ════════
            var browserGo = MakePanel(lobbyRoot.transform, "BrowserPanel");
            var browserPanel = browserGo.AddComponent<LobbyBrowserPanel>();

            // Title bar
            TitleBar(browserGo.transform, "LOBBY BROWSER");

            // Content area with padding
            var browserContent = ContentArea(browserGo.transform);

            // Filter row
            var filterRow = Row(browserContent, 36);
            var filterLayout = filterRow.AddComponent<HorizontalLayoutGroup>();
            filterLayout.spacing = 8; filterLayout.childForceExpandWidth = false; filterLayout.childForceExpandHeight = true;
            filterLayout.padding = new RectOffset(0, 0, 0, 0);

            var searchField = StyledInputField(filterRow.transform, "SearchField", "Search lobbies...");
            searchField.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var mapDd = StyledDropdown(filterRow.transform, "MapDropdown", new[] { "Any Map" });
            mapDd.gameObject.AddComponent<LayoutElement>().preferredWidth = 160;
            var diffDd = StyledDropdown(filterRow.transform, "DifficultyDropdown", new[] { "Any Difficulty" });
            diffDd.gameObject.AddComponent<LayoutElement>().preferredWidth = 160;
            var showFullToggle = StyledToggle(filterRow.transform, "ShowFullToggle", "Show Full");
            showFullToggle.gameObject.AddComponent<LayoutElement>().preferredWidth = 110;

            // Ping row
            var pingRow = Row(browserContent, 28);
            var pingLayout = pingRow.AddComponent<HorizontalLayoutGroup>();
            pingLayout.spacing = 10; pingLayout.childForceExpandHeight = true;
            pingLayout.childForceExpandWidth = false;
            Label(pingRow.transform, "PingLabel", "Max Ping:", 13, ColTextSecondary, TextAnchor.MiddleLeft)
                .AddComponent<LayoutElement>().preferredWidth = 75;
            var pingSlider = StyledSlider(pingRow.transform, "MaxPingSlider", 50, 500, 250);
            pingSlider.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var pingValueLabel = Label(pingRow.transform, "PingValueText", "250ms", 13, ColTextAccent, TextAnchor.MiddleRight);
            pingValueLabel.AddComponent<LayoutElement>().preferredWidth = 55;

            Divider(browserContent);

            // Lobby list
            var listScroll = StyledScrollView(browserContent, "LobbyListScroll");
            listScroll.AddComponent<LayoutElement>().flexibleHeight = 1;
            var listContent = listScroll.GetComponentInChildren<ScrollRect>().content.gameObject;
            var listVlg = listContent.AddComponent<VerticalLayoutGroup>();
            listVlg.spacing = 2; listVlg.childForceExpandWidth = true; listVlg.childForceExpandHeight = false;
            listVlg.padding = new RectOffset(4, 4, 4, 4);
            listContent.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Divider(browserContent);

            // Join code row
            var joinRow = Row(browserContent, 44);
            var joinLayout = joinRow.AddComponent<HorizontalLayoutGroup>();
            joinLayout.spacing = 10; joinLayout.childForceExpandHeight = true; joinLayout.childForceExpandWidth = false;
            Label(joinRow.transform, "JoinLabel", "Join Code:", 14, ColTextSecondary, TextAnchor.MiddleLeft)
                .AddComponent<LayoutElement>().preferredWidth = 80;
            var joinCodeInput = StyledInputField(joinRow.transform, "JoinCodeInput", "ABC123");
            joinCodeInput.characterLimit = 6;
            joinCodeInput.gameObject.AddComponent<LayoutElement>().preferredWidth = 140;
            Label(joinRow.transform, "HostIpLabel", "Host IP:", 14, ColTextSecondary, TextAnchor.MiddleLeft)
                .AddComponent<LayoutElement>().preferredWidth = 60;
            var hostIpInput = StyledInputField(joinRow.transform, "HostIpInput", "127.0.0.1");
            hostIpInput.gameObject.AddComponent<LayoutElement>().preferredWidth = 140;
            var joinByCodeBtn = StyledButton(joinRow.transform, "JoinByCodeButton", "JOIN", ColBtnPrimary);
            joinByCodeBtn.gameObject.AddComponent<LayoutElement>().preferredWidth = 80;
            // Spacer
            var spacer1 = Child(joinRow.transform, "Spacer");
            spacer1.AddComponent<LayoutElement>().flexibleWidth = 1;

            // Browser status text — shows errors like "Connection timed out"
            var browserStatusGo = Child(browserGo.transform, "StatusText");
            var browserStatusLE = browserStatusGo.AddComponent<LayoutElement>();
            browserStatusLE.preferredHeight = 28;
            var browserStatusText = browserStatusGo.AddComponent<Text>();
            browserStatusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            browserStatusText.fontSize = 14;
            browserStatusText.alignment = TextAnchor.MiddleCenter;
            browserStatusText.color = new Color(1f, 0.4f, 0.4f);
            browserStatusText.text = "";
            browserStatusGo.SetActive(false);

            // Action bar — pinned to panel bottom (child of panel, not content)
            var actionBar = Child(browserGo.transform, "ActionBar");
            actionBar.AddComponent<Image>().color = new Color(0.12f, 0.14f, 0.22f, 1.00f);
            actionBar.AddComponent<LayoutElement>().preferredHeight = 44;
            var actionLayout = actionBar.AddComponent<HorizontalLayoutGroup>();
            actionLayout.spacing = 12; actionLayout.childForceExpandWidth = true; actionLayout.childForceExpandHeight = true;
            actionLayout.padding = new RectOffset(20, 20, 4, 4);

            var refreshBtn = StyledButton(actionBar.transform, "RefreshButton", "REFRESH", ColBtnNormal);
            var createBtn = StyledButton(actionBar.transform, "CreateGameButton", "CREATE GAME", ColBtnPrimary);
            var createBtnText = createBtn.GetComponentInChildren<Text>();
            if (createBtnText != null) createBtnText.fontSize = 16;
            var quickMatchBtn = StyledButton(actionBar.transform, "QuickMatchButton", "QUICK MATCH", ColBtnNormal);

            // Wire BrowserPanel
            var bSO = new SerializedObject(browserPanel);
            bSO.FindProperty("_listContainer").objectReferenceValue = listContent.transform;
            bSO.FindProperty("_searchField").objectReferenceValue = searchField;
            bSO.FindProperty("_mapDropdown").objectReferenceValue = mapDd;
            bSO.FindProperty("_difficultyDropdown").objectReferenceValue = diffDd;
            bSO.FindProperty("_showFullToggle").objectReferenceValue = showFullToggle;
            bSO.FindProperty("_maxPingSlider").objectReferenceValue = pingSlider;
            bSO.FindProperty("_maxPingLabel").objectReferenceValue = pingValueLabel.GetComponent<Text>();
            bSO.FindProperty("_joinCodeInput").objectReferenceValue = joinCodeInput;
            bSO.FindProperty("_hostIpInput").objectReferenceValue = hostIpInput;
            bSO.FindProperty("_joinByCodeButton").objectReferenceValue = joinByCodeBtn;
            bSO.FindProperty("_refreshButton").objectReferenceValue = refreshBtn;
            bSO.FindProperty("_createGameButton").objectReferenceValue = createBtn;
            bSO.FindProperty("_quickMatchButton").objectReferenceValue = quickMatchBtn;
            bSO.FindProperty("_statusText").objectReferenceValue = browserStatusText;
            bSO.ApplyModifiedPropertiesWithoutUndo();

            // ════════ ROOM PANEL ════════
            var roomGo = MakePanel(lobbyRoot.transform, "RoomPanel");
            var roomPanel = roomGo.AddComponent<LobbyRoomPanel>();

            // Title bar with join code
            var roomTitle = TitleBar(roomGo.transform, "LOBBY ROOM");
            // Add join code display into the title bar
            var titleHlg = roomTitle.GetComponent<HorizontalLayoutGroup>();
            if (titleHlg == null) { titleHlg = roomTitle.AddComponent<HorizontalLayoutGroup>(); titleHlg.childForceExpandHeight = true; }
            // Restructure: title text + spacer + code label + code value + copy btn
            var existingTitle = roomTitle.transform.Find("TitleLabel");
            if (existingTitle != null) existingTitle.GetComponent<LayoutElement>().flexibleWidth = 0;
            var titleSpacer = Child(roomTitle.transform, "Spacer");
            titleSpacer.AddComponent<LayoutElement>().flexibleWidth = 1;
            Label(roomTitle.transform, "CodeLabel", "Code:", 13, ColTextSecondary, TextAnchor.MiddleRight)
                .AddComponent<LayoutElement>().preferredWidth = 45;
            var joinCodeText = Label(roomTitle.transform, "JoinCodeText", "------", 16, ColTextAccent, TextAnchor.MiddleLeft);
            joinCodeText.GetComponent<Text>().fontStyle = FontStyle.Bold;
            joinCodeText.AddComponent<LayoutElement>().preferredWidth = 80;
            var copyCodeBtn = StyledButton(roomTitle.transform, "CopyCodeButton", "COPY", ColBtnNormal);
            copyCodeBtn.gameObject.AddComponent<LayoutElement>().preferredWidth = 60;
            copyCodeBtn.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
            // Fix: remove duplicate LayoutElement
            var copyLEs = copyCodeBtn.gameObject.GetComponents<LayoutElement>();
            if (copyLEs.Length > 1) Object.DestroyImmediate(copyLEs[1]);
            var copySingleLE = copyCodeBtn.gameObject.GetComponent<LayoutElement>();
            copySingleLE.preferredWidth = 60; copySingleLE.preferredHeight = 30;

            var roomContent = ContentArea(roomGo.transform);

            // Host controls — label+dropdown grouped so they stay aligned
            var hostRow = Row(roomContent, 44);
            var hostLayout = hostRow.AddComponent<HorizontalLayoutGroup>();
            hostLayout.spacing = 16;
            hostLayout.childControlWidth = true;
            hostLayout.childControlHeight = true;
            hostLayout.childForceExpandHeight = true;
            hostLayout.childForceExpandWidth = false;

            // Map group
            var mapGroup = Child(hostRow.transform, "MapGroup");
            mapGroup.AddComponent<LayoutElement>().flexibleWidth = 1;
            var mapGroupHlg = mapGroup.AddComponent<HorizontalLayoutGroup>();
            mapGroupHlg.spacing = 8;
            mapGroupHlg.childControlWidth = true;
            mapGroupHlg.childControlHeight = true;
            mapGroupHlg.childForceExpandHeight = true;
            mapGroupHlg.childForceExpandWidth = false;
            Label(mapGroup.transform, "MapLabel", "Map:", 14, ColTextSecondary, TextAnchor.MiddleLeft)
                .AddComponent<LayoutElement>().preferredWidth = 40;
            var roomMapDd = StyledDropdown(mapGroup.transform, "MapDropdown", new[] { "Default Map" });
            roomMapDd.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            // Difficulty group
            var diffGroup = Child(hostRow.transform, "DiffGroup");
            diffGroup.AddComponent<LayoutElement>().flexibleWidth = 1;
            var diffGroupHlg = diffGroup.AddComponent<HorizontalLayoutGroup>();
            diffGroupHlg.spacing = 8;
            diffGroupHlg.childControlWidth = true;
            diffGroupHlg.childControlHeight = true;
            diffGroupHlg.childForceExpandHeight = true;
            diffGroupHlg.childForceExpandWidth = false;
            Label(diffGroup.transform, "DiffLabel", "Difficulty:", 14, ColTextSecondary, TextAnchor.MiddleLeft)
                .AddComponent<LayoutElement>().preferredWidth = 75;
            var roomDiffDd = StyledDropdown(diffGroup.transform, "DifficultyDropdown", new[] { "Normal", "Hard", "Nightmare" });
            roomDiffDd.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            Divider(roomContent);

            // ── Two-column body: Players (left) | Chat (right) ──
            var bodyRow = Child(roomContent, "BodyRow");
            bodyRow.AddComponent<LayoutElement>().flexibleHeight = 1;
            var bodyHlg = bodyRow.AddComponent<HorizontalLayoutGroup>();
            bodyHlg.spacing = 10;
            bodyHlg.childControlWidth = true;
            bodyHlg.childControlHeight = true;
            bodyHlg.childForceExpandWidth = false;
            bodyHlg.childForceExpandHeight = true;

            // ── Players Column (left) ──
            var playersCol = Child(bodyRow.transform, "PlayersColumn");
            playersCol.AddComponent<LayoutElement>().flexibleWidth = 0.45f;
            var playersVlg = playersCol.AddComponent<VerticalLayoutGroup>();
            playersVlg.spacing = 4;
            playersVlg.childControlWidth = true;
            playersVlg.childControlHeight = true;
            playersVlg.childForceExpandWidth = true;
            playersVlg.childForceExpandHeight = false;

            Label(playersCol.transform, "PlayersHeader", "PLAYERS", 12, ColTextSecondary, TextAnchor.MiddleLeft)
                .AddComponent<LayoutElement>().preferredHeight = 24;

            var slotsContainer = Child(playersCol.transform, "PlayerSlots");
            slotsContainer.AddComponent<LayoutElement>().flexibleHeight = 1;
            var slotsVlg = slotsContainer.AddComponent<VerticalLayoutGroup>();
            slotsVlg.spacing = 3;
            slotsVlg.childControlWidth = true;
            slotsVlg.childControlHeight = true;
            slotsVlg.childForceExpandWidth = true;
            slotsVlg.childForceExpandHeight = false;

            var playerSlots = new LobbyPlayerSlotUI[4];
            for (int i = 0; i < 4; i++)
                playerSlots[i] = BuildPlayerSlot(slotsContainer.transform, i);

            // ── Vertical divider ──
            var vDivider = Child(bodyRow.transform, "VDivider");
            vDivider.AddComponent<Image>().color = ColDivider;
            vDivider.AddComponent<LayoutElement>().preferredWidth = 1;

            // ── Chat Column (right) ──
            var chatCol = Child(bodyRow.transform, "ChatColumn");
            chatCol.AddComponent<LayoutElement>().flexibleWidth = 0.55f;
            var chatVlg = chatCol.AddComponent<VerticalLayoutGroup>();
            chatVlg.spacing = 4;
            chatVlg.childControlWidth = true;
            chatVlg.childControlHeight = true;
            chatVlg.childForceExpandWidth = true;
            chatVlg.childForceExpandHeight = false;

            Label(chatCol.transform, "ChatHeader", "CHAT", 12, ColTextSecondary, TextAnchor.MiddleLeft)
                .AddComponent<LayoutElement>().preferredHeight = 24;

            // Chat scroll
            var chatScroll = StyledScrollView(chatCol.transform, "ChatScroll");
            chatScroll.AddComponent<LayoutElement>().flexibleHeight = 1;
            var chatSR = chatScroll.GetComponentInChildren<ScrollRect>();
            var chatContentGo = chatSR.content.gameObject;

            var chatContentLayout = chatContentGo.AddComponent<VerticalLayoutGroup>();
            chatContentLayout.childControlWidth = true;
            chatContentLayout.childControlHeight = true;
            chatContentLayout.childForceExpandWidth = true;
            chatContentLayout.childForceExpandHeight = false;
            chatContentLayout.padding = new RectOffset(6, 6, 4, 4);
            chatContentGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Anchor content to top so it grows downward
            var chatContentRT = chatContentGo.GetComponent<RectTransform>();
            chatContentRT.anchorMin = new Vector2(0, 1);
            chatContentRT.anchorMax = new Vector2(1, 1);
            chatContentRT.pivot = new Vector2(0.5f, 1);

            var chatTextGo = Child(chatContentGo.transform, "ChatText");
            var chatText = chatTextGo.AddComponent<Text>();
            chatText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            chatText.fontSize = 13; chatText.color = ColTextPrimary;
            chatText.alignment = TextAnchor.UpperLeft;
            chatText.horizontalOverflow = HorizontalWrapMode.Wrap;
            chatText.verticalOverflow = VerticalWrapMode.Truncate;

            // Chat input row
            var chatRow = Row(chatCol.transform, 28);
            var chatLayout = chatRow.AddComponent<HorizontalLayoutGroup>();
            chatLayout.spacing = 8;
            chatLayout.childControlWidth = true;
            chatLayout.childControlHeight = true;
            chatLayout.childForceExpandHeight = true;
            chatLayout.childForceExpandWidth = false;
            var chatInput = StyledInputField(chatRow.transform, "ChatInput", "Type a message...");
            chatInput.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var chatSendBtn = StyledButton(chatRow.transform, "ChatSendButton", "SEND", ColBtnNormal);
            chatSendBtn.gameObject.AddComponent<LayoutElement>().preferredWidth = 80;

            // Status text — shows errors like "All players must be ready"
            var statusGo = Child(roomGo.transform, "StatusText");
            var statusLE = statusGo.AddComponent<LayoutElement>();
            statusLE.preferredHeight = 28;
            var statusText = statusGo.AddComponent<Text>();
            statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            statusText.fontSize = 14;
            statusText.alignment = TextAnchor.MiddleCenter;
            statusText.color = new Color(1f, 0.4f, 0.4f);
            statusText.text = "";
            statusGo.SetActive(false);

            // Action bar — pinned to panel bottom (child of panel, not content)
            var roomActionBar = Child(roomGo.transform, "ActionBar");
            roomActionBar.AddComponent<Image>().color = new Color(0.12f, 0.14f, 0.22f, 1.00f);
            roomActionBar.AddComponent<LayoutElement>().preferredHeight = 60;
            var roomActLayout = roomActionBar.AddComponent<HorizontalLayoutGroup>();
            roomActLayout.spacing = 12; roomActLayout.childForceExpandWidth = true; roomActLayout.childForceExpandHeight = true;
            roomActLayout.padding = new RectOffset(20, 20, 8, 8);

            var leaveBtn = StyledButton(roomActionBar.transform, "LeaveButton", "LEAVE", ColBtnDanger);
            var readyBtn = StyledButton(roomActionBar.transform, "ReadyButton", "READY", ColBtnNormal);
            var readyBtnText = readyBtn.GetComponentInChildren<Text>();
            var startBtn = StyledButton(roomActionBar.transform, "StartButton", "START GAME", ColBtnPrimary);
            // Make START GAME text larger for prominence
            var startBtnText = startBtn.GetComponentInChildren<Text>();
            if (startBtnText != null) startBtnText.fontSize = 16;

            // Wire RoomPanel
            var rSO = new SerializedObject(roomPanel);
            var slotsArr = rSO.FindProperty("_playerSlots");
            slotsArr.arraySize = 4;
            for (int i = 0; i < 4; i++) slotsArr.GetArrayElementAtIndex(i).objectReferenceValue = playerSlots[i];
            rSO.FindProperty("_mapDropdown").objectReferenceValue = roomMapDd;
            rSO.FindProperty("_difficultyDropdown").objectReferenceValue = roomDiffDd;
            rSO.FindProperty("_joinCodeText").objectReferenceValue = joinCodeText.GetComponent<Text>();
            rSO.FindProperty("_copyCodeButton").objectReferenceValue = copyCodeBtn;
            rSO.FindProperty("_readyButton").objectReferenceValue = readyBtn;
            rSO.FindProperty("_readyButtonText").objectReferenceValue = readyBtnText;
            rSO.FindProperty("_startButton").objectReferenceValue = startBtn;
            rSO.FindProperty("_leaveButton").objectReferenceValue = leaveBtn;
            rSO.FindProperty("_chatScrollRect").objectReferenceValue = chatScroll.GetComponentInChildren<ScrollRect>();
            rSO.FindProperty("_chatText").objectReferenceValue = chatText;
            rSO.FindProperty("_chatInput").objectReferenceValue = chatInput;
            rSO.FindProperty("_chatSendButton").objectReferenceValue = chatSendBtn;
            rSO.FindProperty("_statusText").objectReferenceValue = statusText;
            rSO.ApplyModifiedPropertiesWithoutUndo();

            // ════════ TRANSITION PANEL ════════
            var transGo = Child(lobbyRoot.transform, "TransitionPanel");
            Stretch(transGo);
            transGo.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.08f, 0.98f);
            var transPanel = transGo.AddComponent<TransitionLoadingPanel>();
            var transVlg = transGo.AddComponent<VerticalLayoutGroup>();
            transVlg.padding = new RectOffset(200, 200, 0, 0);
            transVlg.spacing = 16;
            transVlg.childAlignment = TextAnchor.MiddleCenter;
            transVlg.childForceExpandWidth = true;
            transVlg.childForceExpandHeight = false;

            // Spacer top
            Child(transGo.transform, "TopSpacer").AddComponent<LayoutElement>().flexibleHeight = 1;

            var phaseText = Label(transGo.transform, "PhaseText", "Connecting...", 22, ColTextPrimary, TextAnchor.MiddleCenter);
            phaseText.AddComponent<LayoutElement>().preferredHeight = 40;

            var progressBar = StyledSlider(transGo.transform, "ProgressBar", 0, 1, 0);
            progressBar.interactable = false;
            progressBar.gameObject.AddComponent<LayoutElement>().preferredHeight = 12;
            // Style the progress bar fill
            var fillArea = progressBar.fillRect;
            if (fillArea != null) fillArea.GetComponent<Image>().color = ColBtnPrimary;

            var tipText = Label(transGo.transform, "TipText", "Stick together with your party for bonus XP!", 14, ColTextSecondary, TextAnchor.MiddleCenter);
            tipText.GetComponent<Text>().fontStyle = FontStyle.Italic;
            tipText.AddComponent<LayoutElement>().preferredHeight = 30;

            // Spacer
            Child(transGo.transform, "MidSpacer").AddComponent<LayoutElement>().preferredHeight = 20;

            var cancelBtn = StyledButton(transGo.transform, "CancelButton", "CANCEL", ColBtnDanger);
            cancelBtn.gameObject.AddComponent<LayoutElement>().preferredHeight = 44;
            cancelBtn.gameObject.AddComponent<LayoutElement>().preferredWidth = 200;
            // Fix duplicate LE
            var cancelLEs = cancelBtn.gameObject.GetComponents<LayoutElement>();
            if (cancelLEs.Length > 1) Object.DestroyImmediate(cancelLEs[1]);
            var cancelLE = cancelBtn.gameObject.GetComponent<LayoutElement>();
            cancelLE.preferredHeight = 44; cancelLE.preferredWidth = 200;
            cancelBtn.gameObject.SetActive(false);

            // Spacer bottom
            Child(transGo.transform, "BotSpacer").AddComponent<LayoutElement>().flexibleHeight = 1;

            // Wire TransitionPanel
            var tSO = new SerializedObject(transPanel);
            tSO.FindProperty("_phaseText").objectReferenceValue = phaseText.GetComponent<Text>();
            tSO.FindProperty("_progressBar").objectReferenceValue = progressBar;
            tSO.FindProperty("_tipText").objectReferenceValue = tipText.GetComponent<Text>();
            tSO.FindProperty("_cancelButton").objectReferenceValue = cancelBtn;
            tSO.ApplyModifiedPropertiesWithoutUndo();

            // ════════ Wire LobbyUIManager ════════
            var uSO = new SerializedObject(uiManager);
            uSO.FindProperty("_browserPanel").objectReferenceValue = browserPanel;
            uSO.FindProperty("_roomPanel").objectReferenceValue = roomPanel;
            uSO.FindProperty("_transitionPanel").objectReferenceValue = transPanel;
            uSO.FindProperty("_lobbyRoot").objectReferenceValue = lobbyRoot;
            uSO.ApplyModifiedPropertiesWithoutUndo();

            roomGo.SetActive(false);
            transGo.SetActive(false);

            BuildEntryPrefab(bSO);

            EditorUtility.SetDirty(canvasGo);
            Debug.Log("[LobbySetup] Created LobbyCanvas with styled UI.");
        }

        // ── Panel / Section Builders ────────────────────

        /// <summary>Creates a centered panel with dark background, anchored to leave margins.</summary>
        static GameObject MakePanel(Transform parent, string name)
        {
            var go = Child(parent, name);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.06f, 0.03f);
            rt.anchorMax = new Vector2(0.94f, 0.97f);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            go.AddComponent<Image>().color = ColPanelBg;

            // Outer vertical layout: title bar + content + action bar
            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 0;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(0, 0, 0, 0);

            // ContentSizeFitter must NOT be on the panel — the RectTransform anchors define its size
            return go;
        }

        /// <summary>Colored title bar at top of panel.</summary>
        static GameObject TitleBar(Transform parent, string title)
        {
            var bar = Child(parent, "TitleBar");
            bar.AddComponent<Image>().color = ColTitleBar;
            bar.AddComponent<LayoutElement>().preferredHeight = 50;
            var hlg = bar.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(20, 20, 0, 0);
            hlg.spacing = 10;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandHeight = true;
            hlg.childForceExpandWidth = false;

            var lbl = Label(bar.transform, "TitleLabel", title, 20, ColTextPrimary, TextAnchor.MiddleLeft);
            lbl.GetComponent<Text>().fontStyle = FontStyle.Bold;
            lbl.AddComponent<LayoutElement>().preferredWidth = 250;

            return bar;
        }

        /// <summary>Padded content area below title bar. Gets flexibleHeight=1.</summary>
        static Transform ContentArea(Transform parent)
        {
            var go = Child(parent, "Content");
            go.AddComponent<LayoutElement>().flexibleHeight = 1;
            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(20, 20, 12, 12);
            vlg.spacing = 6;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            return go.transform;
        }

        /// <summary>Fixed-height row container.</summary>
        static GameObject Row(Transform parent, float height)
        {
            var go = Child(parent, "Row");
            go.AddComponent<LayoutElement>().preferredHeight = height;
            return go;
        }

        /// <summary>Thin horizontal divider line.</summary>
        static void Divider(Transform parent)
        {
            var go = Child(parent, "Divider");
            go.AddComponent<Image>().color = ColDivider;
            go.AddComponent<LayoutElement>().preferredHeight = 1;
        }

        // ── Player Slot ─────────────────────────────────

        static LobbyPlayerSlotUI BuildPlayerSlot(Transform parent, int index)
        {
            var bg = (index % 2 == 0) ? ColRow : ColRowAlt;
            var slotGo = Child(parent, $"PlayerSlot_{index}");
            slotGo.AddComponent<Image>().color = bg;
            slotGo.AddComponent<LayoutElement>().preferredHeight = 44;
            var hlg = slotGo.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(12, 12, 4, 4);
            hlg.spacing = 8;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            var comp = slotGo.AddComponent<LobbyPlayerSlotUI>();

            // Empty state
            var empty = Child(slotGo.transform, "EmptyState");
            var emptyTxt = empty.AddComponent<Text>();
            emptyTxt.text = $"Slot {index + 1}  \u2014  Waiting...";
            emptyTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            emptyTxt.fontSize = 14;
            emptyTxt.color = ColTextSecondary;
            emptyTxt.fontStyle = FontStyle.Italic;
            emptyTxt.alignment = TextAnchor.MiddleLeft;
            var emptyLE = empty.AddComponent<LayoutElement>();
            emptyLE.flexibleWidth = 1;
            emptyLE.preferredHeight = 36;

            // Occupied state
            var occ = Child(slotGo.transform, "OccupiedState");
            var occHlg = occ.AddComponent<HorizontalLayoutGroup>();
            occHlg.spacing = 8; occHlg.childAlignment = TextAnchor.MiddleLeft;
            occHlg.childForceExpandWidth = false; occHlg.childForceExpandHeight = false;
            var occLE = occ.AddComponent<LayoutElement>();
            occLE.flexibleWidth = 1;
            occLE.preferredHeight = 36;
            occ.SetActive(false);

            // Crown — small fixed-size icon
            var crown = Child(occ.transform, "HostCrown");
            var crownImg = crown.AddComponent<Image>(); crownImg.color = ColGold;
            var crownLE = crown.AddComponent<LayoutElement>();
            crownLE.preferredWidth = 18; crownLE.preferredHeight = 18;
            crown.SetActive(false);

            // Ready indicator — small fixed-size dot
            var ready = Child(occ.transform, "ReadyIndicator");
            var readyImg = ready.AddComponent<Image>(); readyImg.color = ColRed;
            var readyLE = ready.AddComponent<LayoutElement>();
            readyLE.preferredWidth = 12; readyLE.preferredHeight = 12;

            // Name
            var nameTxt = TextChild(occ.transform, "NameText", "Player", 15, ColTextPrimary);
            var nameLE = nameTxt.gameObject.AddComponent<LayoutElement>();
            nameLE.flexibleWidth = 1; nameLE.preferredHeight = 36;

            // Level
            var lvlTxt = TextChild(occ.transform, "LevelText", "Lv.1", 13, ColTextSecondary);
            var lvlLE = lvlTxt.gameObject.AddComponent<LayoutElement>();
            lvlLE.preferredWidth = 50; lvlLE.preferredHeight = 36;

            // Ping
            var pingTxt = TextChild(occ.transform, "PingText", "0ms", 13, ColGreen);
            pingTxt.alignment = TextAnchor.MiddleRight;
            var pingLE = pingTxt.gameObject.AddComponent<LayoutElement>();
            pingLE.preferredWidth = 55; pingLE.preferredHeight = 36;

            // Kick
            var kickBtn = StyledButton(occ.transform, "KickButton", "KICK", ColBtnDanger);
            var kickLE = kickBtn.gameObject.AddComponent<LayoutElement>();
            kickLE.preferredWidth = 70; kickLE.preferredHeight = 30;
            kickBtn.gameObject.SetActive(false);

            // Wire
            var so = new SerializedObject(comp);
            so.FindProperty("_nameText").objectReferenceValue = nameTxt;
            so.FindProperty("_levelText").objectReferenceValue = occ.transform.Find("LevelText").GetComponent<Text>();
            so.FindProperty("_readyIndicator").objectReferenceValue = readyImg;
            so.FindProperty("_hostCrown").objectReferenceValue = crownImg;
            so.FindProperty("_pingText").objectReferenceValue = pingTxt;
            so.FindProperty("_emptyState").objectReferenceValue = empty;
            so.FindProperty("_occupiedState").objectReferenceValue = occ;
            so.FindProperty("_kickButton").objectReferenceValue = kickBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            return comp;
        }

        // ── Entry Prefab ────────────────────────────────

        void BuildEntryPrefab(SerializedObject browserSO)
        {
            const string path = "Assets/Prefabs/Lobby/LobbyListEntry.prefab";
            if (!_overwriteExisting && AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                var ex = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var c = ex.GetComponent<LobbyListEntryUI>();
                if (c != null) { browserSO.FindProperty("_entryPrefab").objectReferenceValue = c; browserSO.ApplyModifiedPropertiesWithoutUndo(); }
                return;
            }
            EnsureDirectory("Assets/Prefabs/Lobby");

            var go = new GameObject("LobbyListEntry");
            var bg = go.AddComponent<Image>(); bg.color = ColRow;
            go.AddComponent<LayoutElement>().preferredHeight = 44;
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(16, 16, 6, 6);
            hlg.spacing = 16; hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;

            var entry = go.AddComponent<LobbyListEntryUI>();

            var hostName = TextChild(go.transform, "HostNameText", "Host", 14, ColTextPrimary);
            hostName.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var mapTxt = TextChild(go.transform, "MapText", "Map", 13, ColTextSecondary);
            mapTxt.gameObject.AddComponent<LayoutElement>().preferredWidth = 110;
            var diffTxt = TextChild(go.transform, "DifficultyText", "Normal", 13, ColTextSecondary);
            diffTxt.gameObject.AddComponent<LayoutElement>().preferredWidth = 90;
            var countTxt = TextChild(go.transform, "PlayerCountText", "0/4", 14, ColTextPrimary);
            countTxt.alignment = TextAnchor.MiddleCenter;
            countTxt.gameObject.AddComponent<LayoutElement>().preferredWidth = 45;
            var ePing = TextChild(go.transform, "PingText", "0ms", 13, ColGreen);
            ePing.alignment = TextAnchor.MiddleRight;
            ePing.gameObject.AddComponent<LayoutElement>().preferredWidth = 50;
            var joinBtn = StyledButton(go.transform, "JoinButton", "JOIN", ColBtnPrimary);
            joinBtn.gameObject.AddComponent<LayoutElement>().preferredWidth = 70;

            var eSO = new SerializedObject(entry);
            eSO.FindProperty("_hostNameText").objectReferenceValue = hostName;
            eSO.FindProperty("_mapText").objectReferenceValue = mapTxt;
            eSO.FindProperty("_difficultyText").objectReferenceValue = diffTxt;
            eSO.FindProperty("_playerCountText").objectReferenceValue = countTxt;
            eSO.FindProperty("_pingText").objectReferenceValue = ePing;
            eSO.FindProperty("_joinButton").objectReferenceValue = joinBtn;
            eSO.FindProperty("_background").objectReferenceValue = bg;
            eSO.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            browserSO.FindProperty("_entryPrefab").objectReferenceValue = prefab.GetComponent<LobbyListEntryUI>();
            browserSO.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[LobbySetup] Created {path}");
        }

        // ── Styled UI Primitives ────────────────────────

        static readonly DefaultControls.Resources _uiRes = new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
            checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
            dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
            mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd")
        };

        static readonly Font _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        static Button StyledButton(Transform parent, string name, string label, Color bgColor)
        {
            var go = DefaultControls.CreateButton(_uiRes);
            go.name = name;
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.color = bgColor;

            var btn = go.GetComponent<Button>();
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            cb.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            var txt = go.GetComponentInChildren<Text>();
            if (txt != null)
            {
                txt.text = label;
                txt.font = _font;
                txt.fontSize = 13;
                txt.fontStyle = FontStyle.Bold;
                txt.color = ColTextPrimary;
            }
            return btn;
        }

        static InputField StyledInputField(Transform parent, string name, string placeholder)
        {
            var go = DefaultControls.CreateInputField(_uiRes);
            go.name = name;
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.color = ColInputBg;

            var ph = go.transform.Find("Placeholder");
            if (ph != null) { var t = ph.GetComponent<Text>(); t.text = placeholder; t.font = _font; t.fontSize = 13; t.color = ColTextSecondary; }
            var txt = go.transform.Find("Text");
            if (txt != null) { var t = txt.GetComponent<Text>(); t.font = _font; t.fontSize = 13; t.color = ColTextPrimary; }

            return go.GetComponent<InputField>();
        }

        static Dropdown StyledDropdown(Transform parent, string name, string[] options)
        {
            var go = DefaultControls.CreateDropdown(_uiRes);
            go.name = name;
            go.transform.SetParent(parent, false);

            go.GetComponent<Image>().color = ColInputBg;

            var dd = go.GetComponent<Dropdown>();
            dd.ClearOptions();
            var opts = new System.Collections.Generic.List<Dropdown.OptionData>();
            for (int i = 0; i < options.Length; i++) opts.Add(new Dropdown.OptionData(options[i]));
            dd.AddOptions(opts);

            foreach (var t in go.GetComponentsInChildren<Text>(true))
            {
                t.font = _font; t.fontSize = 13; t.color = ColTextPrimary;
            }
            // Style the dropdown template
            var template = go.transform.Find("Template");
            if (template != null)
            {
                var tmplImg = template.GetComponent<Image>();
                if (tmplImg != null) tmplImg.color = new Color(0.12f, 0.13f, 0.19f, 1f);
            }
            return dd;
        }

        static Toggle StyledToggle(Transform parent, string name, string label)
        {
            var go = DefaultControls.CreateToggle(_uiRes);
            go.name = name;
            go.transform.SetParent(parent, false);

            // Style the toggle background
            var bg = go.transform.Find("Background");
            if (bg != null) bg.GetComponent<Image>().color = ColInputBg;

            var txt = go.GetComponentInChildren<Text>();
            if (txt != null) { txt.text = label; txt.font = _font; txt.fontSize = 13; txt.color = ColTextSecondary; }
            return go.GetComponent<Toggle>();
        }

        static Slider StyledSlider(Transform parent, string name, float min, float max, float value)
        {
            var go = DefaultControls.CreateSlider(_uiRes);
            go.name = name;
            go.transform.SetParent(parent, false);

            // Background track
            var bgTrack = go.transform.Find("Background");
            if (bgTrack != null) bgTrack.GetComponent<Image>().color = ColInputBg;
            // Fill
            var fillArea = go.transform.Find("Fill Area/Fill");
            if (fillArea != null) fillArea.GetComponent<Image>().color = ColTextAccent;
            // Handle
            var handle = go.transform.Find("Handle Slide Area/Handle");
            if (handle != null) handle.GetComponent<Image>().color = ColTextPrimary;

            var s = go.GetComponent<Slider>();
            s.minValue = min; s.maxValue = max; s.value = value;
            s.wholeNumbers = min >= 1;
            return s;
        }

        static GameObject StyledScrollView(Transform parent, string name)
        {
            var go = DefaultControls.CreateScrollView(_uiRes);
            go.name = name;
            go.transform.SetParent(parent, false);

            // Viewport background
            go.GetComponent<Image>().color = ColSection;

            var sr = go.GetComponent<ScrollRect>();
            sr.horizontal = false;

            // Remove horizontal scrollbar
            var hBar = go.transform.Find("Scrollbar Horizontal");
            if (hBar != null) Object.DestroyImmediate(hBar.gameObject);

            // Style vertical scrollbar
            var vBar = go.transform.Find("Scrollbar Vertical");
            if (vBar != null)
            {
                vBar.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.13f, 1f);
                var handleArea = vBar.transform.Find("Sliding Area/Handle");
                if (handleArea != null) handleArea.GetComponent<Image>().color = new Color(0.25f, 0.27f, 0.35f, 1f);
            }

            return go;
        }

        // ── Basic Primitives ────────────────────────────

        static GameObject Child(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        static void Stretch(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) return;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero; rt.anchoredPosition = Vector2.zero;
        }

        static GameObject Label(Transform parent, string name, string text, int fontSize, Color color, TextAnchor align)
        {
            var go = Child(parent, name);
            var t = go.AddComponent<Text>();
            t.text = text; t.font = _font; t.fontSize = fontSize;
            t.color = color; t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return go;
        }

        static Text TextChild(Transform parent, string name, string text, int fontSize, Color color)
        {
            var go = Child(parent, name);
            var t = go.AddComponent<Text>();
            t.text = text; t.font = _font; t.fontSize = fontSize;
            t.color = color; t.alignment = TextAnchor.MiddleLeft;
            return t;
        }

        static void EnsureDirectory(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = path.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
