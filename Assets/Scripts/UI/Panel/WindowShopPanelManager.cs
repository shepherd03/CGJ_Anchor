using System.Collections.Generic;
using Anchor.GameFlow;
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

        [Header("Button")]
        [SerializeField, Tooltip("点击后关闭当前 BuffWindow 的按钮。")]
        private Button closeButton;

        [SerializeField, Tooltip("点击后刷新当前月初商店 Buff 候选列表的按钮。")]
        private Button refreshButton;

        // 当前由面板动态生成的 BuffCard，用于重新注入数据时清理旧实例。
        private readonly List<BuffCardController> generatedBuffCards = new List<BuffCardController>();

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
        /// 打开 BuffWindow，实际动画入口交给 WindowDropBounceAnimator.Open。
        /// </summary>
        public void Open()
        {
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
        /// 关闭 BuffWindow，实际动画入口交给 WindowDropBounceAnimator.Close。
        /// </summary>
        public void Close()
        {
            EnsureBuffWindowAnimator();

            if (buffWindowAnimator == null)
            {
                gameObject.SetActive(false);
                return;
            }

            buffWindowAnimator.Close();
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
        /// 接收流程层刷新出的 Buff 候选数据，并用 Buff 标题刷新 BuffCard 文本。
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
        /// 点击关闭按钮后交给流程 UI 编排器推进下一步。
        /// </summary>
        private void OnCloseButtonClicked()
        {
            GameFlowPanelCoordinator.GetOrCreate().NextStep();
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
        /// 生成单个 BuffCard，并用 Buff 表里的 Title 填充卡片文本。
        /// </summary>
        private void CreateBuffCard(BuffRow buffRow, int index)
        {
            BuffCardController buffCard = Instantiate(buffCardPrefab, buffCardRoot);
            buffCard.name = $"{buffCardPrefab.name}_{index + 1}";
            buffCard.InjectData(GetBuffTitle(buffRow));
            generatedBuffCards.Add(buffCard);
        }

        /// <summary>
        /// 获取 Buff 标题文本，空数据兜底为空字符串。
        /// </summary>
        private static string GetBuffTitle(BuffRow buffRow)
        {
            return buffRow?.Title ?? string.Empty;
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
        }
    }
}
