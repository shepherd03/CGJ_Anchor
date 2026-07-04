using System.Collections;
using Anchor.GameFlow;
using UnityEngine;
using UnityEngine.UI;

namespace Anchor.UI.Panel
{
    [DisallowMultipleComponent]
    public sealed class MainPanelManager : MonoBehaviour
    {
        [Header("Button")]
        [SerializeField, Tooltip("点击后结束本周行动并关闭 MainPanel 的按钮。")]
        private Button nextWeekButton;

        [Header("Week Settlement")]
        [SerializeField, Tooltip("周结算展示面板。为空时会从场景中查找。")]
        private WeekPanelManager weekPanelManager;

        [Header("Bullet Screen")]
        [SerializeField, Tooltip("月结算时播放的弹幕屏幕控制器。为空时会从场景中查找。")]
        private BulletScreenController bulletScreenController;

        private const float BulletScreenDuration = 4f;

        private WindowShopPanelManager windowShopPanelManager;
        private Coroutine flowAdvanceCoroutine;
        // 流程推进协程挂在 GameFlowRunner 上，避免 MainPanel 隐藏后协程被停掉。
        private GameFlowRunner flowCoroutineOwner;
        private bool isAdvancingFlow;

        /// <summary>
        /// Panel 启用时注册下一周按钮点击事件。
        /// </summary>
        private void OnEnable()
        {
            RegisterNextWeekButtonClick();
        }

        /// <summary>
        /// Panel 关闭时注销下一周按钮点击事件，避免重复绑定。
        /// </summary>
        private void OnDisable()
        {
            UnregisterNextWeekButtonClick();
        }

        /// <summary>
        /// 物体销毁时停止托管在 GameFlowRunner 实例上的流程协程。
        /// </summary>
        private void OnDestroy()
        {
            StopFlowAdvanceCoroutine();
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
        }

        /// <summary>
        /// 关闭 MainPanel。
        /// </summary>
        public void Close()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 点击下一周按钮后结束本周行动，隐藏当前 MainPanel，并进入周结算展示。
        /// </summary>
        private void OnNextWeekButtonClicked()
        {
            // 通过 GameFlowRunner 单例推进流程，不再运行时扫描场景。
            GameFlowRunner runner = GameFlowRunner.Instance;

            if (runner == null)
            {
                Debug.LogWarning($"{nameof(MainPanelManager)} cannot find {nameof(GameFlowRunner)} instance.", this);
                return;
            }

            if (isAdvancingFlow)
            {
                return;
            }

            if (runner.Controller == null || runner.Controller.CurrentState != GameFlowState.WeekAction)
            {
                Debug.LogWarning($"{nameof(MainPanelManager)} can only finish week action during {nameof(GameFlowState.WeekAction)}.", this);
                return;
            }

            runner.FinishWeekAction();
            Close();
            flowCoroutineOwner = runner;
            flowAdvanceCoroutine = runner.StartCoroutine(AdvanceFlowAfterWeekResolve(runner));
        }

        /// <summary>
        /// 处理周结算展示，然后根据后续状态打开对应 UI。
        /// </summary>
        private IEnumerator AdvanceFlowAfterWeekResolve(GameFlowRunner runner)
        {
            isAdvancingFlow = true;

            yield return RouteFlowAfterCurrentState(runner);

            flowAdvanceCoroutine = null;
            flowCoroutineOwner = null;
            isAdvancingFlow = false;
        }

        /// <summary>
        /// 按当前流程状态决定下一步 UI；周结算打开 WeekPanel，月结算才播放弹幕。
        /// </summary>
        private IEnumerator RouteFlowAfterCurrentState(GameFlowRunner runner)
        {
            while (runner != null && runner.Controller != null)
            {
                switch (runner.Controller.CurrentState)
                {
                    case GameFlowState.WeekResolve:
                        yield return OpenWeekPanelAndWaitForClose();
                        runner.ContinueFlow();
                        break;
                    case GameFlowState.WeekAction:
                        Open();
                        yield break;
                    case GameFlowState.MonthSettlement:
                        yield return PlayBulletScreenForDuration();
                        runner.ContinueFlow();
                        break;
                    case GameFlowState.BudgetShop:
                        OpenBuffWindow();
                        yield break;
                    case GameFlowState.Ending:
                        yield break;
                    default:
                        yield break;
                }
            }
        }

        /// <summary>
        /// 打开周结算面板，并等待玩家点击关闭。
        /// </summary>
        private IEnumerator OpenWeekPanelAndWaitForClose()
        {
            EnsureWeekPanelManager();

            if (weekPanelManager == null)
            {
                Debug.LogWarning($"{nameof(MainPanelManager)} cannot find {nameof(WeekPanelManager)} in the scene.", this);
                yield break;
            }

            weekPanelManager.Open();

            while (weekPanelManager != null && weekPanelManager.gameObject.activeSelf)
            {
                yield return null;
            }
        }

        /// <summary>
        /// 打开弹幕屏幕并持续指定时间，时间结束后停止并隐藏弹幕屏幕。
        /// </summary>
        private IEnumerator PlayBulletScreenForDuration()
        {
            if (!StartBulletScreen())
            {
                yield break;
            }

            yield return new WaitForSecondsRealtime(BulletScreenDuration);
            StopBulletScreen();
        }

        /// <summary>
        /// 激活弹幕屏幕并启动自动弹幕。
        /// </summary>
        private bool StartBulletScreen()
        {
            EnsureBulletScreenController();

            if (bulletScreenController == null)
            {
                Debug.LogWarning($"{nameof(MainPanelManager)} cannot find {nameof(BulletScreenController)} in the scene.", this);
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
        /// 停止自动弹幕并隐藏弹幕屏幕。
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
        /// 打开月初 BuffWindow。
        /// </summary>
        private void OpenBuffWindow()
        {
            EnsureWindowShopPanelManager();

            if (windowShopPanelManager == null)
            {
                Debug.LogWarning($"{nameof(MainPanelManager)} cannot find {nameof(WindowShopPanelManager)} in the scene.", this);
                return;
            }

            windowShopPanelManager.Open();
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
        /// 查找并缓存场景中的周结算面板，包含初始未激活的面板。
        /// </summary>
        private void EnsureWeekPanelManager()
        {
            if (weekPanelManager == null)
            {
                weekPanelManager = FindObjectOfType<WeekPanelManager>(true);
            }
        }

        /// <summary>
        /// 查找并缓存场景中的弹幕屏幕控制器，包含初始未激活的弹幕屏幕。
        /// </summary>
        private void EnsureBulletScreenController()
        {
            if (bulletScreenController == null)
            {
                bulletScreenController = FindObjectOfType<BulletScreenController>(true);
            }
        }

        /// <summary>
        /// 查找并缓存场景中的 BuffWindow 管理器，包含初始未激活的窗口。
        /// </summary>
        private void EnsureWindowShopPanelManager()
        {
            if (windowShopPanelManager == null)
            {
                windowShopPanelManager = FindObjectOfType<WindowShopPanelManager>(true);
            }
        }

        /// <summary>
        /// 停止当前流程推进协程。
        /// </summary>
        private void StopFlowAdvanceCoroutine()
        {
            if (flowAdvanceCoroutine == null || flowCoroutineOwner == null)
            {
                return;
            }

            flowCoroutineOwner.StopCoroutine(flowAdvanceCoroutine);
            flowAdvanceCoroutine = null;
            flowCoroutineOwner = null;
            isAdvancingFlow = false;
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
