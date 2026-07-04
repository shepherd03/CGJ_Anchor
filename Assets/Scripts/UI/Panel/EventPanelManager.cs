using Anchor.Config.game;
using Anchor.GameFlow;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YokiFrame;

namespace Anchor.UI.Panel
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class EventPanelManager : PanelManagerSingleton<EventPanelManager>
    {
        [Header("Content")]
        [SerializeField] private TMP_Text eventName;
        [SerializeField] private TMP_Text eventContent;
        [SerializeField] private TMP_Text yesText;
        [SerializeField] private TMP_Text noText;
        [SerializeField] private Button buttonYes;
        [SerializeField] private Button buttonNo;

        [Header("Animation")]
        [SerializeField, Min(0.01f)] private float openDuration = 0.32f;
        [SerializeField, Min(0.01f)] private float closeDuration = 0.24f;
        [SerializeField, Min(0.01f)] private float idleSlideDuration = 0.45f;
        [SerializeField, Min(0f)] private float idleSlideDelay = 0.08f;
        [SerializeField] private float idleStartOffsetX = -900f;
        [SerializeField, Range(0.01f, 1f)] private float popStartScale = 0.72f;

        [Header("Parts")]
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private RectTransform idle;

        private CanvasGroup canvasGroup;
        private Vector3 authoredScale;
        private Vector2 authoredIdlePosition;
        private Sequence animationSequence;
        private gameEvent queuedEvent;
        private bool isResolving;
        private bool isVisible;

        protected override void Awake()
        {
            base.Awake();
            CacheReferences();
            CaptureAuthoredLayout();
            EventKit.Type.Register<WeekGameEventTriggeredEvent>(OnWeekGameEventTriggered);
            HideImmediate();
        }

        private void Start()
        {
            TryShowCurrentEvent();
        }

        private void OnEnable()
        {
            RegisterButtonClicks();
        }

        private void OnDisable()
        {
            UnregisterButtonClicks();
            KillAnimation();
        }

        protected override void OnDestroy()
        {
            EventKit.Type.UnRegister<WeekGameEventTriggeredEvent>(OnWeekGameEventTriggered);
            UnregisterButtonClicks();
            KillAnimation();
            base.OnDestroy();
        }

        private void OnWeekGameEventTriggered(WeekGameEventTriggeredEvent flowEvent)
        {
            if (flowEvent.Event == null)
                return;

            if (isResolving)
            {
                queuedEvent = flowEvent.Event;
                return;
            }

            Show(flowEvent.Event);
        }

        public void TryShowCurrentEvent()
        {
            GameFlowRunner runner = GameFlowRunner.Instance;
            if (runner == null || runner.Controller == null || runner.Controller.CurrentState != GameFlowState.WeekEvent)
                return;

            gameEvent currentEvent = runner.CurrentWeekGameEvent;
            if (currentEvent != null && !isVisible)
                Show(currentEvent);
        }

        public void Show(gameEvent eventRow)
        {
            if (eventRow == null)
                return;

            CacheReferences();
            Populate(eventRow);
            SetButtonsInteractable(false);
            isResolving = false;
            isVisible = true;

            KillAnimation();
            panelRoot.localScale = authoredScale * popStartScale;
            idle.anchoredPosition = authoredIdlePosition + Vector2.right * idleStartOffsetX;
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            animationSequence = DOTween.Sequence().SetTarget(this);
            animationSequence.Append(canvasGroup.DOFade(1f, openDuration).SetEase(Ease.OutQuad));
            animationSequence.Join(panelRoot.DOScale(authoredScale, openDuration).SetEase(Ease.OutBack));
            animationSequence.Insert(
                idleSlideDelay,
                idle.DOAnchorPos(authoredIdlePosition, idleSlideDuration, true).SetEase(Ease.OutCubic));
            animationSequence.OnComplete(() =>
            {
                animationSequence = null;
                SetButtonsInteractable(true);
            });
        }

        private void Choose(bool chooseYes)
        {
            if (isResolving)
                return;

            GameFlowRunner runner = GameFlowRunner.Instance;
            if (runner == null)
            {
                Debug.LogWarning($"{nameof(EventPanelManager)} cannot find {nameof(GameFlowRunner)} instance.", this);
                return;
            }

            isResolving = true;
            queuedEvent = null;
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

        private void CloseAfterChoice()
        {
            KillAnimation();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            animationSequence = DOTween.Sequence().SetTarget(this);
            animationSequence.Append(panelRoot.DOScale(authoredScale * popStartScale, closeDuration).SetEase(Ease.InBack));
            animationSequence.Join(canvasGroup.DOFade(0f, closeDuration).SetEase(Ease.InQuad));
            animationSequence.OnComplete(OnCloseAnimationCompleted);
        }

        private void OnCloseAnimationCompleted()
        {
            animationSequence = null;
            isVisible = false;
            panelRoot.localScale = authoredScale;
            idle.anchoredPosition = authoredIdlePosition;

            if (queuedEvent != null)
            {
                gameEvent nextEvent = queuedEvent;
                queuedEvent = null;
                isResolving = false;
                Show(nextEvent);
                return;
            }

            isResolving = false;
            GameFlowPanelCoordinator.GetOrCreate().ResumeAfterWeekGameEventChoice();
        }

        private void Populate(gameEvent eventRow)
        {
            eventName.text = eventRow.Title ?? string.Empty;
            eventContent.text = eventRow.Content ?? string.Empty;
            yesText.text = eventRow.YesText ?? string.Empty;
            noText.text = eventRow.NoText ?? string.Empty;
        }

        private void RegisterButtonClicks()
        {
            if (buttonYes != null)
            {
                buttonYes.onClick.RemoveListener(OnYesClicked);
                buttonYes.onClick.AddListener(OnYesClicked);
            }

            if (buttonNo != null)
            {
                buttonNo.onClick.RemoveListener(OnNoClicked);
                buttonNo.onClick.AddListener(OnNoClicked);
            }
        }

        private void UnregisterButtonClicks()
        {
            if (buttonYes != null)
                buttonYes.onClick.RemoveListener(OnYesClicked);

            if (buttonNo != null)
                buttonNo.onClick.RemoveListener(OnNoClicked);
        }

        private void OnYesClicked() => Choose(true);
        private void OnNoClicked() => Choose(false);

        private void SetButtonsInteractable(bool value)
        {
            if (buttonYes != null)
                buttonYes.interactable = value;
            if (buttonNo != null)
                buttonNo.interactable = value;
        }

        private void CacheReferences()
        {
            if (panelRoot == null)
                panelRoot = transform as RectTransform;
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
        }

        private void CaptureAuthoredLayout()
        {
            authoredScale = panelRoot != null ? panelRoot.localScale : Vector3.one;
            authoredIdlePosition = idle != null ? idle.anchoredPosition : Vector2.zero;
        }

        private void HideImmediate()
        {
            if (canvasGroup == null)
                return;

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            isVisible = false;
        }

        private void KillAnimation()
        {
            if (animationSequence != null)
            {
                animationSequence.Kill();
                animationSequence = null;
            }
        }

        private void OnValidate()
        {
            openDuration = Mathf.Max(0.01f, openDuration);
            closeDuration = Mathf.Max(0.01f, closeDuration);
            idleSlideDuration = Mathf.Max(0.01f, idleSlideDuration);
            idleSlideDelay = Mathf.Max(0f, idleSlideDelay);
        }
    }
}
