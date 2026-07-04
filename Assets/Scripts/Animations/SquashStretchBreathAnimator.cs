using DG.Tweening;
using UnityEngine;

namespace Anchor.Animations
{
    /// <summary>
    /// 用 X/Y 轴缩放模拟角色呼吸和轻微弹跳感的循环动画。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SquashStretchBreathAnimator : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField, Tooltip("需要播放呼吸弹跳缩放的目标。为空时使用当前物体。")]
        private Transform target;

        [SerializeField, Tooltip("启用时是否重新记录当前缩放作为动画基准。角色初始缩放会被保留下来。")]
        private bool captureBaseScaleOnEnable = true;

        [SerializeField, Tooltip("禁用组件时是否把目标缩放恢复到基准值。")]
        private bool resetScaleOnDisable = true;

        [Header("Timing")]
        [SerializeField, Min(0.01f), Tooltip("拉伸到最高点的时长。")]
        private float stretchDuration = 0.18f;

        [SerializeField, Min(0.01f), Tooltip("从拉伸压到矮胖状态的时长。")]
        private float squashDuration = 0.16f;

        [SerializeField, Min(0.01f), Tooltip("从矮胖状态回到基准缩放的时长。")]
        private float recoverDuration = 0.22f;

        [SerializeField, Min(0f), Tooltip("每次完整呼吸弹跳后的停顿时间。")]
        private float loopInterval = 0.04f;

        [SerializeField, Min(0f), Tooltip("动画开始前的固定延迟。")]
        private float startDelay;

        [SerializeField, Min(0f), Tooltip("动画开始前额外随机延迟，避免多个角色完全同步。")]
        private float randomStartDelay = 0.08f;

        [Header("Scale")]
        [SerializeField, Min(0f), Tooltip("整体缩放强度。0 表示不缩放，1 表示使用下方倍率，大于 1 会更夸张。")]
        private float animationStrength = 1f;

        [SerializeField, Min(0f), Tooltip("每轮随机强度浮动范围。0 表示每轮强度一致，0.1 表示在总强度上下浮动 0.1。")]
        private float randomStrengthRange = 0.05f;

        [SerializeField, Min(0.01f), Tooltip("拉伸阶段的 X 轴倍率，小于 1 会变窄。")]
        private float stretchScaleX = 0.94f;

        [SerializeField, Min(0.01f), Tooltip("拉伸阶段的 Y 轴倍率，大于 1 会变高。")]
        private float stretchScaleY = 1.08f;

        [SerializeField, Min(0.01f), Tooltip("压扁阶段的 X 轴倍率，大于 1 会变宽。")]
        private float squashScaleX = 1.08f;

        [SerializeField, Min(0.01f), Tooltip("压扁阶段的 Y 轴倍率，小于 1 会变矮。")]
        private float squashScaleY = 0.92f;

        [Header("Ease")]
        [SerializeField, Tooltip("拉伸阶段的缓动曲线。")]
        private Ease stretchEase = Ease.OutSine;

        [SerializeField, Tooltip("压扁阶段的缓动曲线。")]
        private Ease squashEase = Ease.InOutSine;

        [SerializeField, Tooltip("回到基准缩放时的缓动曲线。OutBack 会有一点弹性。")]
        private Ease recoverEase = Ease.OutBack;

        [Header("Playback")]
        [SerializeField, Tooltip("组件启用时是否自动播放循环动画。")]
        private bool playOnEnable = true;

        [SerializeField, Tooltip("是否忽略 Time.timeScale。暂停界面里的角色预览可开启。")]
        private bool ignoreTimeScale;

        // 目标原始缩放，所有弹跳倍率都基于这个值计算。
        private Vector3 baseScale = Vector3.one;

        // 当前循环缩放动画。
        private Sequence breathSequence;

        // 当前是否需要继续播放下一轮循环。
        private bool isPlaying;

        /// <summary>
        /// 初始化目标引用并记录初始缩放。
        /// </summary>
        private void Awake()
        {
            EnsureTarget();
            CaptureCurrentScaleAsBase();
            ClampSettings();
        }

        /// <summary>
        /// 组件启用时按配置自动播放呼吸弹跳动画。
        /// </summary>
        private void OnEnable()
        {
            EnsureTarget();
            ClampSettings();

            if (captureBaseScaleOnEnable)
            {
                CaptureCurrentScaleAsBase();
            }

            if (playOnEnable)
            {
                Play();
            }
        }

        /// <summary>
        /// 组件禁用时停止 Tween，并按配置恢复缩放。
        /// </summary>
        private void OnDisable()
        {
            Stop(resetScaleOnDisable);
        }

        /// <summary>
        /// 组件销毁时停止 Tween，避免 DOTween 继续持有已销毁目标。
        /// </summary>
        private void OnDestroy()
        {
            Stop(false);
        }

        /// <summary>
        /// Inspector 改动后限制参数范围，防止非法缩放和时长。
        /// </summary>
        private void OnValidate()
        {
            ClampSettings();
            EnsureTarget();
        }

