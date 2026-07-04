using System.Collections;
using Anchor.GameFlow;
using UnityEngine;

namespace Anchor.UI.Panel
{
    [DisallowMultipleComponent]
    public sealed class GameFlowPanelCoordinator : MonoBehaviour
    {
        /// <summary>
        /// 当前场景唯一的流程 UI 编排入口，按钮层只调用这里。
        /// </summary>
        public static GameFlowPanelCoordinator Instance { get; private set; }

        [Header("Bullet Screen")]
        [SerializeField, Tooltip("月结算弹幕屏幕控制器。为空时会从场景中查找。")]
        private BulletScreenController bulletScreenController;

        [SerializeField, Min(0f), Tooltip("月结算弹幕播放时长。")]
        private float bulletScreenDuration = 4f;

        // 当前正在执行的流程 UI 协程。
        private Coroutine flowRoutine;
        // 防止按钮连点造成重复推进。
        private bool isAdvancingFlow;

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
            if (!TryGetRunner(out GameFlowRunner runner))
            {
                return;
            }

            StopFlowRoutine();
            CloseMainPanel();
            CloseWeekPanel();
            CloseGameEndPanel();
            runner.StartNewGame();
            CloseBeginPanel();
            RouteCurrentState(runner);
        }

        /// <summary>
        /// 推进当前流程一步，由这里统一判断该调用哪个流程入口和打开哪个 UI。
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
                    runner.ConfirmBudgetShop();
                    CloseBuffWindow();
                    RouteCurrentState(runner);
                    break;
                case GameFlowState.WeekEvent:
                    EventPanelManager.Instance?.TryShowCurrentEvent();
                    break;
                case GameFlowState.WeekAction:
                    runner.FinishWeekAction();
                    CloseMainPanel();
                    StartFlowRoutine(RouteFlowAfterCurrentState(runner));
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
                    EventPanelManager.Instance?.TryShowCurrentEvent();
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
        /// 处理需要异步等待的流程状态：周结算等待按钮关闭，月结算等待弹幕播放。
        /// </summary>
        private IEnumerator RouteFlowAfterCurrentState(GameFlowRunner runner)
        {
            while (runner != null && runner.Controller != null)
            {
                switch (runner.Controller.CurrentState)
                {
                    case GameFlowState.WeekEvent:
                        EventPanelManager.Instance?.TryShowCurrentEvent();
                        yield break;
                    case GameFlowState.WeekResolve:
                        yield return OpenWeekPanelAndWaitForClose();
                        runner.ContinueFlow();
                        break;
                    case GameFlowState.WeekAction:
                        OpenMainPanel();
                        yield break;
                    case GameFlowState.MonthSettlement:
                        yield return PlayBulletScreenForDuration();
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
        /// 周事件面板完成关闭动画后继续路由当前流程状态。
        /// </summary>
        public void ResumeAfterWeekGameEventChoice()
        {
            if (!TryGetRunner(out GameFlowRunner runner) || runner.Controller == null)
                return;

            RouteCurrentState(runner);
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
            isAdvancingFlow = false;
        }

        /// <summary>
        /// 打开周结算面板，并等待玩家关闭。
        /// </summary>
        private IEnumerator OpenWeekPanelAndWaitForClose()
        {
            WeekPanelManager weekPanelManager = WeekPanelManager.Instance;

            if (weekPanelManager == null)
            {
                Debug.LogWarning($"{nameof(GameFlowPanelCoordinator)} cannot find {nameof(WeekPanelManager)} in the scene.", this);
                yield break;
            }

            weekPanelManager.Open();

            while (weekPanelManager != null && weekPanelManager.gameObject.activeSelf)
            {
                yield return null;
            }
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

            windowShopPanelManager.Open();
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

            mainPanelManager.Open();
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
