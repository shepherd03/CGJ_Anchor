using System.Collections;
using Anchor.Audio;
using Anchor.GameFlow;
using Anchor.UI.Transitions;
using UnityEngine;
using UnityEngine.UI;

namespace Anchor.UI.Panel
{
    [DisallowMultipleComponent]
    public sealed class GameFlowPanelCoordinator : MonoBehaviour
    {
        /// <summary>
        /// 当前场景唯一的流程 UI 编排入口，负责决定各流程面板的显示顺序。
        /// </summary>
        public static GameFlowPanelCoordinator Instance { get; private set; }

        [Header("Bullet Screen")]
        [SerializeField, Tooltip("月结算弹幕屏幕控制器。为空时会从场景中查找。")]
        private BulletScreenController bulletScreenController;

        [SerializeField, Min(0f), Tooltip("月结算弹幕播放时长。")]
        private float bulletScreenDuration = 4f;

        [Header("Month Settlement Poster")]
        [SerializeField, Min(0f), Tooltip("月结算弹幕结束后，阶段宣传图淡入完成后的停留时长。")]
        private float monthSettlementPosterDuration = 4f;

        [SerializeField, Min(0f), Tooltip("阶段宣传图淡入和淡出各自持续的时长。")]
        private float monthSettlementPosterFadeDuration = 0.5f;

        // 当前正在执行的流程 UI 协程。
        private Coroutine flowRoutine;
        // 防止按钮连点造成重复推进。
        private bool isAdvancingFlow;
        // 运行时生成的月结算阶段宣传图覆盖层。
        private GameObject monthSettlementPosterInstance;

        /// <summary>
        /// 获取当前场景的流程 UI 编排器；场景没放时运行时创建一个兜底对象。
        /// </summary>
        public static GameFlowPanelCoordinator GetOrCreate()
        {
            if (Instance != null)
            {
                return Instance;
            }

            GameFlowPanelCoordinator coordinator = FindObjectOfType<GameFlowPanelCoordinator>(true);
            if (coordinator != null)
            {
                return coordinator;
            }

            var coordinatorObject = new GameObject(nameof(GameFlowPanelCoordinator));
            return coordinatorObject.AddComponent<GameFlowPanelCoordinator>();
        }

        /// <summary>
        /// 初始化当前场景唯一的流程 UI 编排器。
        /// </summary>
        private void Awake()
        {
            if (!RegisterInstance())
            {
                enabled = false;
            }
        }

        /// <summary>
        /// 销毁时释放单例，并停止未完成的流程协程。
        /// </summary>
        private void OnDestroy()
        {
            StopFlowRoutine();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// 开始一局新游戏，并按当前流程状态打开第一个页面。
        /// </summary>
        public void StartGame()
        {
            if (isAdvancingFlow)
            {
                return;
            }

            StartFlowRoutine(StartGameWithTransition());
        }

        /// <summary>
        /// 播放开始过渡，在黑场中执行原本的开局 UI 切换。
        /// </summary>
        private IEnumerator StartGameWithTransition()
        {
            ScreenCircleTransition transition = ScreenCircleTransition.GetOrCreate();

            if (transition == null)
            {
                StartGameImmediately();
                yield break;
            }

            yield return transition.Play(StartGameImmediately);
        }

        /// <summary>
        /// 立即开始一局新游戏，并按当前流程状态打开第一个页面。
        /// </summary>
        private void StartGameImmediately()
        {
            if (!TryGetRunner(out GameFlowRunner runner))
            {
                return;
            }

            CloseBuffWindow();
            CloseEventPanel();
            CloseMainPanel();
            CloseWeekPanel();
            CloseGameEndPanel();
            runner.StartNewGame();
            MenuGameBgmPlayer.PlayGameMusic();
            CloseBeginPanel();
            RouteCurrentState(runner);
        }

        /// <summary>
        /// 结束后回到 BeginPanel，并重置、停住当前游戏流程，等待开始按钮重新开局。
        /// </summary>
        public void ReturnToBeginPanel()
        {
            StopFlowRoutine();
            CloseBuffWindow();
            CloseEventPanel();
            CloseMainPanel();
            CloseWeekPanel();
            CloseGameEndPanel();
            ResetCurrentGameFlowForBeginPanel();
            OpenBeginPanel();
            MenuGameBgmPlayer.PlayMenuMusic();
        }

        /// <summary>
        /// 兼容外部手动推进入口；正式 UI 流程由各面板关闭回调推进。
        /// </summary>
        public void NextStep()
        {
            if (isAdvancingFlow)
            {
                return;
            }

            if (!TryGetRunner(out GameFlowRunner runner))
            {
                return;
            }

            if (runner.Controller == null)
            {
                StartGame();
                return;
            }

            switch (runner.Controller.CurrentState)
            {
                case GameFlowState.BudgetShop:
                    OnBuffWindowClosed();
                    break;
                case GameFlowState.WeekEvent:
                    OpenEventPanel();
                    break;
                case GameFlowState.WeekAction:
                    OnMainPanelClosed();
                    break;
                case GameFlowState.WeekResolve:
                case GameFlowState.MonthSettlement:
                    StartFlowRoutine(RouteFlowAfterCurrentState(runner));
                    break;
                case GameFlowState.Ending:
                    OpenGameEndPanel();
                    break;
                default:
                    RouteCurrentState(runner);
                    break;
            }
        }

        /// <summary>
        /// 注册当前场景唯一的 UI 流程编排器。
        /// </summary>
        private bool RegisterInstance()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError($"{nameof(GameFlowPanelCoordinator)} 已存在有效实例，重复的编排器会被禁用：{name}", this);
                return false;
            }

