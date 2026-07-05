using System;
using System.Collections.Generic;
using Anchor.Character.Attributes;
using Anchor.GameFlow;
using Anchor.UI;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YokiFrame;

namespace Anchor.UI.Panel
{
    [DisallowMultipleComponent]
    public sealed class MainPanelManager : PanelManagerSingleton<MainPanelManager>
    {
        [Header("Status Text")]
        [SerializeField, Tooltip("显示 Gold 数值的 TextMeshProUGUI。")]
        private TextMeshProUGUI goldText;

        [SerializeField, Tooltip("显示 Bug 数值的 TextMeshProUGUI。")]
        private TextMeshProUGUI bugText;

        [SerializeField, Tooltip("显示 View 数值的 TextMeshProUGUI。")]
        private TextMeshProUGUI viewText;

        [SerializeField, Tooltip("显示 Audio 数值的 TextMeshProUGUI。")]
        private TextMeshProUGUI audioText;

        [SerializeField, Tooltip("根据当前周剩余行动点动态生成行动点 UI 的容器控制器。为空时不显示。")]
        private APContainerController actionPointContainer;

        [SerializeField, Tooltip("显示 Wishlist 数值的 TextMeshProUGUI。为空时不显示。")]
        private TextMeshProUGUI wishlistText;

        [SerializeField, Tooltip("显示 Quality 数值的 TextMeshProUGUI。为空时不显示。")]
        private TextMeshProUGUI qualityText;

        [Header("Status Change Animation")]
        [SerializeField, Min(0.01f), Tooltip("数值变化时膨胀或缩小的动画时长。")]
        private float statusChangeScaleDuration = 0.12f;

        [SerializeField, Min(0.01f), Tooltip("数值变化后恢复原尺寸和颜色的动画时长。")]
        private float statusChangeRestoreDuration = 0.18f;

        [SerializeField, Min(1f), Tooltip("数值上升时 Text 膨胀到的倍率。")]
        private float statusIncreaseScale = 1.18f;

        [SerializeField, Range(0.01f, 1f), Tooltip("数值下降时 Text 缩小到的倍率。")]
        private float statusDecreaseScale = 0.88f;

        [SerializeField, Tooltip("数值上升时 Text 短暂显示的颜色。")]
        private Color statusIncreaseColor = Color.green;

        [SerializeField, Tooltip("数值下降时 Text 短暂显示的颜色。")]
        private Color statusDecreaseColor = Color.red;

        [Header("Button")]
        [SerializeField, Tooltip("点击后结束本周行动并关闭 MainPanel 的按钮。")]
        private Button nextWeekButton;

        // 当前 MainPanel 关闭后要交还给流程编排器执行的回调。
        private Action onClosed;
        // 是否已经记录过上一次流程数值；第一次刷新只赋值不播放变化动画。
        private bool hasStatusSnapshot;
        // 上一次显示的流程数值，用于判断这次是上升还是下降。
        private int lastGoldValue;
        private int lastBugValue;
        private int lastViewValue;
        private int lastAudioValue;
        private int lastWishlistValue;
        private int lastQualityValue;
        // 缓存 Text 初始颜色和尺寸，动画结束时恢复到原始样式。
        private readonly Dictionary<TMP_Text, Color> statusTextOriginalColors = new Dictionary<TMP_Text, Color>();
        private readonly Dictionary<Transform, Vector3> statusTextOriginalScales = new Dictionary<Transform, Vector3>();
        private readonly HashSet<TMP_Text> statusTextsShowingDelta = new HashSet<TMP_Text>();

        /// <summary>
        /// Panel 启用时注册下一周按钮点击事件和流程数据刷新事件。
        /// </summary>
        private void OnEnable()
        {
            RegisterNextWeekButtonClick();
            RegisterFlowEvents();
            RefreshStatusText();
        }

        /// <summary>
        /// Panel 关闭时注销按钮和流程事件，避免重复绑定。
        /// </summary>
        private void OnDisable()
        {
            UnregisterNextWeekButtonClick();
            UnregisterFlowEvents();
            StopAllStatusTextAnimations();
        }

