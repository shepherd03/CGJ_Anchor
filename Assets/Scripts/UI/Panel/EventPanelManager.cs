using System.Collections;
using Anchor.GameFlow;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YokiFrame;

using EventRow = Anchor.Config.game.gameEvent;

namespace Anchor.UI.Panel
{
    [DisallowMultipleComponent]
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

        private Vector3 authoredScale;
        private Vector2 authoredIdlePosition;
        private Sequence panelSequence;
        private Coroutine typingCoroutine;
        private EventRow queuedEvent;
        private bool isVisible;
        private bool isResolving;

        protected override void Awake()
        {
            base.Awake();
            CacheReferences();
            CaptureAuthoredLayout();
            HideImmediate();
            EventKit.Type.Register<WeekGameEventTriggeredEvent>(OnWeekGameEventTriggered);
        }

        private IEnumerator Start()
        {
            // Covers events emitted from another object's Awake before this listener registered.
            yield return null;
            ShowCurrentEventIfAny();
        }

        private void OnEnable()
        {
            RegisterButtons();
        }

        private void OnDisable()
        {
            UnregisterButtons();
            KillAnimation();
            StopTypewriter();
        }

        protected override void OnDestroy()
        {
            EventKit.Type.UnRegister<WeekGameEventTriggeredEvent>(OnWeekGameEventTriggered);
            UnregisterButtons();
            KillAnimation();
            StopTypewriter();
            base.OnDestroy();
        }

        public void ShowCurrentEventIfAny()
        {
            GameFlowRunner runner = GameFlowRunner.Instance;
            if (runner != null && runner.Controller != null && runner.Controller.CurrentState == GameFlowState.WeekEvent)
                Open(runner.CurrentWeekGameEvent);
        }

        public void TryShowCurrentEvent()
        {
            ShowCurrentEventIfAny();
        }

        public void Open(EventRow eventRow)
        {
            if (eventRow == null)
                return;

            CacheReferences();
            Populate(eventRow);
            KillAnimation();
            StopTypewriter();

            isVisible = true;
            isResolving = false;
            SetButtonsInteractable(false);
            canvasGroup.alpha = 0f;
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
        }

        private void OnWeekGameEventTriggered(WeekGameEventTriggeredEvent flowEvent)
        {
            if (flowEvent.Event == null)
                return;

            if (isResolving || panelSequence != null && !isVisible)
            {
                queuedEvent = flowEvent.Event;
                return;
            }

            Open(flowEvent.Event);
        }

        private void OnYesClicked()
        {
            ResolveChoice(true);
        }

        private void OnNoClicked()
        {
            ResolveChoice(false);
        }

        private void ResolveChoice(bool chooseYes)
        {
            if (!isVisible || isResolving)
                return;

            GameFlowRunner runner = GameFlowRunner.Instance;
            if (runner == null)
                return;

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
            StopTypewriter();
            isVisible = false;
            canvasGroup.blocksRaycasts = false;

            panelSequence = DOTween.Sequence().SetTarget(this);
            panelSequence.Append(panelRoot.DOScale(authoredScale * popupStartScale, closeDuration).SetEase(Ease.InBack));
            panelSequence.Join(canvasGroup.DOFade(0f, closeDuration * 0.8f).SetEase(Ease.InQuad));
            panelSequence.OnComplete(() =>
            {
                panelSequence = null;
                panelRoot.localScale = authoredScale;
                idle.anchoredPosition = authoredIdlePosition;
                isResolving = false;

                if (queuedEvent != null)
                {
                    EventRow next = queuedEvent;
                    queuedEvent = null;
                    Open(next);
                    return;
                }

                GameFlowPanelCoordinator.GetOrCreate().ResumeAfterWeekGameEventChoice();
            });
        }

        private void Populate(EventRow eventRow)
        {
            eventName.text = eventRow.Title ?? string.Empty;
            yesLabel.text = eventRow.YesText ?? string.Empty;
            noLabel.text = eventRow.NoText ?? string.Empty;
            eventContent.text = eventRow.Content ?? string.Empty;
            eventContent.maxVisibleCharacters = 0;
            eventContent.ForceMeshUpdate();
        }

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

        private void PlayTypingSounds(int startInclusive, int endExclusive)
        {
            if (audioSource == null || typeSound == null)
                return;

            for (int i = startInclusive; i < endExclusive; i++)
            {
                if (eventContent.textInfo.characterInfo[i].isVisible)
                    audioSource.PlayOneShot(typeSound, typeSoundVolume);
            }
        }

        private void StopTypewriter()
        {
            if (typingCoroutine == null)
                return;

            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        private void HideImmediate()
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = false;
            panelRoot.localScale = authoredScale;
            idle.anchoredPosition = authoredIdlePosition;
            isVisible = false;
        }

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

        private void CaptureAuthoredLayout()
        {
            authoredScale = panelRoot.localScale;
            authoredIdlePosition = idle.anchoredPosition;
        }

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

        private void UnregisterButtons()
        {
            if (yesButton != null)
                yesButton.onClick.RemoveListener(OnYesClicked);
            if (noButton != null)
                noButton.onClick.RemoveListener(OnNoClicked);
        }

        private void SetButtonsInteractable(bool value)
        {
            if (yesButton != null)
                yesButton.interactable = value;
            if (noButton != null)
                noButton.interactable = value;
        }

        private void PlayOneShot(AudioClip clip, float volume)
        {
            if (audioSource != null && clip != null)
                audioSource.PlayOneShot(clip, volume);
        }

        private void KillAnimation()
        {
            if (panelSequence == null)
                return;

            panelSequence.Kill();
            panelSequence = null;
        }
    }
}
