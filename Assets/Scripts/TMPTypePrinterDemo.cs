using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[ExecuteAlways]
public sealed class TMPTypePrinterDemo : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField, TextArea(3, 8)] private string content = "Welcome to the TypePrinter demo.\nChange the text, swap the TMP font, tune typing speed, add a sound effect, or click the left mouse button to skip instantly.";
    [SerializeField, Tooltip("可选：拖入你自己的 TMP Font Asset。需要显示中文时，请替换成支持中文字符的字体。")]
    private TMP_FontAsset dialogueFont;

    [Header("Typing")]
    [SerializeField] private bool enableTypewriter = true;
    [SerializeField, Min(0f)] private float charactersPerSecond = 18f;
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool allowSkipWithLeftMouse = true;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip typeSound;
    [SerializeField, Range(0f, 1f)] private float typeSoundVolume = 1f;

    [Header("Replay Button")]
    [SerializeField] private Button replayButton;
    [SerializeField] private TMP_Text replayButtonLabel;
    [SerializeField] private string replayButtonText = "Replay Typewriter";

    [Header("Events")]
    [SerializeField] private UnityEvent onTypewriterStarted;
    [SerializeField] private UnityEvent onTypewriterCompleted;

    [Header("Auto Created")]
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private Image dialoguePanel;

    private Coroutine typingCoroutine;
    private bool isTyping;
    private bool isBindingButton;

    private void Reset()
    {
        EnsureDemoSetup();
        ApplyPresentation();
        ShowFullTextImmediate();
    }

    private void Awake()
    {
        EnsureDemoSetup();
        ApplyPresentation();
    }

    private void OnEnable()
    {
        EnsureDemoSetup();
        ApplyPresentation();
        RegisterReplayButton();

        if (!Application.isPlaying)
        {
            ShowFullTextImmediate();
            return;
        }

        if (playOnEnable)
        {
            PlayFromStart();
        }
        else
        {
            ShowFullTextImmediate();
        }
    }

    private void OnDisable()
    {
        UnregisterReplayButton();
        StopTyping();
    }

    private void OnValidate()
    {
        charactersPerSecond = Mathf.Max(0f, charactersPerSecond);

        EnsureDemoSetup();
        ApplyPresentation();

        if (!Application.isPlaying)
        {
            ShowFullTextImmediate();
        }
    }

    private void Update()
    {
        if (!Application.isPlaying || !isTyping || !enableTypewriter || !allowSkipWithLeftMouse)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            ShowInstantly();
        }
    }

    [ContextMenu("Play From Start")]
    public void PlayFromStart()
    {
        EnsureDemoSetup();
        ApplyPresentation();
        StopTyping();

        if (dialogueText == null)
        {
            return;
        }

        dialogueText.text = content ?? string.Empty;
        dialogueText.ForceMeshUpdate();

        onTypewriterStarted?.Invoke();

        int totalCharacters = dialogueText.textInfo.characterCount;
        if (!enableTypewriter || charactersPerSecond <= 0f || totalCharacters <= 0)
        {
            ShowFullTextImmediate();
            onTypewriterCompleted?.Invoke();
            return;
        }

        dialogueText.maxVisibleCharacters = 0;
        isTyping = true;
        typingCoroutine = StartCoroutine(TypeRoutine(totalCharacters));
    }

    [ContextMenu("Show Full Text")]
    public void ShowInstantly()
    {
        if (dialogueText == null)
        {
            return;
        }

        StopTyping();
        ShowFullTextImmediate();
        onTypewriterCompleted?.Invoke();
    }

    public void SetContent(string newContent, bool replay = true)
    {
        content = newContent ?? string.Empty;

        if (replay && Application.isPlaying)
        {
            PlayFromStart();
            return;
        }

        ApplyPresentation();
        ShowFullTextImmediate();
    }

    private IEnumerator TypeRoutine(int totalCharacters)
    {
        float visibleProgress = 0f;
        int revealedCharacters = 0;

        while (revealedCharacters < totalCharacters)
        {
            visibleProgress += charactersPerSecond * Time.deltaTime;
            int nextVisibleCount = Mathf.Clamp(Mathf.FloorToInt(visibleProgress), 0, totalCharacters);

            if (nextVisibleCount > revealedCharacters)
            {
                dialogueText.maxVisibleCharacters = nextVisibleCount;
                PlayTypingSoundRange(revealedCharacters, nextVisibleCount);
                revealedCharacters = nextVisibleCount;
            }

            yield return null;
        }

        isTyping = false;
        typingCoroutine = null;
        dialogueText.maxVisibleCharacters = totalCharacters;
        onTypewriterCompleted?.Invoke();
    }

    private void PlayTypingSoundRange(int startInclusive, int endExclusive)
    {
        if (audioSource == null || typeSound == null)
        {
            return;
        }

        for (int i = startInclusive; i < endExclusive; i++)
        {
            TMP_CharacterInfo character = dialogueText.textInfo.characterInfo[i];
            if (!character.isVisible)
            {
                continue;
            }

            audioSource.PlayOneShot(typeSound, typeSoundVolume);
        }
    }

    private void StopTyping()
    {
        isTyping = false;

        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }
    }

    private void ShowFullTextImmediate()
    {
        if (dialogueText == null)
        {
            return;
        }

        dialogueText.text = content ?? string.Empty;
        dialogueText.maxVisibleCharacters = int.MaxValue;
        dialogueText.ForceMeshUpdate();
    }

    private void ApplyPresentation()
    {
        if (dialogueText != null)
        {
            if (dialogueFont != null)
            {
                dialogueText.font = dialogueFont;
            }

            dialogueText.enableWordWrapping = true;
            dialogueText.fontSize = 44f;
            dialogueText.color = new Color32(245, 245, 245, 255);
            dialogueText.alignment = TextAlignmentOptions.TopLeft;
            dialogueText.richText = true;
        }

        if (replayButtonLabel != null)
        {
            replayButtonLabel.text = string.IsNullOrWhiteSpace(replayButtonText) ? "Replay Typewriter" : replayButtonText;
            replayButtonLabel.alignment = TextAlignmentOptions.Center;
            replayButtonLabel.fontSize = 28f;
            replayButtonLabel.color = Color.white;
        }

        if (dialoguePanel != null)
        {
            dialoguePanel.color = new Color32(18, 22, 30, 220);
        }
    }

    private void RegisterReplayButton()
    {
        if (isBindingButton || replayButton == null)
        {
            return;
        }

        replayButton.onClick.RemoveListener(PlayFromStart);
        replayButton.onClick.AddListener(PlayFromStart);
        isBindingButton = true;
    }

    private void UnregisterReplayButton()
    {
        if (!isBindingButton || replayButton == null)
        {
            return;
        }

        replayButton.onClick.RemoveListener(PlayFromStart);
        isBindingButton = false;
    }

    private void EnsureDemoSetup()
    {
        EnsureAudioSource();
        EnsureCanvas();
        EnsureDialoguePanel();
        EnsureDialogueText();
        EnsureReplayButton();
        EnsureEventSystem();
    }

    private void EnsureAudioSource()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
    }

    private void EnsureCanvas()
    {
        if (rootCanvas == null)
        {
            Transform existing = transform.Find("TypePrinterCanvas");
            if (existing != null)
            {
                rootCanvas = existing.GetComponent<Canvas>();
            }
        }

        if (rootCanvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("TypePrinterCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        rootCanvas = canvasObject.GetComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.pixelPerfect = false;
        rootCanvas.sortingOrder = 0;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private void EnsureDialoguePanel()
    {
        if (dialoguePanel == null && rootCanvas != null)
        {
            Transform existing = rootCanvas.transform.Find("DialoguePanel");
            if (existing != null)
            {
                dialoguePanel = existing.GetComponent<Image>();
            }
        }

        if (dialoguePanel != null || rootCanvas == null)
        {
            return;
        }

        GameObject panelObject = new GameObject("DialoguePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelObject.transform.SetParent(rootCanvas.transform, false);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.sizeDelta = new Vector2(1220f, 300f);
        panelRect.anchoredPosition = new Vector2(0f, 56f);

        dialoguePanel = panelObject.GetComponent<Image>();
    }

    private void EnsureDialogueText()
    {
        if (dialogueText == null && dialoguePanel != null)
        {
            Transform existing = dialoguePanel.transform.Find("DialogueText");
            if (existing != null)
            {
                dialogueText = existing.GetComponent<TextMeshProUGUI>();
            }
        }

        if (dialogueText != null || dialoguePanel == null)
        {
            return;
        }

        GameObject textObject = new GameObject("DialogueText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(dialoguePanel.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(36f, 32f);
        textRect.offsetMax = new Vector2(-36f, -32f);

        dialogueText = textObject.GetComponent<TextMeshProUGUI>();
        dialogueText.text = content ?? string.Empty;
    }

    private void EnsureReplayButton()
    {
        if (replayButton == null && rootCanvas != null)
        {
            Transform existing = rootCanvas.transform.Find("ReplayButton");
            if (existing != null)
            {
                replayButton = existing.GetComponent<Button>();
            }
        }

        if (replayButton != null)
        {
            if (replayButtonLabel == null)
            {
                replayButtonLabel = replayButton.GetComponentInChildren<TMP_Text>(true);
            }

            return;
        }

        if (rootCanvas == null)
        {
            return;
        }

        GameObject buttonObject = new GameObject("ReplayButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(rootCanvas.transform, false);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 0f);
        buttonRect.anchorMax = new Vector2(1f, 0f);
        buttonRect.pivot = new Vector2(1f, 0f);
        buttonRect.sizeDelta = new Vector2(260f, 72f);
        buttonRect.anchoredPosition = new Vector2(-32f, 388f);

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color32(47, 110, 196, 255);

        replayButton = buttonObject.GetComponent<Button>();
        ColorBlock colors = replayButton.colors;
        colors.normalColor = new Color32(47, 110, 196, 255);
        colors.highlightedColor = new Color32(72, 136, 223, 255);
        colors.pressedColor = new Color32(34, 83, 154, 255);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color32(70, 70, 70, 180);
        replayButton.colors = colors;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        replayButtonLabel = labelObject.GetComponent<TextMeshProUGUI>();
    }

    private void EnsureEventSystem()
    {
        EventSystem existingEventSystem = EventSystem.current;
        if (existingEventSystem == null)
        {
            existingEventSystem = Object.FindObjectOfType<EventSystem>();
        }

        if (existingEventSystem != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        eventSystemObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
    }
}
