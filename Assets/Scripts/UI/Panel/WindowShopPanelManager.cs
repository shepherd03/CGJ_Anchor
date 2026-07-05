using System;
using System.Collections.Generic;
using Anchor.Character.Attributes;
using Anchor.GameFlow;
using Anchor.GameFlow.Buffs;
using Anchor.Window;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YokiFrame;

using BuffRow = Anchor.Config.game.buff;

namespace Anchor.UI.Panel
{
    [DisallowMultipleComponent]
    public sealed class WindowShopPanelManager : PanelManagerSingleton<WindowShopPanelManager>
    {
        private const string DollarObjectName = "Dollar";

        [Header("Buff Window")]
        [SerializeField, Tooltip("BuffWindow 上的下落回弹动画组件。为空时会从当前物体或子物体查找。")]
        private WindowDropBounceAnimator buffWindowAnimator;

        [SerializeField, Tooltip("动态生成 BuffCard 的父节点。为空时默认使用当前 UI 节点。")]
        private RectTransform buffCardRoot;

        [SerializeField, Tooltip("用于动态生成的 BuffCard 预制体。")]
        private BuffCardController buffCardPrefab;

        [SerializeField, Tooltip("BuffWindow 的二级介绍弹窗控制器。为空时会从当前物体或子物体查找。")]
        private IntroductionWindowController introductionWindow;

        [Header("Dollar")]
        [SerializeField, Tooltip("显示玩家当前金币数量的 Dollar 文本。为空时会自动查找名为 Dollar 的 UI 节点。")]
        private TextMeshProUGUI dollarText;

        [SerializeField, Min(0.01f), Tooltip("金币变化时膨胀或缩小的动画时长。")]
        private float dollarChangeScaleDuration = 0.12f;

        [SerializeField, Min(0.01f), Tooltip("金币变化后恢复原尺寸和颜色的动画时长。")]
        private float dollarChangeRestoreDuration = 0.18f;

        [SerializeField, Min(1f), Tooltip("金币上升时 Dollar 文本膨胀到的倍率。")]
        private float dollarIncreaseScale = 1.18f;

        [SerializeField, Range(0.01f, 1f), Tooltip("金币下降时 Dollar 文本缩小到的倍率。")]
        private float dollarDecreaseScale = 0.88f;

        [SerializeField, Tooltip("金币上升时 Dollar 文本短暂显示的颜色。")]
        private Color dollarIncreaseColor = Color.green;

        [SerializeField, Tooltip("金币下降时 Dollar 文本短暂显示的颜色。")]
        private Color dollarDecreaseColor = Color.red;

        [Header("Button")]
        [SerializeField, Tooltip("点击后关闭当前 BuffWindow 的按钮。")]
        private Button closeButton;

        [SerializeField, Tooltip("点击后刷新当前月初商店 Buff 候选列表的按钮。")]
        private Button refreshButton;

        // 当前由面板动态生成的 BuffCard，用于重新注入数据时清理旧实例。
        private readonly List<BuffCardController> generatedBuffCards = new List<BuffCardController>();
        // 是否已经记录过上一帧金币值；首次刷新只同步文本，不播放涨跌动画。
        private bool hasDollarSnapshot;
        // 上一次同步到 Dollar 文本的玩家金币值。
        private int lastDollarValue;
        // Dollar 文本原始颜色和尺寸，动画结束时恢复。
        private Color dollarOriginalColor;
        private Vector3 dollarOriginalScale = Vector3.one;
        // 是否已经缓存过 Dollar 文本原始样式。
        private bool hasDollarOriginalState;
        // Dollar 文本正在展示涨跌动画时不被普通刷新覆盖。
        private bool dollarShowingDelta;

        // Buff 图标所在的 Resources 多 Sprite 图片路径，路径不包含文件扩展名。
        private static readonly string[] BuffSpriteSheetResourcePaths =
        {
            "Sprites/shopping1",
            "Sprites/shopping2",
            "Sprites/shopping3",
        };

