using System.Collections.Generic;
using Anchor.Character.Attributes;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YokiFrame;

namespace Anchor.GameFlow
{
    public sealed class GameFlowTestPanel : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private GameFlowRunner mRunner;
        [SerializeField] private Text mStateText;
        [SerializeField] private Text mAttributeText;
        [SerializeField] private Text mLogText;

        [Header("按钮")]
        [SerializeField] private Button mStartButton;
        [SerializeField] private Button mNextStepButton;
        [SerializeField] private Button mNextWeekButton;
        [SerializeField] private Button mProgramButton;
        [SerializeField] private Button mArtButton;
        [SerializeField] private Button mAudioButton;
        [SerializeField] private Button mActionPowerButton;
        [SerializeField] private Button mCoinsButton;
        [SerializeField] private Button mBugButton;

        private readonly Queue<string> mLogs = new();

        private void Awake()
        {
            EnsureRunner();

            if (mStateText == null || mAttributeText == null || mLogText == null)
            {
                CreateRuntimeUI();
            }
        }

        private void OnEnable()
        {
            BindButtons();
            EventKit.Type.Register<GameFlowStateChangedEvent>(OnStateChanged);
            EventKit.Type.Register<WeekGameEventTriggeredEvent>(OnWeekGameEventTriggered);
            EventKit.Type.Register<WeekGameEventResolvedEvent>(OnWeekGameEventResolved);
            EventKit.Type.Register<WeekResolvedEvent>(OnWeekResolved);
            EventKit.Type.Register<MonthSettledEvent>(OnMonthSettled);
            EventKit.Type.Register<GameEndingSelectedEvent>(OnEndingSelected);
            EventKit.Type.Register<CharacterAttributeChangedEvent>(OnAttributeChanged);
            Refresh();
        }

        private void OnDisable()
        {
            UnbindButtons();
            EventKit.Type.UnRegister<GameFlowStateChangedEvent>(OnStateChanged);
            EventKit.Type.UnRegister<WeekGameEventTriggeredEvent>(OnWeekGameEventTriggered);
            EventKit.Type.UnRegister<WeekGameEventResolvedEvent>(OnWeekGameEventResolved);
            EventKit.Type.UnRegister<WeekResolvedEvent>(OnWeekResolved);
            EventKit.Type.UnRegister<MonthSettledEvent>(OnMonthSettled);
            EventKit.Type.UnRegister<GameEndingSelectedEvent>(OnEndingSelected);
            EventKit.Type.UnRegister<CharacterAttributeChangedEvent>(OnAttributeChanged);
        }

        private void Update()
        {
            Refresh();
        }

        public void StartNewGame()
        {
            EnsureRunner();
            mRunner.StartNewGame();
            AddLog("开始新游戏");
        }

        public void AdvanceStep()
        {
            EnsureRunner();
            var controller = mRunner.Controller;
            if (controller == null || controller.MachineState == MachineState.End)
            {
                StartNewGame();
                return;
            }

            switch (controller.CurrentState)
            {
                case GameFlowState.BudgetShop:
                    mRunner.ConfirmBudgetShop();
                    AddLog("确认月初商店，进入本周行动");
                    break;
                case GameFlowState.WeekEvent:
                    mRunner.ChooseWeekGameEventYes();
                    AddLog("周事件默认选择 Y");
                    break;
                case GameFlowState.WeekAction:
                    mRunner.FinishWeekAction();
                    AddLog("结束本周行动，进入周结算");
                    break;
                case GameFlowState.WeekResolve:
                case GameFlowState.MonthSettlement:
                    mRunner.ContinueFlow();
                    AddLog("继续流程");
                    break;
                case GameFlowState.Ending:
                    StartNewGame();
                    break;
                default:
                    mRunner.ContinueFlow();
                    AddLog("尝试继续流程");
                    break;
            }
        }

        public void AdvanceWeek()
        {
            EnsureRunner();
            var controller = mRunner.Controller;
            if (controller == null || controller.MachineState == MachineState.End)
            {
                StartNewGame();
                return;
            }

            if (controller.CurrentState == GameFlowState.BudgetShop)
            {
                mRunner.ConfirmBudgetShop();
                AddLog("确认月初商店");
                return;
            }

            if (controller.CurrentState == GameFlowState.WeekEvent)
            {
                mRunner.ChooseWeekGameEventYes();
                AddLog("周事件默认选择 Y");
                return;
            }

            if (controller.CurrentState == GameFlowState.WeekAction)
            {
                mRunner.FinishWeekAction();
                AddLog("结束本周行动");
                return;
            }

            if (controller.CurrentState == GameFlowState.WeekResolve ||
                controller.CurrentState == GameFlowState.MonthSettlement)
            {
                mRunner.ContinueFlow();
                AddLog("进入下一周或下一月");
                return;
            }

            AddLog("当前状态无法直接进入下一周");
        }

