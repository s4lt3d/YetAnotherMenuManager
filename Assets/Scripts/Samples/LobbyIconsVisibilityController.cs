using System.Collections;
using Core;
using Core.UI;
using UnityEngine;

namespace Samples
{
    [DisallowMultipleComponent]
    public class LobbyIconsVisibilityController : MonoBehaviour
    {
        [SerializeField]
        private MenuController menuController;

        [SerializeField]
        private PauseMenuView pauseMenu;

        [SerializeField]
        private LobbyIconsHudView lobbyIconsView;

        [SerializeField]
        private bool startEnabled;

        [SerializeField]
        private bool instantRefreshOnEnable = true;

        private bool enabledByUser;
        private Coroutine hideRoutine;

        private void Awake()
        {
            ResolveReferences();
            enabledByUser = startEnabled;

            if (lobbyIconsView != null)
            {
                lobbyIconsView.gameObject.SetActive(false);
                lobbyIconsView.SetStackBlocked(true, true);
            }
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (menuController != null)
                menuController.OnMenuStateChanged += HandleMenuStateChanged;

            RefreshVisibility(instantRefreshOnEnable);
        }

        private void OnDisable()
        {
            if (menuController != null)
                menuController.OnMenuStateChanged -= HandleMenuStateChanged;

            if (hideRoutine != null)
            {
                StopCoroutine(hideRoutine);
                hideRoutine = null;
            }
        }

        public void ToggleVisibility()
        {
            SetEnabled(!enabledByUser);
        }

        public void SetEnabled(bool enabled)
        {
            enabledByUser = enabled;
            RefreshVisibility(false);
        }

        private void HandleMenuStateChanged(bool _)
        {
            RefreshVisibility(false);
        }

        private void RefreshVisibility(bool instant)
        {
            if (menuController == null || pauseMenu == null || lobbyIconsView == null)
                return;

            var shouldExist = enabledByUser && menuController.IsMenuOpen(pauseMenu);
            if (shouldExist)
            {
                if (hideRoutine != null)
                {
                    StopCoroutine(hideRoutine);
                    hideRoutine = null;
                }

                if (!lobbyIconsView.gameObject.activeSelf)
                    lobbyIconsView.gameObject.SetActive(true);

                var blocked = !menuController.IsTopMenu(pauseMenu);
                lobbyIconsView.SetStackBlocked(blocked, instant);
                return;
            }

            var canAnimateOut = !instant && lobbyIconsView.gameObject.activeInHierarchy;
            lobbyIconsView.SetStackBlocked(true, !canAnimateOut);

            if (!canAnimateOut)
            {
                lobbyIconsView.gameObject.SetActive(false);
                return;
            }

            if (hideRoutine != null)
                StopCoroutine(hideRoutine);
            hideRoutine = StartCoroutine(HideAfterAnimation());
        }

        private IEnumerator HideAfterAnimation()
        {
            yield return new WaitForSecondsRealtime(lobbyIconsView.AnimationDurationSeconds);
            lobbyIconsView.gameObject.SetActive(false);
            hideRoutine = null;
        }

        private void ResolveReferences()
        {
            if (menuController == null)
                menuController = FindFirstObjectByType<MenuController>(FindObjectsInactive.Include);
        }
    }
}
