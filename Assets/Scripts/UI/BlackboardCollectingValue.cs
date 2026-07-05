using System.Collections;
using Anchor.Character.Attributes;
using Anchor.GameFlow;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Anchor.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class BlackboardCollectingValue : MonoBehaviour
    {
        public enum ValueKind
        {
            Budget,
            Wishlist
        }

        [Header("Blackboard")]
        [SerializeField] private ValueKind mValueKind;
        [SerializeField, Tooltip("关闭后可由外部在指定时机手动播放，避免属性变化时提前触发。")]
        private bool mListenToAttributeChanges = true;

        [Header("Persistent UI")]
        [SerializeField] private Image mTargetIcon;
        [SerializeField] private TMP_Text mAnimatedValue;
        [SerializeField] private TMP_InputField mTestInput;
        [SerializeField] private Image mParticleTemplate;
        [SerializeField] private RectTransform mEffectLayer;
        [SerializeField] private TMP_FontAsset mFontAsset;

        [Header("Replaceable Art")]
        [Tooltip("Any PNG imported as Sprite (2D and UI).")]
        [SerializeField] private Sprite mIconSprite;
        [SerializeField] private Color mFallbackColor = Color.white;
        [Tooltip("Shared scale for the displayed icon and every flying particle.")]
        [SerializeField, Min(0.05f)] private float mIconScale = 1f;

        [Header("Replaceable Audio")]
        [Tooltip("Played once when the particles burst.")]
        [SerializeField] private AudioClip mBurstSound;
        [Tooltip("Played repeatedly while particles arrive at the icon.")]
        [SerializeField] private AudioClip mCollectSound;
        [SerializeField, Range(0f, 1f)] private float mBurstVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float mCollectVolume = 0.65f;
        [SerializeField, Min(0f)] private float mCollectSoundInterval = 0.035f;

        [Header("Collection Animation")]
        [SerializeField, Range(6, 40)] private int mIconCount = 18;
        [SerializeField, Min(20f)] private float mBurstRadius = 180f;
        [SerializeField, Min(0.1f)] private float mFlyDuration = 0.62f;
        [SerializeField, Min(0f)] private float mGatherStartDelay = 0.48f;

        [Header("Number Animation")]
        [SerializeField, Min(0.05f)] private float mNumberDuration = 0.8f;
        [SerializeField] private Ease mNumberEase = Ease.OutCubic;

        private CharacterAttributeSet mAttributes;
        private AudioSource mAudioSource;
        private int mDisplayedValue;
        private Tween mNumberTween;
        private Tween mGatherCallback;
        private float mLastCollectSoundTime = -10f;
        private bool mIsManuallyConfigured;

        private int AttributeId => mValueKind == ValueKind.Budget
            ? CharacterAttributeIds.Coins
            : CharacterAttributeIds.Wishlist;

        private IEnumerator Start()
        {
            EnsureAudioSource();
            ApplyAppearance();

            if (mIsManuallyConfigured)
            {
                yield break;
            }

            yield return null;
            var blackboard = GameFlowRunner.Instance?.Controller?.Blackboard;
            if (blackboard == null)
            {
                Debug.LogError("BlackboardCollectingValue requires an active GameFlowRunner.", this);
                yield break;
            }

            mAttributes = blackboard.PlayerAttributes;
            if (mListenToAttributeChanges)
            {
                mAttributes.Changed += OnAttributeChanged;
            }
            mDisplayedValue = mAttributes.Get(AttributeId);
            RenderValue(mDisplayedValue);
            mTestInput?.SetTextWithoutNotify(mDisplayedValue.ToString());
            if (mTestInput != null)
            {
                mTestInput.onEndEdit.AddListener(ApplyTestValue);
            }
        }

        private void OnDestroy()
        {
            if (mAttributes != null)
            {
                mAttributes.Changed -= OnAttributeChanged;
            }

            if (mTestInput != null)
            {
                mTestInput.onEndEdit.RemoveListener(ApplyTestValue);
            }

            mNumberTween?.Kill();
            mGatherCallback?.Kill();
            DOTween.Kill(this);
        }

        private void OnValidate()
        {
            ApplyAppearance();
        }

        private void OnAttributeChanged(CharacterAttributeChangedEvent change)
        {
            if (change.AttributeSet == mAttributes && change.AttributeId == AttributeId)
            {
                if (change.CurrentValue > change.PreviousValue)
                {
                    PlayTransition(change.CurrentValue);
                }
                else
                {
                    mGatherCallback?.Kill();
                    AnimateValue(change.CurrentValue);
                }
            }
        }

        private void ApplyTestValue(string text)
        {
            if (mAttributes == null || !int.TryParse(text, out var value))
            {
                mTestInput?.SetTextWithoutNotify(mDisplayedValue.ToString());
                return;
            }

            value = Mathf.Max(0, value);
            mTestInput.SetTextWithoutNotify(value.ToString());
            mAttributes.Set(AttributeId, value);
        }

        public void PlayTransition(int targetValue)
        {
            EnsureAudioSource();
            if (targetValue <= mDisplayedValue)
            {
                AnimateValue(targetValue);
                return;
            }

            if (mTargetIcon == null || mParticleTemplate == null)
            {
                AnimateValue(targetValue);
                return;
            }

            if (mBurstSound != null)
            {
                mAudioSource.PlayOneShot(mBurstSound, mBurstVolume);
            }

            var layer = ResolveEffectLayer();
            var destination = (Vector2)layer.InverseTransformPoint(mTargetIcon.rectTransform.position);
            for (var i = 0; i < mIconCount; i++)
            {
                CreateParticle(layer, destination);
            }

            mGatherCallback?.Kill();
            mGatherCallback = DOVirtual.DelayedCall(
                    mGatherStartDelay,
                    () => AnimateValue(targetValue),
                    true)
                .SetTarget(this);
        }

        /// <summary>
        /// 从指定旧值手动播放一次散爆、收集和数值过渡。
        /// </summary>
        public void PlayTransition(int startValue, int targetValue)
        {
            mNumberTween?.Kill();
            mGatherCallback?.Kill();
            mDisplayedValue = Mathf.Max(0, startValue);
            RenderValue(mDisplayedValue);
            PlayTransition(Mathf.Max(0, targetValue));
        }

        /// <summary>
        /// 将现有 HUD 的 Wishlist 图标和数值接入本组件，粒子会自动飞向图标所在位置。
        /// </summary>
        public void ConfigureManualWishlist(Image targetIcon, TMP_Text animatedValue, RectTransform effectLayer)
        {
            if (mAttributes != null && mListenToAttributeChanges)
            {
                mAttributes.Changed -= OnAttributeChanged;
            }

            mIsManuallyConfigured = true;
            mValueKind = ValueKind.Wishlist;
            mListenToAttributeChanges = false;
            mTargetIcon = targetIcon;
            mAnimatedValue = animatedValue;
            mEffectLayer = effectLayer;
            if (mIconSprite == null && targetIcon != null)
            {
                mIconSprite = targetIcon.sprite;
            }

            if (mParticleTemplate == null && effectLayer != null)
            {
                var templateObject = new GameObject("Wishlist Particle Template", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                templateObject.layer = gameObject.layer;
                templateObject.transform.SetParent(effectLayer, false);
                mParticleTemplate = templateObject.GetComponent<Image>();
                mParticleTemplate.rectTransform.sizeDelta = new Vector2(42f, 42f);
                mParticleTemplate.gameObject.SetActive(false);
            }

            EnsureAudioSource();
            ApplyAppearance();
        }

        private void CreateParticle(RectTransform layer, Vector2 destination)
        {
            var particle = Instantiate(mParticleTemplate, layer);
            particle.gameObject.name = mValueKind + " Collect Particle";
            particle.gameObject.SetActive(true);
            particle.raycastTarget = false;
            particle.sprite = mIconSprite;
            particle.preserveAspect = true;
            particle.color = mIconSprite != null ? Color.white : mFallbackColor;

            var rect = particle.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = mParticleTemplate.rectTransform.sizeDelta;
            var origin = (Vector2)layer.InverseTransformPoint(transform.position);
            rect.anchoredPosition = origin;
            rect.localScale = Vector3.zero;

            var angle = Random.Range(0f, Mathf.PI * 2f);
            var radius = Random.Range(mBurstRadius * 0.45f, mBurstRadius);
            var burstPoint = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

            DOTween.Sequence()
                .SetDelay(Random.Range(0f, 0.1f))
                .Append(rect.DOScale(Random.Range(0.8f, 1.25f) * mIconScale, 0.16f).SetEase(Ease.OutBack))
                .Join(rect.DOAnchorPos(burstPoint, 0.28f).SetEase(Ease.OutCubic))
                .Join(rect.DORotate(new Vector3(0f, 0f, Random.Range(-160f, 160f)), 0.28f))
                .AppendInterval(Random.Range(0.04f, 0.14f))
                .Append(rect.DOAnchorPos(destination, mFlyDuration).SetEase(Ease.InBack))
                .Join(rect.DOScale(0.45f * mIconScale, mFlyDuration).SetEase(Ease.InQuad))
                .OnComplete(() =>
                {
                    PlayCollectSound();
                    mTargetIcon.rectTransform.DOKill();
                    mTargetIcon.rectTransform.localScale = Vector3.one * mIconScale;
                    mTargetIcon.rectTransform
                        .DOPunchScale(Vector3.one * (0.18f * mIconScale), 0.28f, 6, 0.5f)
                        .SetUpdate(true);
                    Destroy(particle.gameObject);
                })
                .SetUpdate(true)
                .SetTarget(this);
        }

        private void AnimateValue(int targetValue)
        {
            mNumberTween?.Kill();
            mNumberTween = DOTween.To(
                    () => mDisplayedValue,
                    value =>
                    {
                        mDisplayedValue = value;
                        RenderValue(value);
                    },
                    targetValue,
                    mNumberDuration)
                .SetEase(mNumberEase)
                .SetUpdate(true)
                .SetTarget(this);
        }

        private void RenderValue(int value)
        {
            if (mAnimatedValue != null)
            {
                mAnimatedValue.text = value.ToString("N0");
            }
        }

        private RectTransform ResolveEffectLayer()
        {
            if (mEffectLayer != null)
            {
                return mEffectLayer;
            }

            var canvas = GetComponentInParent<Canvas>();
            return canvas != null ? canvas.transform as RectTransform : transform as RectTransform;
        }

        private void PlayCollectSound()
        {
            if (mCollectSound == null || Time.unscaledTime - mLastCollectSoundTime < mCollectSoundInterval)
            {
                return;
            }

            mLastCollectSoundTime = Time.unscaledTime;
            mAudioSource.PlayOneShot(mCollectSound, mCollectVolume);
        }

        private void EnsureAudioSource()
        {
            if (mAudioSource == null)
            {
                mAudioSource = GetComponent<AudioSource>();
            }

            if (mAudioSource != null)
            {
                mAudioSource.playOnAwake = false;
            }
        }

        private void ApplyAppearance()
        {
            if (mTargetIcon != null)
            {
                mTargetIcon.sprite = mIconSprite;
                mTargetIcon.preserveAspect = true;
                mTargetIcon.color = mIconSprite != null ? Color.white : mFallbackColor;
                mTargetIcon.rectTransform.localScale = Vector3.one * mIconScale;
            }

            if (mParticleTemplate != null)
            {
                mParticleTemplate.sprite = mIconSprite;
                mParticleTemplate.preserveAspect = true;
                mParticleTemplate.color = mIconSprite != null ? Color.white : mFallbackColor;
            }

            if (mFontAsset != null)
            {
                if (mAnimatedValue != null) mAnimatedValue.font = mFontAsset;
                if (mTestInput != null)
                {
                    if (mTestInput.textComponent != null) mTestInput.textComponent.font = mFontAsset;
                    if (mTestInput.placeholder is TMP_Text placeholder) placeholder.font = mFontAsset;
                }
            }
        }
    }
}
