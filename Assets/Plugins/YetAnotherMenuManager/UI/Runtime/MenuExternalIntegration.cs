using System;
using System.Threading;
using Core;
using Core.UI;
using Cysharp.Threading.Tasks;
using Managers;
using UnityEngine;

namespace Core.UI.Runtime
{
    public class MenuActionInvoker : MonoBehaviour
    {
        [SerializeField]
        private MenuController menuController;

        [SerializeField]
        private MenuActionType actionType = MenuActionType.Show;

        [SerializeField]
        private UIMenuComponent targetMenu;

        [SerializeField]
        private MenuGroupDefinition targetGroup;

        [SerializeField]
        private bool pausedValue = true;

        private void Awake()
        {
            if (menuController == null)
                menuController = FindFirstObjectByType<MenuController>(FindObjectsInactive.Include);

            ValidateConfiguration();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateConfiguration();
        }
#endif

        private void ValidateConfiguration()
        {
            switch (actionType)
            {
                case MenuActionType.Show:
                case MenuActionType.Hide:
                case MenuActionType.Toggle:
                    if (targetMenu == null)
                        Debug.LogWarning($"[MenuActionInvoker] '{name}': action '{actionType}' requires targetMenu.", this);
                    break;
                case MenuActionType.ShowGroup:
                    if (targetGroup == null)
                        Debug.LogWarning($"[MenuActionInvoker] '{name}': action '{actionType}' requires targetGroup.", this);
                    break;
            }
        }

        public void InvokeAction()
        {
            InvokeActionAsync().Forget(ex => Debug.LogException(ex, this));
        }

        public UniTask InvokeActionAsync(CancellationToken ct = default)
        {
            if (menuController == null)
                menuController = FindFirstObjectByType<MenuController>(FindObjectsInactive.Include);

            if (menuController == null)
                return UniTask.CompletedTask;

            switch (actionType)
            {
                case MenuActionType.Show:
                    menuController.Show(targetMenu);
                    break;
                case MenuActionType.Hide:
                    menuController.Hide(targetMenu);
                    break;
                case MenuActionType.Toggle:
                    menuController.Toggle(targetMenu);
                    break;
                case MenuActionType.ShowGroup:
                    menuController.ShowMenuGroup(targetGroup);
                    break;
                case MenuActionType.SetPaused:
                    menuController.SetPaused(pausedValue);
                    break;
                case MenuActionType.PopBack:
                    menuController.PopBack();
                    break;
                case MenuActionType.CloseAll:
                    menuController.CloseAll();
                    break;
                case MenuActionType.CloseAllModals:
                    menuController.CloseAllModals();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return UniTask.CompletedTask;
        }
    }

    [DisallowMultipleComponent]
    public class MenuInputModeBridge : MonoBehaviour
    {
        [SerializeField]
        private MenuController menuController;

        [SerializeField]
        private bool clearHintsWhenGameInput = true;

        [SerializeField]
        private MenuHintController hintController;

        private GameInputManager inputManager;

        private void Awake()
        {
            if (menuController == null)
                menuController = FindFirstObjectByType<MenuController>(FindObjectsInactive.Include);
            if (hintController == null)
                hintController = FindFirstObjectByType<MenuHintController>(FindObjectsInactive.Include);
        }

        private void OnEnable()
        {
            Services.TryGet(out inputManager);

            if (menuController != null)
                menuController.OnMenuStateChanged += HandleMenuStateChanged;

            RefreshInputMode(menuController != null && menuController.AnyUIInputMenuOpen);
        }

        private void OnDisable()
        {
            if (menuController != null)
                menuController.OnMenuStateChanged -= HandleMenuStateChanged;
        }

        private void HandleMenuStateChanged(bool hasUIMenus)
        {
            RefreshInputMode(hasUIMenus);
        }

        private void RefreshInputMode(bool hasUIMenus)
        {
            if (inputManager == null)
                Services.TryGet(out inputManager);
            if (inputManager == null)
                return;

            if (hasUIMenus)
            {
                inputManager.SwitchToUIMode();
                hintController?.SetHint("Press ESC to cancel");
            }
            else
            {
                inputManager.SwitchToGameMode();
                if (clearHintsWhenGameInput)
                    hintController?.ClearHint();
            }
        }
    }

    public class TimeScalePauseBridge : MonoBehaviour, IGameplayPauseBridge
    {
        [SerializeField]
        private float pausedTimeScale = 0f;

        [SerializeField]
        private float unpausedTimeScale = 1f;

        public void SetGameplayPaused(bool paused)
        {
            Time.timeScale = paused ? pausedTimeScale : unpausedTimeScale;
        }
    }
}
