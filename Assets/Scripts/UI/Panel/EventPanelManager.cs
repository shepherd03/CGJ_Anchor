using System;
using System.Collections;
using Anchor.GameFlow;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using EventRow = Anchor.Config.game.gameEvent;

namespace Anchor.UI.Panel
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class EventPanelManager : PanelManagerSingleton<EventPanelManager>
    {
        [Header("Panel")]
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform idle;

        [Header("Content")]
        [SerializeField] private TMP_Text eventName;
        [SerializeField] private TMP_Text eventContent;
        [SerializeField] private TMP_Text yesLabel;
        [SerializeField] private TMP_Text noLabel;
        [SerializeField] private Button yesButton;
        [SerializeField] private Button noButton;

        [Header("Animation")]
        [SerializeField, Min(0.01f)] private float popupDuration = 0.34f;
        [SerializeField, Range(0.1f, 1f)] private float popupStartScale = 0.72f;
        [SerializeField, Min(0.01f)] private float idleSlideDuration = 0.48f;
        [SerializeField, Min(0f)] private float idleSlideDistance = 900f;
        [SerializeField, Min(0.01f)] private float closeDuration = 0.24f;

        [Header("Typewriter")]
        [SerializeField, Min(0f)] private float charactersPerSecond = 18f;
        [SerializeField] private AudioClip typeSound;
        [SerializeField, Range(0f, 1f)] private float typeSoundVolume = 1f;

        [Header("Open Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip openSound;
        [SerializeField, Range(0f, 1f)] private float openSoundVolume = 1f;

        // 记录编辑器里摆好的动画基准位置和缩放。
        private Vector3 authoredScale;
        private Vector2 authoredIdlePosition;

        // 当前面板动画、打字机协程和关闭回调。
        private Sequence panelSequence;
        private Coroutine typingCoroutine;
        private Action onClosed;

        // 当前显示状态，避免重复显示和重复结算。
        private bool hasCapturedAuthoredLayout;
        private bool isVisible;
        private bool isResolving;

        /// <summary>
        /// 初始化 EventPanel 引用和初始隐藏状态。
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            CacheReferences();
            CaptureAuthoredLayout();
            HideImmediate();
        }

        /// <summary>
        /// Panel 启用时注册选项按钮点击事件。
        /// </summary>
        private void OnEnable()
        {
            RegisterButtons();
        }

        /// <summary>
        /// Panel 关闭时注销按钮、停止动画和打字机。
        /// </summary>
        private void OnDisable()
        {
            UnregisterButtons();
            KillAnimation();
            StopTypewriter();
        }

        /// <summary>
        /// 销毁时清理按钮监听、动画和单例引用。
        /// </summary>
        protected override void OnDestroy()
        {
            UnregisterButtons();
            KillAnimation();
            StopTypewriter();
            base.OnDestroy();
        }

        /// <summary>
        /// 从当前流程中读取周事件，并打开 EventPanel。
        /// </summary>
        public bool TryShowCurrentEvent(Action closedCallback = null)
        {
            GameFlowRunner runner = GameFlowRunner.Instance;
            if (runner == null || runner.Controller == null || runner.Controller.CurrentState != GameFlowState.WeekEvent)
            {
                return false;
            }

            EventRow currentEvent = runner.CurrentWeekGameEvent;
            if (currentEvent == null || isVisible)
            {
                return false;
            }

            return Open(currentEvent, closedCallback);
        }

        /// <summary>
        /// 兼容旧调用：只展示事件内容，不注入关闭回调。
        /// </summary>
        public void Show(EventRow eventRow)
        {
            Open(eventRow, null);
        }

        /// <summary>
        /// 强制关闭 EventPanel，并清理关闭回调。
        /// </summary>
        public void Close()
        {
            onClosed = null;
            KillAnimation();
            StopTypewriter();
            HideImmediate();
        }

        /// <summary>
        /// 打开 EventPanel，并记录关闭后继续流程的回调。
        /// </summary>
        public bool Open(EventRow eventRow, Action closedCallback = null)
        {
            if (eventRow == null)
            {
                return false;
            }

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            onClosed = closedCallback;
            CacheReferences();
            EnsureAuthoredLayoutCaptured();
            KillAnimation();
            StopTypewriter();
            Populate(eventRow);

            isVisible = true;
            isResolving = false;
            SetButtonsInteractable(false);
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            panelRoot.localScale = authoredScale * popupStartScale;
            idle.anchoredPosition = authoredIdlePosition + Vector2.left * idleSlideDistance;

            PlayOneShot(openSound, openSoundVolume);

            panelSequence = DOTween.Sequence().SetTarget(this);
            panelSequence.Append(canvasGroup.DOFade(1f, popupDuration * 0.65f).SetEase(Ease.OutQuad));
            panelSequence.Join(panelRoot.DOScale(authoredScale, popupDuration).SetEase(Ease.OutBack));
            panelSequence.AppendCallback(() =>
            {
                StartTypewriter(eventRow.Content);
                SetButtonsInteractable(true);
            });
            panelSequence.Append(idle.DOAnchorPos(authoredIdlePosition, idleSlideDuration, true).SetEase(Ease.OutCubic));
            panelSequence.OnComplete(() => panelSequence = null);
            return true;
        }

        /// <summary>
        /// 玩家点击 Yes 选项时提交肯定选择。
        /// </summary>
        private void OnYesClicked()
        {
            ResolveChoice(true);
        }

        /// <summary>
        /// 玩家点击 No 选项时提交否定选择。
        /// </summary>
        private void OnNoClicked()
        {
            ResolveChoice(false);
        }

        /// <summary>
        /// 把玩家选择交给流程层处理，成功后关闭面板并等待回调继续编排。
        /// </summary>
        private void ResolveChoice(bool chooseYes)
        {
            if (!isVisible || isResolving)
            {
                return;
            }

            GameFlowRunner runner = GameFlowRunner.Instance;
            if (runner == null)
            {
                Debug.LogWarning($"{nameof(EventPanelManager)} cannot find {nameof(GameFlowRunner)} instance.", this);
                return;
            }

            isResolving = true;
            SetButtonsInteractable(false);

            bool accepted = chooseYes
                ? runner.ChooseWeekGameEventYes()
                : runner.ChooseWeekGameEventNo();

            if (!accepted)
            {
                isResolving = false;
                SetButtonsInteractable(true);
                return;
            }

            CloseAfterChoice();
        }

        /// <summary>
        /// 播放 EventPanel 关闭动画，动画结束后触发关闭回调。
        /// </summary>
        private void CloseAfterChoice()
        {
            KillAnimation();
            StopTypewriter();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            panelSequence = DOTween.Sequence().SetTarget(this);
            panelSequence.Append(panelRoot.DOScale(authoredScale * popupStartScale, closeDuration).SetEase(Ease.InBack));
            panelSequence.Join(canvasGroup.DOFade(0f, closeDuration * 0.8f).SetEase(Ease.InQuad));
            panelSequence.OnComplete(OnCloseAnimationCompleted);
        }

        /// <summary>
        /// EventPanel 关闭动画完成后恢复布局，并通知流程编排器继续。
        /// </summary>
        private void OnCloseAnimationCompleted()
        {
            panelSequence = null;
            isVisible = false;
            panelRoot.localScale = authoredScale;
            idle.anchoredPosition = authoredIdlePosition;
            isResolving = false;
            NotifyClosed();
        }

        /// <summary>
        /// 执行并清理 EventPanel 关闭回调，防止重复推进流程。
        /// </summary>
        private void NotifyClosed()
        {
            Action closedCallback = onClosed;
            onClosed = null;
            closedCallback?.Invoke();
        }

        /// <summary>
        /// 用周事件表数据刷新标题、正文和选项文本。
        /// </summary>
        private void Populate(EventRow eventRow)
        {
            eventName.text = eventRow.Title ?? string.Empty;
            yesLabel.text = eventRow.YesText ?? string.Empty;
            noLabel.text = eventRow.NoText ?? string.Empty;
            eventContent.text = eventRow.Content ?? string.Empty;
            eventContent.maxVisibleCharacters = 0;
            eventContent.ForceMeshUpdate();
        }

        /// <summary>
        /// 启动正文打字机效果。
        /// </summary>
        private void StartTypewriter(string content)
        {
            StopTypewriter();
            eventContent.text = content ?? string.Empty;
            eventContent.ForceMeshUpdate();

            int count = eventContent.textInfo.characterCount;
            if (charactersPerSecond <= 0f || count <= 0)
            {
                eventContent.maxVisibleCharacters = int.MaxValue;
                return;
            }

            eventContent.maxVisibleCharacters = 0;
            typingCoroutine = StartCoroutine(TypeRoutine(count));
        }

        /// <summary>
        /// 按配置速度逐字显示正文。
        /// </summary>
        private IEnumerator TypeRoutine(int totalCharacters)
        {
            float progress = 0f;
            int revealed = 0;

            while (revealed < totalCharacters)
            {
                progress += charactersPerSecond * Time.deltaTime;
                int next = Mathf.Clamp(Mathf.FloorToInt(progress), 0, totalCharacters);
                if (next > revealed)
                {
                    eventContent.maxVisibleCharacters = next;
                    PlayTypingSounds(revealed, next);
                    revealed = next;
                }

                yield return null;
            }

            eventContent.maxVisibleCharacters = totalCharacters;
            typingCoroutine = null;
        }

        /// <summary>
        /// 为本帧新增显示的可见字符播放打字音效。
        /// </summary>
        private void PlayTypingSounds(int startInclusive, int endExclusive)
        {
            if (audioSource == null || typeSound == null)
            {
                return;
            }

            for (int i = startInclusive; i < endExclusive; i++)
            {
                if (eventContent.textInfo.characterInfo[i].isVisible)
                {
                    audioSource.PlayOneShot(typeSound, typeSoundVolume);
                }
            }
        }

        /// <summary>
        /// 停止当前打字机协程。
        /// </summary>
        private void StopTypewriter()
        {
            if (typingCoroutine == null)
            {
                return;
            }

            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        /// <summary>
        /// 立即隐藏 EventPanel，不触发关闭回调。
        /// </summary>
        private void HideImmediate()
        {
            CacheReferences();
            EnsureAuthoredLayoutCaptured();

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (panelRoot != null)
            {
                // 编排器可能在 inactive 面板 Awake 前调用 Close，必须先捕获布局再回写缩放。
                panelRoot.localScale = authoredScale;
            }

            if (idle != null)
            {
                idle.anchoredPosition = authoredIdlePosition;
            }

            isVisible = false;
            isResolving = false;
        }

        /// <summary>
        /// 缓存 Inspector 或层级中可找到的 UI 引用。
        /// </summary>
        private void CacheReferences()
        {
            if (panelRoot == null)
                panelRoot = transform as RectTransform;
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            if (idle == null)
                idle = transform.Find("Idle") as RectTransform;
            if (eventName == null)
                eventName = transform.Find("EventName")?.GetComponent<TMP_Text>();
            if (eventContent == null)
                eventContent = transform.Find("EventContent")?.GetComponent<TMP_Text>()
                    ?? transform.Find("Text (TMP)")?.GetComponent<TMP_Text>();
            if (yesButton == null)
                yesButton = transform.Find("ButtonYes")?.GetComponent<Button>();
            if (noButton == null)
                noButton = transform.Find("ButtonNo")?.GetComponent<Button>();
            if (yesLabel == null && yesButton != null)
                yesLabel = yesButton.GetComponentInChildren<TMP_Text>(true);
            if (noLabel == null && noButton != null)
                noLabel = noButton.GetComponentInChildren<TMP_Text>(true);
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
            if (audioSource != null)
            {
                audioSource.playOnAwake = false;
                audioSource.loop = false;
            }
        }

        /// <summary>
        /// 记录编辑器里摆好的缩放和 idle 位置，供动画恢复使用。
        /// </summary>
        private void CaptureAuthoredLayout()
        {
            authoredScale = panelRoot != null ? panelRoot.localScale : Vector3.one;
            if (IsZeroScale(authoredScale))
            {
                // 防止运行时误把未初始化或已被隐藏逻辑污染的零缩放继续作为弹窗动画基准。
                authoredScale = Vector3.one;
            }

            authoredIdlePosition = idle != null ? idle.anchoredPosition : Vector2.zero;
            hasCapturedAuthoredLayout = true;
        }

        /// <summary>
        /// 判断缩放是否已经退化到不可见状态。
        /// </summary>
        private static bool IsZeroScale(Vector3 scale)
        {
            return Mathf.Approximately(scale.x, 0f)
                || Mathf.Approximately(scale.y, 0f)
                || Mathf.Approximately(scale.z, 0f);
        }

        /// <summary>
        /// 确保 inactive 面板被外部直接打开时，动画基准布局已经初始化。
        /// </summary>
        private void EnsureAuthoredLayoutCaptured()
        {
            if (hasCapturedAuthoredLayout)
            {
                return;
            }

            CaptureAuthoredLayout();
        }

        /// <summary>
        /// 注册周事件两个选项按钮的点击事件。
        /// </summary>
        private void RegisterButtons()
        {
            CacheReferences();
            if (yesButton != null)
            {
                yesButton.onClick.RemoveListener(OnYesClicked);
                yesButton.onClick.AddListener(OnYesClicked);
            }

            if (noButton != null)
            {
                noButton.onClick.RemoveListener(OnNoClicked);
                noButton.onClick.AddListener(OnNoClicked);
            }
        }

        /// <summary>
        /// 注销周事件两个选项按钮的点击事件。
        /// </summary>
        private void UnregisterButtons()
        {
            if (yesButton != null)
                yesButton.onClick.RemoveListener(OnYesClicked);
            if (noButton != null)
                noButton.onClick.RemoveListener(OnNoClicked);
        }

        /// <summary>
        /// 控制选项按钮是否可交互，避免动画或结算中重复点击。
        /// </summary>
        private void SetButtonsInteractable(bool value)
        {
            if (yesButton != null)
                yesButton.interactable = value;
            if (noButton != null)
                noButton.interactable = value;
        }

        /// <summary>
        /// 播放一次性 UI 音效。
        /// </summary>
        private void PlayOneShot(AudioClip clip, float volume)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip, volume);
            }
        }

        /// <summary>
        /// 停止当前 EventPanel 动画。
        /// </summary>
        private void KillAnimation()
        {
            if (panelSequence == null)
            {
                return;
            }

            panelSequence.Kill();
            panelSequence = null;
        }

        /// <summary>
        /// Inspector 参数变更时限制动画参数为合法范围。
        /// </summary>
        private void OnValidate()
        {
            popupDuration = Mathf.Max(0.01f, popupDuration);
            popupStartScale = Mathf.Clamp(popupStartScale, 0.1f, 1f);
            idleSlideDuration = Mathf.Max(0.01f, idleSlideDuration);
            idleSlideDistance = Mathf.Max(0f, idleSlideDistance);
            closeDuration = Mathf.Max(0.01f, closeDuration);
            charactersPerSecond = Mathf.Max(0f, charactersPerSecond);
            typeSoundVolume = Mathf.Clamp01(typeSoundVolume);
            openSoundVolume = Mathf.Clamp01(openSoundVolume);
        }
    }
}
