using UnityEngine;

/// <summary>
/// 持有并转发单个交互点下 Floating UI 的基础行为。
/// </summary>
[DisallowMultipleComponent]
public sealed class FloatingUIManager : MonoBehaviour
{
    [Header("Floating UI")]
    [SerializeField, Tooltip("扇形 Floating UI 动画控制器。为空时会从当前物体子级查找。")]
    private FloatingUIFan floatingUIFan;

    [SerializeField, Tooltip("行动点 UI 控制器。为空时会从当前物体子级查找。")]
    private FloatingUIActionPoints actionPoints;

    public FloatingUIFan Fan => floatingUIFan;
    public FloatingUIActionPoints ActionPoints => actionPoints;

    /// <summary>
    /// 添加组件时自动缓存当前层级下已有的 Floating UI 组件。
    /// </summary>
    private void Reset()
    {
        CacheFloatingUIReferences();
    }

    /// <summary>
    /// 运行时初始化 Floating UI 组件引用。
    /// </summary>
    private void Awake()
    {
        CacheMissingFloatingUIReferences();
    }

    /// <summary>
    /// Inspector 修改后补齐缺失引用，方便搭房间预制体时少拖字段。
    /// </summary>
    private void OnValidate()
    {
        CacheMissingFloatingUIReferences();
    }

    /// <summary>
    /// 重新查找并缓存当前层级下的 Floating UI 组件。
    /// </summary>
    [ContextMenu("Refresh Floating UI References")]
    public void CacheFloatingUIReferences()
    {
        floatingUIFan = GetComponentInChildren<FloatingUIFan>(true);
        actionPoints = GetComponentInChildren<FloatingUIActionPoints>(true);
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
}