        // 以 BuffRow.Title 为 key 缓存所有可用 Buff 图标，避免每次刷新商品重复读 Resources。
        private static readonly Dictionary<string, Sprite> buffSpritesByTitle = new Dictionary<string, Sprite>();

        // 标记 Buff 图标缓存是否已经构建过。
        private static bool buffSpriteCacheBuilt;

        // 当前 BuffWindow 关闭后要交还给流程编排器执行的回调。
        private Action onClosed;

        /// <summary>
        /// Panel 启用时注册关闭按钮点击事件。
        /// </summary>
        private void OnEnable()
        {
            RegisterBudgetShopBuffOffersRefreshedEvent();
            RegisterPlayerCoinChangedEvent();
            RegisterCloseButtonClick();
            RegisterRefreshButtonClick();
            RefreshDollarText();
        }

        /// <summary>
        /// Panel 关闭时注销事件和按钮点击事件，避免重复绑定。
        /// 新注释：同时停止 Dollar 金币动画，避免禁用后继续驱动共享文本。
        /// </summary>
        private void OnDisable()
        {
            UnregisterBudgetShopBuffOffersRefreshedEvent();
            UnregisterPlayerCoinChangedEvent();
            UnregisterCloseButtonClick();
            UnregisterRefreshButtonClick();
            StopDollarTextAnimation();
        }

        /// <summary>
        /// 打开 BuffWindow，刷新一次候选 Buff，并记录关闭后继续流程的回调。
        /// </summary>
        public void Open(Action closedCallback = null)
        {
            onClosed = closedCallback;
            EnsureBuffWindowAnimator();

            if (buffWindowAnimator == null)
            {
                Debug.LogWarning($"{nameof(WindowShopPanelManager)} cannot find {nameof(WindowDropBounceAnimator)}.", this);
                return;
            }

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            RefreshDollarText();
            RequestBudgetShopBuffOffersRefresh();
            buffWindowAnimator.Open();
        }

        /// <summary>
        /// 关闭 BuffWindow，并清理关闭回调。
        /// </summary>
        public void Close()
        {
            onClosed = null;
            CloseWindow(null);
        }

        /// <summary>
        /// 关闭 BuffWindow，并在关闭动画完成后通知流程编排器继续。
        /// </summary>
        private void CloseAndNotify()
        {
            CloseWindow(NotifyClosed);
        }

        /// <summary>
        /// 关闭 BuffWindow 的统一入口，回调会在窗口真正隐藏后触发。
        /// </summary>
        private void CloseWindow(Action closedCallback)
        {
            EnsureBuffWindowAnimator();

            if (buffWindowAnimator == null)
            {
                gameObject.SetActive(false);
                closedCallback?.Invoke();
                return;
            }

            buffWindowAnimator.Close(closedCallback);
        }

        /// <summary>
        /// 执行并清理 BuffWindow 关闭回调，防止重复推进流程。
        /// </summary>
        private void NotifyClosed()
        {
            Action closedCallback = onClosed;
            onClosed = null;
            closedCallback?.Invoke();
        }

        /// <summary>
        /// 从外部注入 Buff 候选数据，并刷新当前 BuffWindow 的 BuffCard 列表。
        /// </summary>
        public void InjectData(IReadOnlyList<BuffRow> buffRows)
        {
            ClearGeneratedBuffCards();
            EnsureBuffCardRoot();

            if (buffCardRoot == null)
            {
                Debug.LogWarning($"{nameof(WindowShopPanelManager)} needs a buff card root.", this);
                return;
            }

            if (buffCardPrefab == null)
            {
                Debug.LogWarning($"{nameof(WindowShopPanelManager)} needs a buff card prefab.", this);
                return;
            }

            if (buffRows == null)
            {
                return;
            }

            for (int i = 0; i < buffRows.Count; i++)
            {
                CreateBuffCard(buffRows[i], i);
            }
        }

        /// <summary>
        /// 从外部注入单个 Buff 数据，并刷新二级介绍弹窗的标题、简介和正文。
        /// </summary>
        public void InjectIntroductionData(BuffRow buffRow)
        {
            if (buffRow == null)
            {
                InjectIntroductionData(string.Empty, string.Empty, string.Empty, 0);
                return;
            }

            InjectIntroductionData(buffRow.Title, buffRow.Brief, buffRow.Content, buffRow.Cost);
        }