        /// <summary>
        /// 打开 MainPanel，并记录关闭后继续流程的回调。
        /// </summary>
        public void Open(Action closedCallback = null)
        {
            onClosed = closedCallback;

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            RefreshStatusText();
        }

        /// <summary>
        /// 关闭 MainPanel，并清理关闭回调。
        /// </summary>
        public void Close()
        {
            onClosed = null;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 点击下一周按钮后关闭 MainPanel，并在关闭后通知流程编排器继续。
        /// </summary>
        private void OnNextWeekButtonClicked()
        {
            CloseAndNotify();
        }

        /// <summary>
        /// 关闭 MainPanel，并触发本次打开时注入的关闭回调。
        /// </summary>
        private void CloseAndNotify()
        {
            gameObject.SetActive(false);
            NotifyClosed();
        }

        /// <summary>
        /// 执行并清理 MainPanel 关闭回调，防止重复推进流程。
        /// </summary>
        private void NotifyClosed()
        {
            Action closedCallback = onClosed;
            onClosed = null;
            closedCallback?.Invoke();
        }

        /// <summary>
        /// 刷新 MainPanel 上显示的玩家流程属性。
        /// </summary>
        public void RefreshStatusText()
        {
            if (!TryGetCurrentBlackboard(out GameFlowBlackboard blackboard))
            {
                SetStatusTextUnavailable();
                return;
            }

            SetStatusText(goldText, "Gold", blackboard.Coins, lastGoldValue);
            SetStatusText(bugText, "Bug", blackboard.BugScore, lastBugValue);
            SetStatusText(viewText, "View", blackboard.VisualScore, lastViewValue);
            SetStatusText(audioText, "Audio", blackboard.AtmosphereScore, lastAudioValue);
            SetActionPointContainer(blackboard.RemainingActionPoints);
            SetStatusText(wishlistText, "Wishlist", blackboard.WishlistCount, lastWishlistValue);
            SetStatusText(qualityText, "Quality", blackboard.QualityScore, lastQualityValue);
            CacheStatusSnapshot(blackboard);
        }

        /// <summary>
        /// 监听玩家属性变化，变化来自当前流程黑板时刷新 MainPanel 数据。
        /// </summary>
        private void OnPlayerAttributeChanged(CharacterAttributeChangedEvent attributeEvent)
        {
            if (!TryGetCurrentBlackboard(out GameFlowBlackboard blackboard))
            {
                return;
            }

            if (attributeEvent.AttributeSet != blackboard.PlayerAttributes)
            {
                return;
            }

            if (!ShouldRefreshForAttribute(attributeEvent.AttributeId))
            {
                return;
            }

            RefreshStatusText();
        }

        /// <summary>
        /// 监听流程状态变化，进入周开始和周行动阶段时刷新 MainPanel 数据。
        /// </summary>
        private void OnGameFlowStateChanged(GameFlowStateChangedEvent flowEvent)
        {
            if (!TryGetCurrentBlackboard(out GameFlowBlackboard blackboard) || flowEvent.Blackboard != blackboard)
            {
                return;
            }

            if (flowEvent.State == GameFlowState.WeekStart || flowEvent.State == GameFlowState.WeekAction)
            {
                RefreshStatusText();
            }
        }

        /// <summary>
        /// 注册流程数据变化事件。
        /// </summary>
        private void RegisterFlowEvents()
        {
            EventKit.Type.UnRegister<CharacterAttributeChangedEvent>(OnPlayerAttributeChanged);
            EventKit.Type.UnRegister<GameFlowStateChangedEvent>(OnGameFlowStateChanged);
            EventKit.Type.Register<CharacterAttributeChangedEvent>(OnPlayerAttributeChanged);
            EventKit.Type.Register<GameFlowStateChangedEvent>(OnGameFlowStateChanged);
        }

        /// <summary>
        /// 注销流程数据变化事件。
        /// </summary>
        private void UnregisterFlowEvents()
        {
            EventKit.Type.UnRegister<CharacterAttributeChangedEvent>(OnPlayerAttributeChanged);
            EventKit.Type.UnRegister<GameFlowStateChangedEvent>(OnGameFlowStateChanged);
        }

        /// <summary>
        /// 给下一周按钮注册点击事件。
        /// </summary>
        private void RegisterNextWeekButtonClick()
        {
            if (nextWeekButton == null)
            {
                Debug.LogWarning($"{nameof(MainPanelManager)} needs a next week button.", this);
                return;
            }

            nextWeekButton.onClick.RemoveListener(OnNextWeekButtonClicked);
            nextWeekButton.onClick.AddListener(OnNextWeekButtonClicked);
        }

        /// <summary>
        /// 移除下一周按钮点击事件。
        /// </summary>
        private void UnregisterNextWeekButtonClick()
        {
            if (nextWeekButton == null)
            {
                return;
            }

            nextWeekButton.onClick.RemoveListener(OnNextWeekButtonClicked);
        }

        /// <summary>
        /// 判断属性变化是否需要刷新 MainPanel。
        /// </summary>
        private static bool ShouldRefreshForAttribute(int attributeId)
        {
            return attributeId == CharacterAttributeIds.WeeklyActionPower
                || attributeId == CharacterAttributeIds.Coins
                || attributeId == CharacterAttributeIds.Bug
                || attributeId == CharacterAttributeIds.Visual
                || attributeId == CharacterAttributeIds.Atmosphere
                || attributeId == CharacterAttributeIds.Wishlist;
        }

        /// <summary>
        /// 获取当前游戏流程黑板。
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
            hasStatusSnapshot = false;
            StopAllStatusTextAnimations();
            SetText(goldText, "Gold: --");
            SetText(bugText, "Bug: --");
            SetText(viewText, "View: --");
            SetText(audioText, "Audio: --");
            ClearActionPointContainer();
            SetText(wishlistText, "Wishlist: --");
            SetText(qualityText, "Quality: --");
        }

