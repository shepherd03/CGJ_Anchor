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

        [Header("Bullet Screen")]
        [SerializeField, Tooltip("周结算和月结算时播放的弹幕屏幕控制器。为空时会从场景中查找。")]
        private BulletScreenController bulletScreenController;

        private const float BulletScreenDuration = 4f;

        private GameFlowRunner gameFlowRunner;
        private WindowShopPanelManager windowShopPanelManager;
        private Coroutine flowAdvanceCoroutine;
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
        /// 物体销毁时停止托管在 GameFlowRunner 上的流程协程。
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
        /// 点击下一周按钮后结束本周行动，隐藏当前 MainPanel，并播放弹幕后推进流程。
        /// </summary>
        private void OnNextWeekButtonClicked()
        {
            EnsureGameFlowRunner();

            if (gameFlowRunner == null)
            {
                Debug.LogWarning($"{nameof(MainPanelManager)} cannot find {nameof(GameFlowRunner)} in the scene.", this);
                return;
            }

            if (isAdvancingFlow)
            {
                return;
            }

            if (gameFlowRunner.Controller == null || gameFlowRunner.Controller.CurrentState != GameFlowState.WeekAction)
            {
                Debug.LogWarning($"{nameof(MainPanelManager)} can only finish week action during {nameof(GameFlowState.WeekAction)}.", this);
                return;
            }

            gameFlowRunner.FinishWeekAction();
            Close();
            flowAdvanceCoroutine = gameFlowRunner.StartCoroutine(AdvanceFlowAfterBulletScreen());
        }

        /// <summary>
        /// 播放弹幕 4 秒后继续流程，并根据新状态打开对应 UI。
        /// </summary>
        private IEnumerator AdvanceFlowAfterBulletScreen()
        {
            isAdvancingFlow = true;

            yield return PlayBulletScreenForDuration();
            gameFlowRunner.ContinueFlow();
            yield return RouteFlowAfterContinue();

            flowAdvanceCoroutine = null;
            isAdvancingFlow = false;
        }

        /// <summary>
        /// 按当前流程状态决定下一步 UI；月结算会再播放一轮弹幕后继续到月初商店。
        /// </summary>
        private IEnumerator RouteFlowAfterContinue()
        {
            while (gameFlowRunner != null && gameFlowRunner.Controller != null)
            {
                switch (gameFlowRunner.Controller.CurrentState)
                {
                    case GameFlowState.WeekAction:
                        Open();
                        yield break;
                    case GameFlowState.MonthSettlement:
                        yield return PlayBulletScreenForDuration();
                        gameFlowRunner.ContinueFlow();
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
        /// 查找并缓存场景中的 GameFlowRunner。
        /// </summary>
        private void EnsureGameFlowRunner()
        {
            if (gameFlowRunner == null)
            {
                gameFlowRunner = FindObjectOfType<GameFlowRunner>();
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
            if (flowAdvanceCoroutine == null || gameFlowRunner == null)
            {
                return;
            }

            gameFlowRunner.StopCoroutine(flowAdvanceCoroutine);
            flowAdvanceCoroutine = null;
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
