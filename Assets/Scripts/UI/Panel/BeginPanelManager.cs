using Anchor.GameFlow;
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

        private WindowShopPanelManager windowShopPanelManager;

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
        /// 开始按钮点击后启动游戏流程，关闭当前 BeginPanel，再打开 BuffWindow。
        /// </summary>
        private void OnStartButtonClicked()
        {
            // 通过 GameFlowRunner 单例启动流程，避免每次点击时扫描场景。
            GameFlowRunner runner = GameFlowRunner.Instance;

            if (runner == null)
            {
                Debug.LogWarning($"{nameof(BeginPanelManager)} cannot find {nameof(GameFlowRunner)} instance.", this);
                return;
            }

            runner.StartNewGame();
            gameObject.SetActive(false);
            OpenBuffWindow();
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
        /// 打开 BuffWindow 商店面板。
        /// </summary>
        private void OpenBuffWindow()
        {
            EnsureWindowShopPanelManager();

            if (windowShopPanelManager == null)
            {
                Debug.LogWarning($"{nameof(BeginPanelManager)} cannot find {nameof(WindowShopPanelManager)} in the scene.", this);
                return;
            }

            windowShopPanelManager.Open();
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
