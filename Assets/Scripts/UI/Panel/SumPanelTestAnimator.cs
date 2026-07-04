using DG.Tweening;
using TMPro;
using UnityEngine;

public sealed class SumPanelTestAnimator : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField, Min(0f)] private float initialDelay = 0.15f;
    [SerializeField, Min(0f)] private float stepInterval = 0.12f;
    [SerializeField, Min(0.01f)] private float moveDuration = 0.45f;
    [SerializeField, Min(0.01f)] private float numberDuration = 0.65f;
    [SerializeField, Min(0f)] private float group1ElementInterval = 0.07f;
    [SerializeField, Min(0f)] private float group3ElementInterval = 0.07f;
    [SerializeField, Min(0f)] private float group4NumberInterval = 0.10f;

    [Header("Motion")]
    [SerializeField, Min(0f)] private float enterOffsetY = 1200f;
    [SerializeField] private Ease moveEase = Ease.OutCubic;
    [SerializeField] private Ease numberEase = Ease.OutCubic;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Temporary default values")]
    [SerializeField] private int group4TargetValue = 88;
    [SerializeField] private int group5TargetValue = 88888;

    private RectTransform group0;
    private RectTransform group1;
    private RectTransform group2;
    private RectTransform group3;
    private RectTransform group4;
    private RectTransform group5;

    private RectTransform[] group1Elements;
    private RectTransform[] group3Elements;
    private TMP_Text[] group4Numbers;
    private TMP_Text group5Number;

    private Vector2 group0Position;
    private Vector2 group2Position;
    private Vector2[] group1Positions;
    private Vector2[] group3Positions;
    private Sequence openingSequence;
    private bool initialized;

    private void Awake()
    {
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
    /// Replays the opening animation using the currently configured target values.
    /// </summary>
    public void Play()
    {
        Play(group4TargetValue, group5TargetValue);
    }

    /// <summary>
    /// Replays the opening animation. This is the future entry point for real summary data.
    /// </summary>
    public void Play(int group4Value, int group5Value)
    {
        if (!Initialize())
        {
            return;
        }

        group4TargetValue = group4Value;
        group5TargetValue = group5Value;

        openingSequence?.Kill();
        ResetVisuals();

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

        openingSequence.Append(CreateGroup4NumberSequence(group4Value));
        AppendStepInterval();

        openingSequence.AppendCallback(() => group5Number.gameObject.SetActive(true));
        openingSequence.Append(CreateNumberTween(group5Number, group5Value));
    }

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

        if (group0 == null || group1 == null || group2 == null ||
            group3 == null || group4 == null || group5 == null)
        {
            Debug.LogError("SumPanelTestAnimator: Group0-Group5 hierarchy is incomplete.", this);
            return false;
        }

        group1Elements = GetDirectRectChildren(group1);
        group3Elements = GetDirectRectChildren(group3);
        group4Numbers = GetDirectTextChildren(group4);
        group5Number = group5.childCount > 0 ? group5.GetChild(0).GetComponent<TMP_Text>() : null;

        if (group1Elements.Length != 5 || group3Elements.Length != 3 ||
            group4Numbers.Length != 3 || group5Number == null)
        {
            Debug.LogError("SumPanelTestAnimator: Expected Group1=5 children, Group3=3 children, Group4=3 TMP texts, Group5=1 TMP text.", this);
            return false;
        }

        group0Position = group0.anchoredPosition;
        group2Position = group2.anchoredPosition;
        group1Positions = CapturePositions(group1Elements);
        group3Positions = CapturePositions(group3Elements);
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

    private Sequence CreateGroup4NumberSequence(int targetValue)
    {
        Sequence sequence = DOTween.Sequence();
        for (int i = 0; i < group4Numbers.Length; i++)
        {
            TMP_Text number = group4Numbers[i];
            float startTime = i * group4NumberInterval;
            sequence.InsertCallback(startTime, () => number.gameObject.SetActive(true));
            sequence.Insert(startTime, CreateNumberTween(number, targetValue));
        }

        return sequence;
    }

    private Tween CreateNumberTween(TMP_Text label, int targetValue)
    {
        int displayedValue = 0;
        label.text = "0";

        return DOTween.To(
                () => displayedValue,
                value =>
                {
                    displayedValue = value;
                    label.text = value.ToString("N0");
                },
                targetValue,
                numberDuration)
            .SetEase(numberEase);
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