        /// <summary>
        /// 从外部注入介绍弹窗文本和 Cost 数据，并立即刷新二级介绍弹窗。
        /// </summary>
        public void InjectIntroductionData(string title, string brief, string content, int cost)
        {
            EnsureIntroductionWindow();

            if (introductionWindow == null)
            {
                Debug.LogWarning($"{nameof(WindowShopPanelManager)} needs an introduction window.", this);
                return;
            }

            introductionWindow.InjectData(title, brief, content, cost);
        }

        /// <summary>
        /// 兼容旧调用：未传 Cost 时按 0 显示。
        /// </summary>
        public void InjectIntroductionData(string title, string brief, string content)
        {
            InjectIntroductionData(title, brief, content, 0);
        }

        /// <summary>
        /// 打开二级介绍弹窗，并用指定 Buff 数据刷新标题、简介、正文和 Cost。
        /// </summary>
        public void OpenIntroductionWindow(BuffRow buffRow)
        {
            EnsureIntroductionWindow();

            if (introductionWindow == null)
            {
                Debug.LogWarning($"{nameof(WindowShopPanelManager)} needs an introduction window.", this);
                return;
            }

            InjectIntroductionData(buffRow);
            introductionWindow.SetBuyAction(() => TryPurchaseIntroductionBuff(buffRow));
            introductionWindow.Open();
        }

        /// <summary>
        /// 注册月初商店 Buff 候选刷新事件，收到事件后刷新 BuffCard。
        /// </summary>
        private void RegisterBudgetShopBuffOffersRefreshedEvent()
        {
            EventKit.Type.UnRegister<BudgetShopBuffOffersRefreshedEvent>(OnBudgetShopBuffOffersRefreshed);
            EventKit.Type.Register<BudgetShopBuffOffersRefreshedEvent>(OnBudgetShopBuffOffersRefreshed);
        }

        /// <summary>
        /// 注销月初商店 Buff 候选刷新事件。
        /// </summary>
        private void UnregisterBudgetShopBuffOffersRefreshedEvent()
        {
            EventKit.Type.UnRegister<BudgetShopBuffOffersRefreshedEvent>(OnBudgetShopBuffOffersRefreshed);
        }

        /// <summary>
        /// 接收流程层刷新出的 Buff 候选数据，并用 Buff 标题刷新 BuffCard 文本和图标。
        /// </summary>
        private void OnBudgetShopBuffOffersRefreshed(BudgetShopBuffOffersRefreshedEvent flowEvent)
        {
            GameFlowRunner runner = GameFlowRunner.Instance;
            if (runner != null && runner.Controller != null && flowEvent.Blackboard != runner.Controller.Blackboard)
            {
                return;
            }

            InjectData(flowEvent.Offers);
            RefreshDollarText();
        }

        /// <summary>
        /// 注册玩家金币变化事件，用于商店打开时同步 Dollar 文本。
        /// </summary>
        private void RegisterPlayerCoinChangedEvent()
        {
            EventKit.Type.UnRegister<CharacterAttributeChangedEvent>(OnPlayerAttributeChanged);
            EventKit.Type.Register<CharacterAttributeChangedEvent>(OnPlayerAttributeChanged);
        }

        /// <summary>
        /// 注销玩家金币变化事件。
        /// </summary>
        private void UnregisterPlayerCoinChangedEvent()
        {
            EventKit.Type.UnRegister<CharacterAttributeChangedEvent>(OnPlayerAttributeChanged);
        }

        /// <summary>
        /// 玩家金币变化来自当前流程黑板时，刷新 Dollar 文本并播放涨跌反馈。
        /// </summary>
        private void OnPlayerAttributeChanged(CharacterAttributeChangedEvent attributeEvent)
        {
            if (attributeEvent.AttributeId != CharacterAttributeIds.Coins)
            {
                return;
            }

            if (!TryGetCurrentBlackboard(out GameFlowBlackboard blackboard) ||
                attributeEvent.AttributeSet != blackboard.PlayerAttributes)
            {
                return;
            }

            SetDollarText(blackboard.Coins, lastDollarValue);
            CacheDollarSnapshot(blackboard.Coins);
        }

