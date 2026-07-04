using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button), typeof(Image))]
public sealed class EventChoiceButtonVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Floating")]
    [SerializeField, Min(0f)] private float floatAmplitude = 6f;
    [SerializeField, Min(0.01f)] private float floatSpeed = 0.55f;
    [SerializeField, Min(0f)] private float startDelay;

    [Header("Hit Test")]
    [SerializeField, Range(0f, 1f)] private float alphaHitThreshold = 0.1f;

    private RectTransform rectTransform;
    private Image image;
    private Outline hoverOutline;
    private Vector2 restingPosition;
    private Tween floatingTween;

    private void Awake()
    {
        CacheReferences();
        ApplyHitTest();
    }

    private void OnEnable()
    {
        CacheReferences();
        ApplyHitTest();
        SetOutline(false);

        restingPosition = rectTransform.anchoredPosition;
        StartFloating();
    }

    private void OnDisable()
    {
        StopFloating();
        SetOutline(false);
    }

    private void OnDestroy()
    {
        StopFloating();
    }

    private void OnValidate()
    {
        floatAmplitude = Mathf.Max(0f, floatAmplitude);
        floatSpeed = Mathf.Max(0.01f, floatSpeed);
        startDelay = Mathf.Max(0f, startDelay);
        CacheReferences();
        ApplyHitTest();

        if (!Application.isPlaying)
            SetOutline(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetOutline(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetOutline(false);
    }

    private void CacheReferences()
    {
        if (rectTransform == null)
            rectTransform = transform as RectTransform;

        if (image == null)
            image = GetComponent<Image>();

        if (hoverOutline == null)
            hoverOutline = GetComponent<Outline>();
    }

    private void ApplyHitTest()
    {
        if (image != null)
            image.alphaHitTestMinimumThreshold = alphaHitThreshold;
    }

    private void StartFloating()
    {
        StopFloating();

        if (rectTransform == null || floatAmplitude <= 0f)
            return;

        float halfCycleDuration = Mathf.Max(0.01f, 0.5f / floatSpeed);
        floatingTween = rectTransform
            .DOAnchorPosY(restingPosition.y + floatAmplitude, halfCycleDuration, true)
            .SetDelay(startDelay)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetTarget(this);
    }

    private void StopFloating()
    {
        if (floatingTween != null)
        {
            floatingTween.Kill();
            floatingTween = null;
        }

        if (rectTransform != null && Application.isPlaying)
            rectTransform.anchoredPosition = restingPosition;
    }

    private void SetOutline(bool visible)
    {
        if (hoverOutline != null)
            hoverOutline.enabled = visible;
    }
}
