using System;
using DG.Tweening;
using UnityEngine;

namespace Anchor.Window
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class WindowDropBounceAnimator : MonoBehaviour
    {
        [Header("Position")]
        [SerializeField, Tooltip("弹窗启用时的起始锚点坐标，通常配置在目标位置上方。")]
        private Vector2 startAnchoredPosition = new Vector2(0f, 600f);

        [Header("Tween")]
        [SerializeField, Min(0f), Tooltip("从起始点下落到原始位置的动画时长。")]
        private float dropDuration = 0.55f;

        [SerializeField, Tooltip("下落结束时的回弹曲线。OutBack 会越过目标点后回弹。")]
        private Ease dropEase = Ease.OutBack;

        [SerializeField, Min(0f), Tooltip("OutBack/OutElastic 等曲线使用的回弹强度。")]
        private float bounceOvershoot = 1.35f;

        [SerializeField, Tooltip("是否忽略 Time.timeScale，暂停菜单弹窗建议开启。")]
        private bool ignoreTimeScale;

        [Header("Close")]
        [SerializeField, Min(0f), Tooltip("关闭时回到起始点的动画时长。为 0 时直接隐藏。")]
        private float closeDuration = 0.2f;

        [SerializeField, Tooltip("关闭时回到起始点的动画曲线。")]
        private Ease closeEase = Ease.InBack;

        private RectTransform cachedRectTransform;
        private Vector2 targetAnchoredPosition;
        private Tweener dropTween;
        private bool hasCapturedTargetPosition;

        /// <summary>
        /// 当前弹窗是否正在播放下落回弹动画。
        /// </summary>
        public bool IsPlaying => dropTween != null && dropTween.IsActive() && dropTween.IsPlaying();

        /// <summary>
        /// 弹窗最终落点，来自 Awake 时记录的原始 anchoredPosition。
        /// </summary>
        public Vector2 TargetAnchoredPosition => targetAnchoredPosition;

        /// <summary>
        /// 初始化组件引用，并记录编辑器里摆好的原始位置作为落点。
        /// </summary>
        private void Awake()
        {
            cachedRectTransform = GetComponent<RectTransform>();
            targetAnchoredPosition = cachedRectTransform.anchoredPosition;
            hasCapturedTargetPosition = true;
            ClampSettings();
        }

        /// <summary>
        /// 组件启用时，从配置的起始点下落到原始位置并播放回弹效果。
        /// </summary>
        private void OnEnable()
        {
            PlayDropBounce();
        }

        /// <summary>
        /// 组件关闭时停止当前 Tween，避免禁用对象后继续回调。
        /// </summary>
        private void OnDisable()
        {
            KillDropTween();
        }

        /// <summary>
        /// 手动播放下落回弹动画，适合外部窗口管理器主动调用。
        /// </summary>
        [ContextMenu("Play Drop Bounce")]
        public void PlayDropBounce()
        {
            EnsureRectTransform();
            ClampSettings();
            KillDropTween();

            cachedRectTransform.anchoredPosition = startAnchoredPosition;
            dropTween = cachedRectTransform
                .DOAnchorPos(targetAnchoredPosition, dropDuration)
                .SetEase(dropEase, bounceOvershoot)
                .SetUpdate(ignoreTimeScale)
                .SetTarget(this)
                .OnKill(() => dropTween = null);
        }

        /// <summary>
        /// 打开弹窗入口：未激活时先激活，由 OnEnable 播放下落回弹；已激活时直接重播动画。
        /// </summary>
        [ContextMenu("Open")]
        public void Open()
        {
            EnsureRectTransform();

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
                return;
            }

            PlayDropBounce();
        }

        /// <summary>
        /// 关闭弹窗入口：播放回到起始点的收起动画，结束后隐藏当前物体。
        /// </summary>
        [ContextMenu("Close")]
        public void Close()
        {
            Close(null);
        }

        /// <summary>
        /// 关闭弹窗入口：播放收起动画，隐藏完成后通知外部继续编排流程。
        /// </summary>
        public void Close(Action onClosed)
        {
            EnsureRectTransform();
            ClampSettings();

            if (!gameObject.activeSelf)
            {
                onClosed?.Invoke();
                return;
            }

            KillDropTween();

            if (closeDuration <= 0f)
            {
                gameObject.SetActive(false);
                onClosed?.Invoke();
                return;
            }

            dropTween = cachedRectTransform
                .DOAnchorPos(startAnchoredPosition, closeDuration)
                .SetEase(closeEase)
                .SetUpdate(ignoreTimeScale)
                .SetTarget(this)
                .OnComplete(() =>
                {
                    gameObject.SetActive(false);
                    onClosed?.Invoke();
                })
                .OnKill(() => dropTween = null);
        }

        /// <summary>
        /// 把当前 anchoredPosition 重新记录为落点，适合运行时布局变化后调用。
        /// </summary>
        [ContextMenu("Capture Current As Target")]
        public void CaptureCurrentAsTarget()
        {
            EnsureRectTransform();
            targetAnchoredPosition = cachedRectTransform.anchoredPosition;
            hasCapturedTargetPosition = true;
        }

        /// <summary>
        /// 编辑器添加组件时缓存 RectTransform，减少手动配置成本。
        /// </summary>
        private void Reset()
        {
            cachedRectTransform = GetComponent<RectTransform>();
        }

        /// <summary>
        /// 停止当前下落 Tween。
        /// </summary>
        private void KillDropTween()
        {
            if (dropTween == null)
            {
                return;
            }

            dropTween.Kill();
            dropTween = null;
        }

        /// <summary>
        /// 确保 RectTransform 引用有效。
        /// </summary>
        private void EnsureRectTransform()
        {
            if (cachedRectTransform == null)
            {
                cachedRectTransform = GetComponent<RectTransform>();
            }

            if (!hasCapturedTargetPosition && cachedRectTransform != null)
            {
                targetAnchoredPosition = cachedRectTransform.anchoredPosition;
                hasCapturedTargetPosition = true;
            }
        }

        /// <summary>
        /// 限制动画参数，防止 Inspector 配出非法时长。
        /// </summary>
        private void ClampSettings()
        {
            dropDuration = Mathf.Max(0f, dropDuration);
            closeDuration = Mathf.Max(0f, closeDuration);
            bounceOvershoot = Mathf.Max(0f, bounceOvershoot);
        }
    }
}
