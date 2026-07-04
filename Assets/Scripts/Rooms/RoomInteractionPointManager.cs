using UnityEngine;

/// <summary>
/// 管理房间内单个交互点，并持有该交互点对应的 Floating UI。
/// </summary>
[DisallowMultipleComponent]
public sealed class RoomInteractionPointManager : MonoBehaviour
{
    [Header("Interaction Point")]
    [SerializeField, Tooltip("交互点唯一标识。为空时默认使用当前物体名。")]
    private string pointId;

    [SerializeField, Tooltip("当前交互点是否允许交互。")]
    private bool interactable = true;

    [Header("Floating UI")]
    [SerializeField, Tooltip("当前交互点控制的 Floating UI 管理器。为空时会从当前物体子级查找。")]
    private FloatingUIManager floatingUIManager;

    public string PointId => pointId;
    public bool IsInteractable => interactable;
    public FloatingUIManager FloatingUI => floatingUIManager;

    /// <summary>
    /// 添加组件时自动初始化交互点标识和 Floating UI 引用。
    /// </summary>
    private void Reset()
    {
        ResolveDefaultPointId();
        CacheFloatingUIManager();
    }

    /// <summary>
    /// 运行时初始化交互点基础引用。
    /// </summary>
    private void Awake()
    {
        ResolveDefaultPointId();
        CacheMissingFloatingUIManager();
    }

    /// <summary>
    /// Inspector 修改后补齐默认值，避免空 ID 和空引用造成后续查找失败。
    /// </summary>
    private void OnValidate()
    {
        ResolveDefaultPointId();
        CacheMissingFloatingUIManager();
    }

    /// <summary>
    /// 设置当前交互点是否允许交互。
    /// </summary>
    public void SetInteractable(bool value)
    {
        interactable = value;
    }

    /// <summary>
    /// 缓存当前交互点层级下的 Floating UI 管理器。
    /// </summary>
    [ContextMenu("Refresh Floating UI Manager")]
    public void CacheFloatingUIManager()
    {
        floatingUIManager = GetComponentInChildren<FloatingUIManager>(true);
    }

    /// <summary>
    /// 打开当前交互点绑定的 Floating UI。
    /// </summary>
    public void OpenFloatingUI()
    {
        if (!interactable)
        {
            return;
        }

        if (!TryGetFloatingUIManager(out FloatingUIManager manager))
        {
            return;
        }

        manager.OpenFan();
    }

    /// <summary>
    /// 关闭当前交互点绑定的 Floating UI。
    /// </summary>
    public void CloseFloatingUI()
    {
        if (!TryGetFloatingUIManager(out FloatingUIManager manager))
        {
            return;
        }

        manager.CloseFan();
    }

    /// <summary>
    /// 重置当前交互点绑定的行动点 UI。
    /// </summary>
    public void ResetFloatingUIActionPoints()
    {
        if (!TryGetFloatingUIManager(out FloatingUIManager manager))
        {
            return;
        }

        manager.ResetActionPoints();
    }

    /// <summary>
    /// 尝试消耗当前交互点绑定的行动点。
    /// </summary>
    public bool TrySpendActionPoints(int amount)
    {
        if (!interactable)
        {
            return false;
        }

        if (!TryGetFloatingUIManager(out FloatingUIManager manager))
        {
            return false;
        }

        return manager.TrySpendActionPoints(amount);
    }

    /// <summary>
    /// 获取当前交互点绑定的 Floating UI 管理器。
    /// </summary>
    public bool TryGetFloatingUIManager(out FloatingUIManager manager)
    {
        CacheMissingFloatingUIManager();
        manager = floatingUIManager;

        if (manager != null)
        {
            return true;
        }

        Debug.LogWarning($"{nameof(RoomInteractionPointManager)} 缺少 {nameof(FloatingUIManager)} 引用：{name}", this);
        return false;
    }

    /// <summary>
    /// 只在 ID 为空时使用物体名作为默认交互点标识。
    /// </summary>
    private void ResolveDefaultPointId()
    {
        if (string.IsNullOrWhiteSpace(pointId))
        {
            pointId = name;
        }
    }

    /// <summary>
    /// 只补齐空 Floating UI 引用，避免覆盖 Inspector 手动配置。
    /// </summary>
    private void CacheMissingFloatingUIManager()
    {
        if (floatingUIManager == null)
        {
            floatingUIManager = GetComponentInChildren<FloatingUIManager>(true);
        }
    }
}
