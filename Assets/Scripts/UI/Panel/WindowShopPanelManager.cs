using System;
using System.Collections.Generic;
using Anchor.GameFlow;
using Anchor.GameFlow.Buffs;
using Anchor.Window;
using UnityEngine;
using UnityEngine.UI;
using YokiFrame;

using BuffRow = Anchor.Config.game.buff;

namespace Anchor.UI.Panel
{
    [DisallowMultipleComponent]
    public sealed class WindowShopPanelManager : PanelManagerSingleton<WindowShopPanelManager>
    {
        [Header("Buff Window")]
        [SerializeField, Tooltip("BuffWindow 上的下落回弹动画组件。为空时会从当前物体或子物体查找。")]
        private WindowDropBounceAnimator buffWindowAnimator;

        [SerializeField, Tooltip("动态生成 BuffCard 的父节点。为空时默认使用当前 UI 节点。")]
        private RectTransform buffCardRoot;

        [SerializeField, Tooltip("用于动态生成的 BuffCard 预制体。")]
        private BuffCardController buffCardPrefab;

        [SerializeField, Tooltip("BuffWindow 的二级介绍弹窗控制器。为空时会从当前物体或子物体查找。")]
        private IntroductionWindowController introductionWindow;

        [Header("Button")]
        [SerializeField, Tooltip("点击后关闭当前 BuffWindow 的按钮。")]
        private Button closeButton;

        [SerializeField, Tooltip("点击后刷新当前月初商店 Buff 候选列表的按钮。")]
        private Button refreshButton;

        // 当前由面板动态生成的 BuffCard，用于重新注入数据时清理旧实例。
        private readonly List<BuffCardController> generatedBuffCards = new List<BuffCardController>();

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
            RegisterCloseButtonClick();
            RegisterRefreshButtonClick();
        }

        /// <summary>
        /// Panel 关闭时注销事件和按钮点击事件，避免重复绑定。
        /// </summary>
        private void OnDisable()
        {
            UnregisterBudgetShopBuffOffersRefreshedEvent();
            UnregisterCloseButtonClick();
            UnregisterRefreshButtonClick();
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
        }
    }
}
