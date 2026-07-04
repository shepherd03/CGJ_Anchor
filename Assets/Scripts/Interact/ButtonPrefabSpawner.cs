using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 绑定 UGUI Button，点击后切换当前物体子级 Floating UI 的激活状态。
/// </summary>
[DisallowMultipleComponent]
public sealed class ButtonPrefabSpawner : MonoBehaviour
{
    [Header("Button")]
    [FormerlySerializedAs("spawnButton")]
    [SerializeField, Tooltip("触发 UI 开关逻辑的 UGUI Button。")]
    private Button toggleButton;

    [Header("Target")]
    [FormerlySerializedAs("prefabToSpawn")]
    [SerializeField, Tooltip("需要激活和失活的子物体。子物体上通常挂 FloatingUIFan 或 FloatingUIActionPoints。")]
    private GameObject targetObject;

    [SerializeField, Tooltip("游戏开始时目标子物体是否保持激活。关闭则默认隐藏。")]
    private bool startActive;

    // 缓存按钮点击回调，保证注册和解绑使用同一个委托。
    private UnityAction toggleAction;

    /// <summary>
    /// 添加组件时尝试自动补齐按钮和 Floating UI 目标。
    /// </summary>
    private void Reset()
    {
        toggleButton = GetComponent<Button>();
        targetObject = FindFirstFloatingUITarget();
    }

    /// <summary>
    /// 初始化按钮点击回调。
    /// </summary>
    private void Awake()
    {
        toggleAction = ToggleTargetObject;
        ResolveMissingReferences();
        SetTargetActive(startActive);
    }

    /// <summary>
    /// 组件启用时注册按钮点击事件。
    /// </summary>
    private void OnEnable()
    {
        RegisterButtonClick();
    }

    /// <summary>
    /// 组件禁用时移除按钮点击事件，避免重复注册。
    /// </summary>
    private void OnDisable()
    {
        UnregisterButtonClick();
    }

    /// <summary>
    /// 切换目标子物体的激活状态。
    /// </summary>
    public void ToggleTargetObject()
    {
        if (targetObject == null)
        {
            Debug.LogWarning($"{nameof(ButtonPrefabSpawner)} needs a target object.", this);
            return;
        }

        SetTargetActive(!targetObject.activeSelf);
    }

    /// <summary>
    /// 打开目标子物体。
    /// </summary>
    public void OpenTargetObject()
    {
        SetTargetActive(true);
    }

    /// <summary>
    /// 关闭目标子物体。
    /// </summary>
    public void CloseTargetObject()
    {
        SetTargetActive(false);
    }

    /// <summary>
    /// 给按钮注册点击切换回调。
    /// </summary>
    private void RegisterButtonClick()
    {
        EnsureToggleAction();

        if (toggleButton == null)
        {
            Debug.LogWarning($"{nameof(ButtonPrefabSpawner)} needs a toggle button.", this);
            return;
        }

        toggleButton.onClick.RemoveListener(toggleAction);
        toggleButton.onClick.AddListener(toggleAction);
    }

    /// <summary>
    /// 确保按钮点击回调已经初始化。
    /// </summary>
    private void EnsureToggleAction()
    {
        if (toggleAction == null)
        {
            toggleAction = ToggleTargetObject;
        }
    }

    /// <summary>
    /// 从按钮移除点击切换回调。
    /// </summary>
    private void UnregisterButtonClick()
    {
        if (toggleButton == null || toggleAction == null)
        {
            return;
        }

        toggleButton.onClick.RemoveListener(toggleAction);
    }

    /// <summary>
    /// 激活或失活目标子物体。
    /// </summary>
    private void SetTargetActive(bool isActive)
    {
        if (targetObject == null)
        {
            return;
        }

        if (targetObject == gameObject)
        {
            Debug.LogWarning($"{nameof(ButtonPrefabSpawner)} cannot toggle its own GameObject. Assign a child target object.", this);
            return;
        }

        targetObject.SetActive(isActive);
    }

    /// <summary>
    /// 自动补齐未配置的按钮和 Floating UI 目标。
    /// </summary>
    private void ResolveMissingReferences()
    {
        if (toggleButton == null)
        {
            toggleButton = GetComponent<Button>();
        }

        if (targetObject == null)
        {
            targetObject = FindFirstFloatingUITarget();
        }
    }

    /// <summary>
    /// 在当前物体子级中查找第一个 Floating UI 目标。
    /// </summary>
    private GameObject FindFirstFloatingUITarget()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (ContainsFloatingUITarget(child))
            {
                return child.gameObject;
            }
        }

        return null;
    }

    /// <summary>
    /// 判断指定子级是否包含可被按钮控制的 Floating UI 脚本。
    /// </summary>
    private bool ContainsFloatingUITarget(Transform child)
    {
        if (child == null)
        {
            return false;
        }

        return child.GetComponentInChildren<FloatingUIFan>(true) != null
            || child.GetComponentInChildren<FloatingUIActionPoints>(true) != null;
    }
}