            Instance = this;
            return true;
        }

        /// <summary>
        /// 按当前流程状态打开对应 UI；需要等待的状态交给协程处理。
        /// </summary>
        private void RouteCurrentState(GameFlowRunner runner)
        {
            if (runner == null || runner.Controller == null)
            {
                return;
            }

            switch (runner.Controller.CurrentState)
            {
                case GameFlowState.BudgetShop:
                    OpenBuffWindow();
                    break;
                case GameFlowState.WeekEvent:
                    OpenEventPanel();
                    break;
                case GameFlowState.WeekAction:
                    OpenMainPanel();
                    break;
                case GameFlowState.WeekResolve:
                case GameFlowState.MonthSettlement:
                    StartFlowRoutine(RouteFlowAfterCurrentState(runner));
                    break;
                case GameFlowState.Ending:
                    OpenGameEndPanel();
                    break;
            }
        }

        /// <summary>
        /// 处理需要异步等待的流程状态：周结算交给关闭回调，月结算等待弹幕播放。
        /// </summary>
        private IEnumerator RouteFlowAfterCurrentState(GameFlowRunner runner)
        {
            while (runner != null && runner.Controller != null)
            {
                switch (runner.Controller.CurrentState)
                {
                    case GameFlowState.WeekEvent:
                        OpenEventPanel();
                        yield break;
                    case GameFlowState.WeekResolve:
                        OpenWeekPanel();
                        yield break;
                    case GameFlowState.WeekAction:
                        OpenMainPanel();
                        yield break;
                    case GameFlowState.MonthSettlement:
                        yield return PlayBulletScreenForDuration();
                        yield return PlayMonthSettlementPosterForDuration(runner);
                        runner.ContinueFlow();
                        break;
                    case GameFlowState.BudgetShop:
                        OpenBuffWindow();
                        yield break;
                    case GameFlowState.Ending:
                        OpenGameEndPanel();
                        yield break;
                    default:
                        yield break;
                }
            }
        }

        /// <summary>
        /// 统一启动流程协程，防止多个流程推进协程同时运行。
        /// </summary>
        private void StartFlowRoutine(IEnumerator routine)
        {
            StopFlowRoutine();
            isAdvancingFlow = true;
            flowRoutine = StartCoroutine(RunFlowRoutine(routine));
        }

        /// <summary>
        /// 兼容旧调用：周事件面板关闭后继续路由当前流程状态。
        /// </summary>
        public void ResumeAfterWeekGameEventChoice()
        {
            OnEventPanelClosed();
        }

        /// <summary>
        /// BuffWindow 关闭后确认月初商店，并继续路由到周流程。
        /// </summary>
        private void OnBuffWindowClosed()
        {
            if (!TryGetRunner(out GameFlowRunner runner) || runner.Controller == null)
            {
                return;
            }

            if (runner.Controller.CurrentState == GameFlowState.BudgetShop)
            {
                runner.ConfirmBudgetShop();
            }

            RouteCurrentState(runner);
        }

        /// <summary>
        /// EventPanel 关闭后继续路由；有下一个周事件就继续显示，否则进入 MainPanel。
        /// </summary>
        private void OnEventPanelClosed()
        {
            if (!TryGetRunner(out GameFlowRunner runner) || runner.Controller == null)
            {
                return;
            }

            RouteCurrentState(runner);
        }