        /// <summary>
        /// 运行时立即应用当前参数，重建 DOTween 序列让 Inspector 改动马上生效。
        /// </summary>
        [ContextMenu("Apply Runtime Settings")]
        public void ApplyRuntimeSettings()
        {
            EnsureTarget();
            ClampSettings();

            if (target == null)
            {
                return;
            }

            Stop(true);
            isPlaying = true;
            PlayCycle(0f);
        }

        /// <summary>
        /// 手动播放循环呼吸弹跳动画。
        /// </summary>
        [ContextMenu("Play")]
        public void Play()
        {
            EnsureTarget();
            ClampSettings();

            if (target == null)
            {
                return;
            }

            Stop(false);

            float resolvedDelay = startDelay + Random.Range(0f, randomStartDelay);
            isPlaying = true;
            PlayCycle(resolvedDelay);
        }

        /// <summary>
        /// 播放单轮呼吸弹跳动画，结束后按当前状态启动下一轮。
        /// </summary>
        private void PlayCycle(float delay)
        {
            if (!isPlaying || target == null)
            {
                return;
            }

            float resolvedStrength = GetCycleStrength();
            Vector3 stretchScale = GetScaledVector(stretchScaleX, stretchScaleY, resolvedStrength);
            Vector3 squashScale = GetScaledVector(squashScaleX, squashScaleY, resolvedStrength);

            Sequence sequence = null;
            sequence = DOTween.Sequence()
                .SetTarget(this)
                .SetUpdate(ignoreTimeScale)
                .SetDelay(delay)
                .Append(target.DOScale(stretchScale, stretchDuration).SetEase(stretchEase))
                .Append(target.DOScale(squashScale, squashDuration).SetEase(squashEase))
                .Append(target.DOScale(baseScale, recoverDuration).SetEase(recoverEase))
                .AppendInterval(loopInterval)
                .OnComplete(() =>
                {
                    if (breathSequence == sequence)
                    {
                        breathSequence = null;
                    }

                    PlayCycle(0f);
                })
                .OnKill(() =>
                {
                    if (breathSequence == sequence)
                    {
                        breathSequence = null;
                    }
                });

            breathSequence = sequence;
        }

        /// <summary>
        /// 停止循环动画，默认保留目标当前缩放。
        /// </summary>
        [ContextMenu("Stop")]
        public void Stop()
        {
            Stop(false);
        }

        /// <summary>
        /// 停止循环动画，并按需要恢复基准缩放。
        /// </summary>
        public void Stop(bool resetScale)
        {
            isPlaying = false;

            if (breathSequence != null)
            {
                breathSequence.Kill();
                breathSequence = null;
            }

            if (resetScale && target != null)
            {
                target.localScale = baseScale;
            }
        }

        /// <summary>
        /// 把目标当前缩放记录为新的动画基准。
        /// </summary>
        [ContextMenu("Capture Current Scale As Base")]
        public void CaptureCurrentScaleAsBase()
        {
            EnsureTarget();

            if (target != null)
            {
                baseScale = target.localScale;
            }
        }

        /// <summary>
        /// 确保目标引用存在。
        /// </summary>
        private void EnsureTarget()
        {
            if (target == null)
            {
                target = transform;
            }
        }

        /// <summary>
        /// 根据强度插值计算目标缩放，Z 轴始终保持不变。
        /// </summary>
        private Vector3 GetScaledVector(float scaleX, float scaleY, float strength)
        {
            strength = Mathf.Max(0f, strength);
            float resolvedScaleX = Mathf.LerpUnclamped(1f, scaleX, strength);
            float resolvedScaleY = Mathf.LerpUnclamped(1f, scaleY, strength);
            return new Vector3(baseScale.x * resolvedScaleX, baseScale.y * resolvedScaleY, baseScale.z);
        }

        /// <summary>
        /// 计算当前单轮动画的实际强度。
        /// </summary>
        private float GetCycleStrength()
        {
            if (randomStrengthRange <= 0f)
            {
                return animationStrength;
            }

            return Mathf.Max(0f, animationStrength + Random.Range(-randomStrengthRange, randomStrengthRange));
        }

        /// <summary>
        /// 限制 Inspector 参数，避免出现 0 时长或 0 缩放。
        /// </summary>
        private void ClampSettings()
        {
            stretchDuration = Mathf.Max(0.01f, stretchDuration);
            squashDuration = Mathf.Max(0.01f, squashDuration);
            recoverDuration = Mathf.Max(0.01f, recoverDuration);
            loopInterval = Mathf.Max(0f, loopInterval);
            startDelay = Mathf.Max(0f, startDelay);
            randomStartDelay = Mathf.Max(0f, randomStartDelay);
            animationStrength = Mathf.Max(0f, animationStrength);
            randomStrengthRange = Mathf.Max(0f, randomStrengthRange);
            stretchScaleX = Mathf.Max(0.01f, stretchScaleX);
            stretchScaleY = Mathf.Max(0.01f, stretchScaleY);
            squashScaleX = Mathf.Max(0.01f, squashScaleX);
            squashScaleY = Mathf.Max(0.01f, squashScaleY);
        }
    }
}