        /// <summary>
        /// 用当前流程黑板里的玩家金币刷新 Dollar 文本。
        /// </summary>
        private void RefreshDollarText()
        {
            if (!TryGetCurrentBlackboard(out GameFlowBlackboard blackboard))
            {
                SetDollarTextUnavailable();
                return;
            }

            SetDollarText(blackboard.Coins, lastDollarValue);
            CacheDollarSnapshot(blackboard.Coins);
        }

        /// <summary>
        /// 设置 Dollar 文本，并在已有旧值时按金币涨跌播放颜色和缩放反馈。
        /// </summary>
        private void SetDollarText(int currentValue, int previousValue)
        {
            EnsureDollarText();

            if (!hasDollarSnapshot || dollarText == null || currentValue == previousValue)
            {
                if (dollarText != null && dollarShowingDelta)
                {
                    return;
                }

                SetDollarTextValue(currentValue);
                return;
            }

            PlayDollarChangeAnimation(currentValue, currentValue - previousValue);
        }

        /// <summary>
        /// 流程数据不可用时显示 Dollar 占位内容，并清理旧快照。
        /// </summary>
        private void SetDollarTextUnavailable()
        {
            hasDollarSnapshot = false;
            StopDollarTextAnimation();
            SetDollarTextValue("--");
        }

        /// <summary>
        /// 缓存本次金币值，供下一次刷新判断涨跌。
        /// </summary>
        private void CacheDollarSnapshot(int coinValue)
        {
            lastDollarValue = coinValue;
            hasDollarSnapshot = true;
        }

        /// <summary>
        /// 金币上升时显示加数、膨胀并变绿；金币下降时显示减数、缩小并变红；随后恢复当前金币值。
        /// </summary>
        private void PlayDollarChangeAnimation(int currentValue, int deltaValue)
        {
            EnsureDollarText();
            if (dollarText == null)
            {
                return;
            }

            CacheDollarOriginalState();

            bool isIncrease = deltaValue > 0;
            Transform textTransform = dollarText.transform;
            Vector3 targetScale = dollarOriginalScale * (isIncrease ? dollarIncreaseScale : dollarDecreaseScale);
            Color targetColor = isIncrease ? dollarIncreaseColor : dollarDecreaseColor;

            dollarText.DOKill();
            textTransform.DOKill();
            dollarShowingDelta = true;
            dollarText.text = FormatDeltaText(deltaValue);
            dollarText.color = targetColor;
            textTransform.localScale = dollarOriginalScale;

            Sequence sequence = DOTween.Sequence()
                .SetTarget(dollarText)
                .SetUpdate(true);
            sequence.Join(textTransform.DOScale(targetScale, dollarChangeScaleDuration).SetEase(Ease.OutQuad));
            sequence.Append(textTransform.DOScale(dollarOriginalScale, dollarChangeRestoreDuration).SetEase(Ease.OutQuad));
            sequence.Join(dollarText.DOColor(dollarOriginalColor, dollarChangeRestoreDuration).SetEase(Ease.OutQuad));
            sequence.OnComplete(() =>
            {
                dollarShowingDelta = false;
                SetDollarTextValue(currentValue);
            });
            sequence.OnKill(() => dollarShowingDelta = false);
        }

        /// <summary>
        /// 停止 Dollar 动画并恢复缓存的原始颜色和尺寸。
        /// </summary>
        private void StopDollarTextAnimation()
        {
            if (dollarText == null)
            {
                return;
            }

            Transform textTransform = dollarText.transform;
            dollarText.DOKill();
            textTransform.DOKill();
            dollarShowingDelta = false;

            if (hasDollarOriginalState)
            {
                dollarText.color = dollarOriginalColor;
                textTransform.localScale = dollarOriginalScale;
            }
        }

