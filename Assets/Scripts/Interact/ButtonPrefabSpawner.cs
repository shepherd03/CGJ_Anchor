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

    // 缓存关闭动画结束回调，保证添加和移除监听的是同一个委托。
    private UnityAction deactivateAfterCloseAction;

    // 缓存目标上的扇形 UI 动画组件，避免每次点击都重新查找。
    private FloatingUIFan targetFan;

    // 记录按钮当前期望的开关状态，避免关闭动画期间 activeSelf 仍为 true 导致无法反向打开。
    private bool targetActiveState;

    /// <summary>
    /// 添加组件时尝试自动补齐按钮和 Floating UI 目标。
    /// </summary>
    private void Reset()
    {
        toggleButton = GetComponent<Button>();
        targetObject = FindFirstFloatingUITarget();
    }

    /// <summary>
    /// 初始化按钮点击回调、动画组件缓存和目标初始状态。
    /// </summary>
    private void Awake()
    {
        toggleAction = ToggleTargetObject;
        deactivateAfterCloseAction = DeactivateTargetAfterCloseAnimation;
        ResolveMissingReferences();
        CacheFloatingUIComponents();
        ApplyInitialTargetState();
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
    /// 组件销毁时移除关闭动画监听，避免目标 UI 残留回调。
    /// </summary>
    private void OnDestroy()
    {
        RemoveCloseAnimationListener();
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

        SetTargetActive(!targetActiveState);
    }

    /// <summary>
    /// 打开目标子物体并播放展开动画。
    /// </summary>
    public void OpenTargetObject()
    {
        SetTargetActive(true);
    }

    /// <summary>
    /// 关闭目标子物体并播放收起动画。
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
    /// 应用初始开关状态；默认隐藏时不播放关闭动画。
    /// </summary>
    private void ApplyInitialTargetState()
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

        targetActiveState = startActive;

        if (startActive)
        {
            OpenTargetWithAnimation();
        }
        else
        {
            DeactivateTargetImmediate();
        }
    }

    /// <summary>
    /// 激活或失活目标子物体，并优先播放 Floating UI 自带的开关动画。
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

        targetActiveState = isActive;
        CacheFloatingUIComponents();

        if (isActive)
        {
            OpenTargetWithAnimation();
        }
        else
        {
            CloseTargetWithAnimation();
        }
    }

    /// <summary>
    /// 打开目标子物体；对象已激活时手动补播展开动画。
    /// </summary>
    private void OpenTargetWithAnimation()
    {
        RemoveCloseAnimationListener();

        if (!targetObject.activeSelf)
        {
            targetObject.SetActive(true);
            return;
        }

        if (targetFan != null && targetFan.isActiveAndEnabled)
            targetFan.Open();
    }

    /// <summary>
    /// 关闭目标子物体；有 FloatingUIFan 时等待收起动画播完再真正失活。
    /// </summary>
    private void CloseTargetWithAnimation()
    {
        if (!targetObject.activeSelf || targetFan == null || !targetFan.isActiveAndEnabled)
        {
            DeactivateTargetImmediate();
            return;
        }

        if (!targetFan.IsOpen && !targetFan.IsTransitioning)
        {
            DeactivateTargetImmediate();
            return;
        }

        EnsureCloseAction();
        RemoveCloseAnimationListener();
        targetFan.OnClosed.AddListener(deactivateAfterCloseAction);
        targetFan.Close();
    }

    /// <summary>
    /// 关闭动画完成后真正隐藏目标子物体。
    /// </summary>
    private void DeactivateTargetAfterCloseAnimation()
    {
        RemoveCloseAnimationListener();
        DeactivateTargetImmediate();
    }

    /// <summary>
    /// 不播放动画，直接隐藏目标子物体。
    /// </summary>
    private void DeactivateTargetImmediate()
    {
        RemoveCloseAnimationListener();

        if (targetObject != null)
            targetObject.SetActive(false);
    }

    /// <summary>
    /// 缓存目标子物体上的 Floating UI 动画组件。
    /// </summary>
    private void CacheFloatingUIComponents()
    {
        targetFan = targetObject != null
            ? targetObject.GetComponentInChildren<FloatingUIFan>(true)
            : null;
    }

    /// <summary>
    /// 确保关闭动画结束回调已经初始化。
    /// </summary>
    private void EnsureCloseAction()
    {
        if (deactivateAfterCloseAction == null)
            deactivateAfterCloseAction = DeactivateTargetAfterCloseAnimation;
    }

    /// <summary>
    /// 移除关闭动画结束监听，避免重复触发隐藏逻辑。
    /// </summary>
    private void RemoveCloseAnimationListener()
    {
        if (targetFan == null || deactivateAfterCloseAction == null)
            return;

        targetFan.OnClosed.RemoveListener(deactivateAfterCloseAction);
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
