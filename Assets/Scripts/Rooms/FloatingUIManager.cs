using Anchor.GameFlow;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 持有并转发单个交互点下 Floating UI 的基础行为。
/// </summary>
[DisallowMultipleComponent]
public sealed class FloatingUIManager : MonoBehaviour
{
    private const int ManagedButtonCount = 3;

    [Header("Floating UI")]
    [SerializeField, Tooltip("扇形 Floating UI 动画控制器。为空时会从当前物体子级查找。")]
    private FloatingUIFan floatingUIFan;

    [SerializeField, Tooltip("行动点不足反馈控制器。为空时会从当前物体子级查找。")]
    private FloatingUIActionPoints actionPoints;

    [SerializeField, Tooltip("控制当前 Floating UI 开关的按钮组件。为空时会从父级或场景中查找。")]
    private ButtonPrefabSpawner buttonPrefabSpawner;

    // 缓存当前 Floating UI 直接子对象上的三个 Button，前两个接入音效房间行动点逻辑，第三个暂时只持有。
    private readonly Button[] childButtons = new Button[ManagedButtonCount];

    // 缓存三个子按钮的点击回调，保证注册和解绑使用同一个委托实例。
    private readonly UnityAction[] childButtonCallbacks = new UnityAction[ManagedButtonCount];

    public FloatingUIFan Fan => floatingUIFan;
    public FloatingUIActionPoints ActionPoints => actionPoints;
    public ButtonPrefabSpawner Spawner => buttonPrefabSpawner;

    /// <summary>
    /// 添加组件时自动缓存当前层级下已有的 Floating UI 组件。
    /// </summary>
    private void Reset()
    {
        CacheFloatingUIReferences();
        CacheChildButtons();
        CacheButtonPrefabSpawner();
    }

    /// <summary>
    /// 运行时初始化 Floating UI 组件引用。
    /// </summary>
    private void Awake()
    {
        CacheMissingFloatingUIReferences();
        CacheChildButtons();
        CacheMissingButtonPrefabSpawner();
        EnsureChildButtonCallbacks();
    }

    /// <summary>
    /// 组件启用时注册前两个子按钮的音效行动点消耗事件。
    /// </summary>
    private void OnEnable()
    {
        RegisterChildButtonClicks();
    }

    /// <summary>
    /// 组件禁用时移除子按钮事件，避免重复注册。
    /// </summary>
    private void OnDisable()
    {
        UnregisterChildButtonClicks();
    }

    /// <summary>
    /// Inspector 修改后补齐缺失引用，方便搭房间预制体时少拖字段。
    /// </summary>
    private void OnValidate()
    {
        CacheMissingFloatingUIReferences();
        CacheChildButtons();
    }

    /// <summary>
    /// 重新查找并缓存当前层级下的 Floating UI 组件。
    /// </summary>
    [ContextMenu("Refresh Floating UI References")]
    public void CacheFloatingUIReferences()
    {
        floatingUIFan = GetComponentInChildren<FloatingUIFan>(true);
        actionPoints = GetComponentInChildren<FloatingUIActionPoints>(true);
        CacheChildButtons();
        CacheButtonPrefabSpawner();
    }

    /// <summary>
    /// 只补齐空引用，避免覆盖 Inspector 手动指定的目标。
    /// </summary>
    public void CacheMissingFloatingUIReferences()
    {
        if (floatingUIFan == null)
        {
            floatingUIFan = GetComponentInChildren<FloatingUIFan>(true);
        }

        if (actionPoints == null)
        {
            actionPoints = GetComponentInChildren<FloatingUIActionPoints>(true);
        }
    }

    /// <summary>
    /// 重新查找并缓存控制当前 Floating UI 的 ButtonPrefabSpawner。
    /// </summary>
    public void CacheButtonPrefabSpawner()
    {
        buttonPrefabSpawner = FindButtonPrefabSpawner();
    }

    /// <summary>
    /// 只在开关组件引用为空时补齐，避免覆盖 Inspector 手动指定。
    /// </summary>
    public void CacheMissingButtonPrefabSpawner()
    {
        if (buttonPrefabSpawner == null)
        {
            buttonPrefabSpawner = FindButtonPrefabSpawner();
        }
    }

