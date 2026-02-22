using System.Collections.Generic;
using Core.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Samples
{
    public class InventoryMenuView : UIMenuComponent
    {
        [Header("Tab Controls")]
        public Button prevTabButton;
        public Button nextTabButton;
        public Button backButton;
        public TMP_Text tabLabel;

        [Header("Tab Panels")]
        public GameObject[] tabPanels;

        [Header("Tab Behavior")]
        [SerializeField]
        private int initialTabIndex;

        [SerializeField]
        private bool resetToInitialTabOnOpen = true;

        private int currentTabIndex;
        private bool listenersBound;
        private static readonly string[] TabNames = { "Equipment", "Items", "Skills" };

        private void Awake()
        {
            SelectTab(initialTabIndex);
        }

        private void OnEnable()
        {
            BindListeners();
        }

        private void OnDisable()
        {
            UnbindListeners();
        }

        public override void Open(Dictionary<string, object> parameters = null)
        {
            base.Open(parameters);
            if (resetToInitialTabOnOpen)
                SelectTab(initialTabIndex);
            else
                SelectTab(currentTabIndex);
        }

        public override void Resume()
        {
            base.Resume();
            if (resetToInitialTabOnOpen)
                SelectTab(initialTabIndex);
            else
                SelectTab(currentTabIndex);
        }

        public void CycleTab(int direction)
        {
            if (tabPanels == null || tabPanels.Length == 0)
                return;

            SelectTab((currentTabIndex + direction + tabPanels.Length) % tabPanels.Length);
        }

        public void SelectTab(int index)
        {
            if (tabPanels == null || tabPanels.Length == 0)
                return;

            index = Mathf.Clamp(index, 0, tabPanels.Length - 1);
            ShowTab(index);
        }

        private void ShowTab(int index)
        {
            currentTabIndex = index;
            for (var i = 0; i < tabPanels.Length; i++)
            {
                if (tabPanels[i] != null)
                    tabPanels[i].SetActive(i == currentTabIndex);
            }

            if (tabLabel != null && currentTabIndex < TabNames.Length)
                tabLabel.text = TabNames[currentTabIndex];
        }

        private void BindListeners()
        {
            if (listenersBound)
                return;

            if (prevTabButton != null)
                prevTabButton.onClick.AddListener(OnPrevTabClicked);
            if (nextTabButton != null)
                nextTabButton.onClick.AddListener(OnNextTabClicked);

            listenersBound = true;
        }

        private void UnbindListeners()
        {
            if (!listenersBound)
                return;

            if (prevTabButton != null)
                prevTabButton.onClick.RemoveListener(OnPrevTabClicked);
            if (nextTabButton != null)
                nextTabButton.onClick.RemoveListener(OnNextTabClicked);

            listenersBound = false;
        }

        private void OnPrevTabClicked()
        {
            CycleTab(-1);
        }

        private void OnNextTabClicked()
        {
            CycleTab(1);
        }
    }
}
