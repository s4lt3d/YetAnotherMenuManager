using System.Collections.Generic;
using System.Threading;
using Core.UI.Runtime;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Core.UI
{
    public abstract class UIMenuComponent : MonoBehaviour, IAnimatedMenu
    {
        [Header("Selection")]
        [SerializeField]
        protected GameObject defaultSelected;

        [HideInInspector]
        public bool maintainSelection = true;

        protected GameObject lastSelected;
        private bool isTransitioning;

        public bool IsTransitioning => isTransitioning;

        void Update()
        {
            if (!maintainSelection)
                return;

            var eventSystem = EventSystem.current;
            if (eventSystem == null)
                return;

            var current = eventSystem.currentSelectedGameObject;
            if (current != null)
            {
                lastSelected = current;
                return;
            }

            var target = lastSelected != null ? lastSelected : defaultSelected;
            if (target != null)
                eventSystem.SetSelectedGameObject(target);
        }

        public virtual void Open(Dictionary<string, object> parameters = null)
        {
            gameObject.SetActive(true);
            lastSelected = defaultSelected;
            SetInitialSelection();
        }

        public virtual void Close()
        {
            ClearSelectionIfOwned();
            gameObject.SetActive(false);
        }

        public virtual void Pause()
        {
            ClearSelectionIfOwned();
            gameObject.SetActive(false);
        }

        public virtual void Resume()
        {
            gameObject.SetActive(true);
            SetInitialSelection();
        }

        public virtual UniTask OpenAsync(MenuOpenContext context, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            isTransitioning = true;
            try
            {
                Open(context?.Args);
            }
            finally
            {
                isTransitioning = false;
            }

            return UniTask.CompletedTask;
        }

        public virtual UniTask CloseAsync(MenuCloseContext context, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            isTransitioning = true;
            try
            {
                Close();
            }
            finally
            {
                isTransitioning = false;
            }

            return UniTask.CompletedTask;
        }

        public virtual void InstantShow(Dictionary<string, object> args = null)
        {
            Open(args);
        }

        public virtual void InstantHide()
        {
            Close();
        }

        protected void SetInitialSelection()
        {
            if (defaultSelected == null)
            {
                var fallback = FindFirstSelectable();
                if (fallback != null)
                    defaultSelected = fallback;
            }

            if (defaultSelected != null)
                EventSystem.current?.SetSelectedGameObject(defaultSelected);
        }

        private GameObject FindFirstSelectable()
        {
            var selectables = GetComponentsInChildren<Selectable>(true);
            foreach (var selectable in selectables)
            {
                if (selectable == null)
                    continue;

                if (!selectable.IsInteractable() || !selectable.gameObject.activeInHierarchy)
                    continue;

                return selectable.gameObject;
            }

            return null;
        }

        private void ClearSelectionIfOwned()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
                return;

            var current = eventSystem.currentSelectedGameObject;
            if (current == null)
                return;

            if (current == gameObject || current.transform.IsChildOf(transform))
                eventSystem.SetSelectedGameObject(null);
        }
    }
}
