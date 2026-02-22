using System;
using System.Collections.Generic;
using System.Linq;
using Core.Interfaces;
using Core.UI.Runtime;
using UnityEngine;

namespace Core.UI
{
    [DisallowMultipleComponent]
    public class MenuController : MonoBehaviour, IService
    {
        public static MenuController Instance { get; private set; }

        [SerializeField]
        private List<MenuGroupEntry> menuGroupEntries = new();

        [SerializeField]
        private MonoBehaviour gameplayPauseBridge;

        [Serializable]
        public class MenuGroupEntry
        {
            public MenuGroupDefinition group;
            public List<UIMenuComponent> menus;
            public bool hideAllOtherGroups;
            public bool pauseWhenOpen = true;
            public bool isModal;
            public bool usesUIInput = true;
            public bool openOnStartup;
        }

        public Action<bool> OnMenuStateChanged;

        public bool AnyMenuOpen => menuStack.Count > 0;
        public int OpenMenuCount => menuStack.Count;

        public bool AnyUIInputMenuOpen =>
            menuStack.Any(menu => menu != null && menuToGroup.TryGetValue(menu, out var entry) && entry.usesUIInput);

        public bool IsPaused =>
            menuStack.Any(menu => menu != null && menuToGroup.TryGetValue(menu, out var entry) && entry.pauseWhenOpen);

        private IGameplayPauseBridge PauseBridge => gameplayPauseBridge as IGameplayPauseBridge;

        private Dictionary<MenuGroupDefinition, List<UIMenuComponent>> groupMenuLookup = new();
        private readonly Dictionary<UIMenuComponent, MenuGroupEntry> menuToGroup = new();

        private readonly Stack<UIMenuComponent> menuStack = new();
        private readonly Stack<Dictionary<string, object>> menuParameters = new();

        private readonly Dictionary<MenuGroupDefinition, MenuGroupEntry> groupEntryLookup = new();
        private readonly Dictionary<string, MenuGroupDefinition> groupIdLookup = new();

        private bool lastPausedState;

        private void OnEnable()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[MenuController] Extra instance detected, destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Services.TryAdd(this);
        }

        private void Start()
        {
            for (var i = 0; i < menuGroupEntries.Count; i++)
            {
                var entry = menuGroupEntries[i];
                if (entry == null || !entry.openOnStartup)
                    continue;

                ShowMenuGroup(entry.group);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Services.Remove<MenuController>();
                PauseBridge?.SetGameplayPaused(false);
                Instance = null;
            }

            OnMenuStateChanged = null;
        }

        public void InitializeService()
        {
        }

        public void StartService()
        {
            groupMenuLookup = new Dictionary<MenuGroupDefinition, List<UIMenuComponent>>();
            menuToGroup.Clear();
            groupEntryLookup.Clear();
            groupIdLookup.Clear();

            for (var i = 0; i < menuGroupEntries.Count; i++)
            {
                var entry = menuGroupEntries[i];
                if (entry == null || entry.group == null)
                    continue;

                groupEntryLookup[entry.group] = entry;
                groupIdLookup[entry.group.GroupId] = entry.group;

                var list = new List<UIMenuComponent>();
                if (entry.menus != null)
                {
                    for (var j = 0; j < entry.menus.Count; j++)
                    {
                        var menu = entry.menus[j];
                        if (menu == null)
                            continue;

                        list.Add(menu);
                        menuToGroup[menu] = entry;
                    }
                }

                groupMenuLookup[entry.group] = list;
            }
        }

        public void CleanupService()
        {
        }

        public void Show(UIMenuComponent menu, Dictionary<string, object> args = null)
        {
            PushMenu(menu, args);
        }

        public void Hide(UIMenuComponent menu)
        {
            RemoveMenu(menu);
        }

        public void Toggle(UIMenuComponent menu, Dictionary<string, object> args = null)
        {
            if (menu == null)
                return;

            if (IsMenuOpen(menu))
                Hide(menu);
            else
                Show(menu, args);
        }

        public void PushMenu(UIMenuComponent menu, Dictionary<string, object> parameters = null)
        {
            if (menu == null)
                return;

            if (menuStack.Contains(menu))
                return;

            if (menuStack.Count > 0 && menuStack.Peek() != null)
                menuStack.Peek().Pause();

            menuParameters.Push(parameters);
            menuStack.Push(menu);
            menu.Open(parameters);
            NotifyMenuStateChanged();
        }

