using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 管理单个 BuffCard 的 Cost 显示和图标显示。
/// </summary>
[DisallowMultipleComponent]
public class BuffCardController : MonoBehaviour
{
    [Header("View")]
    [SerializeField, Tooltip("手动拖拽的 Cost TextMeshProUGUI，只用于显示 Buff 的 Cost。")]
    private TextMeshProUGUI costText;

    [SerializeField, Tooltip("显示 BuffCard 图标的 Image；留空时优先查找 Icon/IconImage/BuffIcon 子物体，最后使用当前物体上的 Image。")]
    private Image iconImage;

    // 当前 BuffCard 持有的 Cost 数值，用于重复刷新显示。
    private int currentCost;

    // 当前 BuffCard 持有的图标 Sprite，用于重复刷新显示。
    private Sprite currentIcon;

    /// <summary>
    /// 当前 BuffCard 持有的 Cost 数值，只读暴露给外部查询。
    /// </summary>
    public int CurrentCost => currentCost;

    /// <summary>
    /// 当前 BuffCard 持有的图标，只读暴露给外部查询。
    /// </summary>
    public Sprite CurrentIcon => currentIcon;

    /// <summary>
    /// 初始化图标引用；Cost 文本必须手动拖拽，避免自动查找误绑到其他 TMP。
    /// </summary>
    private void Awake()
    {
        EnsureIconReference();
    }

    /// <summary>
    /// 从外部注入 BuffCard Cost 数据，并立即刷新 Cost 显示。
    /// </summary>
    public void InjectData(int cost)
    {
        currentCost = cost;
        RefreshCostText();
    }

    /// <summary>
    /// 从外部注入 BuffCard Cost 和图标数据，并立即刷新显示。
    /// </summary>
    public void InjectData(int cost, Sprite icon)
    {
        currentCost = cost;
        currentIcon = icon;
        RefreshCostText();
        RefreshIconImage();
    }

    /// <summary>
    /// 根据当前 Cost 刷新手动绑定的 TextMeshProUGUI。
    /// </summary>
    private void RefreshCostText()
    {
        if (costText == null)
        {
            Debug.LogWarning($"{nameof(BuffCardController)} needs a manually assigned Cost TextMeshProUGUI.", this);
            return;
        }

        costText.text = currentCost.ToString();
    }

    /// <summary>
    /// 根据当前图标刷新 Image；没有图标时只保留原显示，不强行隐藏根节点按钮图。
    /// </summary>
    private void RefreshIconImage()
    {
        EnsureIconReference();

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
    /// 缓存 Image 引用，优先使用 Inspector 手动配置的组件。
    /// </summary>
    private void EnsureIconReference()
    {
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
