using System.Collections.Generic;
using Anchor.Character.Attributes;
using Anchor.Config;
using Anchor.GameFlow.Buffs;
using UnityEngine;
using YokiFrame;
using YokiFrame.Unity;

using BuffRow = Anchor.Config.game.buff;

namespace Anchor.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class GameFlowRunner : MonoBehaviour
    {
        /// <summary>
        /// 当前场景唯一的游戏流程入口，供 UI 直接调用流程控制方法。
        /// </summary>
        public static GameFlowRunner Instance { get; private set; }

        [Header("启动")]
        [SerializeField] private bool mStartOnAwake = true;
        [SerializeField] private bool mAutoAdvanceInteractiveStates;
        [SerializeField] private bool mLogEvents = true;

        [Header("流程")]
        [SerializeField, Min(1)] private int mTotalMonths = 3;
        [SerializeField, Min(1)] private int mWeeksPerMonth = 4;

        private GameFlowController mController;

        public GameFlowController Controller => mController;
        public IReadOnlyList<BuffRow> CurrentBudgetShopBuffOffers
        {
            get
            {
                EnsureController();
                return mController.CurrentBudgetShopBuffOffers;
            }
        }

        /// <summary>
        /// 初始化流程单例和流程控制器。
        /// </summary>
        private void Awake()
        {
            if (!RegisterInstance())
            {
                enabled = false;
                return;
            }

            CreateController();

            if (mStartOnAwake)
            {
                mController.StartNewGame();
            }
        }

        /// <summary>
        /// Runner 销毁时释放单例引用，避免下一个场景拿到旧实例。
        /// </summary>
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnEnable()
        {
            EventKit.Type.Register<GameFlowStateChangedEvent>(OnStateChanged);
            EventKit.Type.Register<WeekResolvedEvent>(OnWeekResolved);
            EventKit.Type.Register<MonthSettledEvent>(OnMonthSettled);
            EventKit.Type.Register<GameEndingSelectedEvent>(OnEndingSelected);
            EventKit.Type.Register<BudgetShopBuffOffersRefreshedEvent>(OnBudgetShopBuffOffersRefreshed);
            EventKit.Type.Register<BuffPurchasedEvent>(OnBuffPurchased);
            EventKit.Type.Register<CharacterAttributeChangedEvent>(OnPlayerAttributeChanged);
        }

        private void OnDisable()
        {
            EventKit.Type.UnRegister<GameFlowStateChangedEvent>(OnStateChanged);
            EventKit.Type.UnRegister<WeekResolvedEvent>(OnWeekResolved);
            EventKit.Type.UnRegister<MonthSettledEvent>(OnMonthSettled);
            EventKit.Type.UnRegister<GameEndingSelectedEvent>(OnEndingSelected);
            EventKit.Type.UnRegister<BudgetShopBuffOffersRefreshedEvent>(OnBudgetShopBuffOffersRefreshed);
            EventKit.Type.UnRegister<BuffPurchasedEvent>(OnBuffPurchased);
            EventKit.Type.UnRegister<CharacterAttributeChangedEvent>(OnPlayerAttributeChanged);
        }

        private void Update()
        {
            mController?.Update();
        }

        /// <summary>
        /// 注册当前场景唯一的流程入口，阻止多个 Runner 同时驱动流程。
        /// </summary>
        private bool RegisterInstance()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError($"{nameof(GameFlowRunner)} 已存在有效实例，重复的 Runner 会被禁用：{name}", this);
                return false;
            }

            Instance = this;
            return true;
        }

        [ContextMenu("开始新游戏")]
        public void StartNewGame()
        {
            EnsureController();
            mController.StartNewGame();
        }

        [ContextMenu("确认月初商店")]
        public void ConfirmBudgetShop()
        {
            mController?.ConfirmBudgetShop();
        }

        public IReadOnlyList<BuffRow> RefreshBudgetShopBuffOffers(
            int count = GameFlowController.DefaultBudgetShopBuffOfferCount)
        {
            EnsureController();
            return mController.RefreshBudgetShopBuffOffers(count);
        }

        public bool CanPurchaseBudgetShopBuff(int buffId)
        {
            EnsureController();
            return mController.CanPurchaseBudgetShopBuff(buffId);
        }

        public bool TryPurchaseBudgetShopBuff(int buffId)
        {
            EnsureController();
            return mController.TryPurchaseBudgetShopBuff(buffId, out _);
        }

        public bool TryPurchaseBudgetShopBuff(int buffId, out BuffPurchaseResult result)
        {
            EnsureController();
            return mController.TryPurchaseBudgetShopBuff(buffId, out result);
        }

        [ContextMenu("结束本周行动")]
        public void FinishWeekAction()
        {
            mController?.FinishWeekAction();
        }

        [ContextMenu("继续流程")]
        public void ContinueFlow()
        {
            mController?.Continue();
        }

        public bool TryAllocateActionPoints(GameDevelopmentTrack track, int points)
        {
            EnsureController();
            return mController.TryAllocateActionPoints(track, points);
        }

        public bool TryAllocateProgram(int points)
        {
            return TryAllocateActionPoints(GameDevelopmentTrack.Program, points);
        }

        public bool TryAllocateArt(int points)
        {
            return TryAllocateActionPoints(GameDevelopmentTrack.Art, points);
        }

        public bool TryAllocateAudio(int points)
        {
            return TryAllocateActionPoints(GameDevelopmentTrack.Audio, points);
        }

        private void EnsureController()
        {
            if (mController == null)
            {
                CreateController();
            }
        }

        private void CreateController()
        {
            EnsureResKitProvider();

            var settings = new GameFlowSettings
            {
                TotalMonths = Mathf.Max(1, mTotalMonths),
                WeeksPerMonth = Mathf.Max(1, mWeeksPerMonth)
            };

            var attributeCatalog = new CharacterAttributeCatalog(GameConfigs.Tables.TbplayerAttribute.DataList);
            mController = new GameFlowController(
                attributeCatalog,
                settings,
                mAutoAdvanceInteractiveStates,
                GameConfigs.Tables.Tbbuff.DataList);
        }

        private static void EnsureResKitProvider()
        {
            if (ResKit.GetProvider() == null)
            {
                ResKit.SetProvider(new UnityResourceProvider());
            }
        }

        private void OnStateChanged(GameFlowStateChangedEvent flowEvent)
        {
            if (!mLogEvents || flowEvent.Blackboard != mController?.Blackboard)
            {
                return;
            }

            Debug.Log(
                $"[游戏流程] 状态：{GetStateName(flowEvent.State)}，第 {flowEvent.Blackboard.MonthIndex} 月，第 {flowEvent.Blackboard.WeekIndex} 周");
        }

        private void OnWeekResolved(WeekResolvedEvent flowEvent)
        {
            if (!mLogEvents || flowEvent.Blackboard != mController?.Blackboard)
            {
                return;
            }

            Debug.Log("[游戏流程] " + flowEvent.Result.Summary);
        }

        private void OnMonthSettled(MonthSettledEvent flowEvent)
        {
            if (!mLogEvents || flowEvent.Blackboard != mController?.Blackboard)
            {
                return;
            }

            Debug.Log("[游戏流程] " + flowEvent.Result.Summary);
        }

        private void OnEndingSelected(GameEndingSelectedEvent flowEvent)
        {
            if (!mLogEvents || flowEvent.Blackboard != mController?.Blackboard)
            {
                return;
            }

            Debug.Log($"[游戏流程] 结局：{flowEvent.Result.DisplayName}（{flowEvent.Result.EndingId}）");
        }

        private void OnBudgetShopBuffOffersRefreshed(BudgetShopBuffOffersRefreshedEvent flowEvent)
        {
            if (!mLogEvents || flowEvent.Blackboard != mController?.Blackboard)
            {
                return;
            }

            Debug.Log($"[游戏流程] 月初商店候选 Buff：{flowEvent.Offers.Count}");
        }

        private void OnBuffPurchased(BuffPurchasedEvent flowEvent)
        {
            if (!mLogEvents || flowEvent.Blackboard != mController?.Blackboard || !flowEvent.Result.Succeeded)
            {
                return;
            }

            var buff = flowEvent.Result.Buff;
            Debug.Log($"[游戏流程] 购买 Buff：{buff.Title}（{buff.Id}），花费 {flowEvent.Result.Cost}");
        }

        private void OnPlayerAttributeChanged(CharacterAttributeChangedEvent flowEvent)
        {
            if (!mLogEvents || flowEvent.AttributeSet != mController?.Blackboard.PlayerAttributes)
            {
                return;
            }

            Debug.Log(
                $"[游戏流程] 属性变化：{GetAttributeName(flowEvent.AttributeId)} {flowEvent.PreviousValue} -> {flowEvent.CurrentValue}（{flowEvent.Delta:+0;-0;0}）");
        }

        private static string GetStateName(GameFlowState state)
        {
            return state switch
            {
                GameFlowState.NewGame => "新游戏",
                GameFlowState.MonthStart => "月开始",
                GameFlowState.BudgetShop => "月初商店",
                GameFlowState.WeekStart => "周开始",
                GameFlowState.WeekAction => "周行动",
                GameFlowState.WeekResolve => "周结算",
                GameFlowState.MonthSettlement => "月结算",
                GameFlowState.Ending => "结局",
                _ => state.ToString()
            };
        }

        private string GetAttributeName(int attributeId)
        {
            return mController?.Blackboard.AttributeCatalog.GetDisplayName(attributeId) ?? attributeId.ToString();
        }
    }
}