        /// <summary>
        /// 设置流程数值文本，并在已有旧值时按涨跌播放反馈动画。
        /// </summary>
        private void SetStatusText(TextMeshProUGUI text, string label, int currentValue, int previousValue)
        {
            if (!hasStatusSnapshot || text == null || currentValue == previousValue)
            {
                if (text != null && statusTextsShowingDelta.Contains(text))
                {
                    return;
                }

                SetText(text, $"{label}: {currentValue}");
                return;
            }

            PlayStatusChangeAnimation(text, label, currentValue, currentValue - previousValue);
        }

        /// <summary>
        /// 缓存本次流程数值，供下一次刷新判断涨跌。
        /// </summary>
        private void CacheStatusSnapshot(GameFlowBlackboard blackboard)
        {
            lastGoldValue = blackboard.Coins;
            lastBugValue = blackboard.BugScore;
            lastViewValue = blackboard.VisualScore;
            lastAudioValue = blackboard.AtmosphereScore;
            lastWishlistValue = blackboard.WishlistCount;
            lastQualityValue = blackboard.QualityScore;
            hasStatusSnapshot = true;
        }

        /// <summary>
        /// 数值上升时显示加数、膨胀并变绿；数值下降时显示减数、缩小并变红；随后恢复最终数值和原始样式。
        /// </summary>
        private void PlayStatusChangeAnimation(TextMeshProUGUI text, string label, int currentValue, int deltaValue)
        {
            CacheOriginalTextState(text);

            bool isIncrease = deltaValue > 0;
            Transform textTransform = text.transform;
            Color originalColor = statusTextOriginalColors[text];
            Vector3 originalScale = statusTextOriginalScales[textTransform];
            Vector3 targetScale = originalScale * (isIncrease ? statusIncreaseScale : statusDecreaseScale);
            Color targetColor = isIncrease ? statusIncreaseColor : statusDecreaseColor;

            text.DOKill();
            textTransform.DOKill();
            statusTextsShowingDelta.Add(text);
            text.text = FormatDeltaText(deltaValue);
            text.color = targetColor;
            textTransform.localScale = originalScale;

            Sequence sequence = DOTween.Sequence()
                .SetTarget(text)
                .SetUpdate(true);
            sequence.Join(textTransform.DOScale(targetScale, statusChangeScaleDuration).SetEase(Ease.OutQuad));
            sequence.Append(textTransform.DOScale(originalScale, statusChangeRestoreDuration).SetEase(Ease.OutQuad));
            sequence.Join(text.DOColor(originalColor, statusChangeRestoreDuration).SetEase(Ease.OutQuad));
            sequence.OnComplete(() =>
            {
                statusTextsShowingDelta.Remove(text);
                SetText(text, $"{label}: {currentValue}");
            });
            sequence.OnKill(() => statusTextsShowingDelta.Remove(text));
        }

