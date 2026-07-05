using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Anchor.UI.Panel
{
    /// <summary>
    /// 场景内 PanelManager 单例缓存，统一处理 Domain Reload 关闭时的静态状态清理。
    /// </summary>
    internal static class PanelManagerSingletonRegistry
    {
        // 按具体 PanelManager 类型缓存当前场景的唯一实例。
        private static readonly Dictionary<Type, MonoBehaviour> instances = new Dictionary<Type, MonoBehaviour>();

        /// <summary>
        /// 获取指定类型的 PanelManager 实例；缓存为空时从当前场景查找 inactive 物体。
        /// </summary>
        public static T Get<T>() where T : MonoBehaviour
        {
            Type managerType = typeof(T);
            if (instances.TryGetValue(managerType, out MonoBehaviour manager) && manager != null)
            {
                return (T)manager;
            }

            T foundManager = UnityEngine.Object.FindObjectOfType<T>(true);
            if (foundManager != null)
            {
                instances[managerType] = foundManager;
            }

            return foundManager;
        }

        /// <summary>
        /// 注册当前场景的 PanelManager 实例，发现重复实例时禁用后注册者。
        /// </summary>
        public static bool Register<T>(T manager) where T : MonoBehaviour
        {
            Type managerType = typeof(T);
            if (instances.TryGetValue(managerType, out MonoBehaviour existingManager)
                && existingManager != null
                && existingManager != manager)
            {
                Debug.LogError($"{managerType.Name} 已存在有效实例，重复的面板管理器会被禁用：{manager.name}", manager);
                manager.enabled = false;
                return false;
            }

            instances[managerType] = manager;
            return true;
        }

        /// <summary>
        /// 注销被销毁的 PanelManager 实例，避免下一个场景拿到旧引用。
        /// </summary>
        public static void Unregister<T>(T manager) where T : MonoBehaviour
        {
            Type managerType = typeof(T);
            if (instances.TryGetValue(managerType, out MonoBehaviour existingManager) && existingManager == manager)
            {
                instances.Remove(managerType);
            }
        }

        /// <summary>
        /// 进入 Play Mode 时清理所有 PanelManager 静态引用，避免关闭 Domain Reload 后残留旧实例。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            instances.Clear();
        }
    }

    /// <summary>
    /// 场景级 PanelManager 单例基类；继承类可以直接通过 XxxPanelManager.Instance 访问。
    /// </summary>
    public abstract class PanelManagerSingleton<T> : MonoBehaviour where T : PanelManagerSingleton<T>
    {
        /// <summary>
        /// 当前场景唯一的指定类型 PanelManager。
        /// </summary>
        public static T Instance => PanelManagerSingletonRegistry.Get<T>();

        /// <summary>
        /// 初始化当前场景唯一的 PanelManager 实例。
        /// </summary>
        protected virtual void Awake()
        {
            PanelManagerSingletonRegistry.Register((T)this);
        }

        /// <summary>
        /// 销毁时释放 PanelManager 单例引用。
        /// </summary>
        protected virtual void OnDestroy()
        {
            PanelManagerSingletonRegistry.Unregister((T)this);
        }
    }

    [DisallowMultipleComponent]
    public sealed class BeginPanelManager : PanelManagerSingleton<BeginPanelManager>
    {
        private static readonly string[] DefaultAboutUsNames =
        {
            "ANDY",
            "ORANGE",
            "曹老板",
            "草叶",
            "柔狸",
            "卡其",
            "派派_COKI",
            "CAKY",
            "LOUTS"
        };

        [Header("Button")]
        [SerializeField, Tooltip("点击后开始新游戏并关闭当前 BeginPanel 的按钮。")]
        private Button startButton;

        [SerializeField, Tooltip("点击后打开排行榜面板的按钮。")]
        private Button leaderboardButton;

        [SerializeField, Tooltip("点击后退出游戏的跑路按钮。")]
        private Button runAwayButton;

        [SerializeField, Tooltip("点击后打开关于我们页面的按钮。")]
        private Button aboutUsButton;

        [Header("About Us")]
        [SerializeField, Tooltip("关于我们最终停留展示的图片。")]
        private Sprite aboutUsSprite;

        [SerializeField, Tooltip("关于我们页面根节点，Prefab 内预先放好。")]
        private GameObject aboutUsOverlay;

        [SerializeField, Tooltip("关于我们页面整体淡入淡出和交互控制。")]
        private CanvasGroup aboutUsOverlayCanvasGroup;

        [SerializeField, Tooltip("点击后关闭关于我们页面的暗背景图。")]
        private Image aboutUsDimImage;

        [SerializeField, Tooltip("暗背景按钮，点击后关闭关于我们页面。")]
        private Button aboutUsBackgroundButton;

        [SerializeField, Tooltip("名字轮播文本。")]
        private TextMeshProUGUI aboutUsNameLabel;

        [SerializeField, Tooltip("名字轮播透明度控制。")]
        private CanvasGroup aboutUsNameCanvasGroup;

        [SerializeField, Tooltip("最终居中展示的关于我们图片。")]
        private Image aboutUsImage;

        [SerializeField, Tooltip("最终图片透明度控制。")]
        private CanvasGroup aboutUsImageCanvasGroup;

        [SerializeField, Tooltip("打开关于我们后依次轮播的名字。")]
        private string[] aboutUsNames = DefaultAboutUsNames;

        [SerializeField, Min(0.05f), Tooltip("每个名字停留的时间。")]
        private float aboutUsNameHoldDuration = 0.28f;

        [SerializeField, Min(0.01f), Tooltip("名字淡入淡出的时间。")]
        private float aboutUsNameFadeDuration = 0.14f;

        [SerializeField, Range(0f, 1f), Tooltip("关于我们页面打开后立即应用的暗背景透明度。")]
        private float aboutUsFinalDimAlpha = 0.85f;

        [SerializeField, Min(0.01f), Tooltip("名字和图片从上方滑入的时间。")]
        private float aboutUsSlideDuration = 0.45f;

        [SerializeField, Min(0.01f), Tooltip("关于我们页面关闭淡出的时间。")]
        private float aboutUsFinalFadeDuration = 0.28f;

        [Header("Leaderboard")]
        [SerializeField, Tooltip("排行榜面板 Prefab；场景里没有现成排行榜面板时会实例化它。")]
        private LeaderboardPanelManager leaderboardPanelPrefab;

        private LeaderboardPanelManager runtimeLeaderboardPanel;
        private Sequence aboutUsSequence;

        protected override void Awake()
        {
            base.Awake();
            EnsureButtonReferences();
            EnsureAboutUsReferences();
            HideAboutUsOverlayImmediate();
        }

        protected override void OnDestroy()
        {
            KillAboutUsSequence();
            base.OnDestroy();
        }

        /// <summary>
        /// Panel 启用时注册按钮点击事件。
        /// </summary>
        private void OnEnable()
        {
            EnsureButtonReferences();
            EnsureAboutUsReferences();
            RegisterButtonClicks();
        }

        /// <summary>
        /// Panel 关闭时注销按钮事件，避免重复绑定。
        /// </summary>
        private void OnDisable()
        {
            UnregisterButtonClicks();
            HideAboutUsOverlayImmediate();
        }

        /// <summary>
        /// 开始按钮点击后交给流程 UI 编排器启动游戏。
        /// </summary>
        private void OnStartButtonClicked()
        {
            GameFlowPanelCoordinator.GetOrCreate().StartGame();
        }

        /// <summary>
        /// 排行榜按钮点击后弹出排行榜面板。
        /// </summary>
        private void OnLeaderboardButtonClicked()
        {
            LeaderboardPanelManager panel = ResolveLeaderboardPanel();
            if (panel == null)
            {
                Debug.LogWarning($"{nameof(BeginPanelManager)} cannot find or create {nameof(LeaderboardPanelManager)}.", this);
                return;
            }

            panel.Open();
        }

        /// <summary>
        /// 跑路按钮点击后退出游戏。
        /// </summary>
        private void OnRunAwayButtonClicked()
        {
            Application.Quit();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        /// <summary>
        /// 关于我们按钮点击后播放名单轮播，并在结束时显示完整图片。
        /// </summary>
        private void OnAboutUsButtonClicked()
        {
            PlayAboutUsOverlay();
        }

        /// <summary>
        /// 给按钮注册点击事件。
        /// </summary>
        private void RegisterButtonClicks()
        {
            if (startButton == null)
            {
                Debug.LogWarning($"{nameof(BeginPanelManager)} needs a start button.", this);
            }
            else
            {
                startButton.onClick.RemoveListener(OnStartButtonClicked);
                startButton.onClick.AddListener(OnStartButtonClicked);
            }

            if (leaderboardButton == null)
            {
                Debug.LogWarning($"{nameof(BeginPanelManager)} needs a leaderboard button.", this);
            }
            else
            {
                leaderboardButton.onClick.RemoveListener(OnLeaderboardButtonClicked);
                leaderboardButton.onClick.AddListener(OnLeaderboardButtonClicked);
            }

            if (runAwayButton != null)
            {
                runAwayButton.onClick.RemoveListener(OnRunAwayButtonClicked);
                runAwayButton.onClick.AddListener(OnRunAwayButtonClicked);
            }

            if (aboutUsButton == null)
            {
                Debug.LogWarning($"{nameof(BeginPanelManager)} needs an about us button.", this);
            }
            else
            {
                aboutUsButton.onClick.RemoveListener(OnAboutUsButtonClicked);
                aboutUsButton.onClick.AddListener(OnAboutUsButtonClicked);
            }

            if (aboutUsBackgroundButton != null)
            {
                aboutUsBackgroundButton.onClick.RemoveListener(CloseAboutUsOverlay);
                aboutUsBackgroundButton.onClick.AddListener(CloseAboutUsOverlay);
            }
        }

        /// <summary>
        /// 移除按钮点击事件。
        /// </summary>
        private void UnregisterButtonClicks()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(OnStartButtonClicked);
            }

            if (leaderboardButton != null)
            {
                leaderboardButton.onClick.RemoveListener(OnLeaderboardButtonClicked);
            }

            if (runAwayButton != null)
            {
                runAwayButton.onClick.RemoveListener(OnRunAwayButtonClicked);
            }

            if (aboutUsButton != null)
            {
                aboutUsButton.onClick.RemoveListener(OnAboutUsButtonClicked);
            }

            if (aboutUsBackgroundButton != null)
            {
                aboutUsBackgroundButton.onClick.RemoveListener(CloseAboutUsOverlay);
            }
        }

        /// <summary>
        /// 打开 BeginPanel。
        /// </summary>
        public void Open()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// 关闭 BeginPanel。
        /// </summary>
        public void Close()
        {
            gameObject.SetActive(false);
        }

        private void PlayAboutUsOverlay()
        {
            EnsureAboutUsReferences();

            if (aboutUsOverlay == null
                || aboutUsOverlayCanvasGroup == null
                || aboutUsNameLabel == null
                || aboutUsNameCanvasGroup == null
                || aboutUsDimImage == null)
            {
                Debug.LogWarning($"{nameof(BeginPanelManager)} needs complete about us prefab references.", this);
                return;
            }

            KillAboutUsSequence();
            aboutUsOverlay.SetActive(true);
            aboutUsOverlay.transform.SetAsLastSibling();
            aboutUsOverlayCanvasGroup.alpha = 1f;
            aboutUsOverlayCanvasGroup.interactable = true;
            aboutUsOverlayCanvasGroup.blocksRaycasts = true;

            SetGraphicAlpha(aboutUsDimImage, aboutUsFinalDimAlpha);
            aboutUsNameLabel.gameObject.SetActive(true);
            aboutUsNameLabel.text = string.Empty;
            aboutUsNameCanvasGroup.alpha = 0f;
            aboutUsNameLabel.rectTransform.localScale = Vector3.one;

            RectTransform imageRect = aboutUsImage != null ? aboutUsImage.rectTransform : null;
            RectTransform nameRect = aboutUsNameLabel.rectTransform;
            Vector2 nameTargetPosition = nameRect.anchoredPosition;
            Vector2 slideOffset = new Vector2(0f, ResolveAboutUsSlideDistance());

            PrepareAboutUsImageHidden();
            if (imageRect != null)
            {
                imageRect.anchoredPosition = Vector2.zero;
            }

            string[] names = ResolveAboutUsNames();
            aboutUsSequence = DOTween.Sequence()
                .SetTarget(this)
                .SetUpdate(true);

            for (int i = 0; i < names.Length; i++)
            {
                string displayName = names[i];
                aboutUsSequence.AppendCallback(() =>
                {
                    aboutUsNameLabel.text = displayName;
                    aboutUsNameCanvasGroup.alpha = 0f;
                    aboutUsNameLabel.rectTransform.localScale = Vector3.one;
                    nameRect.anchoredPosition = nameTargetPosition + slideOffset;
                });
                aboutUsSequence.Append(nameRect.DOAnchorPos(nameTargetPosition, aboutUsSlideDuration).SetEase(Ease.OutCubic));
                aboutUsSequence.Join(aboutUsNameCanvasGroup.DOFade(1f, aboutUsNameFadeDuration).SetEase(Ease.OutQuad));
                aboutUsSequence.AppendInterval(aboutUsNameHoldDuration);
                aboutUsSequence.Append(aboutUsNameCanvasGroup.DOFade(0f, aboutUsNameFadeDuration).SetEase(Ease.InQuad));
            }

            if (imageRect != null)
            {
                Vector2 imageTargetPosition = Vector2.zero;
                aboutUsSequence.AppendCallback(() =>
                {
                    PrepareAboutUsFinalImage();
                    imageTargetPosition = imageRect.anchoredPosition;
                    imageRect.anchoredPosition = imageTargetPosition + slideOffset;
                });
                aboutUsSequence.Append(imageRect.DOAnchorPos(imageTargetPosition, aboutUsSlideDuration).SetEase(Ease.OutCubic));
            }
            else
            {
                aboutUsSequence.AppendCallback(PrepareAboutUsFinalImage);
            }

            aboutUsSequence.OnComplete(() =>
            {
                aboutUsSequence = null;
                if (aboutUsOverlayCanvasGroup != null)
                {
                    aboutUsOverlayCanvasGroup.alpha = 1f;
                    aboutUsOverlayCanvasGroup.interactable = true;
                    aboutUsOverlayCanvasGroup.blocksRaycasts = true;
                }
            });
        }

        private void CloseAboutUsOverlay()
        {
            if (aboutUsOverlay == null || !aboutUsOverlay.activeSelf)
            {
                return;
            }

            KillAboutUsSequence();
            if (aboutUsOverlayCanvasGroup != null)
            {
                aboutUsOverlayCanvasGroup.interactable = false;
                aboutUsOverlayCanvasGroup.blocksRaycasts = false;
            }

            aboutUsSequence = DOTween.Sequence()
                .SetTarget(this)
                .SetUpdate(true);
            if (aboutUsOverlayCanvasGroup != null)
            {
                aboutUsSequence.Append(aboutUsOverlayCanvasGroup.DOFade(0f, aboutUsFinalFadeDuration * 0.75f).SetEase(Ease.InQuad));
            }
            aboutUsSequence.OnComplete(() =>
            {
                aboutUsSequence = null;
                HideAboutUsOverlayImmediate();
            });
        }

        private void HideAboutUsOverlayImmediate()
        {
            KillAboutUsSequence();

            if (aboutUsOverlayCanvasGroup != null)
            {
                aboutUsOverlayCanvasGroup.alpha = 0f;
                aboutUsOverlayCanvasGroup.interactable = false;
                aboutUsOverlayCanvasGroup.blocksRaycasts = false;
            }

            if (aboutUsDimImage != null)
            {
                SetGraphicAlpha(aboutUsDimImage, 0f);
            }

            if (aboutUsNameCanvasGroup != null)
            {
                aboutUsNameCanvasGroup.alpha = 0f;
            }

            if (aboutUsImageCanvasGroup != null)
            {
                aboutUsImageCanvasGroup.alpha = 0f;
            }

            if (aboutUsOverlay != null)
            {
                aboutUsOverlay.SetActive(false);
            }
        }

        private void PrepareAboutUsFinalImage()
        {
            if (aboutUsNameLabel != null)
            {
                aboutUsNameLabel.gameObject.SetActive(false);
            }

            ShowAboutUsImageImmediate();
        }

        private void PrepareAboutUsImageHidden()
        {
            if (aboutUsImage == null)
            {
                return;
            }

            if (aboutUsSprite != null)
            {
                aboutUsImage.sprite = aboutUsSprite;
            }

            aboutUsImage.preserveAspect = true;
            FitAboutUsImageToOverlay();
            aboutUsImage.gameObject.SetActive(false);
            aboutUsImage.rectTransform.localScale = Vector3.one;
            if (aboutUsImageCanvasGroup != null)
            {
                aboutUsImageCanvasGroup.alpha = 0f;
            }
        }

        private void ShowAboutUsImageImmediate()
        {
            if (aboutUsImage == null)
            {
                return;
            }

            if (aboutUsSprite != null)
            {
                aboutUsImage.sprite = aboutUsSprite;
            }

            if (aboutUsImage.sprite == null)
            {
                Debug.LogWarning($"{nameof(BeginPanelManager)} needs Assets/ArtRes/AbountUs.jpg assigned.", this);
                return;
            }

            aboutUsImage.preserveAspect = true;
            FitAboutUsImageToOverlay();
            aboutUsImage.gameObject.SetActive(true);
            aboutUsImage.rectTransform.localScale = Vector3.one;
            if (aboutUsImageCanvasGroup != null)
            {
                aboutUsImageCanvasGroup.alpha = 1f;
            }

            aboutUsImage.transform.SetAsLastSibling();
            if (aboutUsNameLabel != null)
            {
                aboutUsNameLabel.transform.SetAsLastSibling();
            }
        }

        private float ResolveAboutUsSlideDistance()
        {
            RectTransform overlayRect = aboutUsOverlay != null ? aboutUsOverlay.transform as RectTransform : null;
            float parentHeight = overlayRect != null ? overlayRect.rect.height : 0f;

            if (parentHeight <= 1f)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                RectTransform canvasRect = canvas != null ? canvas.transform as RectTransform : null;
                parentHeight = canvasRect != null ? canvasRect.rect.height : 1080f;
            }

            return Mathf.Max(420f, parentHeight * 0.7f);
        }

        private void FitAboutUsImageToOverlay()
        {
            if (aboutUsImage == null)
            {
                return;
            }

            RectTransform overlayRect = aboutUsOverlay != null ? aboutUsOverlay.transform as RectTransform : null;
            Vector2 parentSize = overlayRect != null ? overlayRect.rect.size : Vector2.zero;

            if (parentSize.x <= 1f || parentSize.y <= 1f)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                RectTransform canvasRect = canvas != null ? canvas.transform as RectTransform : null;
                parentSize = canvasRect != null ? canvasRect.rect.size : new Vector2(1920f, 1080f);
            }

            Sprite displayedSprite = aboutUsSprite != null ? aboutUsSprite : aboutUsImage.sprite;
            Vector2 sourceSize = displayedSprite != null
                ? new Vector2(displayedSprite.rect.width, displayedSprite.rect.height)
                : new Vector2(800f, 565f);
            float maxWidth = Mathf.Min(parentSize.x * 0.78f, 960f);
            float maxHeight = Mathf.Min(parentSize.y * 0.74f, 680f);
            float scale = Mathf.Min(maxWidth / sourceSize.x, maxHeight / sourceSize.y);
            scale = Mathf.Max(0.01f, scale);

            aboutUsImage.rectTransform.sizeDelta = sourceSize * scale;
            aboutUsImage.rectTransform.anchoredPosition = Vector2.zero;
        }

        private LeaderboardPanelManager ResolveLeaderboardPanel()
        {
            if (runtimeLeaderboardPanel != null)
            {
                return runtimeLeaderboardPanel;
            }

            runtimeLeaderboardPanel = LeaderboardPanelManager.Instance;
            if (runtimeLeaderboardPanel != null)
            {
                return runtimeLeaderboardPanel;
            }

            if (leaderboardPanelPrefab == null)
            {
                return null;
            }

            Transform parent = ResolvePanelParent();
            runtimeLeaderboardPanel = Instantiate(leaderboardPanelPrefab, parent, false);
            runtimeLeaderboardPanel.name = leaderboardPanelPrefab.name;
            return runtimeLeaderboardPanel;
        }

        private Transform ResolvePanelParent()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                return canvas.transform;
            }

            return transform.parent != null ? transform.parent : transform;
        }

        private void EnsureButtonReferences()
        {
            if (startButton == null)
            {
                startButton = transform.Find("Start")?.GetComponent<Button>();
            }

            if (leaderboardButton == null)
            {
                leaderboardButton = transform.Find("Leaderboard")?.GetComponent<Button>();
            }

            if (runAwayButton == null)
            {
                runAwayButton = transform.Find("RunAway")?.GetComponent<Button>()
                    ?? transform.Find("跑路")?.GetComponent<Button>();
            }

            if (aboutUsButton == null)
            {
                aboutUsButton = transform.Find("AboutUs")?.GetComponent<Button>()
                    ?? transform.Find("关于我们")?.GetComponent<Button>();
            }
        }

        private void EnsureAboutUsReferences()
        {
            if (aboutUsOverlay == null)
            {
                Transform existingOverlay = transform.Find("AboutUsOverlay");
                if (existingOverlay != null)
                {
                    aboutUsOverlay = existingOverlay.gameObject;
                }
            }

            if (aboutUsOverlay == null)
            {
                return;
            }

            if (aboutUsOverlayCanvasGroup == null)
            {
                aboutUsOverlayCanvasGroup = aboutUsOverlay.GetComponent<CanvasGroup>();
            }

            if (aboutUsDimImage == null)
            {
                aboutUsDimImage = aboutUsOverlay.transform.Find("DimBackground")?.GetComponent<Image>();
            }

            if (aboutUsBackgroundButton == null)
            {
                aboutUsBackgroundButton = aboutUsDimImage != null ? aboutUsDimImage.GetComponent<Button>() : null;
            }

            if (aboutUsNameLabel == null)
            {
                aboutUsNameLabel = aboutUsOverlay.transform.Find("NameCarousel")?.GetComponent<TextMeshProUGUI>();
            }

            if (aboutUsNameCanvasGroup == null)
            {
                aboutUsNameCanvasGroup = aboutUsNameLabel != null ? aboutUsNameLabel.GetComponent<CanvasGroup>() : null;
            }

            if (aboutUsImage == null)
            {
                aboutUsImage = aboutUsOverlay.transform.Find("FinalImage")?.GetComponent<Image>();
            }

            if (aboutUsImageCanvasGroup == null)
            {
                aboutUsImageCanvasGroup = aboutUsImage != null ? aboutUsImage.GetComponent<CanvasGroup>() : null;
            }

            if (aboutUsImage != null && aboutUsSprite != null)
            {
                aboutUsImage.sprite = aboutUsSprite;
            }
        }

        private string[] ResolveAboutUsNames()
        {
            if (aboutUsNames == null || aboutUsNames.Length == 0)
            {
                return DefaultAboutUsNames;
            }

            var validNames = new List<string>(aboutUsNames.Length);
            for (int i = 0; i < aboutUsNames.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(aboutUsNames[i]))
                {
                    validNames.Add(aboutUsNames[i]);
                }
            }

            return validNames.Count > 0 ? validNames.ToArray() : DefaultAboutUsNames;
        }

        private void KillAboutUsSequence()
        {
            aboutUsSequence?.Kill();
            aboutUsSequence = null;
        }

        private static void SetGraphicAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null)
            {
                return;
            }

            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }

        /// <summary>
        /// 挂到 Button 本体时自动填充按钮引用。
        /// </summary>
        private void Reset()
        {
            EnsureButtonReferences();
            EnsureAboutUsReferences();
        }
    }
}
