using System;
using Anchor.GameFlow;
using Anchor.UI;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Anchor.UI.Panel
{
    [DisallowMultipleComponent]
    public sealed class WeekPanelManager : PanelManagerSingleton<WeekPanelManager>
    {
        [Header("Status Text")]
        [SerializeField, Tooltip("显示 Bug 数值的 TextMeshProUGUI。")]
        private TextMeshProUGUI bugText;

        [SerializeField, Tooltip("显示 View 数值的 TextMeshProUGUI。")]
        private TextMeshProUGUI viewText;

        [SerializeField, Tooltip("显示 Audio 数值的 TextMeshProUGUI。")]
        private TextMeshProUGUI audioText;

        [SerializeField, Tooltip("显示 Wishlist 数值的 TextMeshProUGUI。")]
        private TextMeshProUGUI wishlistText;

        [Header("Button")]
        [SerializeField, Tooltip("点击后关闭当前 WeekPanel 的按钮。")]
        private Button closeButton;

        [Header("Close Button Popup")]
        [SerializeField, Tooltip("关闭按钮出现时随机选取其中一条作为按钮文本。")]
        private string[] closeButtonTextOptions = { "继续", "下一周", "完成" };
        [SerializeField, Range(0f, 1f)] private float closeButtonPopupStartScale = 0.65f;
        [SerializeField, Min(0.01f)] private float closeButtonPopupDuration = 0.28f;
        [SerializeField] private Ease closeButtonPopupEase = Ease.OutBack;

        [Header("Wishlist Collection Effect")]
        [SerializeField, Tooltip("可选；为空时会自动接入 MainPanel 左下角的 Wishlist 图标和数值。")]
        private BlackboardCollectingValue wishlistCollectionEffect;

        [Header("Close Animation")]
        [SerializeField, Min(1f)] private float closeOvershootScale = 1.05f;
        [SerializeField, Min(0.01f)] private float closeExpandDuration = 0.12f;
        [SerializeField, Min(0.01f)] private float closeShrinkDuration = 0.24f;
        [SerializeField] private Ease closeExpandEase = Ease.OutQuad;
        [SerializeField] private Ease closeShrinkEase = Ease.InBack;

        // 当前 WeekPanel 关闭后要交还给流程编排器执行的回调。
        private Action onClosed;
        // SumPanel 动画完成前禁用关闭按钮，避免玩家跳过结算展示。
        private global::SumPanelTestAnimator sumPanelAnimator;
        private Sequence closeSequence;
        private Tween closeButtonPopupTween;
        private Vector3 authoredScale;
        private Vector3 closeButtonAuthoredScale = Vector3.one;
        private TMP_Text closeButtonTmpLabel;
        private Text closeButtonLegacyLabel;
        private bool isClosing;

        protected override void Awake()
        {
            base.Awake();
            authoredScale = transform.localScale;
            ResolveCloseButton();
            CacheCloseButtonPresentation();
            ResolveSumPanelAnimator();
        }

        /// <summary>
        /// Panel 启用时注册关闭按钮点击事件。
        /// </summary>
        private void OnEnable()
        {
            KillCloseAnimation();
            ResolveCloseButton();
            CacheCloseButtonPresentation();
            ResolveSumPanelAnimator();
            RegisterAnimationCompletion();
            RegisterCloseButtonClick();
            HideCloseButton();
            if (sumPanelAnimator == null)
            {
                ShowCloseButton();
            }
            RefreshStatusText();
        }

        /// <summary>
        /// Panel 关闭时注销关闭按钮点击事件，避免重复绑定。
        /// </summary>
        private void OnDisable()
        {
            UnregisterAnimationCompletion();
            UnregisterCloseButtonClick();
            KillCloseAnimation();
            KillCloseButtonPopup();
        }

        /// <summary>
        /// 外部打开 WeekPanel 的入口，并记录关闭后继续流程的回调。
        /// </summary>
        public void Open(Action closedCallback = null)
        {
            onClosed = closedCallback;

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
            else
            {
                ResolveSumPanelAnimator();
                RegisterAnimationCompletion();
                HideCloseButton();
                if (sumPanelAnimator == null)
                {
                    ShowCloseButton();
                }
                sumPanelAnimator?.Play();
            }

            RefreshStatusText();
        }

        /// <summary>
        /// 关闭 WeekPanel，并清理关闭回调。
        /// </summary>
        public void Close()
        {
            KillCloseAnimation();
            onClosed = null;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 关闭 WeekPanel，并触发本次打开时注入的关闭回调。
        /// </summary>
        private void CloseAndNotify()
        {
            if (isClosing)
            {
                return;
            }

            isClosing = true;
            SetCloseButtonInteractable(false);
            KillCloseButtonPopup();
            closeSequence?.Kill();
            closeSequence = DOTween.Sequence()
                .SetTarget(this)
                .SetUpdate(true);
            closeSequence.Append(transform.DOScale(
                    authoredScale * closeOvershootScale,
                    closeExpandDuration)
                .SetEase(closeExpandEase));
            closeSequence.Append(transform.DOScale(Vector3.zero, closeShrinkDuration)
                .SetEase(closeShrinkEase));
            closeSequence.OnComplete(() =>
            {
                closeSequence = null;
                isClosing = false;
                transform.localScale = authoredScale;
                gameObject.SetActive(false);
                NotifyClosed();
            });
        }

        /// <summary>
        /// 执行并清理 WeekPanel 关闭回调，防止重复推进流程。
        /// </summary>
        private void NotifyClosed()
        {
            Action closedCallback = onClosed;
            onClosed = null;
            closedCallback?.Invoke();
        }

        /// <summary>
        /// 刷新 WeekPanel 上显示的玩家流程属性。
        /// </summary>
        public void RefreshStatusText()
        {
            if (!TryGetCurrentBlackboard(out GameFlowBlackboard blackboard))
            {
                SetStatusTextUnavailable();
                return;
            }

            SetText(bugText, $"Bug: {blackboard.BugScore}");
            SetText(viewText, $"View: {blackboard.VisualScore}");
            SetText(audioText, $"Audio: {blackboard.AtmosphereScore}");
            SetText(wishlistText, $"Wishlist: {blackboard.WishlistCount}");
        }

        /// <summary>
        /// 获取当前游戏流程黑板，和 MainPanel 使用同一份流程数据。
        /// </summary>
        private static bool TryGetCurrentBlackboard(out GameFlowBlackboard blackboard)
        {
            blackboard = GameFlowRunner.Instance != null && GameFlowRunner.Instance.Controller != null
                ? GameFlowRunner.Instance.Controller.Blackboard
                : null;

            return blackboard != null;
        }

        /// <summary>
        /// 流程数据不可用时显示占位内容。
        /// </summary>
        private void SetStatusTextUnavailable()
        {
            SetText(bugText, "Bug: --");
            SetText(viewText, "View: --");
            SetText(audioText, "Audio: --");
            SetText(wishlistText, "Wishlist: --");
        }

        /// <summary>
        /// 设置 TMP 文本，未绑定时直接跳过。
        /// </summary>
        private static void SetText(TextMeshProUGUI text, string value)
        {
            if (text != null)
            {
                text.text = value;
            }
        }

        /// <summary>
        /// 给关闭按钮注册点击事件。
        /// </summary>
        private void RegisterCloseButtonClick()
        {
            if (closeButton == null)
            {
                Debug.LogWarning($"{nameof(WeekPanelManager)} needs a close button.", this);
                return;
            }

            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.RemoveListener(CloseAndNotify);
            closeButton.onClick.AddListener(CloseAndNotify);
        }

        /// <summary>
        /// 移除关闭按钮点击事件。
        /// </summary>
        private void UnregisterCloseButtonClick()
        {
            if (closeButton == null)
            {
                return;
            }

            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.RemoveListener(CloseAndNotify);
        }

        private void ResolveSumPanelAnimator()
        {
            if (sumPanelAnimator == null)
            {
                sumPanelAnimator = GetComponent<global::SumPanelTestAnimator>();
            }
        }

        private void RegisterAnimationCompletion()
        {
            UnregisterAnimationCompletion();
            if (sumPanelAnimator != null)
            {
                sumPanelAnimator.Completed += OnSumPanelAnimationCompleted;
            }
        }

        private void UnregisterAnimationCompletion()
        {
            if (sumPanelAnimator != null)
            {
                sumPanelAnimator.Completed -= OnSumPanelAnimationCompleted;
            }
        }

        private void OnSumPanelAnimationCompleted()
        {
            PlayWishlistCollectionEffect();
            ShowCloseButton();
        }

        private void ResolveCloseButton()
        {
            if (closeButton == null)
            {
                closeButton = transform.Find("CloseButton")?.GetComponent<Button>();
            }
        }

        private void CacheCloseButtonPresentation()
        {
            if (closeButton == null)
            {
                return;
            }

            closeButtonAuthoredScale = closeButton.transform.localScale;
            closeButtonTmpLabel = closeButton.GetComponentInChildren<TMP_Text>(true);
            closeButtonLegacyLabel = closeButton.GetComponentInChildren<Text>(true);
        }

        private void HideCloseButton()
        {
            KillCloseButtonPopup();
            if (closeButton == null)
            {
                return;
            }

            closeButton.interactable = false;
            closeButton.transform.localScale = closeButtonAuthoredScale;
            closeButton.gameObject.SetActive(false);
        }

        private void ShowCloseButton()
        {
            if (closeButton == null)
            {
                return;
            }

            ApplyRandomCloseButtonText();
            closeButton.gameObject.SetActive(true);
            closeButton.interactable = false;
            closeButton.transform.localScale = closeButtonAuthoredScale * closeButtonPopupStartScale;
            KillCloseButtonPopup();
            closeButtonPopupTween = closeButton.transform
                .DOScale(closeButtonAuthoredScale, closeButtonPopupDuration)
                .SetEase(closeButtonPopupEase)
                .SetUpdate(true)
                .SetTarget(this)
                .OnComplete(() =>
                {
                    closeButtonPopupTween = null;
                    SetCloseButtonInteractable(true);
                });
        }

        private void ApplyRandomCloseButtonText()
        {
            if (closeButtonTextOptions == null || closeButtonTextOptions.Length == 0)
            {
                return;
            }

            int startIndex = UnityEngine.Random.Range(0, closeButtonTextOptions.Length);
            for (int offset = 0; offset < closeButtonTextOptions.Length; offset++)
            {
                string option = closeButtonTextOptions[(startIndex + offset) % closeButtonTextOptions.Length];
                if (string.IsNullOrWhiteSpace(option))
                {
                    continue;
                }

                if (closeButtonTmpLabel != null) closeButtonTmpLabel.text = option;
                if (closeButtonLegacyLabel != null) closeButtonLegacyLabel.text = option;
                return;
            }
        }

        private void PlayWishlistCollectionEffect()
        {
            if (!TryGetCurrentBlackboard(out GameFlowBlackboard blackboard))
            {
                return;
            }

            MainPanelManager mainPanel = FindObjectOfType<MainPanelManager>(true);
            TextMeshProUGUI valueText = mainPanel != null ? mainPanel.WishlistText : null;
            if (valueText != null)
            {
                Transform wishRoot = valueText.transform.parent;
                Image targetIcon = wishRoot != null ? wishRoot.GetComponentInChildren<Image>(true) : null;
                Canvas canvas = valueText.GetComponentInParent<Canvas>();
                RectTransform effectLayer = canvas != null ? canvas.transform as RectTransform : null;

                if (wishlistCollectionEffect == null)
                {
                    GameObject effectOwner = wishRoot != null ? wishRoot.gameObject : valueText.gameObject;
                    wishlistCollectionEffect = effectOwner.GetComponent<BlackboardCollectingValue>();
                    if (wishlistCollectionEffect == null)
                    {
                        wishlistCollectionEffect = effectOwner.AddComponent<BlackboardCollectingValue>();
                    }

                }

                wishlistCollectionEffect.ConfigureManualWishlist(targetIcon, valueText, effectLayer);
            }

            if (wishlistCollectionEffect == null)
            {
                Debug.LogWarning("WeekPanelManager could not find the bottom-left Wishlist UI for its collection effect.", this);
                return;
            }

            WeekResolveResult result = blackboard.LastWeekResult;
            wishlistCollectionEffect.PlayTransition(result.WishlistStartValue, result.WishlistEndValue);
        }

        private void KillCloseButtonPopup()
        {
            closeButtonPopupTween?.Kill();
            closeButtonPopupTween = null;
        }

        private void SetCloseButtonInteractable(bool interactable)
        {
            if (closeButton != null)
            {
                closeButton.interactable = interactable;
            }
        }

        private void KillCloseAnimation()
        {
            closeSequence?.Kill();
            closeSequence = null;
            isClosing = false;
            if (authoredScale != Vector3.zero)
            {
                transform.localScale = authoredScale;
            }
        }

        /// <summary>
        /// 挂到 Button 本体时自动填充按钮引用。
        /// </summary>
        private void Reset()
        {
            ResolveCloseButton();
        }
    }
}
