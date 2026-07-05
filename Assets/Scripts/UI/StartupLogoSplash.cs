using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Anchor.UI
{
    /// <summary>
    /// 打包后在 Unity 启动画面结束、首场景显示前后，覆盖播放一次 Resources/Logo.png。
    /// </summary>
    public sealed class StartupLogoSplash : MonoBehaviour
    {
        // Logo 资源固定放在 Assets/Resources/Logo.png。
        private const string LogoResourcePath = "Logo";
        // Logo 覆盖层总显示时长，包含淡入和淡出。
        private const float SplashDuration = 3f;
        // 单段淡入/淡出时长。
        private const float FadeDuration = 0.75f;
        // 顶层 Canvas 排序，确保盖住首场景普通 UI。
        private const int SplashSortingOrder = 32767;

        // 防止首场景或 Domain Reload 场景下重复创建。
        private static bool hasCreated;

        // 运行时顶层 Canvas 的透明度控制。
        private CanvasGroup canvasGroup;

        /// <summary>
        /// Play Mode 或 Player 启动时重置静态状态，避免关闭 Domain Reload 后残留。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            hasCreated = false;
        }

        /// <summary>
        /// 首场景加载前创建一次 Logo 播放器。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateSplash()
        {
            if (hasCreated)
            {
                return;
            }

            hasCreated = true;
            var host = new GameObject(nameof(StartupLogoSplash));
            host.AddComponent<StartupLogoSplash>();
        }

        /// <summary>
        /// 初始化并开始播放 Logo 淡入淡出。
        /// </summary>
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            StartCoroutine(PlaySplashRoutine());
        }

        /// <summary>
        /// 加载 Logo 图片，创建全屏 UI，并按淡入、停留、淡出的顺序播放。
        /// </summary>
        private IEnumerator PlaySplashRoutine()
        {
            Sprite logoSprite = Resources.Load<Sprite>(LogoResourcePath);
            Texture2D logoTexture = logoSprite == null ? Resources.Load<Texture2D>(LogoResourcePath) : null;
            if (logoSprite == null && logoTexture == null)
            {
                Debug.LogWarning($"{nameof(StartupLogoSplash)} cannot load Resources/{LogoResourcePath}.png.", this);
                Destroy(gameObject);
                yield break;
            }

            BuildSplashCanvas(logoSprite, logoTexture);

            float fadeDuration = Mathf.Min(FadeDuration, SplashDuration * 0.5f);
            float holdDuration = Mathf.Max(0f, SplashDuration - fadeDuration * 2f);
            yield return FadeCanvasGroup(0f, 1f, fadeDuration);

            if (holdDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(holdDuration);
            }

            yield return FadeCanvasGroup(1f, 0f, fadeDuration);
            Destroy(gameObject);
        }

        /// <summary>
        /// 创建顶层全屏 Canvas，并把 Logo 图片铺满屏幕。
        /// </summary>
        private void BuildSplashCanvas(Sprite logoSprite, Texture2D logoTexture)
        {
            var canvasObject = new GameObject(
                "StartupLogoSplashCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = SplashSortingOrder;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGroup = canvasObject.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            CreateBlackBackground(canvasObject.transform);
            CreateLogoImage(canvasObject.transform, logoSprite, logoTexture);
        }

        /// <summary>
        /// 创建黑色底图，避免 Logo 有透明区域时露出首场景。
        /// </summary>
        private static void CreateBlackBackground(Transform parent)
        {
            var backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
            backgroundObject.transform.SetParent(parent, false);
            StretchToFullScreen(backgroundObject.transform as RectTransform);

            Image background = backgroundObject.GetComponent<Image>();
            background.color = Color.black;
            background.raycastTarget = true;
        }

        /// <summary>
        /// 创建全屏 Logo 图；优先使用 Sprite，失败时使用 Texture2D。
        /// </summary>
        private static void CreateLogoImage(Transform parent, Sprite logoSprite, Texture2D logoTexture)
        {
            if (logoSprite != null)
            {
                var imageObject = new GameObject("Logo", typeof(RectTransform), typeof(Image));
                imageObject.transform.SetParent(parent, false);
                StretchToFullScreen(imageObject.transform as RectTransform);

                Image image = imageObject.GetComponent<Image>();
                image.sprite = logoSprite;
                image.color = Color.white;
                image.preserveAspect = false;
                image.raycastTarget = false;
                return;
            }

            var rawImageObject = new GameObject("Logo", typeof(RectTransform), typeof(RawImage));
            rawImageObject.transform.SetParent(parent, false);
            StretchToFullScreen(rawImageObject.transform as RectTransform);

            RawImage rawImage = rawImageObject.GetComponent<RawImage>();
            rawImage.texture = logoTexture;
            rawImage.color = Color.white;
            rawImage.raycastTarget = false;
        }

        /// <summary>
        /// 把 RectTransform 拉伸为父节点全屏。
        /// </summary>
        private static void StretchToFullScreen(RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// 使用真实时间淡入或淡出顶层 Canvas。
        /// </summary>
        private IEnumerator FadeCanvasGroup(float fromAlpha, float toAlpha, float duration)
        {
            if (canvasGroup == null)
            {
                yield break;
            }

            if (duration <= 0f)
            {
                canvasGroup.alpha = toAlpha;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                canvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, progress);
                yield return null;
            }

            canvasGroup.alpha = toAlpha;
        }
    }
}
