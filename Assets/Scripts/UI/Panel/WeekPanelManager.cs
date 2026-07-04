using UnityEngine;
using UnityEngine.UI;

namespace Anchor.UI.Panel
{
    [DisallowMultipleComponent]
    public sealed class WeekPanelManager : PanelManagerSingleton<WeekPanelManager>
    {
        [Header("Button")]
        [SerializeField, Tooltip("点击后关闭当前 WeekPanel 的按钮。")]
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
        /// 外部打开 WeekPanel 的入口。
        /// </summary>
        public void Open()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// 关闭 WeekPanel。
        /// </summary>
        public void Close()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 给关闭按钮注册点击事件。
        /// </summary>
        private void RegisterCloseButtonClick()
        {
            if (closeButton == null)
            {
                Debug.LogWarning($"{nameof(WeekPanelManager)} needs a close button.", this);
                return;
            }

            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
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
