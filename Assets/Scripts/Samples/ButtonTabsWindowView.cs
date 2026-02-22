using System.Collections.Generic;
using Core.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Samples
{
    public class ButtonTabsWindowView : SampleMenuView
    {
        [Header("Top Tab Buttons")]
        public Button[] tabButtons;

        [Header("Top Tab Panels")]
        public GameObject[] tabPanels;

        [SerializeField]
        private TMP_Text activeTabLabel;

        [SerializeField]
        private string[] tabNames = { "General", "Display", "Advanced" };

        [SerializeField]
        private int initialTabIndex;

        [SerializeField]
        private bool resetToInitialTabOnOpen = true;

        private int currentTabIndex;
        private UnityAction[] tabActions;
        private bool listenersBound;

        private void Awake()
        {
            SelectTab(initialTabIndex);
        }

        private void OnEnable()
        {
            BindTabListeners();
        }

        private void OnDisable()
        {
            UnbindTabListeners();
        }

        public override void Open(Dictionary<string, object> parameters = null)
        {
            base.Open(parameters);
            SelectTab(resetToInitialTabOnOpen ? initialTabIndex : currentTabIndex);
        }

        public override void Resume()
        {
            base.Resume();
            SelectTab(resetToInitialTabOnOpen ? initialTabIndex : currentTabIndex);
        }

        public void SelectTab(int index)
        {
            if (tabPanels == null || tabPanels.Length == 0)
                return;

            index = Mathf.Clamp(index, 0, tabPanels.Length - 1);
            currentTabIndex = index;

            for (var i = 0; i < tabPanels.Length; i++)
            {
                var panel = tabPanels[i];
                if (panel != null)
                    panel.SetActive(i == currentTabIndex);
            }

            if (tabButtons != null)
            {
                for (var i = 0; i < tabButtons.Length; i++)
                {
                    var button = tabButtons[i];
                    if (button != null)
                        button.interactable = i != currentTabIndex;
                }
            }

            if (activeTabLabel != null)
                activeTabLabel.text = ResolveTabName(currentTabIndex);
        }

        private string ResolveTabName(int index)
        {
            if (tabNames != null
                && index >= 0
                && index < tabNames.Length
                && !string.IsNullOrWhiteSpace(tabNames[index]))
                return tabNames[index];

            if (tabButtons != null && index >= 0 && index < tabButtons.Length && tabButtons[index] != null)
                return tabButtons[index].name;

            return $"Tab {index + 1}";
        }

        private void BindTabListeners()
        {
            if (listenersBound || tabButtons == null)
                return;

            tabActions = new UnityAction[tabButtons.Length];
            for (var i = 0; i < tabButtons.Length; i++)
            {
                var button = tabButtons[i];
                if (button == null)
                    continue;

                var index = i;
                UnityAction action = () => SelectTab(index);
                tabActions[i] = action;
                button.onClick.AddListener(action);
            }

            listenersBound = true;
        }

        private void UnbindTabListeners()
        {
            if (!listenersBound)
                return;

            if (tabButtons != null && tabActions != null)
            {
                var count = Mathf.Min(tabButtons.Length, tabActions.Length);
                for (var i = 0; i < count; i++)
                {
                    var button = tabButtons[i];
                    var action = tabActions[i];
                    if (button != null && action != null)
                        button.onClick.RemoveListener(action);
                }
            }

            tabActions = null;
            listenersBound = false;
        }
    }
}
