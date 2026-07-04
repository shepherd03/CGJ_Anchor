using UnityEngine;
using UnityEngine.UI;

namespace Anchor.UI.Panel
{
    [DisallowMultipleComponent]
    public sealed class BeginPanelManager : MonoBehaviour
    {
        [Header("Button")]
        [SerializeField, Tooltip("点击后开始新游戏并关闭当前 BeginPanel 的按钮。")]
        private Button startButton;

        /// <summary>
        /// Panel 启用时注册开始按钮点击事件。
        /// </summary>
        private void OnEnable()
        {
            RegisterButtonClick();
        }

        /// <summary>
        /// Panel 关闭时注销按钮事件，避免重复绑定。
        /// </summary>
        private void OnDisable()
        {
            UnregisterButtonClick();
        }

        /// <summary>
        /// 开始按钮点击后交给流程 UI 编排器启动游戏。
        /// </summary>
        private void OnStartButtonClicked()
        {
            GameFlowPanelCoordinator.GetOrCreate().StartGame();
        }

        /// <summary>
        /// 给开始按钮注册点击事件。
        /// </summary>
        private void RegisterButtonClick()
        {
            if (startButton == null)
            {
                Debug.LogWarning($"{nameof(BeginPanelManager)} needs a start button.", this);
                return;
            }

            startButton.onClick.RemoveListener(OnStartButtonClicked);
            startButton.onClick.AddListener(OnStartButtonClicked);
        }

        /// <summary>
        /// 移除开始按钮点击事件。
        /// </summary>
        private void UnregisterButtonClick()
        {
            if (startButton == null)
            {
                return;
            }

            startButton.onClick.RemoveListener(OnStartButtonClicked);
        }

        /// <summary>
        /// 关闭 BeginPanel。
        /// </summary>
        public void Close()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 挂到 Button 本体时自动填充按钮引用。
        /// </summary>
        private void Reset()
        {
            if (startButton == null)
            {
                startButton = GetComponent<Button>();
            }
        }
    }
}
