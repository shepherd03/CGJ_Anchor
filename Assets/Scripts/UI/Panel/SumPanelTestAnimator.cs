using System;
using System.Collections.Generic;
using Anchor.GameFlow;
using DG.Tweening;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public sealed class SumPanelTestAnimator : MonoBehaviour
{
    public event Action Completed;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float initialDelay = 0.15f;
    [SerializeField, Min(0f)] private float stepInterval = 0.12f;
    [SerializeField, Min(0.01f)] private float moveDuration = 0.45f;
    [SerializeField, Min(0.01f)] private float numberDuration = 0.65f;
    [SerializeField, Min(0f)] private float group1ElementInterval = 0.07f;
    [SerializeField, Min(0f)] private float group3ElementInterval = 0.07f;
    [SerializeField, Min(0f)] private float group4NumberInterval = 0.10f;

    [Header("Wishlist Modifier Animation")]
    [SerializeField, Min(0f)] private float group6OffsetY = 50f;
    [SerializeField, Min(0.01f)] private float group6EnterDuration = 0.3f;
    [SerializeField, Min(0.01f)] private float group6ExitDuration = 0.65f;
    [SerializeField] private Ease group6EnterEase = Ease.OutCubic;
    [SerializeField] private Ease group6ExitEase = Ease.InCubic;

    [Header("Motion")]
    [SerializeField, Min(0f)] private float enterOffsetY = 1200f;
    [SerializeField] private Ease moveEase = Ease.OutCubic;
    [SerializeField] private Ease numberEase = Ease.OutCubic;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip panelOpenSound;
    [SerializeField, Range(0f, 1f)] private float panelOpenSoundVolume = 1f;
    [SerializeField] private AudioClip group6EnterSound;
    [SerializeField, Range(0f, 1f)] private float group6EnterSoundVolume = 1f;
    [SerializeField] private AudioClip valueUpdateSound;
    [SerializeField, Range(0f, 1f)] private float valueUpdateSoundVolume = 1f;

    [Header("Temporary default values")]
    [SerializeField] private int group4TargetValue = 88;
    [SerializeField] private int group5TargetValue = 88888;

    [Header("Game Flow Data")]
    [SerializeField, Tooltip("播放动画时优先读取 GameFlowBlackboard，同步 Bug/View/Audio/Wishlist。")]
    private bool syncWithGameFlowDataOnPlay = true;

    private RectTransform group0;
    private RectTransform group1;
    private RectTransform group2;
    private RectTransform group3;
    private RectTransform group4;
    private RectTransform group5;
    private RectTransform group6;

    private RectTransform[] group1Elements;
    private RectTransform[] group3Elements;
    private TMP_Text[] group4Numbers;
    private TMP_Text group5Number;
    private TMP_Text bonusNameText;
    private TMP_Text bonusValueText;
    private CanvasGroup group6CanvasGroup;

    private Vector2 group0Position;
    private Vector2 group2Position;
    private Vector2[] group1Positions;
    private Vector2[] group3Positions;
    private Vector2 group6Position;
    private Sequence openingSequence;
    private bool initialized;

    private void Awake()
    {
        ResolveAudioSource();
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
        Play();
    }

    private void OnDisable()
    {
        openingSequence?.Kill();
        openingSequence = null;
    }

    private void OnDestroy()
    {
        openingSequence?.Kill();
    }

    /// <summary>
    /// 使用当前流程数据重播开场动画；流程数据不可用时使用 Inspector 中的临时默认值。
    /// </summary>
    public void Play()
    {
        if (syncWithGameFlowDataOnPlay && TryGetCurrentBlackboard(out GameFlowBlackboard blackboard))
        {
            WeekResolveResult weekResult = blackboard.LastWeekResult;
            Play(
                blackboard.BugScore,
                blackboard.VisualScore,
                blackboard.AtmosphereScore,
                weekResult.WishlistStartValue,
                weekResult.WishlistModifiersOrEmpty);
            return;
        }

        Play(group4TargetValue, group5TargetValue);
    }

    /// <summary>
    /// 使用同一个数值刷新 Group4 的三个动画数字，并刷新 Group5 的 Wishlist 动画数字。
    /// </summary>
    public void Play(int group4Value, int group5Value)
    {
        Play(group4Value, group4Value, group4Value, group5Value);
    }

    /// <summary>
    /// 按 Group4 子物体顺序刷新 Bug、View、Audio，并刷新 Group5 的 Wishlist。
    /// </summary>
    public void Play(int bugValue, int viewValue, int audioValue, int wishlistValue)
    {
        Play(bugValue, viewValue, audioValue, wishlistValue, null);
    }

    private void Play(
        int bugValue,
        int viewValue,
        int audioValue,
        int wishlistValue,
        IReadOnlyList<WishlistModifierResult> wishlistModifiers)
    {
        if (!Initialize())
        {
            return;
        }

        group4TargetValue = bugValue;
        group5TargetValue = wishlistValue;

        openingSequence?.Kill();
        ResetVisuals();
        PlayPanelOpenSound();

        openingSequence = DOTween.Sequence()
            .SetTarget(this)
            .SetUpdate(useUnscaledTime);

        openingSequence.AppendInterval(initialDelay);

        Sequence movingGroups = DOTween.Sequence();
        movingGroups.Join(CreateMoveTween(group0, group0Position));
        movingGroups.Join(CreateStaggeredMove(
            group1Elements,
            group1Positions,
            group1ElementInterval));
        movingGroups.Join(CreateMoveTween(group2, group2Position));
        openingSequence.Append(movingGroups);
        AppendStepInterval();

        openingSequence.Append(CreateStaggeredMove(
            group3Elements,
            group3Positions,
            group3ElementInterval));
        AppendStepInterval();

        openingSequence.Append(CreateGroup4NumberSequence(bugValue, viewValue, audioValue));
        AppendStepInterval();

        openingSequence.AppendCallback(() => group5Number.gameObject.SetActive(true));
        openingSequence.Append(CreateNumberTween(group5Number, wishlistValue));

        if (wishlistModifiers != null && wishlistModifiers.Count > 0)
        {
            AppendStepInterval();
            openingSequence.Append(CreateWishlistModifierSequence(wishlistModifiers));
        }

        openingSequence.OnComplete(() => Completed?.Invoke());
    }

    /// <summary>
    /// 设置临时默认动画数值；流程数据不可用时 Play() 会使用这组值。
    /// </summary>
    public void SetTargetValues(int group4Value, int group5Value)
    {
        group4TargetValue = group4Value;
        group5TargetValue = group5Value;
    }

    private bool Initialize()
    {
        if (initialized)
        {
            return true;
        }

        group0 = FindGroup("Group0");
        group1 = FindGroup("Group1");
        group2 = FindGroup("Group2");
        group3 = FindGroup("Group3");
        group4 = FindGroup("Group4");
        group5 = FindGroup("Group5");
        group6 = FindGroup("Group6");

        if (group0 == null || group1 == null || group2 == null ||
            group3 == null || group4 == null || group5 == null || group6 == null)
        {
            Debug.LogError("SumPanelTestAnimator: Group0-Group6 hierarchy is incomplete.", this);
            return false;
        }

        group1Elements = GetDirectRectChildren(group1);
        group3Elements = GetDirectRectChildren(group3);
        group4Numbers = GetDirectTextChildren(group4);
        group5Number = group5.childCount > 0 ? group5.GetChild(0).GetComponent<TMP_Text>() : null;
        bonusNameText = group6.Find("BonusName")?.GetComponent<TMP_Text>();
        bonusValueText = group6.Find("Bonus")?.GetComponent<TMP_Text>();
        group6CanvasGroup = group6.GetComponent<CanvasGroup>();
        if (group6CanvasGroup == null)
        {
            group6CanvasGroup = group6.gameObject.AddComponent<CanvasGroup>();
        }

        if (group1Elements.Length != 5 || group3Elements.Length != 3 ||
            group4Numbers.Length != 3 || group5Number == null ||
            bonusNameText == null || bonusValueText == null)
        {
            Debug.LogError("SumPanelTestAnimator: Expected Group1=5 children, Group3=3 children, Group4=3 TMP texts, Group5=1 TMP text, and Group6 BonusName/Bonus TMP texts.", this);
            return false;
        }

        group0Position = group0.anchoredPosition;
        group2Position = group2.anchoredPosition;
        group1Positions = CapturePositions(group1Elements);
        group3Positions = CapturePositions(group3Elements);
        group6Position = group6.anchoredPosition;
        initialized = true;
        return true;
    }

    private void ResetVisuals()
    {
        group0.DOKill();
        group2.DOKill();
        Kill(group1Elements);
        Kill(group3Elements);
        Kill(group4Numbers);
        group5Number.DOKill();
        group6.DOKill();
        group6CanvasGroup.DOKill();

        group0.anchoredPosition = Below(group0Position);
        group2.anchoredPosition = Below(group2Position);
        SetBelow(group1Elements, group1Positions);
        SetBelow(group3Elements, group3Positions);

        foreach (TMP_Text number in group4Numbers)
        {
            number.text = "0";
            number.gameObject.SetActive(false);
        }

        group5Number.text = "0";
        group5Number.gameObject.SetActive(false);

        group6.anchoredPosition = group6Position - Vector2.up * group6OffsetY;
        group6CanvasGroup.alpha = 0f;
        group6.gameObject.SetActive(false);
    }

    private Tween CreateMoveTween(RectTransform target, Vector2 destination)
    {
        return target.DOAnchorPos(destination, moveDuration).SetEase(moveEase);
    }

    private Sequence CreateStaggeredMove(
        RectTransform[] targets,
        Vector2[] destinations,
        float elementInterval)
    {
        Sequence sequence = DOTween.Sequence();
        for (int i = 0; i < targets.Length; i++)
        {
            sequence.Insert(i * elementInterval, CreateMoveTween(targets[i], destinations[i]));
        }

        return sequence;
    }

    private Sequence CreateGroup4NumberSequence(int bugValue, int viewValue, int audioValue)
    {
        Sequence sequence = DOTween.Sequence();
        int[] targetValues = { bugValue, viewValue, audioValue };

        for (int i = 0; i < group4Numbers.Length; i++)
        {
            TMP_Text number = group4Numbers[i];
            float startTime = i * group4NumberInterval;
            sequence.InsertCallback(startTime, () => number.gameObject.SetActive(true));
            sequence.Insert(startTime, CreateNumberTween(number, targetValues[i]));
        }

        return sequence;
    }

    private Sequence CreateWishlistModifierSequence(IReadOnlyList<WishlistModifierResult> modifiers)
    {
        Sequence sequence = DOTween.Sequence();

        for (int i = 0; i < modifiers.Count; i++)
        {
            WishlistModifierResult modifier = modifiers[i];

            sequence.AppendCallback(() =>
            {
                bonusNameText.text = modifier.SourceName;
                bonusValueText.text = modifier.DisplayValue;
                group6.anchoredPosition = group6Position - Vector2.up * group6OffsetY;
                group6CanvasGroup.alpha = 0f;
                group6.gameObject.SetActive(true);
                PlayGroup6EnterSound();
            });

            sequence.Append(group6.DOAnchorPos(group6Position, group6EnterDuration)
                .SetEase(group6EnterEase));
            sequence.Join(group6CanvasGroup.DOFade(1f, group6EnterDuration));

            sequence.Append(CreateNumberTween(
                group5Number,
                modifier.BeforeWishlistCount,
                modifier.AfterWishlistCount,
                group6ExitDuration));
            sequence.Join(group6.DOAnchorPos(
                    group6Position + Vector2.up * group6OffsetY,
                    group6ExitDuration)
                .SetEase(group6ExitEase));
            sequence.Join(group6CanvasGroup.DOFade(0f, group6ExitDuration));
            sequence.AppendCallback(() => group6.gameObject.SetActive(false));
        }

        return sequence;
    }

    /// <summary>
    /// 获取当前游戏流程黑板，和 MainPanel、WeekPanel 使用同一份流程数据。
    /// </summary>
    private static bool TryGetCurrentBlackboard(out GameFlowBlackboard blackboard)
    {
        blackboard = GameFlowRunner.Instance != null && GameFlowRunner.Instance.Controller != null
            ? GameFlowRunner.Instance.Controller.Blackboard
            : null;

        return blackboard != null;
    }

    private Tween CreateNumberTween(TMP_Text label, int targetValue)
    {
        return CreateNumberTween(label, 0, targetValue, numberDuration);
    }

    private Tween CreateNumberTween(TMP_Text label, int startValue, int targetValue, float duration)
    {
        int displayedValue = startValue;

        return DOTween.To(
                () => displayedValue,
                value =>
                {
                    displayedValue = value;
                    label.text = value.ToString("N0");
                },
                targetValue,
                duration)
            .SetEase(numberEase)
            .OnStart(() =>
            {
                displayedValue = startValue;
                label.text = startValue.ToString("N0");
                PlayValueUpdateSound();
            });
    }

    public void PlayPanelOpenSound()
    {
        PlayOneShot(panelOpenSound, panelOpenSoundVolume);
    }

    public void PlayGroup6EnterSound()
    {
        PlayOneShot(group6EnterSound, group6EnterSoundVolume);
    }

    public void PlayValueUpdateSound()
    {
        PlayOneShot(valueUpdateSound, valueUpdateSoundVolume);
    }

    private void ResolveAudioSource()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
        }
    }

    private void PlayOneShot(AudioClip clip, float volume)
    {
        ResolveAudioSource();
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip, volume);
        }
    }

    private void AppendStepInterval()
    {
        if (stepInterval > 0f)
        {
            openingSequence.AppendInterval(stepInterval);
        }
    }

    private Vector2 Below(Vector2 position)
    {
        return position + Vector2.down * enterOffsetY;
    }

    private void SetBelow(RectTransform[] targets, Vector2[] positions)
    {
        for (int i = 0; i < targets.Length; i++)
        {
            targets[i].anchoredPosition = Below(positions[i]);
        }
    }

    private RectTransform FindGroup(string groupName)
    {
        Transform child = transform.Find(groupName);
        return child as RectTransform;
    }

    private static RectTransform[] GetDirectRectChildren(RectTransform parent)
    {
        var children = new RectTransform[parent.childCount];
        for (int i = 0; i < parent.childCount; i++)
        {
            children[i] = parent.GetChild(i) as RectTransform;
        }

        return children;
    }

    private static TMP_Text[] GetDirectTextChildren(RectTransform parent)
    {
        var texts = new TMP_Text[parent.childCount];
        for (int i = 0; i < parent.childCount; i++)
        {
            texts[i] = parent.GetChild(i).GetComponent<TMP_Text>();
        }

        return texts;
    }

    private static Vector2[] CapturePositions(RectTransform[] targets)
    {
        var positions = new Vector2[targets.Length];
        for (int i = 0; i < targets.Length; i++)
        {
            positions[i] = targets[i].anchoredPosition;
        }

        return positions;
    }

    private static void Kill(RectTransform[] targets)
    {
        foreach (RectTransform target in targets)
        {
            target.DOKill();
        }
    }

    private static void Kill(TMP_Text[] targets)
    {
        foreach (TMP_Text target in targets)
        {
            target.DOKill();
        }
    }
}