    /// <summary>
    /// 打开当前交互点绑定的扇形 Floating UI。
    /// </summary>
    public void OpenFan()
    {
        if (floatingUIFan == null)
        {
            Debug.LogWarning($"{nameof(FloatingUIManager)} 缺少 {nameof(FloatingUIFan)} 引用：{name}", this);
            return;
        }

        floatingUIFan.Open();
    }

    /// <summary>
    /// 关闭当前交互点绑定的扇形 Floating UI。
    /// </summary>
    public void CloseFan()
    {
        if (floatingUIFan == null)
        {
            Debug.LogWarning($"{nameof(FloatingUIManager)} 缺少 {nameof(FloatingUIFan)} 引用：{name}", this);
            return;
        }

        floatingUIFan.Close();
    }

    /// <summary>
    /// 兼容旧入口；行动点已经改由 GameFlow 管理，Floating UI 不再重置内部点数。
    /// </summary>
    public void ResetActionPoints()
    {
        // 旧内部行动点已经删除，保留方法避免现有房间交互代码失效。
    }

    /// <summary>
    /// 尝试消耗外部 GameFlow 的音效行动点，失败时显示 Floating UI 提示。
    /// </summary>
    public bool TrySpendActionPoints(int amount)
    {
        return TrySpendAudioActionPoints(amount);
    }

    /// <summary>
    /// 尝试消耗外部 GameFlow 的音效行动点。
    /// </summary>
    private bool TrySpendAudioActionPoints(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (amount == 0)
        {
            return true;
        }

        if (!TryGetGameFlowRunner(out GameFlowRunner runner))
        {
            ShowInsufficientActionPointsFeedback();
            return false;
        }

        bool success = TrySpendAudioActionPoints(runner, amount);
        if (!success)
        {
            ShowInsufficientActionPointsFeedback();
        }

        return success;
    }

    /// <summary>
    /// 按点数调用 GameFlowRunner 上对应的音效行动点消耗方法。
    /// </summary>
    private static bool TrySpendAudioActionPoints(GameFlowRunner runner, int amount)
    {
        return amount switch
        {
            1 => runner.TrySpendAudioOneActionPoint(),
            2 => runner.TrySpendAudioTwoActionPoints(),
            _ => runner.TryAllocateAudio(amount)
        };
    }

    /// <summary>
    /// 缓存当前物体前三个直接子对象上的 Button。
    /// </summary>
    private void CacheChildButtons()
    {
        for (int i = 0; i < childButtons.Length; i++)
        {
            Transform child = transform.childCount > i ? transform.GetChild(i) : null;
            childButtons[i] = child != null ? child.GetComponent<Button>() : null;
        }
    }

    /// <summary>
    /// 注册音效房间行动点按钮点击事件。
    /// </summary>
    private void RegisterChildButtonClicks()
    {
        CacheChildButtons();
        CacheMissingButtonPrefabSpawner();
        EnsureChildButtonCallbacks();

        for (int i = 0; i < childButtonCallbacks.Length; i++)
        {
            RegisterButtonClick(i, childButtonCallbacks[i]);
        }
    }

    /// <summary>
    /// 移除 Floating UI 子按钮点击事件。
    /// </summary>
    private void UnregisterChildButtonClicks()
    {
        EnsureChildButtonCallbacks();

        for (int i = 0; i < childButtonCallbacks.Length; i++)
        {
            UnregisterButtonClick(i, childButtonCallbacks[i]);
        }
    }

