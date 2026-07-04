using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 管理单个 BuffCard 的数据注入、文本显示和图标显示。
/// </summary>
[DisallowMultipleComponent]
public class BuffCardController : MonoBehaviour
{
    [Header("View")]
    [SerializeField, Tooltip("显示 BuffCard 内容的 TextMeshProUGUI；留空时自动查找自身或子物体上的第一个 TextMeshProUGUI。")]
    private TextMeshProUGUI contentText;

    [SerializeField, Tooltip("显示 BuffCard 图标的 Image；留空时优先查找 Icon/IconImage/BuffIcon 子物体，最后使用当前物体上的 Image。")]
    private Image iconImage;

    // 当前 BuffCard 持有的文本内容，用于重复刷新显示。
    private string currentContent = string.Empty;

    // 当前 BuffCard 持有的图标 Sprite，用于重复刷新显示。
    private Sprite currentIcon;

    /// <summary>
    /// 当前 BuffCard 持有的文本内容，只读暴露给外部查询。
    /// </summary>
    public string CurrentContent => currentContent;

    /// <summary>
    /// 当前 BuffCard 持有的图标，只读暴露给外部查询。
    /// </summary>
    public Sprite CurrentIcon => currentIcon;

    /// <summary>
    /// 初始化文本和图标引用，避免外部注入时找不到显示组件。
    /// </summary>
    private void Awake()
    {
        EnsureReferences();
    }

    /// <summary>
    /// 从外部注入 BuffCard 文本数据，并立即刷新文本显示。
    /// </summary>
    public void InjectData(string content)
    {
        currentContent = content ?? string.Empty;
        RefreshContentText();
    }

    /// <summary>
    /// 从外部注入 BuffCard 文本和图标数据，并立即刷新显示。
    /// </summary>
    public void InjectData(string content, Sprite icon)
    {
        currentContent = content ?? string.Empty;
        currentIcon = icon;
        RefreshContentText();
        RefreshIconImage();
    }

    /// <summary>
    /// 根据当前文本内容刷新 TextMeshProUGUI。
    /// </summary>
    private void RefreshContentText()
    {
        EnsureReferences();

        if (contentText == null)
        {
            return;
        }

        contentText.text = currentContent;
    }

    /// <summary>
    /// 根据当前图标刷新 Image；没有图标时只保留原显示，不强行隐藏根节点按钮图。
    /// </summary>
    private void RefreshIconImage()
    {
        EnsureReferences();

        if (iconImage == null)
        {
            if (currentIcon != null)
            {
                Debug.LogWarning($"{nameof(BuffCardController)} cannot find Image on self or children.", this);
            }

            return;
        }

        if (currentIcon == null)
        {
            return;
        }

        iconImage.sprite = currentIcon;
        iconImage.preserveAspect = true;
    }

    /// <summary>
    /// 缓存 TextMeshProUGUI 和 Image 引用，优先使用 Inspector 手动配置的组件。
    /// </summary>
    private void EnsureReferences()
    {
        if (contentText == null)
        {
            contentText = GetComponent<TextMeshProUGUI>();
            if (contentText == null)
            {
                contentText = GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }

        if (iconImage == null)
        {
            iconImage = FindIconImage();
        }
    }

    /// <summary>
    /// 查找 BuffCard 图标 Image，优先使用明确命名的子节点，兜底使用当前节点 Image。
    /// </summary>
    private Image FindIconImage()
    {
        Image[] images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
            {
                continue;
            }

            string imageName = image.name;
            if (imageName == "Icon" || imageName == "IconImage" || imageName == "BuffIcon")
            {
                return image;
            }
        }

        return GetComponent<Image>();
    }
}