        /// <summary>
        /// 首次动画前记录 Dollar 文本原始颜色和尺寸。
        /// </summary>
        private void CacheDollarOriginalState()
        {
            if (hasDollarOriginalState || dollarText == null)
            {
                return;
            }

            dollarOriginalColor = dollarText.color;
            dollarOriginalScale = dollarText.transform.localScale;
            hasDollarOriginalState = true;
        }

        /// <summary>
        /// 写入 Dollar 文本数值，未找到文本时直接跳过。
        /// </summary>
        private void SetDollarTextValue(int value)
        {
            SetDollarTextValue(value.ToString());
        }

        /// <summary>
        /// 写入 Dollar 文本内容，未找到文本时直接跳过。
        /// </summary>
        private void SetDollarTextValue(string value)
        {
            EnsureDollarText();

            if (dollarText != null)
            {
                dollarText.text = value;
            }
        }

        /// <summary>
        /// 格式化金币变化期间临时显示的加数或减数。
        /// </summary>
        private static string FormatDeltaText(int deltaValue)
        {
            return deltaValue > 0 ? $"+{deltaValue}" : deltaValue.ToString();
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
        /// 给关闭按钮注册点击事件。
        /// </summary>
        private void RegisterCloseButtonClick()
        {
            if (closeButton == null)
            {
                Debug.LogWarning($"{nameof(WindowShopPanelManager)} needs a close button.", this);
                return;
            }

            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.RemoveListener(OnCloseButtonClicked);
            closeButton.onClick.AddListener(OnCloseButtonClicked);
        }

        /// <summary>
        /// 给刷新按钮注册点击事件。
        /// </summary>
        private void RegisterRefreshButtonClick()
        {
            EnsureRefreshButton();

            if (refreshButton == null)
            {
                Debug.LogWarning($"{nameof(WindowShopPanelManager)} needs a refresh button.", this);
                return;
            }

            refreshButton.onClick.RemoveListener(OnRefreshButtonClicked);
            refreshButton.onClick.AddListener(OnRefreshButtonClicked);
        }

        /// <summary>
        /// 点击关闭按钮后关闭 BuffWindow，流程推进交给关闭回调处理。
        /// </summary>
        private void OnCloseButtonClicked()
        {
            CloseAndNotify();
        }

        /// <summary>
        /// 点击刷新按钮后请求流程层刷新月初商店 Buff 候选数据。
        /// </summary>
        private void OnRefreshButtonClicked()
        {
            RequestBudgetShopBuffOffersRefresh();
        }

        /// <summary>
        /// 请求流程层刷新月初商店 Buff 候选数据，刷新结果由事件回调注入 UI。
        /// </summary>
        private void RequestBudgetShopBuffOffersRefresh()
        {
            GameFlowRunner runner = GameFlowRunner.Instance;
            if (runner == null)
            {
                Debug.LogWarning($"{nameof(WindowShopPanelManager)} cannot find {nameof(GameFlowRunner)} instance.", this);
                return;
            }

            runner.RefreshBudgetShopBuffOffers();
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
            closeButton.onClick.RemoveListener(OnCloseButtonClicked);
        }

        /// <summary>
        /// 移除刷新按钮点击事件。
        /// </summary>
        private void UnregisterRefreshButtonClick()
        {
            if (refreshButton == null)
            {
                return;
            }

            refreshButton.onClick.RemoveListener(OnRefreshButtonClicked);
        }

        /// <summary>
        /// 查找并缓存 BuffWindow 动画组件。
        /// </summary>
        private void EnsureBuffWindowAnimator()
        {
            if (buffWindowAnimator != null)
            {
                return;
            }

            buffWindowAnimator = GetComponent<WindowDropBounceAnimator>();

            if (buffWindowAnimator == null)
            {
                buffWindowAnimator = GetComponentInChildren<WindowDropBounceAnimator>(true);
            }
        }

        /// <summary>
        /// 查找并缓存刷新按钮；优先使用 Inspector 配置，兜底查找名为 Refresh 的子按钮。
        /// </summary>
        private void EnsureRefreshButton()
        {
            if (refreshButton != null)
            {
                return;
            }

            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].name == "Refresh")
                {
                    refreshButton = buttons[i];
                    return;
                }
            }
        }

        /// <summary>
        /// 查找并缓存 BuffWindow 的二级介绍弹窗控制器。
        /// </summary>
        private void EnsureIntroductionWindow()
        {
            if (introductionWindow != null)
            {
                return;
            }

            introductionWindow = GetComponentInChildren<IntroductionWindowController>(true);
        }

        /// <summary>
        /// 查找并缓存 Dollar UI 下的金币文本；优先查当前面板，兜底查根节点，适配 HUD 共享 Dollar。
        /// </summary>
        private void EnsureDollarText()
        {
            if (dollarText != null)
            {
                return;
            }

            dollarText = FindDollarText(transform);

            if (dollarText == null && transform.root != transform)
            {
                dollarText = FindDollarText(transform.root);
            }
        }

        /// <summary>
        /// 在指定层级下查找名为 Dollar 的 UI 节点，并返回其子级第一个 TextMeshProUGUI。
        /// </summary>
        private static TextMeshProUGUI FindDollarText(Transform root)
        {
            if (root == null)
            {
                return null;
            }

            Transform dollar = FindChildByName(root, DollarObjectName);
            return dollar != null ? dollar.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        }

        /// <summary>
        /// 递归查找指定名称的子节点。
        /// </summary>
        private static Transform FindChildByName(Transform root, string targetName)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == targetName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindChildByName(root.GetChild(i), targetName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        /// <summary>
        /// 查找并缓存动态 BuffCard 的父节点。
        /// </summary>
        private void EnsureBuffCardRoot()
        {
            if (buffCardRoot != null)
            {
                return;
            }

            buffCardRoot = transform as RectTransform;
        }

        /// <summary>
        /// 生成单个 BuffCard，并用 Buff 表里的 Cost 填充卡片文本、Title 查找图标。
        /// </summary>
        private void CreateBuffCard(BuffRow buffRow, int index)
        {
            BuffCardController buffCard = Instantiate(buffCardPrefab, buffCardRoot);
            string buffTitle = GetBuffTitle(buffRow);
            Sprite buffIcon = LoadBuffIconByTitle(buffTitle);

            buffCard.name = $"{buffCardPrefab.name}_{index + 1}";
            buffCard.InjectData(buffRow.Cost, buffIcon);
            RegisterBuffCardClick(buffCard, buffRow);
            generatedBuffCards.Add(buffCard);
        }

        /// <summary>
        /// 给动态生成的 BuffCard 按钮绑定点击事件，点击后打开二级介绍弹窗。
        /// </summary>
        private void RegisterBuffCardClick(BuffCardController buffCard, BuffRow buffRow)
        {
            Button buffButton = FindBuffCardButton(buffCard);
            if (buffButton == null)
            {
                Debug.LogWarning($"{nameof(WindowShopPanelManager)} cannot find button on generated BuffCard.", buffCard);
                return;
            }

            buffButton.onClick.AddListener(() => OpenIntroductionWindow(buffRow));
        }

        /// <summary>
        /// 购买当前介绍弹窗指向的 Buff；成功后刷新商店卡片并关闭二级弹窗。
        /// </summary>
        private void TryPurchaseIntroductionBuff(BuffRow buffRow)
        {
            if (buffRow == null)
            {
                Debug.LogWarning($"{nameof(WindowShopPanelManager)} cannot purchase an empty Buff.", this);
                return;
            }

            GameFlowRunner runner = GameFlowRunner.Instance;
            if (runner == null)
            {
                Debug.LogWarning($"{nameof(WindowShopPanelManager)} cannot find {nameof(GameFlowRunner)} instance.", this);
                return;
            }

            if (!runner.TryPurchaseBudgetShopBuff(buffRow.Id, out BuffPurchaseResult result))
            {
                Debug.LogWarning($"{nameof(WindowShopPanelManager)} failed to purchase Buff {buffRow.Id}: {result.Status}. {result.Message}", this);
                return;
            }

            InjectData(runner.CurrentBudgetShopBuffOffers);
            introductionWindow.Close();
        }

        /// <summary>
        /// 查找 BuffCard 上用于打开介绍弹窗的按钮，优先使用卡片根节点按钮。
        /// </summary>
        private static Button FindBuffCardButton(BuffCardController buffCard)
        {
            if (buffCard == null)
            {
                return null;
            }

            Button buffButton = buffCard.GetComponent<Button>();
            if (buffButton != null)
            {
                return buffButton;
            }

            return buffCard.GetComponentInChildren<Button>(true);
        }

        /// <summary>
        /// 获取 Buff 标题文本，空数据兜底为空字符串。
        /// </summary>
        private static string GetBuffTitle(BuffRow buffRow)
        {
            return buffRow?.Title ?? string.Empty;
        }

        /// <summary>
        /// 通过 Buff 标题加载对应图标；标题对应的是多 Sprite 图片里的子 Sprite 名称。
        /// </summary>
        private static Sprite LoadBuffIconByTitle(string buffTitle)
        {
            if (string.IsNullOrWhiteSpace(buffTitle))
            {
                return null;
            }

            EnsureBuffSpriteCache();

            if (buffSpritesByTitle.TryGetValue(buffTitle, out Sprite icon))
            {
                return icon;
            }

            Debug.LogWarning($"{nameof(WindowShopPanelManager)} cannot find Buff icon sprite: {buffTitle}.");
            return null;
        }

        /// <summary>
        /// 从 Resources 下的三张多 Sprite 商品图集中建立标题到 Sprite 的查找缓存。
        /// </summary>
        private static void EnsureBuffSpriteCache()
        {
            if (buffSpriteCacheBuilt)
            {
                return;
            }

            buffSpritesByTitle.Clear();
            for (int i = 0; i < BuffSpriteSheetResourcePaths.Length; i++)
            {
                CacheBuffSpritesFromSheet(BuffSpriteSheetResourcePaths[i]);
            }

            buffSpriteCacheBuilt = true;
        }

        /// <summary>
        /// 读取单张多 Sprite 商品图集，并将其中的子 Sprite 名称登记为 Buff 标题 key。
        /// </summary>
        private static void CacheBuffSpritesFromSheet(string resourcePath)
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
            if (sprites == null || sprites.Length == 0)
            {
                Debug.LogWarning($"{nameof(WindowShopPanelManager)} cannot load Buff sprite sheet: Resources/{resourcePath}.");
                return;
            }

            for (int i = 0; i < sprites.Length; i++)
            {
                Sprite sprite = sprites[i];
                if (sprite == null || string.IsNullOrWhiteSpace(sprite.name))
                {
                    continue;
                }

                if (buffSpritesByTitle.ContainsKey(sprite.name))
                {
                    Debug.LogWarning($"{nameof(WindowShopPanelManager)} found duplicate Buff sprite name: {sprite.name}. First sprite is kept.");
                    continue;
                }

                buffSpritesByTitle.Add(sprite.name, sprite);
            }
        }

        /// <summary>
        /// 清理上一次由当前面板动态生成的 BuffCard。
        /// </summary>
        private void ClearGeneratedBuffCards()
        {
            for (int i = generatedBuffCards.Count - 1; i >= 0; i--)
            {
                BuffCardController buffCard = generatedBuffCards[i];
                if (buffCard == null)
                {
                    continue;
                }

                DestroyGeneratedBuffCard(buffCard);
            }

            generatedBuffCards.Clear();
        }

        /// <summary>
        /// 按当前运行环境销毁动态生成的 BuffCard。
        /// </summary>
        private static void DestroyGeneratedBuffCard(BuffCardController buffCard)
        {
            if (Application.isPlaying)
            {
                Destroy(buffCard.gameObject);
                return;
            }

            DestroyImmediate(buffCard.gameObject);
        }

        /// <summary>
        /// 编辑器添加组件时自动填充当前物体上的 BuffWindow 动画组件和 BuffCard 父节点。
        /// </summary>
        private void Reset()
        {
            EnsureBuffWindowAnimator();
            EnsureBuffCardRoot();
            EnsureRefreshButton();
            EnsureIntroductionWindow();
            EnsureDollarText();
        }
    }
}
