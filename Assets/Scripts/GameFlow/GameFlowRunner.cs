using Anchor.Character.Attributes;
using Anchor.Config;
using UnityEngine;
using YokiFrame;
using YokiFrame.Unity;

namespace Anchor.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class GameFlowRunner : MonoBehaviour
    {
        [Header("启动")]
        [SerializeField] private bool mStartOnAwake = true;
        [SerializeField] private bool mAutoAdvanceInteractiveStates;
        [SerializeField] private bool mLogEvents = true;

        [Header("流程")]
        [SerializeField, Min(1)] private int mTotalMonths = 3;
        [SerializeField, Min(1)] private int mWeeksPerMonth = 4;

        private GameFlowController mController;

        public GameFlowController Controller => mController;

        private void Awake()
        {
            CreateController();

            if (mStartOnAwake)
            {
                mController.StartNewGame();
            }
        }

        private void OnEnable()
        {
            EventKit.Type.Register<GameFlowStateChangedEvent>(OnStateChanged);
            EventKit.Type.Register<WeekResolvedEvent>(OnWeekResolved);
            EventKit.Type.Register<MonthSettledEvent>(OnMonthSettled);
            EventKit.Type.Register<GameEndingSelectedEvent>(OnEndingSelected);
            EventKit.Type.Register<CharacterAttributeChangedEvent>(OnPlayerAttributeChanged);
        }

        private void OnDisable()
        {
            EventKit.Type.UnRegister<GameFlowStateChangedEvent>(OnStateChanged);
            EventKit.Type.UnRegister<WeekResolvedEvent>(OnWeekResolved);
            EventKit.Type.UnRegister<MonthSettledEvent>(OnMonthSettled);
            EventKit.Type.UnRegister<GameEndingSelectedEvent>(OnEndingSelected);
            EventKit.Type.UnRegister<CharacterAttributeChangedEvent>(OnPlayerAttributeChanged);
        }

        private void Update()
        {
            mController?.Update();
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

        public bool TryAllocateDesign(int points)
        {
            return TryAllocateActionPoints(GameDevelopmentTrack.Design, points);
        }

        public bool TryAllocateTesting(int points)
        {
            return TryAllocateActionPoints(GameDevelopmentTrack.Testing, points);
        }

        public bool TryAllocateMarketing(int points)
        {
            return TryAllocateActionPoints(GameDevelopmentTrack.Marketing, points);
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
            mController = new GameFlowController(attributeCatalog, settings, mAutoAdvanceInteractiveStates);
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
