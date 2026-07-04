using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    [Header("Button")]
    [SerializeField, Tooltip("点击后关闭 Buff 介绍二级弹窗的按钮。")]
    private Button closeButton;

    [SerializeField, Tooltip("点击后购买当前 Buff 的按钮。购买入口未接入前只关闭弹窗。")]
    private Button buyButton;

    // 当前标题文本，用于外部查询和重复刷新。
    private string currentTitle = string.Empty;

    // 当前简介文本，用于外部查询和重复刷新。
    private string currentBrief = string.Empty;

    // 当前正文文本，用于外部查询和重复刷新。
    private string currentContent = string.Empty;

    // 当前购买按钮回调；由 WindowShopPanelManager 注入真实购买逻辑。
    private Action buyAction;

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
        EnsureButtonReferences();
    }

    /// <summary>
    /// 弹窗启用时注册关闭和购买按钮点击事件。
    /// </summary>
    private void OnEnable()
    {
        RegisterCloseButtonClick();
        RegisterBuyButtonClick();
    }

    /// <summary>
    /// 弹窗禁用时注销按钮点击事件，避免重复绑定。
    /// </summary>
    private void OnDisable()
    {
        UnregisterCloseButtonClick();
        UnregisterBuyButtonClick();
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
    /// 设置购买按钮点击后的业务回调，未设置时购买按钮只关闭弹窗。
    /// </summary>
    public void SetBuyAction(Action action)
    {
        buyAction = action;
    }

    /// <summary>
    /// 点击关闭按钮后关闭 Buff 介绍二级弹窗。
    /// </summary>
    private void OnCloseButtonClicked()
    {
        Close();
    }

    /// <summary>
    /// 点击购买按钮后执行购买逻辑；购买入口未接入前只关闭弹窗。
    /// </summary>
    private void OnBuyButtonClicked()
    {
        if (buyAction != null)
        {
            buyAction.Invoke();
            return;
        }

        Close();
    }

    /// <summary>
    /// 给关闭按钮注册点击事件。
    /// </summary>
    private void RegisterCloseButtonClick()
    {
        EnsureButtonReferences();

        if (closeButton == null)
        {
            Debug.LogWarning($"{nameof(IntroductionWindowController)} needs a close button.", this);
            return;
        }

        closeButton.onClick.RemoveListener(OnCloseButtonClicked);
        closeButton.onClick.AddListener(OnCloseButtonClicked);
    }

    /// <summary>
    /// 给购买按钮注册点击事件。
    /// </summary>
    private void RegisterBuyButtonClick()
    {
        EnsureButtonReferences();

        if (buyButton == null)
        {
            Debug.LogWarning($"{nameof(IntroductionWindowController)} needs a buy button.", this);
            return;
        }

        buyButton.onClick.RemoveListener(OnBuyButtonClicked);
        buyButton.onClick.AddListener(OnBuyButtonClicked);
    }

    /// <summary>
    /// 移除关闭按钮点击事件。
    /// </summary>
    private void UnregisterCloseButtonClick()
    {
        if (closeButton == null)
        {
            return;
        }

        closeButton.onClick.RemoveListener(OnCloseButtonClicked);
    }

    /// <summary>
    /// 移除购买按钮点击事件。
    /// </summary>
    private void UnregisterBuyButtonClick()
    {
        if (buyButton == null)
        {
            return;
        }

        buyButton.onClick.RemoveListener(OnBuyButtonClicked);
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
    /// 缓存关闭和购买按钮引用，优先使用 Inspector 手动配置的按钮。
    /// </summary>
    private void EnsureButtonReferences()
    {
        Button[] cachedButtons = null;

        if (closeButton == null)
        {
            closeButton = FindButtonByName("Close", ref cachedButtons);
        }

        if (buyButton == null)
        {
            buyButton = FindButtonByName("Buy", ref cachedButtons);
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
    /// 按子物体名称查找对应的 Button。
    /// </summary>
    private Button FindButtonByName(string buttonName, ref Button[] cachedButtons)
    {
        if (cachedButtons == null)
        {
            cachedButtons = GetComponentsInChildren<Button>(true);
        }

        for (int i = 0; i < cachedButtons.Length; i++)
        {
            Button button = cachedButtons[i];
            if (button != null && button.name == buttonName)
            {
                return button;
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
        EnsureButtonReferences();
    }
}
