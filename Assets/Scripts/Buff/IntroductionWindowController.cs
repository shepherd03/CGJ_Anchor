using TMPro;
using UnityEngine;

/// <summary>
/// 管理 BuffWindow 二级介绍弹窗的标题、简介和正文文本。
/// </summary>
[DisallowMultipleComponent]
public sealed class IntroductionWindowController : MonoBehaviour
{
    [Header("Text")]
    [SerializeField, Tooltip("显示 Buff 标题的 TextMeshProUGUI。")]
    private TextMeshProUGUI titleText;

    [SerializeField, Tooltip("显示 Buff 简介的 TextMeshProUGUI。")]
    private TextMeshProUGUI briefText;

    [SerializeField, Tooltip("显示 Buff 正文说明的 TextMeshProUGUI。")]
    private TextMeshProUGUI contentText;

    // 当前标题文本，用于外部查询和重复刷新。
    private string currentTitle = string.Empty;

    // 当前简介文本，用于外部查询和重复刷新。
    private string currentBrief = string.Empty;

    // 当前正文文本，用于外部查询和重复刷新。
    private string currentContent = string.Empty;

    /// <summary>
    /// 当前弹窗标题，只读暴露给外部查询。
    /// </summary>
    public string CurrentTitle => currentTitle;

    /// <summary>
    /// 当前弹窗简介，只读暴露给外部查询。
    /// </summary>
    public string CurrentBrief => currentBrief;

    /// <summary>
    /// 当前弹窗正文，只读暴露给外部查询。
    /// </summary>
    public string CurrentContent => currentContent;

    /// <summary>
    /// 初始化文本引用，避免首次注入数据时找不到显示组件。
    /// </summary>
    private void Awake()
    {
        EnsureTextReferences();
    }

    /// <summary>
    /// 打开 Buff 介绍二级弹窗。
    /// </summary>
    public void Open()
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// 关闭 Buff 介绍二级弹窗。
    /// </summary>
    public void Close()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 从外部注入完整介绍弹窗数据，并立即刷新三个文本。
    /// </summary>
    public void InjectData(string title, string brief, string content)
    {
        SetTitle(title);
        SetBrief(brief);
        SetContent(content);
    }

    /// <summary>
    /// 设置 Buff 标题文本。
    /// </summary>
    public void SetTitle(string title)
    {
        currentTitle = title ?? string.Empty;
        EnsureTextReferences();
        SetText(titleText, currentTitle);
    }

    /// <summary>
    /// 设置 Buff 简介文本。
    /// </summary>
    public void SetBrief(string brief)
    {
        currentBrief = brief ?? string.Empty;
        EnsureTextReferences();
        SetText(briefText, currentBrief);
    }

    /// <summary>
    /// 设置 Buff 正文说明文本。
    /// </summary>
    public void SetContent(string content)
    {
        currentContent = content ?? string.Empty;
        EnsureTextReferences();
        SetText(contentText, currentContent);
    }

    /// <summary>
    /// 清空介绍弹窗的全部文本。
    /// </summary>
    public void Clear()
    {
        InjectData(string.Empty, string.Empty, string.Empty);
    }

    /// <summary>
    /// 缓存三个 TextMeshProUGUI 引用，优先使用 Inspector 手动配置的组件。
    /// </summary>
    private void EnsureTextReferences()
    {
        TextMeshProUGUI[] cachedTexts = null;

        if (titleText == null)
        {
            titleText = FindTextByName("Title", ref cachedTexts);
        }

        if (briefText == null)
        {
            briefText = FindTextByName("Brief", ref cachedTexts);
        }

        if (contentText == null)
        {
            contentText = FindTextByName("Content", ref cachedTexts);
        }
    }

    /// <summary>
    /// 按子物体名称查找对应的 TextMeshProUGUI。
    /// </summary>
    private TextMeshProUGUI FindTextByName(string textName, ref TextMeshProUGUI[] cachedTexts)
    {
        if (cachedTexts == null)
        {
            cachedTexts = GetComponentsInChildren<TextMeshProUGUI>(true);
        }

        for (int i = 0; i < cachedTexts.Length; i++)
        {
            TextMeshProUGUI text = cachedTexts[i];
            if (text != null && text.name == textName)
            {
                return text;
            }
        }

        return null;
    }

    /// <summary>
    /// 设置 TMP 文本，未绑定时直接跳过。
    /// </summary>
    private static void SetText(TextMeshProUGUI text, string value)
    {
        if (text != null)
        {
            text.text = value;
        }
    }

    /// <summary>
    /// 编辑器添加组件时按 Title、Brief、Content 子物体名自动填充文本引用。
    /// </summary>
    private void Reset()
    {
        EnsureTextReferences();
    }
}
