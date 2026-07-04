using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
[RequireComponent(typeof(Image))]
public sealed class FloatingUIButtonVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private enum VisualState
    {
        Normal,
        Hover,
        Pressed,
        Released,
        Disabled
    }

    [Serializable]
    private sealed class SpriteSlots
    {
        public Sprite normal;
        public Sprite hover;
        public Sprite pressed;
        public Sprite released;
        public Sprite disabled;

        public Sprite Get(VisualState state)
        {
            switch (state)
            {
                case VisualState.Hover:
                    return hover != null ? hover : normal;
                case VisualState.Pressed:
                    return pressed != null ? pressed : Get(VisualState.Hover);
                case VisualState.Released:
                    return released != null ? released : Get(VisualState.Hover);
                case VisualState.Disabled:
                    return disabled != null ? disabled : normal;
                default:
                    return normal;
            }
        }
    }

    [Serializable]
    private sealed class ColorStates
    {
        public Color normal = Color.clear;
        public Color hover = new Color(1f, 1f, 1f, 0.04f);
        public Color pressed = new Color(1f, 1f, 1f, 0.08f);
        public Color released = new Color(1f, 1f, 1f, 0.05f);
        public Color disabled = new Color(1f, 1f, 1f, 0.03f);

        public Color Get(VisualState state)
        {
            switch (state)
            {
                case VisualState.Hover:
                    return hover;
                case VisualState.Pressed:
                    return pressed;
                case VisualState.Released:
                    return released;
                case VisualState.Disabled:
                    return disabled;
                default:
                    return normal;
            }
        }
    }

    [Serializable]
    private sealed class FloatStates
    {
        public float normal = 1f;
        public float hover = 1.01f;
        public float pressed = 0.985f;
        public float released = 1.02f;
        public float disabled = 1f;

        public float Get(VisualState state)
        {
            switch (state)
            {
                case VisualState.Hover:
                    return hover;
                case VisualState.Pressed:
                    return pressed;
                case VisualState.Released:
                    return released;
                case VisualState.Disabled:
                    return disabled;
                default:
                    return normal;
            }
        }
    }

    [Serializable]
    private sealed class TransitionSettings
    {
        public float colorDuration = 0.12f;
        public float scaleDuration = 0.14f;
        public float releasedHoldDuration = 0.08f;
        public Ease ease = Ease.OutQuad;
    }

    [Header("Slots")]
    [SerializeField] private Image backgroundSlot;
    [SerializeField] private Image frameOverlaySlot;
    [SerializeField] private Image hoverOverlaySlot;
    [SerializeField] private Image pressOverlaySlot;
    [SerializeField] private Image releaseOverlaySlot;
    [SerializeField] private Image[] fallbackFrameSlots;

    [Header("Future Art Sprites")]
    [SerializeField] private SpriteSlots backgroundSprites = new SpriteSlots();
    [SerializeField] private SpriteSlots frameOverlaySprites = new SpriteSlots();
    [SerializeField] private SpriteSlots hoverOverlaySprites = new SpriteSlots();
    [SerializeField] private SpriteSlots pressOverlaySprites = new SpriteSlots();
    [SerializeField] private SpriteSlots releaseOverlaySprites = new SpriteSlots();

    [Header("Current Placeholder Colors")]
    [SerializeField] private ColorStates backgroundColors = new ColorStates();
    [SerializeField] private ColorStates fallbackFrameColors = new ColorStates
    {
        normal = new Color(1f, 1f, 1f, 0.75f),
        hover = new Color(1f, 1f, 1f, 1f),
        pressed = new Color(0.85f, 0.92f, 1f, 1f),
        released = new Color(1f, 1f, 1f, 1f),
        disabled = new Color(1f, 1f, 1f, 0.3f)
    };
    [SerializeField] private ColorStates frameOverlayColors = new ColorStates
    {
        normal = Color.clear,
        hover = Color.clear,
        pressed = Color.clear,
        released = Color.clear,
        disabled = Color.clear
    };
    [SerializeField] private ColorStates hoverOverlayColors = new ColorStates
    {
        normal = Color.clear,
        hover = new Color(0.65f, 0.86f, 1f, 0.12f),
        pressed = Color.clear,
        released = Color.clear,
        disabled = Color.clear
    };
    [SerializeField] private ColorStates pressOverlayColors = new ColorStates
    {
        normal = Color.clear,
        hover = Color.clear,
        pressed = new Color(0f, 0f, 0f, 0.12f),
        released = Color.clear,
        disabled = Color.clear
    };
    [SerializeField] private ColorStates releaseOverlayColors = new ColorStates
    {
        normal = Color.clear,
        hover = Color.clear,
        pressed = Color.clear,
        released = new Color(1f, 1f, 1f, 0.14f),
        disabled = Color.clear
    };

    [Header("Motion")]
    [SerializeField] private Vector3 baseScale = Vector3.one;
    [SerializeField] private FloatStates scaleStates = new FloatStates();
    [SerializeField] private TransitionSettings transitions = new TransitionSettings();
    [SerializeField] private bool syncBaseScaleFromCurrentBeforeHover = true;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField, Tooltip("鼠标悬浮在这张 Card 上时播放。")]
    private AudioClip hoverSound;
    [SerializeField, Tooltip("按下这张 Card 时播放。")]
    private AudioClip pressedSound;

    [Header("Sprite Background")]
    [SerializeField] private bool disableBrightShadowEffectsWhenSpritePresent = true;
    [SerializeField] private bool preserveSpriteOriginalColor = true;
    [SerializeField, Range(0f, 1f)] private float brightShadowLuminanceThreshold = 0.6f;
    [SerializeField, Range(0f, 1f)] private float brightShadowAlphaThreshold = 0.1f;

    private Button button;
    private RectTransform rectTransform;
    private BaseMeshEffect[] backgroundEffects;
    private bool[] authoredBackgroundEffectStates;
    private ScreenSpaceEdgeFlame flameEffect;
    private bool isPointerInside;
    private bool isPointerDown;
    private Sequence releaseSequence;
    private Tween scaleTween;
    private Sprite authoredBackgroundSprite;
    private Sprite authoredFrameOverlaySprite;
    private Sprite authoredHoverOverlaySprite;
    private Sprite authoredPressOverlaySprite;
    private Sprite authoredReleaseOverlaySprite;

    private void Reset()
    {
        CacheReferences();
        CaptureAuthoredSprites();
        CaptureCurrentScaleAsBase();
        ApplyImmediate(VisualState.Normal);
    }

    private void Awake()
    {
        CacheReferences();
        CaptureAuthoredSprites();
        CaptureCurrentScaleAsBase();
        ApplyImmediate(GetRestingState());
    }

    private void OnEnable()
    {
        CacheReferences();
        CaptureAuthoredSprites();
        ApplyImmediate(GetRestingState());
    }

    private void OnDisable()
    {
        KillTweens();
        isPointerInside = false;
        isPointerDown = false;
    }

    private void OnValidate()
    {
        CacheReferences();
        CaptureAuthoredSprites();
        CaptureCurrentScaleAsBase();
        ClampTransitions();

        if (!Application.isPlaying)
            ApplyImmediate(GetRestingState());
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerInside = true;

        if (!CanInteract())
            return;

        PlayOneShot(hoverSound);

        SyncBaseScaleFromCurrentIfNeeded();

        if (flameEffect != null)
            flameEffect.SetHoverState(true);

        if (!isPointerDown)
            TransitionTo(VisualState.Hover);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerInside = false;

        if (!CanInteract())
            return;

        if (flameEffect != null)
            flameEffect.SetHoverState(false);

        if (!isPointerDown)
            TransitionTo(VisualState.Normal);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPointerDown = true;

        if (!CanInteract())
            return;

        PlayOneShot(pressedSound);

        TransitionTo(VisualState.Pressed);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPointerDown = false;

        if (!CanInteract())
            return;

        PlayReleaseState();
    }

    [ContextMenu("Auto Bind Slots")]
    public void AutoBindSlots()
    {
        CacheReferences();
        ApplyImmediate(GetRestingState());
    }

    [ContextMenu("Capture Current Scale As Base")]
    public void CaptureCurrentScaleAsBase()
    {
        if (rectTransform == null)
            rectTransform = transform as RectTransform;

        if (rectTransform != null)
            baseScale = rectTransform.localScale;
    }

    public void SyncBaseScaleFromCurrentIfNeeded()
    {
        if (!syncBaseScaleFromCurrentBeforeHover || rectTransform == null)
            return;

        // Hover should grow from the card's current authored/runtime size,
        // not from an outdated serialized baseScale captured in another scene.
        if ((rectTransform.localScale - baseScale).sqrMagnitude > 0.0001f)
            baseScale = rectTransform.localScale;
    }

    private void CacheReferences()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (rectTransform == null)
            rectTransform = transform as RectTransform;

        if (backgroundSlot == null)
            backgroundSlot = GetComponent<Image>();

        if (backgroundSlot != null)
            backgroundEffects = backgroundSlot.GetComponents<BaseMeshEffect>();
        bool needsEffectStateCapture = backgroundEffects != null
            && (authoredBackgroundEffectStates == null || authoredBackgroundEffectStates.Length != backgroundEffects.Length);

        if (needsEffectStateCapture)
            authoredBackgroundEffectStates = new bool[backgroundEffects.Length];

        if (backgroundEffects != null && (needsEffectStateCapture || !Application.isPlaying))
        {
            for (int i = 0; i < backgroundEffects.Length; i++)
                authoredBackgroundEffectStates[i] = backgroundEffects[i] != null && backgroundEffects[i].enabled;
        }

        if (flameEffect == null)
            flameEffect = GetComponent<ScreenSpaceEdgeFlame>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null && Application.isPlaying)
            audioSource = gameObject.AddComponent<AudioSource>();

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
        }

        if (frameOverlaySlot == null)
            frameOverlaySlot = FindChildImage("FrameOverlaySlot");

        if (hoverOverlaySlot == null)
            hoverOverlaySlot = FindChildImage("HoverOverlaySlot");

        if (pressOverlaySlot == null)
            pressOverlaySlot = FindChildImage("PressOverlaySlot");

        if (releaseOverlaySlot == null)
            releaseOverlaySlot = FindChildImage("ReleaseOverlaySlot");

        if (fallbackFrameSlots == null || fallbackFrameSlots.Length == 0)
        {
            fallbackFrameSlots = new[]
            {
                FindChildImage("FrameTop"),
                FindChildImage("FrameBottom"),
                FindChildImage("FrameLeft"),
                FindChildImage("FrameRight")
            };
        }
    }

    private Image FindChildImage(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<Image>() : null;
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (audioSource == null)
            CacheReferences();

        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    private void CaptureAuthoredSprites()
    {
        authoredBackgroundSprite = backgroundSlot != null ? backgroundSlot.sprite : null;
        authoredFrameOverlaySprite = frameOverlaySlot != null ? frameOverlaySlot.sprite : null;
        authoredHoverOverlaySprite = hoverOverlaySlot != null ? hoverOverlaySlot.sprite : null;
        authoredPressOverlaySprite = pressOverlaySlot != null ? pressOverlaySlot.sprite : null;
        authoredReleaseOverlaySprite = releaseOverlaySlot != null ? releaseOverlaySlot.sprite : null;
    }

    private void ClampTransitions()
    {
        transitions.colorDuration = Mathf.Max(0f, transitions.colorDuration);
        transitions.scaleDuration = Mathf.Max(0f, transitions.scaleDuration);
        transitions.releasedHoldDuration = Mathf.Max(0f, transitions.releasedHoldDuration);

        scaleStates.normal = Mathf.Max(0.01f, scaleStates.normal);
        scaleStates.hover = Mathf.Max(0.01f, scaleStates.hover);
        scaleStates.pressed = Mathf.Max(0.01f, scaleStates.pressed);
        scaleStates.released = Mathf.Max(0.01f, scaleStates.released);
        scaleStates.disabled = Mathf.Max(0.01f, scaleStates.disabled);
    }

    private bool CanInteract()
    {
        return button != null && button.IsInteractable();
    }

    private VisualState GetRestingState()
    {
        return CanInteract() ? VisualState.Normal : VisualState.Disabled;
    }

    private void PlayReleaseState()
    {
        VisualState settleState = isPointerInside ? VisualState.Hover : VisualState.Normal;
        TransitionTo(VisualState.Released);

        if (transitions.releasedHoldDuration <= 0f)
        {
            TransitionTo(settleState);
            return;
        }

        if (releaseSequence != null)
            releaseSequence.Kill();

        releaseSequence = DOTween.Sequence().SetTarget(this);
        releaseSequence.AppendInterval(transitions.releasedHoldDuration);
        releaseSequence.AppendCallback(() => TransitionTo(settleState));
    }

    private void TransitionTo(VisualState state)
    {
        if (!Application.isPlaying)
        {
            ApplyImmediate(state);
            return;
        }

        ApplyImageState(backgroundSlot, backgroundSprites.Get(state), backgroundColors.Get(state), true);
        ApplyImageState(frameOverlaySlot, frameOverlaySprites.Get(state), frameOverlayColors.Get(state), true);
        ApplyImageState(hoverOverlaySlot, hoverOverlaySprites.Get(state), hoverOverlayColors.Get(state), true);
        ApplyImageState(pressOverlaySlot, pressOverlaySprites.Get(state), pressOverlayColors.Get(state), true);
        ApplyImageState(releaseOverlaySlot, releaseOverlaySprites.Get(state), releaseOverlayColors.Get(state), true);
        ApplyFallbackFrameState(state, true);
        ApplyScaleState(state, true);
    }

    private void ApplyImmediate(VisualState state)
    {
        ApplyImageState(backgroundSlot, backgroundSprites.Get(state), backgroundColors.Get(state), false);
        ApplyImageState(frameOverlaySlot, frameOverlaySprites.Get(state), frameOverlayColors.Get(state), false);
        ApplyImageState(hoverOverlaySlot, hoverOverlaySprites.Get(state), hoverOverlayColors.Get(state), false);
        ApplyImageState(pressOverlaySlot, pressOverlaySprites.Get(state), pressOverlayColors.Get(state), false);
        ApplyImageState(releaseOverlaySlot, releaseOverlaySprites.Get(state), releaseOverlayColors.Get(state), false);
        ApplyFallbackFrameState(state, false);
        ApplyScaleState(state, false);
    }

    private void ApplyImageState(Image target, Sprite sprite, Color color, bool animated)
    {
        if (target == null)
            return;

        Sprite resolvedSprite = ResolveSprite(target, sprite);
        Color resolvedColor = ResolveColor(target, resolvedSprite, color);
        target.sprite = resolvedSprite;
        target.preserveAspect = resolvedSprite != null;
        target.raycastTarget = target == backgroundSlot;

        bool shouldShow = resolvedSprite != null || color.a > 0.001f || target == backgroundSlot;
        target.enabled = shouldShow;

        if (target == backgroundSlot)
            UpdateBackgroundEffects(resolvedSprite);

        if (animated && Application.isPlaying)
        {
            target.DOKill();
            target.DOColor(resolvedColor, transitions.colorDuration).SetEase(transitions.ease).SetTarget(target);
        }
        else
        {
            target.color = resolvedColor;
        }
    }

    private Sprite ResolveSprite(Image target, Sprite sprite)
    {
        if (sprite != null)
            return sprite;

        if (target == backgroundSlot)
            return authoredBackgroundSprite;

        if (target == frameOverlaySlot)
            return authoredFrameOverlaySprite;

        if (target == hoverOverlaySlot)
            return authoredHoverOverlaySprite;

        if (target == pressOverlaySlot)
            return authoredPressOverlaySprite;

        if (target == releaseOverlaySlot)
            return authoredReleaseOverlaySprite;

        return target.sprite;
    }

    private Color ResolveColor(Image target, Sprite resolvedSprite, Color color)
    {
        if (target == backgroundSlot && resolvedSprite != null && preserveSpriteOriginalColor)
            return new Color(1f, 1f, 1f, color.a);

        return color;
    }

    private void UpdateBackgroundEffects(Sprite resolvedSprite)
    {
        if (backgroundEffects == null)
            return;

        for (int i = 0; i < backgroundEffects.Length; i++)
        {
            BaseMeshEffect effect = backgroundEffects[i];
            if (effect != null)
                effect.enabled = ShouldDisableEffectForSprite(effect, resolvedSprite)
                    ? false
                    : authoredBackgroundEffectStates != null && i < authoredBackgroundEffectStates.Length && authoredBackgroundEffectStates[i];
        }
    }

    private bool ShouldDisableEffectForSprite(BaseMeshEffect effect, Sprite resolvedSprite)
    {
        if (resolvedSprite == null || !disableBrightShadowEffectsWhenSpritePresent || effect == null)
            return false;

        if (!(effect is Shadow shadow))
            return false;

        Color color = shadow.effectColor;
        float luminance = color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
        return color.a >= brightShadowAlphaThreshold && luminance >= brightShadowLuminanceThreshold;
    }

    private void ApplyFallbackFrameState(VisualState state, bool animated)
    {
        if (fallbackFrameSlots == null)
            return;

        bool useFallbackFrame = frameOverlaySprites.Get(state) == null;
        Color targetColor = useFallbackFrame ? fallbackFrameColors.Get(state) : Color.clear;

        for (int i = 0; i < fallbackFrameSlots.Length; i++)
        {
            Image frame = fallbackFrameSlots[i];
            if (frame == null)
                continue;

            frame.enabled = useFallbackFrame || targetColor.a > 0.001f;
            frame.raycastTarget = false;

            if (animated && Application.isPlaying)
            {
                frame.DOKill();
                frame.DOColor(targetColor, transitions.colorDuration).SetEase(transitions.ease).SetTarget(frame);
            }
            else
            {
                frame.color = targetColor;
            }
        }
    }

private void ApplyScaleState(VisualState state, bool animated)
    {
        if (rectTransform == null)
            return;

        Vector3 targetScale = baseScale * scaleStates.Get(state);

        if (scaleTween != null)
        {
            scaleTween.Kill();
            scaleTween = null;
        }

        if (animated && Application.isPlaying)
        {
            scaleTween = rectTransform
                .DOScale(targetScale, transitions.scaleDuration)
                .SetEase(transitions.ease);
        }
        else
        {
            rectTransform.localScale = targetScale;
        }
    }

private void KillTweens()
    {
        if (backgroundSlot != null)
            backgroundSlot.DOKill();

        if (frameOverlaySlot != null)
            frameOverlaySlot.DOKill();

        if (hoverOverlaySlot != null)
            hoverOverlaySlot.DOKill();

        if (pressOverlaySlot != null)
            pressOverlaySlot.DOKill();

        if (releaseOverlaySlot != null)
            releaseOverlaySlot.DOKill();

        if (fallbackFrameSlots != null)
        {
            for (int i = 0; i < fallbackFrameSlots.Length; i++)
            {
                if (fallbackFrameSlots[i] != null)
                    fallbackFrameSlots[i].DOKill();
            }
        }

        if (scaleTween != null)
        {
            scaleTween.Kill();
            scaleTween = null;
        }

        if (releaseSequence != null)
        {
            releaseSequence.Kill();
            releaseSequence = null;
        }
    }
}