        public void AllocateProgram()
        {
            AllocateActionPoint(GameDevelopmentTrack.Program, "程序");
        }

        public void AllocateArt()
        {
            AllocateActionPoint(GameDevelopmentTrack.Art, "画面");
        }

        public void AllocateAudio()
        {
            AllocateActionPoint(GameDevelopmentTrack.Audio, "音效");
        }

        public void AddWeeklyActionPower()
        {
            AddAttribute(CharacterAttributeIds.WeeklyActionPower, 1);
        }

        public void AddCoins()
        {
            AddAttribute(CharacterAttributeIds.Coins, 100);
        }

        public void AddBug()
        {
            AddAttribute(CharacterAttributeIds.Bug, 5);
        }

        private void AllocateActionPoint(GameDevelopmentTrack track, string displayName)
        {
            EnsureRunner();
            var success = mRunner.TryAllocateActionPoints(track, 1);
            AddLog(success ? $"投入 1 点行动力到{displayName}" : $"投入{displayName}失败：行动力不足或状态不正确");
        }

        private void AddAttribute(int attributeId, int delta)
        {
            EnsureRunner();
            if (mRunner.Controller == null || mRunner.Controller.MachineState == MachineState.End)
            {
                mRunner.StartNewGame();
            }

            var blackboard = mRunner.Controller.Blackboard;
            blackboard.PlayerAttributes.Add(attributeId, delta);
            AddLog($"{GetAttributeName(attributeId)} {delta:+0;-0;0}");
        }

        private void BindButtons()
        {
            if (mStartButton != null) mStartButton.onClick.AddListener(StartNewGame);
            if (mNextStepButton != null) mNextStepButton.onClick.AddListener(AdvanceStep);
            if (mNextWeekButton != null) mNextWeekButton.onClick.AddListener(AdvanceWeek);
            if (mProgramButton != null) mProgramButton.onClick.AddListener(AllocateProgram);
            if (mArtButton != null) mArtButton.onClick.AddListener(AllocateArt);
            if (mAudioButton != null) mAudioButton.onClick.AddListener(AllocateAudio);
            if (mActionPowerButton != null) mActionPowerButton.onClick.AddListener(AddWeeklyActionPower);
            if (mCoinsButton != null) mCoinsButton.onClick.AddListener(AddCoins);
            if (mBugButton != null) mBugButton.onClick.AddListener(AddBug);
        }

        private void UnbindButtons()
        {
            if (mStartButton != null) mStartButton.onClick.RemoveListener(StartNewGame);
            if (mNextStepButton != null) mNextStepButton.onClick.RemoveListener(AdvanceStep);
            if (mNextWeekButton != null) mNextWeekButton.onClick.RemoveListener(AdvanceWeek);
            if (mProgramButton != null) mProgramButton.onClick.RemoveListener(AllocateProgram);
            if (mArtButton != null) mArtButton.onClick.RemoveListener(AllocateArt);
            if (mAudioButton != null) mAudioButton.onClick.RemoveListener(AllocateAudio);
            if (mActionPowerButton != null) mActionPowerButton.onClick.RemoveListener(AddWeeklyActionPower);
            if (mCoinsButton != null) mCoinsButton.onClick.RemoveListener(AddCoins);
            if (mBugButton != null) mBugButton.onClick.RemoveListener(AddBug);
        }

        /// <summary>
        /// 获取测试面板使用的流程入口，优先保留 Inspector 显式引用。
        /// </summary>
        private void EnsureRunner()
        {
            if (mRunner == null)
            {
                // 使用 GameFlowRunner 单例，避免测试面板运行时扫描场景。
                mRunner = GameFlowRunner.Instance;
            }
        }

