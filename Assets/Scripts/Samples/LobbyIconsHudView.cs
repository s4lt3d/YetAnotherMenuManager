using System.Collections;
using System.Collections.Generic;
using Core.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Samples
{
    public class LobbyIconsHudView : UIMenuComponent
    {
        [Header("Buttons")]
        public Button inventoryButton;
        public Button settingsButton;
        public Button unitInfoButton;
        public Button confirmationButton;
        public Button portraitSheetButton;
        public Button closeButton;

        [Header("Animation")]
        [SerializeField]
        private RectTransform[] animatedIcons;

        [SerializeField]
        private float hiddenOffset = 180f;

        [SerializeField]
        private float transitionDuration = 0.2f;

        [SerializeField]
        private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private Vector2[] visiblePositions;
        private Vector2[] hiddenPositions;
        private bool positionsCached;
        private bool stackBlocked = true;
        private CanvasGroup canvasGroup;
        private Coroutine transitionRoutine;

        public float AnimationDurationSeconds => Mathf.Max(0.01f, transitionDuration);

        private void Awake()
        {
            EnsureCanvasGroup();
        }

        public override void Open(Dictionary<string, object> parameters = null)
        {
            base.Open(parameters);
            EnsurePositions();
            ApplyAnimationState(stackBlocked ? 1f : 0f);
        }

        public override void Resume()
        {
            base.Resume();
            EnsurePositions();
            ApplyAnimationState(stackBlocked ? 1f : 0f);
        }

        public override void Close()
        {
            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
                transitionRoutine = null;
            }

            base.Close();
        }

        public void SetStackBlocked(bool blocked, bool instant = false)
        {
            EnsurePositions();
            stackBlocked = blocked;

            if (instant)
            {
                if (transitionRoutine != null)
                {
                    StopCoroutine(transitionRoutine);
                    transitionRoutine = null;
                }

                ApplyAnimationState(blocked ? 1f : 0f);
                return;
            }

            if (transitionRoutine != null)
                StopCoroutine(transitionRoutine);
            transitionRoutine = StartCoroutine(AnimateTo(blocked ? 1f : 0f));
        }

        private IEnumerator AnimateTo(float target)
        {
            var elapsed = 0f;
            var duration = Mathf.Max(0.01f, transitionDuration);
            var start = GetCurrentBlockLerp();

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var curved = transitionCurve != null ? transitionCurve.Evaluate(t) : t;
                var lerp = Mathf.Lerp(start, target, curved);
                ApplyAnimationState(lerp);
                yield return null;
            }

            ApplyAnimationState(target);
            transitionRoutine = null;
        }

        private float GetCurrentBlockLerp()
        {
            if (canvasGroup == null)
                EnsureCanvasGroup();

            return 1f - (canvasGroup != null ? canvasGroup.alpha : 1f);
        }

        private void EnsurePositions()
        {
            if (positionsCached)
                return;

            if (animatedIcons == null || animatedIcons.Length == 0)
                animatedIcons = BuildIconListFromButtons();

            if (animatedIcons == null)
                return;

            visiblePositions = new Vector2[animatedIcons.Length];
            hiddenPositions = new Vector2[animatedIcons.Length];

            for (var i = 0; i < animatedIcons.Length; i++)
            {
                var rect = animatedIcons[i];
                if (rect == null)
                    continue;

                visiblePositions[i] = rect.anchoredPosition;
                var direction = GetNearestEdgeDirection(rect);
                hiddenPositions[i] = visiblePositions[i] + direction * hiddenOffset;
            }

            positionsCached = true;
        }

        private RectTransform[] BuildIconListFromButtons()
        {
            var icons = new List<RectTransform>(6);
            AddButtonRect(icons, inventoryButton);
            AddButtonRect(icons, settingsButton);
            AddButtonRect(icons, unitInfoButton);
            AddButtonRect(icons, confirmationButton);
            AddButtonRect(icons, portraitSheetButton);
            AddButtonRect(icons, closeButton);
            return icons.ToArray();
        }

        private static void AddButtonRect(List<RectTransform> icons, Button button)
        {
            if (button == null)
                return;

            var rect = button.GetComponent<RectTransform>();
            if (rect != null)
                icons.Add(rect);
        }

        private static Vector2 GetNearestEdgeDirection(RectTransform rect)
        {
            var left = rect.anchorMin.x;
            var right = 1f - rect.anchorMax.x;
            var bottom = rect.anchorMin.y;
            var top = 1f - rect.anchorMax.y;

            var minDistance = left;
            var direction = Vector2.left;

            if (right < minDistance)
            {
                minDistance = right;
                direction = Vector2.right;
            }

            if (bottom < minDistance)
            {
                minDistance = bottom;
                direction = Vector2.down;
            }

            if (top < minDistance)
                direction = Vector2.up;

            return direction;
        }

        private void EnsureCanvasGroup()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        private void ApplyAnimationState(float blockedLerp)
        {
            blockedLerp = Mathf.Clamp01(blockedLerp);
            EnsureCanvasGroup();

            if (animatedIcons != null && visiblePositions != null && hiddenPositions != null)
            {
                for (var i = 0; i < animatedIcons.Length; i++)
                {
                    var rect = animatedIcons[i];
                    if (rect == null)
                        continue;

                    rect.anchoredPosition = Vector2.Lerp(visiblePositions[i], hiddenPositions[i], blockedLerp);
                }
            }

            var alpha = 1f - blockedLerp;
            canvasGroup.alpha = alpha;
            var interactable = alpha > 0.98f;
            canvasGroup.interactable = interactable;
            canvasGroup.blocksRaycasts = interactable;
            SetButtonsInteractable(interactable);
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (inventoryButton != null)
                inventoryButton.interactable = interactable;
            if (settingsButton != null)
                settingsButton.interactable = interactable;
            if (unitInfoButton != null)
                unitInfoButton.interactable = interactable;
            if (confirmationButton != null)
                confirmationButton.interactable = interactable;
            if (portraitSheetButton != null)
                portraitSheetButton.interactable = interactable;
            if (closeButton != null)
                closeButton.interactable = interactable;
        }
    }
}
