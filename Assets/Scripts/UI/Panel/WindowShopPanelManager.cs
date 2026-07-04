using Anchor.GameFlow;
using Anchor.Window;
using UnityEngine;
using UnityEngine.UI;

namespace Anchor.UI.Panel
{
    [DisallowMultipleComponent]
    public sealed class WindowShopPanelManager : MonoBehaviour
    {
        [Header("Buff Window")]
        [SerializeField, Tooltip("BuffWindow 上的下落回弹动画组件。为空时会从当前物体或子物体查找。")]
        private WindowDropBounceAnimator buffWindowAnimator;

        [Header("Button")]
        [SerializeField, Tooltip("点击后关闭当前 BuffWindow 的按钮。")]
        private Button closeButton;

        private MainPanelManager mainPanelManager;

        /// <summary>
        /// Panel 启用时注册关闭按钮点击事件。
        /// </summary>
        private void OnEnable()
        {
            RegisterCloseButtonClick();
        }

        /// <summary>
        /// Panel 关闭时注销关闭按钮点击事件，避免重复绑定。
        /// </summary>
        private void OnDisable()
        {
            UnregisterCloseButtonClick();
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
        /// 点击关闭按钮后关闭 BuffWindow，并打开 MainPanel。
        /// </summary>
        private void OnCloseButtonClicked()
        {
            if (!TryConfirmBudgetShop())
            {
                return;
            }

            Close();
            OpenMainPanel();
        }

        /// <summary>
        /// 在月初商店阶段确认 Buff 选择，让流程进入周行动阶段。
        /// </summary>
        private bool TryConfirmBudgetShop()
        {
            // 通过 GameFlowRunner 单例确认流程，不再运行时扫描场景。
            GameFlowRunner runner = GameFlowRunner.Instance;

            if (runner == null || runner.Controller == null)
            {
                Debug.LogWarning($"{nameof(WindowShopPanelManager)} cannot find active {nameof(GameFlowRunner)} instance.", this);
                return false;
            }

            GameFlowState currentState = runner.Controller.CurrentState;
            if (currentState == GameFlowState.BudgetShop)
            {
                runner.ConfirmBudgetShop();
                return true;
            }

            if (currentState == GameFlowState.WeekAction)
            {
                return true;
            }

            Debug.LogWarning($"{nameof(WindowShopPanelManager)} cannot confirm budget shop during {currentState}.", this);
            return false;
        }

        /// <summary>
        /// 打开 MainPanel。
        /// </summary>
        private void OpenMainPanel()
        {
            EnsureMainPanelManager();

            if (mainPanelManager == null)
            {
                Debug.LogWarning($"{nameof(WindowShopPanelManager)} cannot find {nameof(MainPanelManager)} in the scene.", this);
                return;
            }

            mainPanelManager.Open();
        }

        /// <summary>
        /// 查找并缓存场景中的 MainPanel 管理器，包含初始未激活的面板。
        /// </summary>
        private void EnsureMainPanelManager()
        {
            if (mainPanelManager == null)
            {
                mainPanelManager = FindObjectOfType<MainPanelManager>(true);
            }
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
        /// 编辑器添加组件时自动填充当前物体上的 BuffWindow 动画组件。
        /// </summary>
        private void Reset()
        {
            EnsureBuffWindowAnimator();
        }
    }
}
