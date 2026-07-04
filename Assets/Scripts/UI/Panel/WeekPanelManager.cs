using System;
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

        // 当前 WeekPanel 关闭后要交还给流程编排器执行的回调。
        private Action onClosed;

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
        /// 外部打开 WeekPanel 的入口，并记录关闭后继续流程的回调。
        /// </summary>
        public void Open(Action closedCallback = null)
        {
            onClosed = closedCallback;

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// 关闭 WeekPanel，并清理关闭回调。
        /// </summary>
        public void Close()
        {
            onClosed = null;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 关闭 WeekPanel，并触发本次打开时注入的关闭回调。
        /// </summary>
        private void CloseAndNotify()
        {
            gameObject.SetActive(false);
            NotifyClosed();
        }

        /// <summary>
        /// 执行并清理 WeekPanel 关闭回调，防止重复推进流程。
        /// </summary>
        private void NotifyClosed()
        {
            Action closedCallback = onClosed;
            onClosed = null;
            closedCallback?.Invoke();
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
            closeButton.onClick.RemoveListener(CloseAndNotify);
            closeButton.onClick.AddListener(CloseAndNotify);
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
            closeButton.onClick.RemoveListener(CloseAndNotify);
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
