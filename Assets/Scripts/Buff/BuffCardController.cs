using TMPro;
using UnityEngine;

/// <summary>
/// 管理单个 BuffCard 的数据注入和文本显示。
/// </summary>
[DisallowMultipleComponent]
public class BuffCardController : MonoBehaviour
{
    [Header("View")]
    [SerializeField, Tooltip("显示 BuffCard 内容的 TextMeshProUGUI；留空时自动查找自身或子物体上的第一个 TextMeshProUGUI。")]
    private TextMeshProUGUI contentText;
    private string currentContent = string.Empty;

    /// <summary>
    /// 当前 BuffCard 持有的文本内容，只读暴露给外部查询。
    /// </summary>
    public string CurrentContent => currentContent;

    /// <summary>
    /// 初始化文本引用，避免外部注入时找不到显示组件。
    /// </summary>
    private void Awake()
    {
        EnsureReferences();
    }

    /// <summary>
    /// 从外部注入 BuffCard 文本数据，并立即刷新显示。
    /// </summary>
    public void InjectData(string content)
    {
        currentContent = content ?? string.Empty;
        RefreshContentText();
    }

    /// <summary>
    /// 根据当前文本内容刷新 TextMeshProUGUI。
    /// </summary>
    private void RefreshContentText()
    {
        EnsureReferences();

        if (contentText == null)
        {
            Debug.LogWarning($"{nameof(BuffCardController)} cannot find TextMeshProUGUI on self or children.", this);
            return;
        }

        contentText.text = currentContent;
    }

    /// <summary>
    /// 缓存 TextMeshProUGUI 引用，优先使用 Inspector 手动配置的组件。
    /// </summary>
    private void EnsureReferences()
    {
        if (contentText != null)
        {
            return;
        }

        contentText = GetComponent<TextMeshProUGUI>();
        if (contentText == null)
        {
            contentText = GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }
}