        private void CreateRuntimeUI()
        {
            EnsureEventSystem();

            var canvas = CreateCanvas();
            CreateBackground(canvas.transform);

            var title = CreateText(canvas.transform, "标题", "游戏流程测试", 36, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetTopLeft(title.rectTransform, 32f, -24f, 520f, 52f);

            var statePanel = CreatePanel(canvas.transform, "状态面板", 32f, -92f, 420f, 252f);
            CreatePanelTitle(statePanel.transform, "流程状态");
            mStateText = CreateText(statePanel.transform, "状态文本", "状态：未开始", 23, FontStyle.Normal, TextAnchor.UpperLeft);
            SetStretch(mStateText.rectTransform, 20f, 58f, 20f, 20f);

            var attrPanel = CreatePanel(canvas.transform, "属性面板", 32f, -360f, 420f, 384f);
            CreatePanelTitle(attrPanel.transform, "玩家属性");
            mAttributeText = CreateText(attrPanel.transform, "属性文本", "属性：无", 23, FontStyle.Normal, TextAnchor.UpperLeft);
            SetStretch(mAttributeText.rectTransform, 20f, 58f, 20f, 20f);

            var flowPanel = CreatePanel(canvas.transform, "流程按钮面板", 476f, -92f, 540f, 252f);
            CreatePanelTitle(flowPanel.transform, "流程控制");
            mStartButton = CreateButton(flowPanel.transform, "开始新游戏按钮", "开始新游戏", 20f, 64f, 150f, 52f);
            mNextStepButton = CreateButton(flowPanel.transform, "下一步按钮", "下一步", 190f, 64f, 150f, 52f);
            mNextWeekButton = CreateButton(flowPanel.transform, "下一周按钮", "下一周", 360f, 64f, 150f, 52f);
            mActionPowerButton = CreateButton(flowPanel.transform, "行动力加一按钮", "周行动力 +1", 20f, 140f, 150f, 52f);
            mCoinsButton = CreateButton(flowPanel.transform, "金币加一百按钮", "金币 +100", 190f, 140f, 150f, 52f);
            mBugButton = CreateButton(flowPanel.transform, "Bug加五按钮", "Bug +5", 360f, 140f, 150f, 52f);

            var actionPanel = CreatePanel(canvas.transform, "行动点面板", 476f, -360f, 540f, 248f);
            CreatePanelTitle(actionPanel.transform, "消费行动点");
            mProgramButton = CreateButton(actionPanel.transform, "投入程序按钮", "程序 -1", 20f, 64f, 150f, 52f);
            mArtButton = CreateButton(actionPanel.transform, "投入美术按钮", "美术 -1", 190f, 64f, 150f, 52f);
            mAudioButton = CreateButton(actionPanel.transform, "投入音效按钮", "音效 -1", 360f, 64f, 150f, 52f);

            var logPanel = CreatePanel(canvas.transform, "日志面板", 1040f, -92f, 520f, 516f);
            CreatePanelTitle(logPanel.transform, "测试日志");
            mLogText = CreateText(logPanel.transform, "日志文本", "等待操作", 20, FontStyle.Normal, TextAnchor.UpperLeft);
            SetStretch(mLogText.rectTransform, 20f, 58f, 20f, 20f);
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            new GameObject("事件系统", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static Canvas CreateCanvas()
        {
            var canvasObject = new GameObject("游戏流程测试界面", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static void CreateBackground(Transform parent)
        {
            var background = new GameObject("背景", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = background.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = background.GetComponent<Image>();
            image.color = new Color(0.08f, 0.09f, 0.11f, 1f);
            image.raycastTarget = false;
        }

        private static GameObject CreatePanel(Transform parent, string name, float x, float y, float width, float height)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = panel.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetTopLeft(rect, x, y, width, height);

            var image = panel.GetComponent<Image>();
            image.color = new Color(0.13f, 0.15f, 0.18f, 0.95f);
            return panel;
        }

        private static void CreatePanelTitle(Transform parent, string value)
        {
            var title = CreateText(parent, "标题", value, 24, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetTopLeft(title.rectTransform, 18f, -12f, 320f, 36f);
        }

        private static Button CreateButton(Transform parent, string name, string label, float x, float y, float width, float height)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetTopLeft(rect, x, -y, width, height);

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.22f, 0.38f, 0.64f, 1f);

            var button = buttonObject.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = new Color(0.22f, 0.38f, 0.64f, 1f);
            colors.highlightedColor = new Color(0.30f, 0.48f, 0.76f, 1f);
            colors.pressedColor = new Color(0.16f, 0.27f, 0.46f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            var text = CreateText(buttonObject.transform, "文本", label, 22, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            text.raycastTarget = false;
            return button;
        }

        private static Text CreateText(Transform parent, string name, string value, int size, FontStyle style, TextAnchor alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            var text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = new Color(0.94f, 0.96f, 0.98f, 1f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static void SetTopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void SetStretch(RectTransform rect, float left, float top, float right, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private void Refresh()
        {
            EnsureRunner();
            var controller = mRunner != null ? mRunner.Controller : null;
            var blackboard = controller != null ? controller.Blackboard : null;

            if (mStateText != null)
            {
                if (controller == null || blackboard == null || controller.MachineState == MachineState.End)
                {
                    mStateText.text = "状态：未开始";
                }
                else
                {
                    mStateText.text =
                        $"状态：{GetStateName(controller.CurrentState)}\n" +
                        $"月份：第 {blackboard.MonthIndex} 月\n" +
                        $"周数：第 {blackboard.WeekIndex} 周\n" +
                        $"周事件：{GetCurrentWeekEventTitle(controller)}\n" +
                        $"结算：{(blackboard.CurrentMonth != null ? blackboard.CurrentMonth.DisplayName : "无")}";
                }
            }

            if (mAttributeText != null)
            {
                if (blackboard == null)
                {
                    mAttributeText.text = "属性：无";
                }
                else
                {
                    mAttributeText.text =
                        $"周行动力：{blackboard.CurrentWeekActionPower:0}\n" +
                        $"Bug 值：{blackboard.BugScore:0}\n" +
                        $"画面：{blackboard.VisualScore:0}\n" +
                        $"氛围：{blackboard.AtmosphereScore:0}\n" +
                        $"金币：{blackboard.Coins:0}\n" +
                        $"愿望单数量：{blackboard.WishlistCount:0}\n" +
                        $"质量分：{blackboard.QualityScore:0}";
                }
            }
        }

        private void AddLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            mLogs.Enqueue(message);
            while (mLogs.Count > 12)
            {
                mLogs.Dequeue();
            }

            if (mLogText == null)
            {
                return;
            }

            mLogText.text = string.Join("\n", mLogs);
        }

        private void OnStateChanged(GameFlowStateChangedEvent flowEvent)
        {
            if (flowEvent.Blackboard == mRunner?.Controller?.Blackboard)
            {
                AddLog($"状态切换：{GetStateName(flowEvent.State)}");
            }
        }

        private void OnWeekResolved(WeekResolvedEvent flowEvent)
        {
            if (flowEvent.Blackboard == mRunner?.Controller?.Blackboard)
            {
                AddLog(flowEvent.Result.Summary);
            }
        }

        private void OnWeekGameEventTriggered(WeekGameEventTriggeredEvent flowEvent)
        {
            if (flowEvent.Blackboard == mRunner?.Controller?.Blackboard && flowEvent.Event != null)
            {
                AddLog($"周事件触发：{flowEvent.Event.Title}");
            }
        }

        private void OnWeekGameEventResolved(WeekGameEventResolvedEvent flowEvent)
        {
            if (flowEvent.Blackboard == mRunner?.Controller?.Blackboard && flowEvent.Result.Event != null)
            {
                AddLog($"周事件选择：{flowEvent.Result.Event.Title} => {(flowEvent.Result.ChooseYes ? "Y" : "N")}");
            }
        }

        private void OnMonthSettled(MonthSettledEvent flowEvent)
        {
            if (flowEvent.Blackboard == mRunner?.Controller?.Blackboard)
            {
                AddLog(flowEvent.Result.Summary);
            }
        }

        private void OnEndingSelected(GameEndingSelectedEvent flowEvent)
        {
            if (flowEvent.Blackboard == mRunner?.Controller?.Blackboard)
            {
                AddLog($"结局：{flowEvent.Result.DisplayName}");
            }
        }

        private void OnAttributeChanged(CharacterAttributeChangedEvent flowEvent)
        {
            if (flowEvent.AttributeSet == mRunner?.Controller?.Blackboard.PlayerAttributes)
            {
                AddLog($"{GetAttributeName(flowEvent.AttributeId)}：{flowEvent.PreviousValue} -> {flowEvent.CurrentValue}");
            }
        }

        private static string GetStateName(GameFlowState state)
        {
            return state switch
            {
                GameFlowState.NewGame => "新游戏",
                GameFlowState.MonthStart => "月开始",
                GameFlowState.BudgetShop => "月初商店",
                GameFlowState.WeekStart => "周开始",
                GameFlowState.WeekEvent => "周事件",
                GameFlowState.WeekAction => "周行动",
                GameFlowState.WeekResolve => "周结算",
                GameFlowState.MonthSettlement => "月结算",
                GameFlowState.Ending => "结局",
                _ => state.ToString()
            };
        }

        private string GetAttributeName(int attributeId)
        {
            return mRunner?.Controller?.Blackboard.AttributeCatalog.GetDisplayName(attributeId) ?? attributeId.ToString();
        }

        private static string GetCurrentWeekEventTitle(GameFlowController controller)
        {
            return controller?.CurrentWeekGameEvent != null ? controller.CurrentWeekGameEvent.Title : "无";
        }
    }
}
