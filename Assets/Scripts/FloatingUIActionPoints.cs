using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class FloatingUIActionPoints : MonoBehaviour
{
    [Header("Action Points")]
    [SerializeField, Min(0)] private int startingActionPoints = 10;
    [SerializeField] private int[] cardCosts = { 2, 3, 4 };

    [Header("Feedback")]
    [SerializeField, Min(0.1f)] private float warningDuration = 1.2f;
    [SerializeField] private string warningText = "NOT ENOUGH AP!";

    private readonly List<Button> boundButtons = new List<Button>();
    private readonly List<UnityAction> boundCallbacks = new List<UnityAction>();
    private Canvas hudCanvas;
    private Text actionPointText;
    private Text warningLabel;
    private CanvasGroup warningGroup;
    private Coroutine warningRoutine;
    private int currentActionPoints;

    public int CurrentActionPoints => currentActionPoints;

    private void Awake()
    {
        ClampSettings();
        StartCoroutine(EnsureEventSystemAfterStartup());
        CreateHud();
        BindCardButtons();
        SetActionPoints(startingActionPoints);
        HideWarningImmediate();
    }

    private void OnValidate()
    {
        ClampSettings();
    }

    private void OnDestroy()
    {
        for (int i = 0; i < boundButtons.Count; i++)
        {
            if (boundButtons[i] != null && i < boundCallbacks.Count)
                boundButtons[i].onClick.RemoveListener(boundCallbacks[i]);
        }

        if (hudCanvas != null)
            Destroy(hudCanvas.gameObject);
    }

    public void SetActionPoints(int value)
    {
        currentActionPoints = Mathf.Max(0, value);
        RefreshValue();
    }

    public bool TrySpend(int amount)
    {
        amount = Mathf.Max(0, amount);

        if (currentActionPoints < amount)
        {
            ShowInsufficientWarning();
            return false;
        }

        SetActionPoints(currentActionPoints - amount);
        return true;
    }

    [ContextMenu("Reset Action Points")]
    public void ResetActionPoints()
    {
        SetActionPoints(startingActionPoints);
    }

    private void BindCardButtons()
    {
        boundButtons.Clear();
        boundCallbacks.Clear();

        for (int i = 0; i < 3; i++)
        {
            Transform card = transform.Find("Card_" + (i + 1));
            Button button = card != null ? card.GetComponent<Button>() : null;
            boundButtons.Add(button);

            if (button == null)
            {
                boundCallbacks.Add(null);
                continue;
            }

            int cardIndex = i;
            UnityAction callback = () => OnCardClicked(cardIndex);
            boundCallbacks.Add(callback);
            button.onClick.AddListener(callback);
        }
    }

    private void OnCardClicked(int cardIndex)
    {
        int cost = cardIndex >= 0 && cardIndex < cardCosts.Length
            ? Mathf.Max(0, cardCosts[cardIndex])
            : 0;

        TrySpend(cost);
    }

    private void CreateHud()
    {
        GameObject canvasObject = new GameObject(
            "ActionPointHUD",
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

        GameObject panelObject = new GameObject("ActionPointPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.SetParent(canvasObject.transform, false);
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(28f, -28f);
        panelRect.sizeDelta = new Vector2(250f, 76f);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.035f, 0.07f, 0.13f, 0.9f);
        panelImage.raycastTarget = false;

        actionPointText = CreateText(
            "ActionPointValue",
            panelRect,
            new Vector2(18f, -8f),
            new Vector2(214f, 60f),
            34,
            FontStyle.Bold,
            new Color(1f, 0.87f, 0.28f, 1f),
            TextAnchor.MiddleLeft);

        warningLabel = CreateText(
            "InsufficientWarning",
            canvasObject.transform as RectTransform,
            new Vector2(28f, -116f),
            new Vector2(390f, 54f),
            28,
            FontStyle.Bold,
            new Color(1f, 0.15f, 0.15f, 1f),
            TextAnchor.MiddleLeft);

        RectTransform warningRect = warningLabel.rectTransform;
        warningRect.anchorMin = new Vector2(0f, 1f);
        warningRect.anchorMax = new Vector2(0f, 1f);
        warningRect.pivot = new Vector2(0f, 1f);
        warningRect.anchoredPosition = new Vector2(28f, -116f);

        warningLabel.text = warningText;
        warningLabel.raycastTarget = false;
        warningGroup = warningLabel.gameObject.AddComponent<CanvasGroup>();
    }

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

    private void RefreshValue()
    {
        if (actionPointText != null)
            actionPointText.text = "AP  " + currentActionPoints;
    }

    private void ShowInsufficientWarning()
    {
        if (warningRoutine != null)
            StopCoroutine(warningRoutine);

        warningRoutine = StartCoroutine(PlayWarning());
    }

    private IEnumerator PlayWarning()
    {
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

    private void HideWarningImmediate()
    {
        if (warningGroup != null)
            warningGroup.alpha = 0f;
    }

    private static IEnumerator EnsureEventSystemAfterStartup()
    {
        yield return null;
        yield return null;

        if (FindObjectOfType<EventSystem>() != null)
            yield break;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private void ClampSettings()
    {
        startingActionPoints = Mathf.Max(0, startingActionPoints);
        warningDuration = Mathf.Max(0.1f, warningDuration);

        if (cardCosts == null || cardCosts.Length != 3)
            cardCosts = new[] { 2, 3, 4 };

        for (int i = 0; i < cardCosts.Length; i++)
            cardCosts[i] = Mathf.Max(0, cardCosts[i]);
    }
}