        /// <summary>
        /// MainPanel 关闭后结束本周行动，并进入周结算流程。
        /// </summary>
        private void OnMainPanelClosed()
        {
            if (!TryGetRunner(out GameFlowRunner runner) || runner.Controller == null)
            {
                return;
            }

            if (runner.Controller.CurrentState == GameFlowState.WeekAction)
            {
                runner.FinishWeekAction();
            }

            StartFlowRoutine(RouteFlowAfterCurrentState(runner));
        }

        /// <summary>
        /// WeekPanel 关闭后继续流程，可能进入下周、月结算或结局。
        /// </summary>
        private void OnWeekPanelClosed()
        {
            if (!TryGetRunner(out GameFlowRunner runner) || runner.Controller == null)
            {
                return;
            }

            if (runner.Controller.CurrentState == GameFlowState.WeekResolve)
            {
                runner.ContinueFlow();
            }

            StartFlowRoutine(RouteFlowAfterCurrentState(runner));
        }

        /// <summary>
        /// 流程协程收尾时清理运行状态。
        /// </summary>
        private IEnumerator RunFlowRoutine(IEnumerator routine)
        {
            yield return routine;

            flowRoutine = null;
            isAdvancingFlow = false;
        }

        /// <summary>
        /// 停止当前流程协程。
        /// </summary>
        private void StopFlowRoutine()
        {
            if (flowRoutine != null)
            {
                StopCoroutine(flowRoutine);
                flowRoutine = null;
            }

            StopBulletScreen();
            HideMonthSettlementPoster();
            isAdvancingFlow = false;
        }

        /// <summary>
        /// 播放月结算弹幕，结束后停止并隐藏弹幕屏幕。
        /// </summary>
        private IEnumerator PlayBulletScreenForDuration()
        {
            if (!StartBulletScreen())
            {
                yield break;
            }

            yield return new WaitForSecondsRealtime(bulletScreenDuration);
            StopBulletScreen();
        }

        /// <summary>
        /// 按月结算类型显示对应 Resources 宣传图，显示结束后销毁。
        /// </summary>
        private IEnumerator PlayMonthSettlementPosterForDuration(GameFlowRunner runner)
        {
            MonthSettlementType settlementType = runner != null && runner.Controller != null
                ? runner.Controller.Blackboard.LastMonthResult.SettlementType
                : MonthSettlementType.PublicRelease;

            string resourcePath = GetMonthSettlementPosterResourcePath(settlementType);

            if (string.IsNullOrEmpty(resourcePath))
            {
                yield break;
            }

            if (!ShowMonthSettlementPoster(resourcePath, out CanvasGroup posterCanvasGroup))
            {
                yield break;
            }

            yield return FadeCanvasGroup(posterCanvasGroup, 0f, 1f, monthSettlementPosterFadeDuration);

            if (monthSettlementPosterDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(monthSettlementPosterDuration);
            }

            yield return FadeCanvasGroup(posterCanvasGroup, 1f, 0f, monthSettlementPosterFadeDuration);
            HideMonthSettlementPoster();
        }

        /// <summary>
        /// 获取月结算类型对应的 Resources 图片路径；不写扩展名，兼容 png/jpg。
        /// </summary>
        private static string GetMonthSettlementPosterResourcePath(MonthSettlementType settlementType)
        {
            switch (settlementType)
            {
                case MonthSettlementType.PvRelease:
                    return "PV";
                case MonthSettlementType.ClosedBeta:
                    return "内测";
                case MonthSettlementType.PublicRelease:
                    return "公测";
                case MonthSettlementType.FinalRelease:
                    // 当前默认三个月流程的最后一月是 FinalRelease，但资源只提供了公测图。
                    return "公测";
                default:
                    return null;
            }
        }

