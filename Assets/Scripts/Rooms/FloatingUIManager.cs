using Anchor.GameFlow;
using UnityEngine;
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

    [SerializeField, Tooltip("行动点 UI 控制器。为空时会从当前物体子级查找。")]
    private FloatingUIActionPoints actionPoints;

    // 缓存当前 Floating UI 直接子对象上的三个 Button，前两个接入音效房间行动点逻辑，第三个暂时只持有。
    private readonly Button[] childButtons = new Button[ManagedButtonCount];

    public FloatingUIFan Fan => floatingUIFan;
    public FloatingUIActionPoints ActionPoints => actionPoints;

    /// <summary>
    /// 添加组件时自动缓存当前层级下已有的 Floating UI 组件。
    /// </summary>
    private void Reset()
    {
        CacheFloatingUIReferences();
        CacheChildButtons();
    }

    /// <summary>
    /// 运行时初始化 Floating UI 组件引用。
    /// </summary>
    private void Awake()
    {
        CacheMissingFloatingUIReferences();
        CacheChildButtons();
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
    /// 重置当前交互点绑定的行动点 UI。
    /// </summary>
    public void ResetActionPoints()
    {
        if (actionPoints == null)
        {
            Debug.LogWarning($"{nameof(FloatingUIManager)} 缺少 {nameof(FloatingUIActionPoints)} 引用：{name}", this);
            return;
        }

        actionPoints.ResetActionPoints();
    }

    /// <summary>
    /// 尝试从当前交互点绑定的行动点 UI 中消耗指定点数。
    /// </summary>
    public bool TrySpendActionPoints(int amount)
    {
        if (actionPoints == null)
        {
            Debug.LogWarning($"{nameof(FloatingUIManager)} 缺少 {nameof(FloatingUIActionPoints)} 引用：{name}", this);
            return false;
        }

        return actionPoints.TrySpend(amount);
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
        RegisterButtonClick(0, TrySpendAudioOneActionPoint);
        RegisterButtonClick(1, TrySpendAudioTwoActionPoints);
    }

    /// <summary>
    /// 移除音效房间行动点按钮点击事件。
    /// </summary>
    private void UnregisterChildButtonClicks()
    {
        UnregisterButtonClick(0, TrySpendAudioOneActionPoint);
        UnregisterButtonClick(1, TrySpendAudioTwoActionPoints);
    }

    /// <summary>
    /// 给指定子按钮注册点击事件，注册前先移除同一事件防止重复绑定。
    /// </summary>
    private void RegisterButtonClick(int buttonIndex, UnityEngine.Events.UnityAction action)
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
    private void UnregisterButtonClick(int buttonIndex, UnityEngine.Events.UnityAction action)
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
    /// 第一个子按钮：消耗 1 点音效房间行动点。
    /// </summary>
    private void TrySpendAudioOneActionPoint()
    {
        if (!TryGetGameFlowRunner(out GameFlowRunner runner))
        {
            return;
        }

        runner.TrySpendAudioOneActionPoint();
    }

    /// <summary>
    /// 第二个子按钮：消耗 2 点音效房间行动点。
    /// </summary>
    private void TrySpendAudioTwoActionPoints()
    {
        if (!TryGetGameFlowRunner(out GameFlowRunner runner))
        {
            return;
        }

        runner.TrySpendAudioTwoActionPoints();
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
}