        public void PopMenu()
        {
            if (menuStack.Count == 0)
                return;

            var closing = menuStack.Pop();
            menuParameters.Pop();
            closing.Close();

            if (menuStack.Count > 0)
                menuStack.Peek().Resume();

            NotifyMenuStateChanged();
        }

        public void PopBack()
        {
            PopMenu();
        }

        public bool IsMenuOpen(UIMenuComponent menu)
        {
            return menu != null && menuStack.Contains(menu);
        }

        public bool IsTopMenu(UIMenuComponent menu)
        {
            return menu != null && menuStack.Count > 0 && menuStack.Peek() == menu;
        }

        public bool RemoveMenu(UIMenuComponent target)
        {
            if (target == null || menuStack.Count == 0 || !menuStack.Contains(target))
                return false;

            var aboveMenus = new List<(UIMenuComponent menu, Dictionary<string, object> parms)>();
            while (menuStack.Count > 0)
            {
                var top = menuStack.Peek();
                var parms = menuParameters.Peek();

                if (top == target)
                    break;

                menuStack.Pop();
                menuParameters.Pop();
                top.Close();
                aboveMenus.Add((top, parms));
            }

            if (menuStack.Count == 0)
                return false;

            var closing = menuStack.Pop();
            menuParameters.Pop();
            closing.Close();

            if (menuStack.Count > 0)
                menuStack.Peek().Resume();

            for (var i = aboveMenus.Count - 1; i >= 0; i--)
            {
                var item = aboveMenus[i];
                menuStack.Push(item.menu);
                menuParameters.Push(item.parms);
                item.menu.Open(item.parms);
            }

            NotifyMenuStateChanged();
            return true;
        }

        public void CloseAll()
        {
            while (menuStack.Count > 0)
            {
                var menu = menuStack.Pop();
                menuParameters.Pop();
                menu.Close();
            }

            NotifyMenuStateChanged();
        }

        public void CloseAllModals()
        {
            while (IsTopMenuModal())
                PopMenu();
        }

        public void ShowMenuGroup(MenuGroupDefinition group, Dictionary<string, object> args = null)
        {
            if (group == null)
                return;

            if (!groupMenuLookup.TryGetValue(group, out var menus) || menus == null || menus.Count == 0)
                return;

            if (groupEntryLookup.TryGetValue(group, out var entry) && entry.hideAllOtherGroups)
            {
                foreach (var openMenu in menuStack)
                    openMenu?.Pause();
            }

            for (var i = 0; i < menus.Count; i++)
                PushMenu(menus[i], args);
        }

        public void ShowMenuGroup(string groupId, Dictionary<string, object> args = null)
        {
            if (TryGetMenuGroup(groupId, out var group))
                ShowMenuGroup(group, args);
        }

        public void HideMenuGroup(MenuGroupDefinition group)
        {
            if (group == null)
                return;

            if (!groupMenuLookup.TryGetValue(group, out var menus) || menus == null || menus.Count == 0)
                return;

            for (var i = 0; i < menus.Count; i++)
                RemoveMenu(menus[i]);
        }

        public void SetPaused(bool paused)
        {
            PauseBridge?.SetGameplayPaused(paused);
        }

        public bool TryGetMenuFromGroup<T>(MenuGroupDefinition group, out T menu) where T : UIMenuComponent
        {
            menu = null;
            return groupMenuLookup != null
                   && groupMenuLookup.TryGetValue(group, out var menus)
                   && (menu = menus.OfType<T>().FirstOrDefault()) != null;
        }

        public bool TryGetMenuFromGroup<T>(string groupId, out T menu) where T : UIMenuComponent
        {
            menu = null;
            return TryGetMenuGroup(groupId, out var group) && TryGetMenuFromGroup(group, out menu);
        }

        public bool TryGetMenuGroup(string groupId, out MenuGroupDefinition group)
        {
            group = null;
            return !string.IsNullOrWhiteSpace(groupId) && groupIdLookup.TryGetValue(groupId, out group);
        }

        public bool IsTopMenuModal()
        {
            return menuStack.TryPeek(out var top)
                   && top != null
                   && menuToGroup.TryGetValue(top, out var entry)
                   && entry.isModal;
        }

        private void NotifyMenuStateChanged()
        {
            var paused = IsPaused;
            if (paused != lastPausedState)
            {
                PauseBridge?.SetGameplayPaused(paused);
                lastPausedState = paused;
            }

            OnMenuStateChanged?.Invoke(AnyUIInputMenuOpen);
        }
    }
}
