using System;
using Anchor.GameFlow;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Anchor.UI.Panel
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class EndPanelAnimator : MonoBehaviour
    {
        private const int EndingSlotCount = 4;

        public event Action Completed;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float initialDelay = 0.15f;
        [SerializeField, Min(0f)] private float stepInterval = 0.12f;
        [SerializeField, Min(0.01f)] private float moveDuration = 0.45f;
        [SerializeField, Min(0.01f)] private float numberDuration = 0.65f;
        [SerializeField, Min(0.01f)] private float endingArtworkMoveDuration = 0.55f;
        [SerializeField, Min(0f)] private float group1ElementInterval = 0.07f;
        [SerializeField, Min(0f)] private float group3ElementInterval = 0.07f;
        [SerializeField, Min(0f)] private float numberInterval = 0.10f;

        [Header("Motion")]
        [SerializeField, Min(0f), Tooltip("元素完全移出屏幕后，额外留出的屏外距离。")]
        private float offscreenPadding = 50f;
        [SerializeField] private Ease moveEase = Ease.OutCubic;
        [SerializeField] private Ease numberEase = Ease.OutCubic;
        [SerializeField] private bool useUnscaledTime = true;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField, Tooltip("Group0 开始入场时播放一次。")]
        private AudioClip group0EnterSound;
        [SerializeField, Range(0f, 1f)] private float group0EnterSoundVolume = 1f;
        [SerializeField, Tooltip("Group3 每个子元素开始入场时播放一次，当前共播放三次。")]
        private AudioClip group3ElementSound;
        [SerializeField, Range(0f, 1f)] private float group3ElementSoundVolume = 1f;
        [SerializeField, Tooltip("Group4 与 Group5 的数值过渡动画开始时播放一次。")]
        private AudioClip numberTransitionSound;
        [SerializeField, Range(0f, 1f)] private float numberTransitionSoundVolume = 1f;

        [Header("Ending Artwork Targets")]
        [SerializeField] private Image resultImage;
        [SerializeField] private Image idleImage;

        [Header("Result Artwork Slots (0 Hit, 1 Boutique, 2 Buggy, 3 Storm)")]
        [SerializeField, Tooltip("Slot 0=hit 好评如潮；Slot 1=boutique 小众精品；Slot 2=buggy 无人问津；Slot 3=storm 暴死结局。")]
        private Sprite[] resultSprites = new Sprite[EndingSlotCount];

        [Header("Idle Artwork Slots (0 Hit, 1 Boutique, 2 Buggy, 3 Storm)")]
        [SerializeField, Tooltip("Slot 0=hit 好评如潮；Slot 1=boutique 小众精品；Slot 2=buggy 无人问津；Slot 3=storm 暴死结局。")]
        private Sprite[] idleSprites = new Sprite[EndingSlotCount];

        [Header("Preview Fallback Values")]
        [SerializeField] private int previewBugValue = 88;
        [SerializeField] private int previewViewValue = 88;
        [SerializeField] private int previewAudioValue = 88;
        [SerializeField] private int previewWishlistValue = 88888;
        [SerializeField] private string previewEndingId = "hit";

        private RectTransform group0;
        private RectTransform group1;
        private RectTransform group2;
        private RectTransform group3;
        private RectTransform group4;
        private RectTransform group5;
        private RectTransform[] group1Elements;
        private RectTransform[] group3Elements;
        private TMP_Text[] scoreNumbers;
        private TMP_Text wishlistNumber;
        private RectTransform resultRect;
        private RectTransform idleRect;
        private Vector2 group0Position;
        private Vector2 group2Position;
        private Vector2[] group1Positions;
        private Vector2[] group3Positions;
        private Vector2 resultPosition;
        private Vector2 idlePosition;
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
            KillOpeningSequence();
        }

        private void OnDestroy()
        {
            KillOpeningSequence();
        }

        public void Play()
        {
            if (TryGetCurrentBlackboard(out GameFlowBlackboard blackboard))
            {
                Play(
                    blackboard.BugScore,
                    blackboard.VisualScore,
                    blackboard.AtmosphereScore,
                    blackboard.WishlistCount,
                    blackboard.EndingResult.EndingId);
                return;
            }

            Play(
                previewBugValue,
                previewViewValue,
                previewAudioValue,
                previewWishlistValue,
                previewEndingId);
        }

        public void Play(int bugValue, int viewValue, int audioValue, int wishlistValue, string endingId)
        {
            if (!Initialize())
            {
                return;
            }

            ApplyEndingArtwork(endingId);
            KillOpeningSequence();
            ResetVisuals();

            openingSequence = DOTween.Sequence()
                .SetTarget(this)
                .SetUpdate(useUnscaledTime);
            openingSequence.AppendInterval(initialDelay);
            openingSequence.AppendCallback(PlayGroup0EnterSound);

            Sequence openingGroups = DOTween.Sequence();
            openingGroups.Join(CreateMoveTween(group0, group0Position));
            openingGroups.Join(CreateStaggeredMove(group1Elements, group1Positions, group1ElementInterval, null));
            openingGroups.Join(CreateMoveTween(group2, group2Position));
            openingSequence.Append(openingGroups);
            AppendStepInterval();

            openingSequence.Append(CreateStaggeredMove(
                group3Elements,
                group3Positions,
                group3ElementInterval,
                PlayGroup3ElementSound));
            AppendStepInterval();

            openingSequence.AppendCallback(PlayNumberTransitionSound);
            openingSequence.Append(CreateNumberSequence(bugValue, viewValue, audioValue, wishlistValue));
            openingSequence.Append(CreateEndingArtworkSequence());
            openingSequence.OnComplete(() => Completed?.Invoke());
        }

        /// <summary>
        /// 使用完全独立于 Blackboard 的虚拟数据重播动画，仅供 EndPanel Debug UI 调用。
        /// </summary>
        public void PlayDebug(int wishlistValue, int qualityScore)
        {
            int safeWishlist = Mathf.Max(0, wishlistValue);
            int safeQuality = Mathf.Max(0, qualityScore);
            int virtualBug = safeQuality < 160 ? 55 : 0;
            int virtualBaseScore = FindBaseScoreForQuality(safeQuality, virtualBug);
            string endingId = ResolveDebugEndingId(safeQuality, safeWishlist, virtualBug);

            Play(
                virtualBug,
                virtualBaseScore,
                virtualBaseScore,
                safeWishlist,
                endingId);
        }

        public void ApplyEndingArtwork(string endingId)
        {
            int slot = GetEndingSlot(endingId);
            ApplySprite(resultImage, resultSprites, slot);
            ApplySprite(idleImage, idleSprites, slot);
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
                Debug.LogError($"{nameof(EndPanelAnimator)}: Group0-Group5 hierarchy is incomplete.", this);
                return false;
            }

            group1Elements = GetDirectRectChildren(group1);
            group3Elements = GetDirectRectChildren(group3);
            scoreNumbers = new[]
            {
                group4.Find("BugValue")?.GetComponent<TMP_Text>(),
                group4.Find("ArtValue")?.GetComponent<TMP_Text>(),
                group4.Find("AudioValue")?.GetComponent<TMP_Text>()
            };
            wishlistNumber = group5.childCount > 0 ? group5.GetChild(0).GetComponent<TMP_Text>() : null;
            resultImage = resultImage != null ? resultImage : transform.Find("Result")?.GetComponent<Image>();
            idleImage = idleImage != null ? idleImage : transform.Find("Idle")?.GetComponent<Image>();

            if (Array.Exists(scoreNumbers, number => number == null) ||
                wishlistNumber == null || resultImage == null || idleImage == null)
            {
                Debug.LogError($"{nameof(EndPanelAnimator)}: Expected Group4 BugValue/ArtValue/AudioValue, one Group5 TMP text, Result Image and Idle Image.", this);
                return false;
            }

            group0Position = group0.anchoredPosition;
            group2Position = group2.anchoredPosition;
            group1Positions = CapturePositions(group1Elements);
            group3Positions = CapturePositions(group3Elements);
            resultRect = resultImage.rectTransform;
            idleRect = idleImage.rectTransform;
            resultPosition = resultRect.anchoredPosition;
            idlePosition = idleRect.anchoredPosition;
            initialized = true;
            return true;
        }

        private void ResetVisuals()
        {
            group0.DOKill();
            group2.DOKill();
            Kill(group1Elements);
            Kill(group3Elements);
            Kill(scoreNumbers);
            wishlistNumber.DOKill();
            resultRect.DOKill();
            idleRect.DOKill();

            Canvas.ForceUpdateCanvases();
            group0.anchoredPosition = GetOffscreenPosition(group0, group0Position, Vector2.down);
            group2.anchoredPosition = GetOffscreenPosition(group2, group2Position, Vector2.down);
            SetOffscreen(group1Elements, group1Positions, Vector2.down);
            SetOffscreen(group3Elements, group3Positions, Vector2.down);
            resultRect.anchoredPosition = GetOffscreenPosition(resultRect, resultPosition, Vector2.left);
            idleRect.anchoredPosition = GetOffscreenPosition(idleRect, idlePosition, Vector2.down);

            foreach (TMP_Text number in scoreNumbers)
            {
                number.text = "0";
                number.gameObject.SetActive(false);
            }

            wishlistNumber.text = "0";
            wishlistNumber.gameObject.SetActive(false);
        }

        private Sequence CreateStaggeredMove(
            RectTransform[] targets,
            Vector2[] destinations,
            float interval,
            Action onEachStart)
        {
            Sequence sequence = DOTween.Sequence();
            for (int i = 0; i < targets.Length; i++)
            {
                RectTransform target = targets[i];
                float startTime = i * interval;
                if (onEachStart != null)
                {
                    sequence.InsertCallback(startTime, () => onEachStart());
                }
                sequence.Insert(startTime, CreateMoveTween(target, destinations[i]));
            }

            return sequence;
        }

        private Sequence CreateEndingArtworkSequence()
        {
            Sequence sequence = DOTween.Sequence();
            sequence.Join(resultRect.DOAnchorPos(resultPosition, endingArtworkMoveDuration).SetEase(moveEase));
            sequence.Join(idleRect.DOAnchorPos(idlePosition, endingArtworkMoveDuration).SetEase(moveEase));
            return sequence;
        }

        private Sequence CreateNumberSequence(int bugValue, int viewValue, int audioValue, int wishlistValue)
        {
            Sequence sequence = DOTween.Sequence();
            int[] values = { bugValue, viewValue, audioValue };

            for (int i = 0; i < scoreNumbers.Length; i++)
            {
                TMP_Text number = scoreNumbers[i];
                float startTime = i * numberInterval;
                sequence.InsertCallback(startTime, () => number.gameObject.SetActive(true));
                sequence.Insert(startTime, CreateNumberTween(number, values[i]));
            }

            float wishlistStart = scoreNumbers.Length * numberInterval;
            sequence.InsertCallback(wishlistStart, () => wishlistNumber.gameObject.SetActive(true));
            sequence.Insert(wishlistStart, CreateNumberTween(wishlistNumber, wishlistValue));
            return sequence;
        }

        private Tween CreateMoveTween(RectTransform target, Vector2 destination)
        {
            return target.DOAnchorPos(destination, moveDuration).SetEase(moveEase);
        }

        private Tween CreateNumberTween(TMP_Text label, int targetValue)
        {
            int displayedValue = 0;
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

        private void PlayGroup0EnterSound()
        {
            PlayOneShot(group0EnterSound, group0EnterSoundVolume);
        }

        private void PlayGroup3ElementSound()
        {
            PlayOneShot(group3ElementSound, group3ElementSoundVolume);
        }

        private void PlayNumberTransitionSound()
        {
            PlayOneShot(numberTransitionSound, numberTransitionSoundVolume);
        }

        private void PlayOneShot(AudioClip clip, float volume)
        {
            ResolveAudioSource();
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip, volume);
            }
        }

        private void ResolveAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (audioSource != null)
            {
                audioSource.playOnAwake = false;
            }
        }

        private void KillOpeningSequence()
        {
            openingSequence?.Kill();
            openingSequence = null;
        }

        private void AppendStepInterval()
        {
            if (stepInterval > 0f)
            {
                openingSequence.AppendInterval(stepInterval);
            }
        }

        private Vector2 GetOffscreenPosition(RectTransform target, Vector2 destination, Vector2 direction)
        {
            Canvas rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            RectTransform canvasRect = rootCanvas != null ? rootCanvas.transform as RectTransform : null;
            RectTransform parentRect = target.parent as RectTransform;
            if (canvasRect == null || parentRect == null)
            {
                return destination + direction.normalized * 2000f;
            }

            target.anchoredPosition = destination;
            var targetCorners = new Vector3[4];
            var canvasCorners = new Vector3[4];
            target.GetWorldCorners(targetCorners);
            canvasRect.GetWorldCorners(canvasCorners);

            Vector3 worldDelta = Vector3.zero;
            if (direction == Vector2.left)
            {
                float padding = offscreenPadding * Mathf.Abs(canvasRect.lossyScale.x);
                worldDelta.x = canvasCorners[0].x - padding - targetCorners[2].x;
            }
            else
            {
                float padding = offscreenPadding * Mathf.Abs(canvasRect.lossyScale.y);
                worldDelta.y = canvasCorners[0].y - padding - targetCorners[1].y;
            }

            Vector3 localDelta = parentRect.InverseTransformVector(worldDelta);
            return destination + new Vector2(localDelta.x, localDelta.y);
        }

        private RectTransform FindGroup(string groupName)
        {
            return transform.Find(groupName) as RectTransform;
        }

        private static bool TryGetCurrentBlackboard(out GameFlowBlackboard blackboard)
        {
            blackboard = GameFlowRunner.Instance != null && GameFlowRunner.Instance.Controller != null
                ? GameFlowRunner.Instance.Controller.Blackboard
                : null;
            return blackboard != null;
        }

        private static int GetEndingSlot(string endingId)
        {
            switch (endingId)
            {
                case "hit": return 0;
                case "boutique": return 1;
                case "buggy": return 2;
                case "storm": return 3;
                default:
                    Debug.LogWarning($"Unknown ending id '{endingId}', using slot 3 (storm).");
                    return 3;
            }
        }

        private static string ResolveDebugEndingId(int qualityScore, int wishlistValue, int bugScore)
        {
            if (qualityScore >= 220 && wishlistValue >= 900 && bugScore <= 20)
            {
                return "hit";
            }

            if (qualityScore >= 160 && wishlistValue >= 550)
            {
                return "boutique";
            }

            return bugScore >= 55 ? "buggy" : "storm";
        }

        private static int FindBaseScoreForQuality(int qualityScore, int bugScore)
        {
            if (qualityScore <= 0)
            {
                return 0;
            }

            float multiplier = Mathf.Clamp01(1f - Mathf.Max(0, bugScore) / 100f);
            if (multiplier <= 0f)
            {
                return qualityScore;
            }

            int estimate = Mathf.Max(0, Mathf.RoundToInt(qualityScore / multiplier));
            for (int candidate = Mathf.Max(0, estimate - 3); candidate <= estimate + 3; candidate++)
            {
                if (GameFlowBlackboard.CalculateQualityScore(candidate, candidate, bugScore) == qualityScore)
                {
                    return candidate;
                }
            }

            return estimate;
        }

        private static void ApplySprite(Image image, Sprite[] sprites, int slot)
        {
            if (image != null && sprites != null && slot >= 0 && slot < sprites.Length)
            {
                image.sprite = sprites[slot];
            }
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

        private static Vector2[] CapturePositions(RectTransform[] targets)
        {
            var positions = new Vector2[targets.Length];
            for (int i = 0; i < targets.Length; i++)
            {
                positions[i] = targets[i].anchoredPosition;
            }

            return positions;
        }

        private void SetOffscreen(RectTransform[] targets, Vector2[] positions, Vector2 direction)
        {
            for (int i = 0; i < targets.Length; i++)
            {
                targets[i].anchoredPosition = GetOffscreenPosition(targets[i], positions[i], direction);
            }
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

        private void OnValidate()
        {
            ResizeSlots(ref resultSprites);
            ResizeSlots(ref idleSprites);
        }

        private static void ResizeSlots(ref Sprite[] slots)
        {
            if (slots == null || slots.Length != EndingSlotCount)
            {
                Array.Resize(ref slots, EndingSlotCount);
            }
        }
    }
}
