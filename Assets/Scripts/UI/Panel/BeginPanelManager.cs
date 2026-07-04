using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Anchor.UI.Panel
{
    /// <summary>
    /// 场景内 PanelManager 单例缓存，统一处理 Domain Reload 关闭时的静态状态清理。
    /// </summary>
    internal static class PanelManagerSingletonRegistry
    {
        // 按具体 PanelManager 类型缓存当前场景的唯一实例。
        private static readonly Dictionary<Type, MonoBehaviour> instances = new Dictionary<Type, MonoBehaviour>();

        /// <summary>
        /// 获取指定类型的 PanelManager 实例；缓存为空时从当前场景查找 inactive 物体。
        /// </summary>
        public static T Get<T>() where T : MonoBehaviour
        {
            Type managerType = typeof(T);
            if (instances.TryGetValue(managerType, out MonoBehaviour manager) && manager != null)
            {
                return (T)manager;
            }

            T foundManager = UnityEngine.Object.FindObjectOfType<T>(true);
            if (foundManager != null)
            {
                instances[managerType] = foundManager;
            }

            return foundManager;
        }

        /// <summary>
        /// 注册当前场景的 PanelManager 实例，发现重复实例时禁用后注册者。
        /// </summary>
        public static bool Register<T>(T manager) where T : MonoBehaviour
        {
            Type managerType = typeof(T);
            if (instances.TryGetValue(managerType, out MonoBehaviour existingManager)
                && existingManager != null
                && existingManager != manager)
            {
                Debug.LogError($"{managerType.Name} 已存在有效实例，重复的面板管理器会被禁用：{manager.name}", manager);
                manager.enabled = false;
                return false;
            }

            instances[managerType] = manager;
            return true;
        }

        /// <summary>
        /// 注销被销毁的 PanelManager 实例，避免下一个场景拿到旧引用。
        /// </summary>
        public static void Unregister<T>(T manager) where T : MonoBehaviour
        {
            Type managerType = typeof(T);
            if (instances.TryGetValue(managerType, out MonoBehaviour existingManager) && existingManager == manager)
            {
                instances.Remove(managerType);
            }
        }

        /// <summary>
        /// 进入 Play Mode 时清理所有 PanelManager 静态引用，避免关闭 Domain Reload 后残留旧实例。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            instances.Clear();
        }
    }

    /// <summary>
    /// 场景级 PanelManager 单例基类；继承类可以直接通过 XxxPanelManager.Instance 访问。
    /// </summary>
    public abstract class PanelManagerSingleton<T> : MonoBehaviour where T : PanelManagerSingleton<T>
    {
        /// <summary>
        /// 当前场景唯一的指定类型 PanelManager。
        /// </summary>
        public static T Instance => PanelManagerSingletonRegistry.Get<T>();

        /// <summary>
        /// 初始化当前场景唯一的 PanelManager 实例。
        /// </summary>
        protected virtual void Awake()
        {
            PanelManagerSingletonRegistry.Register((T)this);
        }

        /// <summary>
        /// 销毁时释放 PanelManager 单例引用。
        /// </summary>
        protected virtual void OnDestroy()
        {
            PanelManagerSingletonRegistry.Unregister((T)this);
        }
    }

    [DisallowMultipleComponent]
    public sealed class BeginPanelManager : PanelManagerSingleton<BeginPanelManager>
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