        /// <summary>
        /// 格式化变化期间临时显示的加数或减数。
        /// </summary>
        private static string FormatDeltaText(int deltaValue)
        {
            return deltaValue > 0 ? $"+{deltaValue}" : deltaValue.ToString();
        }

        /// <summary>
        /// 首次动画前记录 Text 原始颜色和尺寸。
        /// </summary>
        private void CacheOriginalTextState(TMP_Text text)
        {
            if (!statusTextOriginalColors.ContainsKey(text))
            {
                statusTextOriginalColors[text] = text.color;
            }

            Transform textTransform = text.transform;
            if (!statusTextOriginalScales.ContainsKey(textTransform))
            {
                statusTextOriginalScales[textTransform] = textTransform.localScale;
            }
        }

        /// <summary>
        /// 停止所有状态文本动画，并把 Text 恢复到缓存的原始颜色和尺寸。
        /// </summary>
        private void StopAllStatusTextAnimations()
        {
            StopStatusTextAnimation(goldText);
            StopStatusTextAnimation(bugText);
            StopStatusTextAnimation(viewText);
            StopStatusTextAnimation(audioText);
            StopStatusTextAnimation(wishlistText);
            StopStatusTextAnimation(qualityText);
        }

        /// <summary>
        /// 停止单个状态文本动画并恢复原始样式。
        /// </summary>
        private void StopStatusTextAnimation(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            Transform textTransform = text.transform;
            text.DOKill();
            textTransform.DOKill();
            statusTextsShowingDelta.Remove(text);

            if (statusTextOriginalColors.TryGetValue(text, out Color originalColor))
            {
                text.color = originalColor;
            }

            if (statusTextOriginalScales.TryGetValue(textTransform, out Vector3 originalScale))
            {
                textTransform.localScale = originalScale;
            }
        }

        /// <summary>
        /// 将当前剩余行动点数量交给 AP 容器刷新。
        /// </summary>
        private void SetActionPointContainer(int actionPointCount)
        {
            EnsureActionPointContainer();

            if (actionPointContainer != null)
            {
                actionPointContainer.SetActionPointCount(actionPointCount);
            }
        }

        /// <summary>
        /// 流程数据不可用时清空 AP 容器。
        /// </summary>
        private void ClearActionPointContainer()
        {
            EnsureActionPointContainer();

            if (actionPointContainer != null)
            {
                actionPointContainer.Clear();
            }
        }

        /// <summary>
        /// AP 容器未手动配置时，从 MainPanel 子级自动查找。
        /// </summary>
        private void EnsureActionPointContainer()
        {
            if (actionPointContainer == null)
            {
                actionPointContainer = GetComponentInChildren<APContainerController>(true);
            }
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
        /// 挂到 Button 本体时自动填充按钮引用。
        /// </summary>
        private void Reset()
        {
            if (nextWeekButton == null)
            {
                nextWeekButton = GetComponent<Button>();
            }
        }
    }
}
