using Core;
using Managers;
using Core.UI;
using UnityEngine;

[RequireComponent(typeof(MenuController))]
public class MenuInputRouter : MonoBehaviour
{
    [SerializeField]
    private MenuController menuController;

    [Header("Menus")]
    [SerializeField]
    private UIMenuComponent pauseMenu;

    [SerializeField]
    private UIMenuComponent rootGameplayMenu;

    private GameInputManager inputManager;

    private void Awake()
    {
        if (menuController == null)
            menuController = GetComponent<MenuController>();
    }

    private void OnEnable()
    {
        Services.TryGet(out inputManager);
    }

    private void Update()
    {
        if (inputManager == null)
            Services.TryGet(out inputManager);

        if (inputManager == null || menuController == null)
            return;

        var pausePressed = inputManager.GetMenuPausePressed(0);
        var cancelPressed = inputManager.GetMenuCancelPressed(0);

        if (pausePressed)
            OnPauseRequested();

        if (cancelPressed && !pausePressed)
            OnCancelRequested();
    }

    private void OnCancelRequested()
    {
        if (menuController == null)
            return;

        if (!menuController.AnyMenuOpen)
        {
            OnPauseRequested();
            return;
        }

        if (rootGameplayMenu != null
            && menuController.IsTopMenu(rootGameplayMenu)
            && menuController.OpenMenuCount <= 1)
        {
            OnPauseRequested();
            return;
        }

        menuController.PopBack();
    }

    private void OnPauseRequested()
    {
        if (menuController == null || pauseMenu == null)
            return;

        if (!menuController.IsMenuOpen(pauseMenu))
        {
            menuController.Show(pauseMenu);
            return;
        }

        while (menuController.AnyMenuOpen && !menuController.IsTopMenu(pauseMenu))
            menuController.PopBack();

        if (menuController.IsTopMenu(pauseMenu))
            menuController.PopBack();
    }
}
