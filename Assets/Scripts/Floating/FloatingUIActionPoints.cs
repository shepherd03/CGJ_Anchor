using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class FloatingUIActionPoints : MonoBehaviour
{
    [Header("Feedback")]
    [Tooltip("行动点不足提示的显示时长。")]
    [SerializeField, Min(0.1f)] private float warningDuration = 1.2f;

    [Tooltip("外部行动点不足时显示的提示文本。")]
    [SerializeField] private string warningText = "NOT ENOUGH AP!";

    // 只负责外部行动点不足提示，不再持有或显示内部行动点数值。
    private Canvas hudCanvas;
    private Text warningLabel;
    private CanvasGroup warningGroup;
    private Coroutine warningRoutine;

    /// <summary>
    /// 初始化 Floating UI 的行动点不足提示，不再创建左上角 AP 数值面板。
    /// </summary>
    private void Awake()
    {
        ClampSettings();
        StartCoroutine(EnsureEventSystemAfterStartup());
        CreateWarningHud();
        HideWarningImmediate();
    }

    /// <summary>
    /// 组件启用时恢复行动点不足提示画布。
    /// </summary>
    private void OnEnable()
    {
        SetWarningCanvasVisible(true);
    }

    /// <summary>
    /// 组件关闭时隐藏提示画布并停止警告动画。
    /// </summary>
    private void OnDisable()
    {
        StopWarningRoutine();
        HideWarningImmediate();
        SetWarningCanvasVisible(false);
    }

    /// <summary>
    /// Inspector 修改后修正提示参数。
    /// </summary>
    private void OnValidate()
    {
        ClampSettings();
    }

    /// <summary>
    /// 对象销毁时停止提示动画并销毁动态提示画布。
    /// </summary>
    private void OnDestroy()
    {
        StopWarningRoutine();

        if (hudCanvas != null)
            Destroy(hudCanvas.gameObject);
    }

    /// <summary>
    /// 显示外部行动点不足提示。
    /// </summary>
    public void ShowInsufficientWarning()
    {
        if (warningLabel == null || warningGroup == null)
        {
            return;
        }

        if (warningRoutine != null)
            StopCoroutine(warningRoutine);

        warningRoutine = StartCoroutine(PlayWarning());
    }

    /// <summary>
    /// 创建只用于行动点不足反馈的动态画布。
    /// </summary>
    private void CreateWarningHud()
    {
        GameObject canvasObject = new GameObject(
            "ActionPointWarningHUD",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        hudCanvas = canvasObject.GetComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        warningLabel = CreateText(
            "InsufficientWarning",
            canvasObject.transform as RectTransform,
            new Vector2(28f, -28f),
            new Vector2(390f, 54f),
            28,
            FontStyle.Bold,
            new Color(1f, 0.15f, 0.15f, 1f),
            TextAnchor.MiddleLeft);

        RectTransform warningRect = warningLabel.rectTransform;
        warningRect.anchorMin = new Vector2(0f, 1f);
        warningRect.anchorMax = new Vector2(0f, 1f);
        warningRect.pivot = new Vector2(0f, 1f);
        warningRect.anchoredPosition = new Vector2(28f, -28f);

        warningLabel.text = warningText;
        warningLabel.raycastTarget = false;
        warningGroup = warningLabel.gameObject.AddComponent<CanvasGroup>();
    }

    /// <summary>
    /// 创建提示文本节点。
    /// </summary>
    private static Text CreateText(
        string objectName,
        RectTransform parent,
        Vector2 anchoredPosition,
        Vector2 size,
        int fontSize,
        FontStyle fontStyle,
        Color color,
        TextAnchor alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    /// <summary>
    /// 播放行动点不足提示的淡出动画。
    /// </summary>
    private IEnumerator PlayWarning()
    {
        if (warningLabel == null || warningGroup == null)
        {
            warningRoutine = null;
            yield break;
        }

        warningLabel.text = warningText;
        warningGroup.alpha = 1f;

        float holdDuration = warningDuration * 0.55f;
        float fadeDuration = Mathf.Max(0.01f, warningDuration - holdDuration);
        float elapsed = 0f;

        while (elapsed < holdDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            warningGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }

        HideWarningImmediate();
        warningRoutine = null;
    }

    /// <summary>
    /// 立即隐藏行动点不足提示。
    /// </summary>
    private void HideWarningImmediate()
    {
        if (warningGroup != null)
            warningGroup.alpha = 0f;
    }

    /// <summary>
    /// 停止行动点不足警告动画。
    /// </summary>
    private void StopWarningRoutine()
    {
        if (warningRoutine == null)
            return;

        StopCoroutine(warningRoutine);
        warningRoutine = null;
    }

    /// <summary>
    /// 控制动态创建的行动点不足提示画布显示或隐藏。
    /// </summary>
    private void SetWarningCanvasVisible(bool isVisible)
    {
        if (hudCanvas != null)
            hudCanvas.gameObject.SetActive(isVisible);
    }

    /// <summary>
    /// 确保场景中存在 UGUI 事件系统，保证 Floating UI 按钮能响应点击。
    /// </summary>
    private static IEnumerator EnsureEventSystemAfterStartup()
    {
        yield return null;
        yield return null;

        if (FindObjectOfType<EventSystem>() != null)
            yield break;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    /// <summary>
    /// 修正行动点不足提示参数。
    /// </summary>
    private void ClampSettings()
    {
        warningDuration = Mathf.Max(0.1f, warningDuration);
    }
}
