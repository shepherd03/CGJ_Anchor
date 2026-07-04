using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class FloatingUIFan : MonoBehaviour
{
    private enum SpreadDirection
    {
        Left,
        Right
    }

    [Header("Layout")]
    [SerializeField] private SpreadDirection spreadDirection = SpreadDirection.Left;
    [SerializeField] private Vector2 expandOrigin = Vector2.zero;
    [SerializeField] private float collapsedScale = 0.82f;

    [Header("Opening")]
    [SerializeField] private float openDuration = 0.9f;
    [SerializeField] private float stagger = 0.12f;
    [SerializeField] private Ease moveEase = Ease.OutBack;
    [SerializeField] private Ease rotateEase = Ease.OutCubic;
    [SerializeField] private Ease scaleEase = Ease.OutBack;

    [Header("Floating")]
    [FormerlySerializedAs("floatDistance")]
    [SerializeField] private float floatAmplitude = 10f;
    [SerializeField] private float floatSpeed = 0.625f;
    [SerializeField] private float floatDelayStep = 0.18f;
    [SerializeField] private Ease floatEase = Ease.InOutSine;

    private RectTransform[] cards;
    private CardLayout[] authoredLayouts;

    private void Awake()
    {
        ClampSettings();
        cards = CollectCards();
        authoredLayouts = CaptureAuthoredLayouts(cards);
    }

    private void Start()
    {
        PlayOpenAnimation();
    }

    [ContextMenu("Play Open Animation")]
    public void PlayOpenAnimation()
    {
        if (cards == null || cards.Length == 0)
            return;

        KillAllTweens();

        for (int i = 0; i < cards.Length; i++)
        {
            RectTransform card = cards[i];
            if (card == null)
                continue;

            CardLayout targetLayout = GetLayoutForDirection(i);
            card.anchoredPosition = expandOrigin;
            card.localEulerAngles = Vector3.zero;
            card.localScale = Vector3.one * collapsedScale;

            float delay = i * stagger;
            Sequence opening = DOTween.Sequence().SetTarget(card);
            opening.AppendInterval(delay);
            opening.Append(card.DOAnchorPos(targetLayout.Position, openDuration).SetEase(moveEase));
            opening.Join(card.DOLocalRotate(new Vector3(0f, 0f, targetLayout.RotationZ), openDuration).SetEase(rotateEase));
            opening.Join(card.DOScale(targetLayout.Scale, openDuration * 0.85f).SetEase(scaleEase));

            int index = i;
            opening.OnComplete(() => StartFloating(index));
        }
    }

    private void StartFloating(int index)
    {
        if (cards == null || index < 0 || index >= cards.Length)
            return;

        RectTransform card = cards[index];
        if (card == null)
            return;

        CardLayout targetLayout = GetLayoutForDirection(index);
        float halfCycleDuration = Mathf.Max(0.01f, 0.5f / floatSpeed);

        card
            .DOAnchorPosY(targetLayout.Position.y + floatAmplitude, halfCycleDuration)
            .SetDelay(index * floatDelayStep)
            .SetEase(floatEase)
            .SetLoops(-1, LoopType.Yoyo)
            .SetTarget(card);
    }

    private CardLayout GetLayoutForDirection(int index)
    {
        if (authoredLayouts == null || index < 0 || index >= authoredLayouts.Length)
            return default;

        CardLayout layout = authoredLayouts[index];
        if (spreadDirection == SpreadDirection.Left)
            return layout;

        layout.Position = new Vector2(-layout.Position.x, layout.Position.y);
        layout.RotationZ = -layout.RotationZ;
        return layout;
    }

    private static RectTransform[] CollectCards(Transform root)
    {
        List<RectTransform> collectedCards = new List<RectTransform>(root.childCount);

        for (int i = 0; i < root.childCount; i++)
        {
            if (root.GetChild(i) is RectTransform rect)
                collectedCards.Add(rect);
        }

        return collectedCards.ToArray();
    }

    private RectTransform[] CollectCards()
    {
        return CollectCards(transform);
    }

    private static CardLayout[] CaptureAuthoredLayouts(IReadOnlyList<RectTransform> sourceCards)
    {
        CardLayout[] layouts = new CardLayout[sourceCards.Count];

        for (int i = 0; i < sourceCards.Count; i++)
        {
            RectTransform card = sourceCards[i];
            if (card == null)
                continue;

            layouts[i] = new CardLayout(
                card.anchoredPosition,
                Mathf.DeltaAngle(0f, card.localEulerAngles.z),
                card.localScale);
        }

        return layouts;
    }

    private void OnValidate()
    {
        ClampSettings();
    }

    private void ClampSettings()
    {
        collapsedScale = Mathf.Max(0.01f, collapsedScale);
        openDuration = Mathf.Max(0.01f, openDuration);
        stagger = Mathf.Max(0f, stagger);
        floatAmplitude = Mathf.Max(0f, floatAmplitude);
        floatSpeed = Mathf.Max(0.01f, floatSpeed);
        floatDelayStep = Mathf.Max(0f, floatDelayStep);
    }

    private void OnDestroy()
    {
        KillAllTweens();
    }

    private void KillAllTweens()
    {
        if (cards == null)
            return;

        foreach (RectTransform card in cards)
        {
            if (card != null)
                card.DOKill();
        }
    }

    [Serializable]
    private struct CardLayout
    {
        public Vector2 Position;
        public float RotationZ;
        public Vector3 Scale;

        public CardLayout(Vector2 position, float rotationZ, Vector3 scale)
        {
            Position = position;
            RotationZ = rotationZ;
            Scale = scale;
        }
    }
}
