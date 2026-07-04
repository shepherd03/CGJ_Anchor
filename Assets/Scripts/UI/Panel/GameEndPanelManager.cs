using UnityEngine;
using UnityEngine.UI;

namespace Anchor.UI.Panel
{
    [DisallowMultipleComponent]
    public sealed class GameEndPanelManager : PanelManagerSingleton<GameEndPanelManager>
    {
        [Header("Button")]
        [SerializeField, Tooltip("点击后回到 BeginPanel 的按钮。")]
        private Button closeButton;

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
        /// 外部打开 GameEndPanel 的入口。
        /// </summary>
        public void Open()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// 关闭 GameEndPanel。
        /// </summary>
        public void Close()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 结束面板按钮点击后交给流程 UI 编排器返回开始界面。
        /// </summary>
        private void OnReturnToBeginButtonClicked()
        {
            GameFlowPanelCoordinator.GetOrCreate().ReturnToBeginPanel();
        }

        /// <summary>
        /// 给返回开始界面按钮注册点击事件。
        /// </summary>
        private void RegisterCloseButtonClick()
        {
            if (closeButton == null)
            {
                Debug.LogWarning($"{nameof(GameEndPanelManager)} needs a close button.", this);
                return;
            }

            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.RemoveListener(OnReturnToBeginButtonClicked);
            closeButton.onClick.AddListener(OnReturnToBeginButtonClicked);
        }

        /// <summary>
        /// 移除返回开始界面按钮点击事件。
        /// </summary>
        private void UnregisterCloseButtonClick()
        {
            if (closeButton == null)
            {
                return;
            }

            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.RemoveListener(OnReturnToBeginButtonClicked);
        }

        /// <summary>
        /// 挂到 Button 本体时自动填充按钮引用。
        /// </summary>
        private void Reset()
        {
            if (closeButton == null)
            {
                closeButton = GetComponent<Button>();
            }
        }
    }
}