        /// <summary>
        /// 创建顶层全屏 Canvas 并显示指定 Resources 图片。
        /// </summary>
        private bool ShowMonthSettlementPoster(string resourcePath, out CanvasGroup canvasGroup)
        {
            canvasGroup = null;
            HideMonthSettlementPoster();

            Sprite posterSprite = Resources.Load<Sprite>(resourcePath);
            Texture2D posterTexture = posterSprite == null ? Resources.Load<Texture2D>(resourcePath) : null;

            if (posterSprite == null && posterTexture == null)
            {
                Debug.LogWarning($"{nameof(GameFlowPanelCoordinator)} cannot load Resources/{resourcePath}.", this);
                return false;
            }

            monthSettlementPosterInstance = new GameObject(
                "MonthSettlementPosterCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));

            Canvas canvas = monthSettlementPosterInstance.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32767;

            CanvasScaler scaler = monthSettlementPosterInstance.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGroup = monthSettlementPosterInstance.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            if (posterSprite != null)
            {
                var imageObject = new GameObject("Poster", typeof(RectTransform), typeof(Image));
                imageObject.transform.SetParent(monthSettlementPosterInstance.transform, false);
                StretchToFullScreen(imageObject.transform as RectTransform);

                Image image = imageObject.GetComponent<Image>();
                image.sprite = posterSprite;
                image.preserveAspect = false;
                image.raycastTarget = true;
                return true;
            }

            var rawImageObject = new GameObject("Poster", typeof(RectTransform), typeof(RawImage));
            rawImageObject.transform.SetParent(monthSettlementPosterInstance.transform, false);
            StretchToFullScreen(rawImageObject.transform as RectTransform);

            RawImage rawImage = rawImageObject.GetComponent<RawImage>();
            rawImage.texture = posterTexture;
            rawImage.raycastTarget = true;
            return true;
        }

        /// <summary>
        /// 用未缩放时间淡入或淡出指定 CanvasGroup。
        /// </summary>
        private static IEnumerator FadeCanvasGroup(CanvasGroup canvasGroup, float startAlpha, float endAlpha, float duration)
        {
            if (canvasGroup == null)
            {
                yield break;
            }

            if (duration <= 0f)
            {
                canvasGroup.alpha = endAlpha;
                yield break;
            }

            float elapsed = 0f;
            canvasGroup.alpha = startAlpha;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            canvasGroup.alpha = endAlpha;
        }

        /// <summary>
        /// 销毁月结算阶段宣传图覆盖层。
        /// </summary>
        private void HideMonthSettlementPoster()
        {
            if (monthSettlementPosterInstance == null)
            {
                return;
            }

            Destroy(monthSettlementPosterInstance);
            monthSettlementPosterInstance = null;
        }

        /// <summary>
        /// 将 RectTransform 拉伸到父节点全屏。
        /// </summary>
        private static void StretchToFullScreen(RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// 启动弹幕屏幕。
        /// </summary>
        private bool StartBulletScreen()
        {
            EnsureBulletScreenController();

            if (bulletScreenController == null)
            {
                Debug.LogWarning($"{nameof(GameFlowPanelCoordinator)} cannot find {nameof(BulletScreenController)} in the scene.", this);
                return false;
            }

            if (!bulletScreenController.gameObject.activeSelf)
            {
                bulletScreenController.gameObject.SetActive(true);
            }

            bulletScreenController.StartSpawning();
            bulletScreenController.SpawnRandomBullet();
            return true;
        }

        /// <summary>
        /// 停止并隐藏弹幕屏幕。
        /// </summary>
        private void StopBulletScreen()
        {
            if (bulletScreenController == null)
            {
                return;
            }

            bulletScreenController.StopSpawning();
            bulletScreenController.ClearAllBullets();
            bulletScreenController.gameObject.SetActive(false);
        }

        /// <summary>
        /// 关闭开始页面。
        /// </summary>
        private void CloseBeginPanel()
        {
            BeginPanelManager beginPanelManager = BeginPanelManager.Instance;

            if (beginPanelManager != null)
            {
                beginPanelManager.Close();
            }
        }

        /// <summary>
        /// 打开开始页面。
        /// </summary>
        private void OpenBeginPanel()
        {
            BeginPanelManager beginPanelManager = BeginPanelManager.Instance;

            if (beginPanelManager == null)
            {
                Debug.LogWarning($"{nameof(GameFlowPanelCoordinator)} cannot find {nameof(BeginPanelManager)} in the scene.", this);
                return;
            }

            beginPanelManager.Open();
        }

        /// <summary>
        /// 打开月初 BuffWindow。
        /// </summary>
        private void OpenBuffWindow()
        {
            WindowShopPanelManager windowShopPanelManager = WindowShopPanelManager.Instance;

            if (windowShopPanelManager == null)
            {
                Debug.LogWarning($"{nameof(GameFlowPanelCoordinator)} cannot find {nameof(WindowShopPanelManager)} in the scene.", this);
                return;
            }

            windowShopPanelManager.Open(OnBuffWindowClosed);
        }

        /// <summary>
        /// 关闭月初 BuffWindow。
        /// </summary>
        private void CloseBuffWindow()
        {
            WindowShopPanelManager windowShopPanelManager = WindowShopPanelManager.Instance;

            if (windowShopPanelManager != null)
            {
                windowShopPanelManager.Close();
            }
        }

        /// <summary>
        /// 打开周事件面板，由面板关闭回调继续路由流程。
        /// </summary>
        private void OpenEventPanel()
        {
            EventPanelManager eventPanelManager = EventPanelManager.Instance;

            if (eventPanelManager == null)
            {
                Debug.LogWarning($"{nameof(GameFlowPanelCoordinator)} cannot find {nameof(EventPanelManager)} in the scene.", this);
                return;
            }

            eventPanelManager.TryShowCurrentEvent(OnEventPanelClosed);
        }

        /// <summary>
        /// 关闭周事件面板。
        /// </summary>
        private void CloseEventPanel()
        {
            EventPanelManager eventPanelManager = EventPanelManager.Instance;

            if (eventPanelManager != null)
            {
                eventPanelManager.Close();
            }
        }

        /// <summary>
        /// 打开主操作页面。
        /// </summary>
        private void OpenMainPanel()
        {
            MainPanelManager mainPanelManager = MainPanelManager.Instance;

            if (mainPanelManager == null)
            {
                Debug.LogWarning($"{nameof(GameFlowPanelCoordinator)} cannot find {nameof(MainPanelManager)} in the scene.", this);
                return;
            }

            mainPanelManager.Open(OnMainPanelClosed);
        }

        /// <summary>
        /// 关闭主操作页面。
        /// </summary>
        private void CloseMainPanel()
        {
            MainPanelManager mainPanelManager = MainPanelManager.Instance;

            if (mainPanelManager != null)
            {
                mainPanelManager.Close();
            }
        }

        /// <summary>
        /// 关闭周结算页面。
        /// </summary>
        private void CloseWeekPanel()
        {
            WeekPanelManager weekPanelManager = WeekPanelManager.Instance;

            if (weekPanelManager != null)
            {
                weekPanelManager.Close();
            }
        }

        /// <summary>
        /// 打开周结算页面，由关闭回调继续推进流程。
        /// </summary>
        private void OpenWeekPanel()
        {
            WeekPanelManager weekPanelManager = WeekPanelManager.Instance;

            if (weekPanelManager == null)
            {
                Debug.LogWarning($"{nameof(GameFlowPanelCoordinator)} cannot find {nameof(WeekPanelManager)} in the scene.", this);
                OnWeekPanelClosed();
                return;
            }

            weekPanelManager.Open(OnWeekPanelClosed);
        }

        /// <summary>
        /// 打开游戏结束页面。
        /// </summary>
        private void OpenGameEndPanel()
        {
            GameEndPanelManager gameEndPanelManager = GameEndPanelManager.Instance;

            if (gameEndPanelManager == null)
            {
                Debug.LogWarning($"{nameof(GameFlowPanelCoordinator)} cannot find {nameof(GameEndPanelManager)} in the scene.", this);
                return;
            }

            gameEndPanelManager.Open();
        }

        /// <summary>
        /// 关闭游戏结束页面。
        /// </summary>
        private void CloseGameEndPanel()
        {
            GameEndPanelManager gameEndPanelManager = GameEndPanelManager.Instance;

            if (gameEndPanelManager != null)
            {
                gameEndPanelManager.Close();
            }
        }

        /// <summary>
        /// 获取当前场景的 GameFlowRunner 单例。
        /// </summary>
        private bool TryGetRunner(out GameFlowRunner runner)
        {
            runner = GameFlowRunner.Instance;

            if (runner != null)
            {
                return true;
            }

            Debug.LogWarning($"{nameof(GameFlowPanelCoordinator)} cannot find {nameof(GameFlowRunner)} instance.", this);
            return false;
        }

        /// <summary>
        /// 重置当前游戏流程，让 BeginPanel 打开时看到开局状态，并等待下一次 StartGame。
        /// </summary>
        private static void ResetCurrentGameFlowForBeginPanel()
        {
            GameFlowRunner runner = GameFlowRunner.Instance;

            if (runner != null)
            {
                runner.ResetCurrentGameFlowForBeginPanel();
            }
        }

        /// <summary>
        /// 查找并缓存弹幕屏幕控制器。
        /// </summary>
        private void EnsureBulletScreenController()
        {
            if (bulletScreenController == null)
            {
                bulletScreenController = FindObjectOfType<BulletScreenController>(true);
            }
        }
    }
}
