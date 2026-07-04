using Anchor.Character.Attributes;
using Anchor.GameFlow;
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

        [SerializeField, Tooltip("显示当前周剩余行动点的 TextMeshProUGUI。为空时不显示。")]
        private TextMeshProUGUI actionPointText;

        [SerializeField, Tooltip("显示 Wishlist 数值的 TextMeshProUGUI。为空时不显示。")]
        private TextMeshProUGUI wishlistText;

        [SerializeField, Tooltip("显示 Quality 数值的 TextMeshProUGUI。为空时不显示。")]
        private TextMeshProUGUI qualityText;

        [Header("Button")]
        [SerializeField, Tooltip("点击后结束本周行动并关闭 MainPanel 的按钮。")]
        private Button nextWeekButton;

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
        }

        /// <summary>
        /// 打开 MainPanel。
        /// </summary>
        public void Open()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            RefreshStatusText();
        }

        /// <summary>
        /// 关闭 MainPanel。
        /// </summary>
        public void Close()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 点击下一周按钮后交给流程 UI 编排器推进下一步。
        /// </summary>
        private void OnNextWeekButtonClicked()
        {
            GameFlowPanelCoordinator.GetOrCreate().NextStep();
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

            SetText(goldText, $"Gold: {blackboard.Coins}");
            SetText(bugText, $"Bug: {blackboard.BugScore}");
            SetText(viewText, $"View: {blackboard.VisualScore}");
            SetText(audioText, $"Audio: {blackboard.AtmosphereScore}");
            SetText(actionPointText, $"AP: {blackboard.RemainingActionPoints}");
            SetText(wishlistText, $"Wishlist: {blackboard.WishlistCount}");
            SetText(qualityText, $"Quality: {blackboard.QualityScore}");
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
                || attributeId == CharacterAttributeIds.Wishlist
                || attributeId == CharacterAttributeIds.Quality;
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
            SetText(goldText, "Gold: --");
            SetText(bugText, "Bug: --");
            SetText(viewText, "View: --");
            SetText(audioText, "Audio: --");
            SetText(actionPointText, "AP: --");
            SetText(wishlistText, "Wishlist: --");
            SetText(qualityText, "Quality: --");
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