    /// <summary>
    /// 给指定子按钮注册点击事件，注册前先移除同一事件防止重复绑定。
    /// </summary>
    private void RegisterButtonClick(int buttonIndex, UnityAction action)
    {
        if (!TryGetChildButton(buttonIndex, out Button button))
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    /// <summary>
    /// 从指定子按钮移除点击事件。
    /// </summary>
    private void UnregisterButtonClick(int buttonIndex, UnityAction action)
    {
        if (!TryGetChildButton(buttonIndex, out Button button))
        {
            return;
        }

        button.onClick.RemoveListener(action);
    }

    /// <summary>
    /// 获取指定下标的子按钮。
    /// </summary>
    private bool TryGetChildButton(int buttonIndex, out Button button)
    {
        button = null;

        if (buttonIndex < 0 || buttonIndex >= childButtons.Length)
        {
            return false;
        }

        button = childButtons[buttonIndex];
        return button != null;
    }

    /// <summary>
    /// 确保三个子按钮都有稳定的点击回调。
    /// </summary>
    private void EnsureChildButtonCallbacks()
    {
        childButtonCallbacks[0] ??= OnFirstChildButtonClicked;
        childButtonCallbacks[1] ??= OnSecondChildButtonClicked;
        childButtonCallbacks[2] ??= OnThirdChildButtonClicked;
    }

    /// <summary>
    /// 第一个子按钮：尝试消耗 1 点音效行动点，然后关闭 Floating UI。
    /// </summary>
    private void OnFirstChildButtonClicked()
    {
        bool actionApplied = TrySpendAudioActionPoints(1);
        CloseFloatingUIFromSpawner();
        LockSpawnerForCurrentWeekIfActionApplied(actionApplied);
    }

    /// <summary>
    /// 第二个子按钮：尝试消耗 2 点音效行动点，然后关闭 Floating UI。
    /// </summary>
    private void OnSecondChildButtonClicked()
    {
        bool actionApplied = TrySpendAudioActionPoints(2);
        CloseFloatingUIFromSpawner();
        LockSpawnerForCurrentWeekIfActionApplied(actionApplied);
    }

    /// <summary>
    /// 第三个子按钮：暂时只负责关闭 Floating UI。
    /// </summary>
    private void OnThirdChildButtonClicked()
    {
        CloseFloatingUIFromSpawner();
    }

    /// <summary>
    /// 调用 ButtonPrefabSpawner 的完整按钮点击链，复用原按钮再次点击的全部效果。
    /// </summary>
    private void CloseFloatingUIFromSpawner()
    {
        CacheMissingButtonPrefabSpawner();

        if (buttonPrefabSpawner != null)
        {
            buttonPrefabSpawner.InvokeToggleButtonClick();
            return;
        }

        CloseFan();
    }

    /// <summary>
    /// 房间行动成功后锁定入口按钮，避免玩家本周重复打开同一个 Floating UI。
    /// </summary>
    private void LockSpawnerForCurrentWeekIfActionApplied(bool actionApplied)
    {
        if (!actionApplied)
        {
            return;
        }

        CacheMissingButtonPrefabSpawner();

        if (buttonPrefabSpawner != null)
        {
            buttonPrefabSpawner.LockForCurrentWeek();
        }
    }

    /// <summary>
    /// 显示外部行动点不足反馈。
    /// </summary>
    private void ShowInsufficientActionPointsFeedback()
    {
        CacheMissingFloatingUIReferences();

        if (actionPoints != null)
        {
            actionPoints.ShowInsufficientWarning();
        }
    }

    /// <summary>
    /// 获取当前场景的游戏流程入口。
    /// </summary>
    private bool TryGetGameFlowRunner(out GameFlowRunner runner)
    {
        runner = GameFlowRunner.Instance;

        if (runner != null)
        {
            return true;
        }

        Debug.LogWarning($"{nameof(FloatingUIManager)} 找不到 {nameof(GameFlowRunner)}，无法消耗音效房间行动点：{name}", this);
        return false;
    }

    /// <summary>
    /// 查找控制当前 Floating UI 所在层级的 ButtonPrefabSpawner。
    /// </summary>
    private ButtonPrefabSpawner FindButtonPrefabSpawner()
    {
        ButtonPrefabSpawner parentSpawner = GetComponentInParent<ButtonPrefabSpawner>(true);
        if (parentSpawner != null && parentSpawner.ControlsTarget(transform))
        {
            return parentSpawner;
        }

        ButtonPrefabSpawner[] spawners = FindObjectsOfType<ButtonPrefabSpawner>(true);
        for (int i = 0; i < spawners.Length; i++)
        {
            if (spawners[i] != null && spawners[i].ControlsTarget(transform))
            {
                return spawners[i];
            }
        }

        return null;
    }
}
