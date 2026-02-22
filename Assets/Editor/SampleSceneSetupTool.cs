using System.Collections.Generic;
using Core.UI;
using Core.UI.Runtime;
using Samples;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EditorTools
{
    public static class SampleSceneSetupTool
    {
        private const string HudSampleRoot = "Assets/Samples/GameplayHudSample";
        private const string HudGroupFolder = HudSampleRoot + "/Groups";
        private const string HudPrefabFolder = HudSampleRoot + "/Prefabs";

        private const string GameplayHudPrefabPath = HudPrefabFolder + "/GameplayHud.prefab";
        private const string PauseMenuPrefabPath = HudPrefabFolder + "/PauseMenu.prefab";
        private const string LobbyIconsPrefabPath = HudPrefabFolder + "/LobbyIconsHud.prefab";
        private const string InventoryPrefabPath = HudPrefabFolder + "/InventoryPage.prefab";
        private const string SettingsModalPrefabPath = HudPrefabFolder + "/SettingsModal.prefab";
        private const string UnitInfoModalPrefabPath = HudPrefabFolder + "/UnitInfoModal.prefab";
        private const string ConfirmationModalPrefabPath = HudPrefabFolder + "/ConfirmationModal.prefab";
        private const string PortraitSheetPrefabPath = HudPrefabFolder + "/PortraitSheet.prefab";

        [MenuItem("Tools/YetAnotherMenuManager/Setup Gameplay HUD Sample Scene")]
        public static void SetupGameplayHudScene()
        {
            EnsureFolder(HudSampleRoot);
            EnsureFolder(HudGroupFolder);

            var prefabs = LoadPrefabs();
            if (!prefabs.IsValid)
            {
                Debug.LogError("[SampleSceneSetupTool] Missing sample prefabs. Reimport the GameplayHudSample prefab assets.");
                return;
            }

            var groups = CreateGroups();
            WireScene(prefabs, groups);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SampleSceneSetupTool] Active scene was configured with the MenuController HUD sample.");
        }

        public static void SetupSampleSceneBatchMode()
        {
            SetupGameplayHudScene();
        }

        private static void WireScene(PrefabRefs prefabs, GroupRefs groups)
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[SampleSceneSetupTool] No valid active scene found. Open a scene before running this tool.");
                return;
            }

            CleanupLegacySceneObjects();

            var canvas = EnsureCanvas();
            EnsureEventSystem();

            var hudLayer = EnsureLayerRoot(canvas.transform, "HUDLayer");
            var overlayLayer = EnsureLayerRoot(canvas.transform, "OverlayLayer");
            var modalLayer = EnsureLayerRoot(canvas.transform, "ModalLayer");
            var debugLayer = EnsureLayerRoot(canvas.transform, "DebugLayer");

            var runtimeRoot = EnsureRuntimeRoot(canvas.transform);
            RemoveMissingScriptsRecursively(runtimeRoot);

            var menuController = runtimeRoot.GetComponent<MenuController>() ?? runtimeRoot.AddComponent<MenuController>();
            var inputRouter = runtimeRoot.GetComponent<MenuInputRouter>() ?? runtimeRoot.AddComponent<MenuInputRouter>();
            var inputModeBridge = runtimeRoot.GetComponent<MenuInputModeBridge>() ?? runtimeRoot.AddComponent<MenuInputModeBridge>();
            var pauseBridge = runtimeRoot.GetComponent<TimeScalePauseBridge>() ?? runtimeRoot.AddComponent<TimeScalePauseBridge>();
            var lobbyController = runtimeRoot.GetComponent<LobbyIconsVisibilityController>() ??
                runtimeRoot.AddComponent<LobbyIconsVisibilityController>();

            var instances = CreateMenuInstances(prefabs, hudLayer, overlayLayer, modalLayer, debugLayer);
            SetAllMenusInactive(instances);

            ConfigureMenuController(menuController, pauseBridge, groups, instances);
            ConfigureInputRouter(inputRouter, menuController, instances.pauseMenu, instances.gameplayHud);
            ConfigureInputModeBridge(inputModeBridge, menuController);
            ConfigureLobbyIconsController(lobbyController, menuController, instances.pauseMenu, instances.lobbyIconsHud);
            AutoAssignModalCloseButtons(instances);
            ConfigureButtonActions(menuController, lobbyController, instances);

            CreateOrUpdateInstructionBanner(canvas.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static GroupRefs CreateGroups()
        {
            var gameplay = CreateOrUpdateGroupAsset(HudGroupFolder + "/HudGameplay.asset", "hud.gameplay", "Gameplay HUD");
            var lobby = CreateOrUpdateGroupAsset(HudGroupFolder + "/HudLobby.asset", "hud.lobby", "Lobby Icons");
            var pages = CreateOrUpdateGroupAsset(HudGroupFolder + "/HudPages.asset", "hud.pages", "HUD Pages");
            var modals = CreateOrUpdateGroupAsset(HudGroupFolder + "/HudModals.asset", "hud.modals", "HUD Modals");
            return new GroupRefs(gameplay, lobby, pages, modals);
        }

        private static PrefabRefs LoadPrefabs()
        {
            return new PrefabRefs(
                AssetDatabase.LoadAssetAtPath<GameplayHudView>(GameplayHudPrefabPath),
                AssetDatabase.LoadAssetAtPath<PauseMenuView>(PauseMenuPrefabPath),
                AssetDatabase.LoadAssetAtPath<LobbyIconsHudView>(LobbyIconsPrefabPath),
                AssetDatabase.LoadAssetAtPath<InventoryMenuView>(InventoryPrefabPath),
                AssetDatabase.LoadAssetAtPath<SampleMenuView>(SettingsModalPrefabPath),
                AssetDatabase.LoadAssetAtPath<SampleMenuView>(UnitInfoModalPrefabPath),
                AssetDatabase.LoadAssetAtPath<SampleMenuView>(ConfirmationModalPrefabPath),
                AssetDatabase.LoadAssetAtPath<SampleMenuView>(PortraitSheetPrefabPath));
        }

        private static MenuInstances CreateMenuInstances(PrefabRefs prefabs, RectTransform hudLayer, RectTransform overlayLayer,
            RectTransform modalLayer, RectTransform debugLayer)
        {
            var gameplayHud = CreateOrReplaceMenuInstance(prefabs.gameplayHud, hudLayer, "GameplayHud");
            var pauseMenu = CreateOrReplaceMenuInstance(prefabs.pauseMenu, overlayLayer, "PauseMenu");
            var lobbyIconsHud = CreateOrReplaceMenuInstance(prefabs.lobbyIconsHud, debugLayer, "LobbyIconsHud");
            var inventory = CreateOrReplaceMenuInstance(prefabs.inventoryPage, overlayLayer, "InventoryPage");
            var settings = CreateOrReplaceMenuInstance(prefabs.settingsModal, modalLayer, "SettingsModal");
            var unitInfo = CreateOrReplaceMenuInstance(prefabs.unitInfoModal, modalLayer, "UnitInfoModal");
            var confirmation = CreateOrReplaceMenuInstance(prefabs.confirmationModal, modalLayer, "ConfirmationModal");
            var portrait = CreateOrReplaceMenuInstance(prefabs.portraitSheet, modalLayer, "PortraitSheet");
            var buttonTabsWindow = CreateOrReplaceButtonTabsWindow(modalLayer, "ButtonTabsWindow");

            return new MenuInstances(gameplayHud, pauseMenu, lobbyIconsHud, inventory, settings, unitInfo, confirmation, portrait,
                buttonTabsWindow);
        }

        private static ButtonTabsWindowView CreateOrReplaceButtonTabsWindow(RectTransform parent, string instanceName)
        {
            var existing = parent.Find(instanceName);
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            var window = new GameObject(instanceName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(ButtonTabsWindowView));
            window.layer = UiLayer();

            var windowRect = window.GetComponent<RectTransform>();
            windowRect.SetParent(parent, false);
            windowRect.anchorMin = new Vector2(0.5f, 0.5f);
            windowRect.anchorMax = new Vector2(0.5f, 0.5f);
            windowRect.pivot = new Vector2(0.5f, 0.5f);
            windowRect.sizeDelta = new Vector2(980f, 620f);
            windowRect.anchoredPosition = Vector2.zero;

            var windowImage = window.GetComponent<Image>();
            windowImage.color = new Color(0.13f, 0.14f, 0.17f, 0.97f);

            var font = ResolveTmpFont();

            var titleBar = CreatePanel(windowRect, "TitleBar", new Color(0.08f, 0.09f, 0.11f, 0.98f));
            titleBar.anchorMin = new Vector2(0f, 1f);
            titleBar.anchorMax = new Vector2(1f, 1f);
            titleBar.pivot = new Vector2(0.5f, 1f);
            titleBar.sizeDelta = new Vector2(0f, 58f);
            titleBar.anchoredPosition = Vector2.zero;

            var windowTitle = CreateText(titleBar, "Title", "Window Tabs Example", 24, TextAlignmentOptions.MidlineLeft, font);
            var titleRect = windowTitle.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 0f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = new Vector2(18f, 0f);
            titleRect.offsetMax = new Vector2(-120f, 0f);
            windowTitle.color = new Color(0.95f, 0.95f, 0.97f, 1f);

            var closeButton = CreateButton(titleBar, "CloseButton", "X", font, 22);
            var closeRect = (RectTransform)closeButton.transform;
            closeRect.anchorMin = new Vector2(1f, 0.5f);
            closeRect.anchorMax = new Vector2(1f, 0.5f);
            closeRect.pivot = new Vector2(1f, 0.5f);
            closeRect.sizeDelta = new Vector2(48f, 38f);
            closeRect.anchoredPosition = new Vector2(-16f, 0f);

            var tabRow = new GameObject("TabRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            tabRow.layer = UiLayer();
            var tabRowRect = tabRow.GetComponent<RectTransform>();
            tabRowRect.SetParent(windowRect, false);
            tabRowRect.anchorMin = new Vector2(0f, 1f);
            tabRowRect.anchorMax = new Vector2(1f, 1f);
            tabRowRect.pivot = new Vector2(0.5f, 1f);
            tabRowRect.sizeDelta = new Vector2(-36f, 50f);
            tabRowRect.anchoredPosition = new Vector2(0f, -68f);

            var tabLayout = tabRow.GetComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 8f;
            tabLayout.padding = new RectOffset(0, 0, 0, 0);
            tabLayout.childControlHeight = true;
            tabLayout.childControlWidth = true;
            tabLayout.childForceExpandHeight = true;
            tabLayout.childForceExpandWidth = true;

            var generalTab = CreateButton(tabRowRect, "GeneralTabButton", "General", font, 18);
            generalTab.gameObject.AddComponent<LayoutElement>().minHeight = 46f;
            var displayTab = CreateButton(tabRowRect, "DisplayTabButton", "Display", font, 18);
            displayTab.gameObject.AddComponent<LayoutElement>().minHeight = 46f;
            var advancedTab = CreateButton(tabRowRect, "AdvancedTabButton", "Advanced", font, 18);
            advancedTab.gameObject.AddComponent<LayoutElement>().minHeight = 46f;

            var activeLabel = CreateText(windowRect, "ActiveTabLabel", "General", 20, TextAlignmentOptions.MidlineLeft, font);
            var activeLabelRect = activeLabel.rectTransform;
            activeLabelRect.anchorMin = new Vector2(0f, 1f);
            activeLabelRect.anchorMax = new Vector2(1f, 1f);
            activeLabelRect.pivot = new Vector2(0.5f, 1f);
            activeLabelRect.sizeDelta = new Vector2(-40f, 36f);
            activeLabelRect.anchoredPosition = new Vector2(0f, -124f);
            activeLabel.color = new Color(0.87f, 0.90f, 0.97f, 1f);

            var contentRoot = new GameObject("ContentRoot", typeof(RectTransform));
            contentRoot.layer = UiLayer();
            var contentRect = contentRoot.GetComponent<RectTransform>();
            contentRect.SetParent(windowRect, false);
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.offsetMin = new Vector2(18f, 84f);
            contentRect.offsetMax = new Vector2(-18f, -170f);

            var generalPanel = CreateTabPanel(contentRect, "GeneralTabPanel", new Color(0.16f, 0.19f, 0.24f, 1f),
                "General Tab\n\nThis demonstrates top-button tabs like a desktop window.");
            var displayPanel = CreateTabPanel(contentRect, "DisplayTabPanel", new Color(0.17f, 0.22f, 0.19f, 1f),
                "Display Tab\n\nSwitching tabs toggles full content regions.");
            var advancedPanel = CreateTabPanel(contentRect, "AdvancedTabPanel", new Color(0.23f, 0.18f, 0.18f, 1f),
                "Advanced Tab\n\nUse this pattern for settings pages, tools, or inspectors.");

            var generalText = CreateText(generalPanel.transform, "GeneralText", "Use top buttons to switch sections.", 20,
                TextAlignmentOptions.TopLeft, font);
            var displayText = CreateText(displayPanel.transform, "DisplayText", "Each tab can host a distinct layout.", 20,
                TextAlignmentOptions.TopLeft, font);
            var advancedText = CreateText(advancedPanel.transform, "AdvancedText",
                "This is a second tab pattern beyond Inventory.", 20, TextAlignmentOptions.TopLeft, font);

            PositionBodyText(generalText.rectTransform);
            PositionBodyText(displayText.rectTransform);
            PositionBodyText(advancedText.rectTransform);

            var footer = CreatePanel(windowRect, "Footer", new Color(0.10f, 0.11f, 0.13f, 0.98f));
            footer.anchorMin = new Vector2(0f, 0f);
            footer.anchorMax = new Vector2(1f, 0f);
            footer.pivot = new Vector2(0.5f, 0f);
            footer.sizeDelta = new Vector2(0f, 64f);
            footer.anchoredPosition = Vector2.zero;

            var backButton = CreateButton(footer, "BackButton", "Close", font, 20);
            var backRect = (RectTransform)backButton.transform;
            backRect.anchorMin = new Vector2(1f, 0.5f);
            backRect.anchorMax = new Vector2(1f, 0.5f);
            backRect.pivot = new Vector2(1f, 0.5f);
            backRect.sizeDelta = new Vector2(160f, 42f);
            backRect.anchoredPosition = new Vector2(-18f, 0f);

            var view = window.GetComponent<ButtonTabsWindowView>();
            view.primaryButton = generalTab;
            view.secondaryButton = displayTab;
            view.tertiaryButton = advancedTab;
            view.backButton = backButton;
            view.closeButtons = new[] { closeButton };
            view.tabButtons = new[] { generalTab, displayTab, advancedTab };
            view.tabPanels = new[] { generalPanel, displayPanel, advancedPanel };

            var serialized = new SerializedObject(view);
            serialized.FindProperty("activeTabLabel").objectReferenceValue = activeLabel;

            var tabNamesProperty = serialized.FindProperty("tabNames");
            tabNamesProperty.ClearArray();
            tabNamesProperty.arraySize = 3;
            tabNamesProperty.GetArrayElementAtIndex(0).stringValue = "General";
            tabNamesProperty.GetArrayElementAtIndex(1).stringValue = "Display";
            tabNamesProperty.GetArrayElementAtIndex(2).stringValue = "Advanced";

            serialized.FindProperty("initialTabIndex").intValue = 0;
            serialized.FindProperty("resetToInitialTabOnOpen").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(window);
            EditorUtility.SetDirty(view);
            return view;
        }

        private static RectTransform CreatePanel(RectTransform parent, string name, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.layer = UiLayer();
            var rect = panel.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            panel.GetComponent<Image>().color = color;
            return rect;
        }

        private static Button CreateButton(RectTransform parent, string name, string label, TMP_FontAsset font, int fontSize)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.layer = UiLayer();
            var buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.SetParent(parent, false);

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.24f, 0.29f, 0.38f, 1f);

            var button = buttonObject.GetComponent<Button>();
            var labelText = CreateText(buttonRect, "Label", label, fontSize, TextAlignmentOptions.Center, font);
            Stretch(labelText.rectTransform);
            labelText.color = new Color(0.95f, 0.97f, 1f, 1f);

            return button;
        }

        private static GameObject CreateTabPanel(RectTransform parent, string name, Color color, string title)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.layer = UiLayer();

            var rect = panel.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Stretch(rect);

            var image = panel.GetComponent<Image>();
            image.color = color;

            var titleText = CreateText(rect, "Header", title, 24, TextAlignmentOptions.TopLeft, ResolveTmpFont());
            var titleRect = titleText.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(-40f, 120f);
            titleRect.anchoredPosition = new Vector2(0f, -16f);
            titleText.color = new Color(0.96f, 0.96f, 0.97f, 1f);
            titleText.enableWordWrapping = true;

            return panel;
        }

        private static void PositionBodyText(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(26f, 24f);
            rect.offsetMax = new Vector2(-26f, -150f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
        }

        private static T CreateOrReplaceMenuInstance<T>(T prefab, Transform parent, string instanceName) where T : UIMenuComponent
        {
            if (prefab == null)
                return null;

            var existing = parent.Find(instanceName);
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            var instanceObject = PrefabUtility.InstantiatePrefab(prefab.gameObject, parent) as GameObject;
            if (instanceObject == null)
                return null;

            instanceObject.name = instanceName;
            var instance = instanceObject.GetComponent<T>();
            EditorUtility.SetDirty(instanceObject);
            return instance;
        }

        private static void SetAllMenusInactive(MenuInstances instances)
        {
            SetMenuInactive(instances.gameplayHud);
            SetMenuInactive(instances.pauseMenu);
            SetMenuInactive(instances.lobbyIconsHud);
            SetMenuInactive(instances.inventoryPage);
            SetMenuInactive(instances.settingsModal);
            SetMenuInactive(instances.unitInfoModal);
            SetMenuInactive(instances.confirmationModal);
            SetMenuInactive(instances.portraitSheet);
            SetMenuInactive(instances.buttonTabsWindow);
        }

        private static void SetMenuInactive(UIMenuComponent menu)
        {
            if (menu != null)
                menu.gameObject.SetActive(false);
        }

        private static void ConfigureMenuController(MenuController menuController, TimeScalePauseBridge pauseBridge, GroupRefs groups,
            MenuInstances instances)
        {
            var serialized = new SerializedObject(menuController);
            serialized.FindProperty("gameplayPauseBridge").objectReferenceValue = pauseBridge;

            var entries = serialized.FindProperty("menuGroupEntries");
            entries.ClearArray();
            entries.arraySize = 4;

            ConfigureEntry(entries.GetArrayElementAtIndex(0), groups.gameplay,
                new UIMenuComponent[] { instances.gameplayHud }, false, false, false, true);

            ConfigureEntry(entries.GetArrayElementAtIndex(1), groups.lobby,
                new UIMenuComponent[] { instances.lobbyIconsHud }, false, false, true, false);

            ConfigureEntry(entries.GetArrayElementAtIndex(2), groups.pages,
                new UIMenuComponent[] { instances.pauseMenu, instances.inventoryPage }, false, true, true, false);

            ConfigureEntry(entries.GetArrayElementAtIndex(3), groups.modals,
                new UIMenuComponent[]
                {
                    instances.settingsModal,
                    instances.unitInfoModal,
                    instances.confirmationModal,
                    instances.portraitSheet,
                    instances.buttonTabsWindow
                }, false, true, true, false);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(menuController);
        }

        private static void ConfigureEntry(SerializedProperty entry, MenuGroupDefinition group, UIMenuComponent[] menus,
            bool hideAllOtherGroups, bool pauseWhenOpen, bool usesUiInput, bool openOnStartup)
        {
            entry.FindPropertyRelative("group").objectReferenceValue = group;
            entry.FindPropertyRelative("hideAllOtherGroups").boolValue = hideAllOtherGroups;
            entry.FindPropertyRelative("pauseWhenOpen").boolValue = pauseWhenOpen;
            entry.FindPropertyRelative("isModal").boolValue = group != null && group.name.Contains("Modals");
            entry.FindPropertyRelative("usesUIInput").boolValue = usesUiInput;
            entry.FindPropertyRelative("openOnStartup").boolValue = openOnStartup;

            var menusProperty = entry.FindPropertyRelative("menus");
            menusProperty.ClearArray();
            menusProperty.arraySize = menus.Length;
            for (var i = 0; i < menus.Length; i++)
                menusProperty.GetArrayElementAtIndex(i).objectReferenceValue = menus[i];
        }

        private static void ConfigureInputModeBridge(MenuInputModeBridge bridge, MenuController controller)
        {
            var serialized = new SerializedObject(bridge);
            serialized.FindProperty("menuController").objectReferenceValue = controller;
            serialized.FindProperty("hintController").objectReferenceValue = null;
            serialized.FindProperty("clearHintsWhenGameInput").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bridge);
        }

        private static void ConfigureInputRouter(MenuInputRouter router, MenuController menuController, PauseMenuView pauseMenu,
            GameplayHudView gameplayHud)
        {
            var serialized = new SerializedObject(router);
            serialized.FindProperty("menuController").objectReferenceValue = menuController;
            serialized.FindProperty("pauseMenu").objectReferenceValue = pauseMenu;
            serialized.FindProperty("rootGameplayMenu").objectReferenceValue = gameplayHud;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(router);
        }

        private static void ConfigureLobbyIconsController(LobbyIconsVisibilityController controller, MenuController menuController,
            PauseMenuView pauseMenu, LobbyIconsHudView lobbyIcons)
        {
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("menuController").objectReferenceValue = menuController;
            serialized.FindProperty("pauseMenu").objectReferenceValue = pauseMenu;
            serialized.FindProperty("lobbyIconsView").objectReferenceValue = lobbyIcons;
            serialized.FindProperty("startEnabled").boolValue = false;
            serialized.FindProperty("instantRefreshOnEnable").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        private static void ConfigureButtonActions(MenuController menuController, LobbyIconsVisibilityController lobbyController,
            MenuInstances instances)
        {
            BindAction(instances.gameplayHud.pauseButton, menuController, MenuActionType.Toggle, instances.pauseMenu);

            BindAction(instances.pauseMenu.resumeButton, menuController, MenuActionType.PopBack);
            BindMethod(instances.pauseMenu.lobbyIconsButton, lobbyController.ToggleVisibility, true);
            BindAction(instances.pauseMenu.inventoryButton, menuController, MenuActionType.Show, instances.inventoryPage);
            BindAction(instances.pauseMenu.settingsButton, menuController, MenuActionType.Show, instances.settingsModal);
            BindAction(instances.pauseMenu.unitInfoButton, menuController, MenuActionType.Show, instances.unitInfoModal);
            BindAction(instances.pauseMenu.confirmationButton, menuController, MenuActionType.Show, instances.confirmationModal);
            BindAction(instances.pauseMenu.portraitSheetButton, menuController, MenuActionType.Show, instances.buttonTabsWindow);
            SetButtonLabel(instances.pauseMenu.portraitSheetButton, "Window Tabs");

            BindAction(instances.lobbyIconsHud.inventoryButton, menuController, MenuActionType.Show, instances.inventoryPage);
            BindAction(instances.lobbyIconsHud.settingsButton, menuController, MenuActionType.Show, instances.settingsModal);
            BindAction(instances.lobbyIconsHud.unitInfoButton, menuController, MenuActionType.Show, instances.unitInfoModal);
            BindAction(instances.lobbyIconsHud.confirmationButton, menuController, MenuActionType.Show, instances.confirmationModal);
            BindAction(instances.lobbyIconsHud.portraitSheetButton, menuController, MenuActionType.Show, instances.buttonTabsWindow);
            SetButtonLabel(instances.lobbyIconsHud.portraitSheetButton, "Window Tabs");
            BindMethod(instances.lobbyIconsHud.closeButton, lobbyController.ToggleVisibility, true);

            BindAction(instances.inventoryPage.backButton, menuController, MenuActionType.PopBack);

            BindAction(instances.settingsModal.primaryButton, menuController, MenuActionType.Show, instances.confirmationModal);
            BindAction(instances.settingsModal.secondaryButton, menuController, MenuActionType.PopBack);
            BindAction(instances.settingsModal.tertiaryButton, menuController, MenuActionType.Show, instances.portraitSheet);
            BindCloseActions(instances.settingsModal, menuController, MenuActionType.PopBack);

            BindAction(instances.unitInfoModal.primaryButton, menuController, MenuActionType.Show, instances.portraitSheet);
            BindAction(instances.unitInfoModal.secondaryButton, menuController, MenuActionType.PopBack);
            BindAction(instances.unitInfoModal.tertiaryButton, menuController, MenuActionType.Show, instances.confirmationModal);
            BindCloseActions(instances.unitInfoModal, menuController, MenuActionType.PopBack);

            BindAction(instances.confirmationModal.primaryButton, menuController, MenuActionType.CloseAllModals);
            BindAction(instances.confirmationModal.secondaryButton, menuController, MenuActionType.PopBack);
            BindAction(instances.confirmationModal.tertiaryButton, menuController, MenuActionType.PopBack);
            BindCloseActions(instances.confirmationModal, menuController, MenuActionType.PopBack);

            BindAction(instances.portraitSheet.primaryButton, menuController, MenuActionType.PopBack);
            BindAction(instances.portraitSheet.secondaryButton, menuController, MenuActionType.PopBack);
            BindAction(instances.portraitSheet.tertiaryButton, menuController, MenuActionType.PopBack);
            BindCloseActions(instances.portraitSheet, menuController, MenuActionType.PopBack);
            BindCloseActions(instances.buttonTabsWindow, menuController, MenuActionType.PopBack);

            EditorUtility.SetDirty(instances.gameplayHud);
            EditorUtility.SetDirty(instances.pauseMenu);
            EditorUtility.SetDirty(instances.lobbyIconsHud);
            EditorUtility.SetDirty(instances.inventoryPage);
            EditorUtility.SetDirty(instances.settingsModal);
            EditorUtility.SetDirty(instances.unitInfoModal);
            EditorUtility.SetDirty(instances.confirmationModal);
            EditorUtility.SetDirty(instances.portraitSheet);
            EditorUtility.SetDirty(instances.buttonTabsWindow);
        }

        private static void BindAction(Button button, MenuController controller, MenuActionType actionType,
            UIMenuComponent targetMenu = null, MenuGroupDefinition targetGroup = null)
        {
            if (button == null)
                return;

            var invokers = button.GetComponents<MenuActionInvoker>();
            var invoker = invokers.Length > 0 ? invokers[0] : button.gameObject.AddComponent<MenuActionInvoker>();
            for (var i = 1; i < invokers.Length; i++)
                Object.DestroyImmediate(invokers[i], true);

            var serialized = new SerializedObject(invoker);
            serialized.FindProperty("menuController").objectReferenceValue = controller;
            serialized.FindProperty("actionType").enumValueIndex = (int)actionType;
            serialized.FindProperty("targetMenu").objectReferenceValue = targetMenu;
            serialized.FindProperty("targetGroup").objectReferenceValue = targetGroup;
            serialized.FindProperty("pausedValue").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            ClearButtonListeners(button);
            UnityEventTools.AddPersistentListener(button.onClick, invoker.InvokeAction);

            EditorUtility.SetDirty(invoker);
            EditorUtility.SetDirty(button);
        }

        private static void BindMethod(Button button, UnityAction action, bool removeActionInvoker)
        {
            if (button == null || action == null)
                return;

            if (removeActionInvoker)
            {
                var invokers = button.GetComponents<MenuActionInvoker>();
                for (var i = 0; i < invokers.Length; i++)
                    Object.DestroyImmediate(invokers[i], true);
            }

            ClearButtonListeners(button);
            UnityEventTools.AddPersistentListener(button.onClick, action);
            EditorUtility.SetDirty(button);
        }

        private static void BindCloseActions(SampleMenuView menuView, MenuController controller, MenuActionType actionType)
        {
            if (menuView == null)
                return;

            var bound = new HashSet<Button>();
            if (menuView.backButton != null)
            {
                BindAction(menuView.backButton, controller, actionType);
                bound.Add(menuView.backButton);
            }

            if (menuView.closeButtons == null)
                return;

            for (var i = 0; i < menuView.closeButtons.Length; i++)
            {
                var button = menuView.closeButtons[i];
                if (button == null || bound.Contains(button))
                    continue;

                BindAction(button, controller, actionType);
                bound.Add(button);
            }
        }

        private static void AutoAssignModalCloseButtons(MenuInstances instances)
        {
            AutoAssignCloseButtons(instances.settingsModal);
            AutoAssignCloseButtons(instances.unitInfoModal);
            AutoAssignCloseButtons(instances.confirmationModal);
            AutoAssignCloseButtons(instances.portraitSheet);
            AutoAssignCloseButtons(instances.buttonTabsWindow);
        }

        private static void AutoAssignCloseButtons(SampleMenuView view)
        {
            if (view == null)
                return;

            var candidates = CollectCloseButtons(view);
            var serialized = new SerializedObject(view);
            var closeButtonsProperty = serialized.FindProperty("closeButtons");
            if (closeButtonsProperty == null)
                return;

            closeButtonsProperty.ClearArray();
            closeButtonsProperty.arraySize = candidates.Count;
            for (var i = 0; i < candidates.Count; i++)
                closeButtonsProperty.GetArrayElementAtIndex(i).objectReferenceValue = candidates[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
        }

        private static List<Button> CollectCloseButtons(SampleMenuView view)
        {
            var found = new List<Button>();
            var allButtons = view.GetComponentsInChildren<Button>(true);
            for (var i = 0; i < allButtons.Length; i++)
            {
                var button = allButtons[i];
                if (button == null || button == view.backButton)
                    continue;
                if (button == view.primaryButton || button == view.secondaryButton || button == view.tertiaryButton)
                    continue;
                if (!IsCloseButtonName(button.name))
                    continue;
                if (found.Contains(button))
                    continue;

                found.Add(button);
            }

            return found;
        }

        private static bool IsCloseButtonName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            var compact = name.Replace(" ", string.Empty)
                .Replace("_", string.Empty)
                .ToLowerInvariant();

            return compact.Contains("close")
                   || compact == "back"
                   || compact.Contains("backbutton");
        }

        private static void SetButtonLabel(Button button, string text)
        {
            if (button == null || string.IsNullOrWhiteSpace(text))
                return;

            var tmp = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null)
            {
                tmp.text = text;
                EditorUtility.SetDirty(tmp);
                return;
            }

            var legacy = button.GetComponentInChildren<Text>(true);
            if (legacy != null)
            {
                legacy.text = text;
                EditorUtility.SetDirty(legacy);
            }
        }

        private static void ClearButtonListeners(Button button)
        {
            for (var i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(button.onClick, i);

            button.onClick.RemoveAllListeners();
        }

        private static void CreateOrUpdateInstructionBanner(Transform canvasRoot)
        {
            const string bannerName = "SampleInstructions";
            var existing = canvasRoot.Find(bannerName) as RectTransform;

            GameObject bannerGo;
            RectTransform banner;

            if (existing != null)
            {
                bannerGo = existing.gameObject;
                banner = existing;
            }
            else
            {
                bannerGo = new GameObject(bannerName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                bannerGo.layer = UiLayer();
                banner = bannerGo.GetComponent<RectTransform>();
                banner.SetParent(canvasRoot, false);
                banner.anchorMin = new Vector2(0f, 1f);
                banner.anchorMax = new Vector2(0f, 1f);
                banner.pivot = new Vector2(0f, 1f);
                banner.anchoredPosition = new Vector2(20f, -220f);
                banner.sizeDelta = new Vector2(650f, 210f);
            }

            bannerGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            var labelTransform = banner.Find("Label");
            TextMeshProUGUI label;
            if (labelTransform != null)
            {
                label = labelTransform.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                label = CreateText(banner, "Label", string.Empty, 22, TextAlignmentOptions.TopLeft, ResolveTmpFont());
                var labelRect = label.rectTransform;
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(1f, 1f);
                labelRect.offsetMin = new Vector2(18f, 14f);
                labelRect.offsetMax = new Vector2(-18f, -14f);
                label.color = new Color(0.95f, 0.95f, 0.95f, 1f);
            }

            label.text = "MenuController HUD demo\n" +
                         "- Pause button opens menu stack\n" +
                         "- Toggle Lobby Icons to pin edge shortcuts\n" +
                         "- Window Tabs button opens top-button tab window example\n" +
                         "- Icons move out/in based on stack state\n" +
                         "- ESC = Back / Resume";
        }

        private static void CleanupLegacySceneObjects()
        {
            var genericMenus = Object.FindObjectsByType<global::UI.GenericMenu>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var i = 0; i < genericMenus.Length; i++)
            {
                var menu = genericMenus[i];
                if (menu != null)
                    Object.DestroyImmediate(menu.gameObject);
            }

            DestroyByName("MenuController");
            DestroyByName("RuntimeSample");
            DestroyByName("MenuRuntime");
        }

        private static void DestroyByName(string objectName)
        {
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < transforms.Length; i++)
            {
                var transform = transforms[i];
                if (transform == null || transform.name != objectName)
                    continue;

                Object.DestroyImmediate(transform.gameObject);
            }
        }

        private static void RemoveMissingScriptsRecursively(GameObject root)
        {
            if (root == null)
                return;

            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
                if (transforms[i] != null)
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transforms[i].gameObject);
        }

        private static Canvas EnsureCanvas()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null)
            {
                var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                    typeof(GraphicRaycaster));
                canvasGo.layer = UiLayer();
                canvas = canvasGo.GetComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = canvas.gameObject.AddComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();

            return canvas;
        }

        private static void EnsureEventSystem()
        {
            var eventSystem = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (eventSystem == null)
            {
                var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem));
                eventSystemGo.transform.SetAsLastSibling();
                eventSystem = eventSystemGo.GetComponent<EventSystem>();
            }

            var eventSystemObject = eventSystem.gameObject;
            var standalone = eventSystemObject.GetComponent<StandaloneInputModule>();
            if (standalone != null)
                Object.DestroyImmediate(standalone);

            var touch = eventSystemObject.GetComponent<TouchInputModule>();
            if (touch != null)
                Object.DestroyImmediate(touch);

            if (eventSystemObject.GetComponent<InputSystemUIInputModule>() == null)
                eventSystemObject.AddComponent<InputSystemUIInputModule>();

            EditorUtility.SetDirty(eventSystemObject);
        }

        private static RectTransform EnsureLayerRoot(Transform parent, string name)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null)
                return existing;

            var go = new GameObject(name, typeof(RectTransform));
            go.layer = UiLayer();
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Stretch(rect);
            return rect;
        }

        private static GameObject EnsureRuntimeRoot(Transform canvasTransform)
        {
            var existing = canvasTransform.Find("MenuRuntime");
            if (existing != null)
                return existing.gameObject;

            var go = new GameObject("MenuRuntime", typeof(RectTransform));
            go.layer = UiLayer();
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(canvasTransform, false);
            Stretch(rect);
            return go;
        }

        private static MenuGroupDefinition CreateOrUpdateGroupAsset(string path, string groupId, string displayName)
        {
            var group = AssetDatabase.LoadAssetAtPath<MenuGroupDefinition>(path);
            if (group == null)
            {
                group = ScriptableObject.CreateInstance<MenuGroupDefinition>();
                AssetDatabase.CreateAsset(group, path);
            }

            var serialized = new SerializedObject(group);
            serialized.FindProperty("groupId").stringValue = groupId;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(group);
            return group;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string value, int fontSize,
            TextAlignmentOptions anchor, TMP_FontAsset font)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.layer = UiLayer();
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            var text = go.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static TMP_FontAsset ResolveTmpFont()
        {
            if (TMP_Settings.defaultFontAsset != null)
                return TMP_Settings.defaultFontAsset;

            var fontGuids = AssetDatabase.FindAssets("t:TMP_FontAsset");
            for (var i = 0; i < fontGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(fontGuids[i]);
                var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (fontAsset != null)
                    return fontAsset;
            }

            return null;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            var parent = System.IO.Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
            var name = System.IO.Path.GetFileName(folderPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
                return;

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static int UiLayer()
        {
            var layer = LayerMask.NameToLayer("UI");
            return layer >= 0 ? layer : 0;
        }

        private readonly struct GroupRefs
        {
            public GroupRefs(MenuGroupDefinition gameplay, MenuGroupDefinition lobby, MenuGroupDefinition pages,
                MenuGroupDefinition modals)
            {
                this.gameplay = gameplay;
                this.lobby = lobby;
                this.pages = pages;
                this.modals = modals;
            }

            public readonly MenuGroupDefinition gameplay;
            public readonly MenuGroupDefinition lobby;
            public readonly MenuGroupDefinition pages;
            public readonly MenuGroupDefinition modals;
        }

        private readonly struct PrefabRefs
        {
            public PrefabRefs(GameplayHudView gameplayHud, PauseMenuView pauseMenu, LobbyIconsHudView lobbyIconsHud,
                InventoryMenuView inventoryPage, SampleMenuView settingsModal, SampleMenuView unitInfoModal,
                SampleMenuView confirmationModal, SampleMenuView portraitSheet)
            {
                this.gameplayHud = gameplayHud;
                this.pauseMenu = pauseMenu;
                this.lobbyIconsHud = lobbyIconsHud;
                this.inventoryPage = inventoryPage;
                this.settingsModal = settingsModal;
                this.unitInfoModal = unitInfoModal;
                this.confirmationModal = confirmationModal;
                this.portraitSheet = portraitSheet;
            }

            public readonly GameplayHudView gameplayHud;
            public readonly PauseMenuView pauseMenu;
            public readonly LobbyIconsHudView lobbyIconsHud;
            public readonly InventoryMenuView inventoryPage;
            public readonly SampleMenuView settingsModal;
            public readonly SampleMenuView unitInfoModal;
            public readonly SampleMenuView confirmationModal;
            public readonly SampleMenuView portraitSheet;

            public bool IsValid => gameplayHud != null
                                   && pauseMenu != null
                                   && lobbyIconsHud != null
                                   && inventoryPage != null
                                   && settingsModal != null
                                   && unitInfoModal != null
                                   && confirmationModal != null
                                   && portraitSheet != null;
        }

        private readonly struct MenuInstances
        {
            public MenuInstances(GameplayHudView gameplayHud, PauseMenuView pauseMenu, LobbyIconsHudView lobbyIconsHud,
                InventoryMenuView inventoryPage, SampleMenuView settingsModal, SampleMenuView unitInfoModal,
                SampleMenuView confirmationModal, SampleMenuView portraitSheet, ButtonTabsWindowView buttonTabsWindow)
            {
                this.gameplayHud = gameplayHud;
                this.pauseMenu = pauseMenu;
                this.lobbyIconsHud = lobbyIconsHud;
                this.inventoryPage = inventoryPage;
                this.settingsModal = settingsModal;
                this.unitInfoModal = unitInfoModal;
                this.confirmationModal = confirmationModal;
                this.portraitSheet = portraitSheet;
                this.buttonTabsWindow = buttonTabsWindow;
            }

            public readonly GameplayHudView gameplayHud;
            public readonly PauseMenuView pauseMenu;
            public readonly LobbyIconsHudView lobbyIconsHud;
            public readonly InventoryMenuView inventoryPage;
            public readonly SampleMenuView settingsModal;
            public readonly SampleMenuView unitInfoModal;
            public readonly SampleMenuView confirmationModal;
            public readonly SampleMenuView portraitSheet;
            public readonly ButtonTabsWindowView buttonTabsWindow;
        }
    }
}
