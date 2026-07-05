using System;
using System.Collections.Generic;
using Anchor.GameFlow;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Anchor.UI.Panel
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class LeaderboardPanelManager : PanelManagerSingleton<LeaderboardPanelManager>
    {
        [Header("Panel")]
        [SerializeField, Tooltip("弹窗动画目标；为空时使用当前 RectTransform。")]
        private RectTransform panelRoot;

        [SerializeField, Tooltip("控制排行榜面板淡入淡出和交互状态。")]
        private CanvasGroup canvasGroup;

        [Header("Rows")]
        [SerializeField, Tooltip("排行榜条目生成父节点。")]
        private RectTransform rowRoot;

        [SerializeField, Tooltip("排行榜条目模板；运行时会复制它并隐藏模板本体。")]
        private LeaderboardEntryView rowTemplate;

        [SerializeField, Min(0), Tooltip("最多显示多少条。0 表示不限制。")]
        private int maxVisibleRows = 100;

        [Header("Text")]
        [SerializeField, Tooltip("无数据时显示的文本。")]
        private TextMeshProUGUI emptyStateText;

        [Header("Button")]
        [SerializeField, Tooltip("右上角关闭按钮。")]
        private Button closeButton;

        [Header("Animation")]
        [SerializeField, Min(0.01f), Tooltip("排行榜弹出动画时长。")]
        private float popupDuration = 0.3f;

        [SerializeField, Range(0.1f, 1f), Tooltip("排行榜弹出前的起始缩放。")]
        private float popupStartScale = 0.78f;

        [SerializeField, Min(0.01f), Tooltip("排行榜关闭动画时长。")]
        private float closeDuration = 0.2f;

        private readonly List<LeaderboardEntryView> generatedRows = new List<LeaderboardEntryView>();
        private readonly List<LeaderboardRowData> currentRows = new List<LeaderboardRowData>();
        private Sequence panelSequence;
        private Vector3 authoredScale = Vector3.one;
        private bool hasInjectedRows;
        private bool hasCapturedAuthoredLayout;

        protected override void Awake()
        {
            base.Awake();
            EnsureReferences();
            CaptureAuthoredLayout();
            HideImmediate();
        }

        private void OnEnable()
        {
            KillAnimation();
            RegisterCloseButtonClick();

            if (hasInjectedRows)
            {
                RebuildRows(currentRows);
            }
            else
            {
                RefreshFromSavedRankings();
            }
        }

        private void OnDisable()
        {
            UnregisterCloseButtonClick();
            KillAnimation();
        }

        /// <summary>
        /// 打开排行榜，并默认显示本地游戏结束记录。
        /// </summary>
        public void Open()
        {
            hasInjectedRows = false;
            EnsureReferences();
            EnsureAuthoredLayoutCaptured();

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
            else
            {
                RefreshFromSavedRankings();
            }

            PlayOpenAnimation();
        }

        /// <summary>
        /// 打开排行榜，并使用外部最终传入的数据排序显示。
        /// </summary>
        public void OpenWithData(IEnumerable<LeaderboardRowData> rows)
        {
            InjectData(rows);
            EnsureReferences();
            EnsureAuthoredLayoutCaptured();

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            PlayOpenAnimation();
        }

        /// <summary>
        /// 只刷新数据，不强制打开面板；适合面板已经显示时更新服务端排名。
        /// </summary>
        public void InjectData(IEnumerable<LeaderboardRowData> rows)
        {
            hasInjectedRows = true;
            CopySortedRows(rows, currentRows);

            if (gameObject.activeInHierarchy)
            {
                RebuildRows(currentRows);
            }
        }

        /// <summary>
        /// 从现有 GameLeaderboardService 读取本地排行榜。
        /// </summary>
        public void RefreshFromSavedRankings()
        {
            hasInjectedRows = false;
            currentRows.Clear();

            IReadOnlyList<GameLeaderboardEntry> rankings = GameLeaderboardService.GetRankings(maxVisibleRows);
            for (int i = 0; i < rankings.Count; i++)
            {
                GameLeaderboardEntry entry = rankings[i];
                currentRows.Add(new LeaderboardRowData(
                    entry.EndingDisplayName,
                    entry.QualityScore,
                    entry.WishlistCount,
                    entry.EndingDisplayName,
                    entry.EndingSummary,
                    entry.CompletedAtUtcTicks));
            }

            RebuildRows(currentRows);
        }

        public void Close()
        {
            if (!gameObject.activeInHierarchy)
            {
                HideImmediate();
                gameObject.SetActive(false);
                return;
            }

            PlayCloseAnimation();
        }

        private void RebuildRows(IReadOnlyList<LeaderboardRowData> rows)
        {
            EnsureReferences();
            ClearGeneratedRows();

            if (rowTemplate == null || rowRoot == null)
            {
                Debug.LogWarning($"{nameof(LeaderboardPanelManager)} needs row root and row template.", this);
                SetEmptyStateVisible(true);
                return;
            }

            rowTemplate.gameObject.SetActive(false);

            int count = maxVisibleRows > 0 ? Math.Min(maxVisibleRows, rows.Count) : rows.Count;
            for (int i = 0; i < count; i++)
            {
                LeaderboardEntryView row = Instantiate(rowTemplate, rowRoot);
                row.name = $"{rowTemplate.name}_{i + 1}";
                row.gameObject.SetActive(true);
                row.InjectData(i + 1, rows[i]);
                generatedRows.Add(row);
            }

            SetEmptyStateVisible(count == 0);
        }

        private static void CopySortedRows(IEnumerable<LeaderboardRowData> sourceRows, List<LeaderboardRowData> targetRows)
        {
            targetRows.Clear();

            if (sourceRows == null)
            {
                return;
            }

            var sortableRows = new List<SortableLeaderboardRow>();
            int index = 0;
            foreach (LeaderboardRowData row in sourceRows)
            {
                sortableRows.Add(new SortableLeaderboardRow(row, index));
                index++;
            }

            sortableRows.Sort(CompareRows);
            for (int i = 0; i < sortableRows.Count; i++)
            {
                targetRows.Add(sortableRows[i].Row);
            }
        }

        private static int CompareRows(SortableLeaderboardRow left, SortableLeaderboardRow right)
        {
            int scoreCompare = right.Row.Score.CompareTo(left.Row.Score);
            if (scoreCompare != 0)
            {
                return scoreCompare;
            }

            int timeCompare = right.Row.CompletedAtUtcTicks.CompareTo(left.Row.CompletedAtUtcTicks);
            if (timeCompare != 0)
            {
                return timeCompare;
            }

            return left.SourceIndex.CompareTo(right.SourceIndex);
        }

        private void ClearGeneratedRows()
        {
            for (int i = generatedRows.Count - 1; i >= 0; i--)
            {
                LeaderboardEntryView row = generatedRows[i];
                if (row == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(row.gameObject);
                }
                else
                {
                    DestroyImmediate(row.gameObject);
                }
            }

            generatedRows.Clear();
        }

        private void SetEmptyStateVisible(bool visible)
        {
            if (emptyStateText != null)
            {
                emptyStateText.gameObject.SetActive(visible);
            }
        }

        private void RegisterCloseButtonClick()
        {
            EnsureReferences();

            if (closeButton == null)
            {
                Debug.LogWarning($"{nameof(LeaderboardPanelManager)} needs a close button.", this);
                return;
            }

            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }

        private void UnregisterCloseButtonClick()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Close);
            }
        }

        private void EnsureReferences()
        {
            if (panelRoot == null)
            {
                panelRoot = transform as RectTransform;
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (rowRoot == null)
            {
                Transform found = transform.Find("Content/Scroll View/Viewport/RowRoot");
                rowRoot = found as RectTransform;
            }

            if (rowTemplate == null)
            {
                rowTemplate = GetComponentInChildren<LeaderboardEntryView>(true);
            }

            if (emptyStateText == null)
            {
                Transform found = transform.Find("Content/EmptyStateText");
                emptyStateText = found != null ? found.GetComponent<TextMeshProUGUI>() : null;
            }

            if (closeButton == null)
            {
                Transform found = transform.Find("CloseButton");
                closeButton = found != null ? found.GetComponent<Button>() : null;
            }
        }

        private void PlayOpenAnimation()
        {
            EnsureReferences();
            EnsureAuthoredLayoutCaptured();
            KillAnimation();

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = true;
            panelRoot.localScale = authoredScale * popupStartScale;

            panelSequence = DOTween.Sequence()
                .SetTarget(this)
                .SetUpdate(true);
            panelSequence.Append(canvasGroup.DOFade(1f, popupDuration * 0.75f).SetEase(Ease.OutQuad));
            panelSequence.Join(panelRoot.DOScale(authoredScale, popupDuration).SetEase(Ease.OutBack));
            panelSequence.OnComplete(() =>
            {
                panelSequence = null;
                panelRoot.localScale = authoredScale;
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            });
        }

        private void PlayCloseAnimation()
        {
            EnsureReferences();
            EnsureAuthoredLayoutCaptured();
            KillAnimation();

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            panelSequence = DOTween.Sequence()
                .SetTarget(this)
                .SetUpdate(true);
            panelSequence.Append(panelRoot.DOScale(authoredScale * popupStartScale, closeDuration).SetEase(Ease.InBack));
            panelSequence.Join(canvasGroup.DOFade(0f, closeDuration * 0.8f).SetEase(Ease.InQuad));
            panelSequence.OnComplete(() =>
            {
                panelSequence = null;
                HideImmediate();
                gameObject.SetActive(false);
            });
        }

        private void HideImmediate()
        {
            EnsureReferences();
            EnsureAuthoredLayoutCaptured();
            KillAnimation();

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (panelRoot != null)
            {
                panelRoot.localScale = authoredScale;
            }
        }

        private void CaptureAuthoredLayout()
        {
            authoredScale = panelRoot != null ? panelRoot.localScale : Vector3.one;
            if (IsZeroScale(authoredScale))
            {
                authoredScale = Vector3.one;
            }

            hasCapturedAuthoredLayout = true;
        }

        private void EnsureAuthoredLayoutCaptured()
        {
            if (!hasCapturedAuthoredLayout)
            {
                CaptureAuthoredLayout();
            }
        }

        private static bool IsZeroScale(Vector3 scale)
        {
            return Mathf.Approximately(scale.x, 0f)
                || Mathf.Approximately(scale.y, 0f)
                || Mathf.Approximately(scale.z, 0f);
        }

        private void KillAnimation()
        {
            panelSequence?.Kill();
            panelSequence = null;
        }

        private void Reset()
        {
            EnsureReferences();
        }

        private void OnValidate()
        {
            popupDuration = Mathf.Max(0.01f, popupDuration);
            popupStartScale = Mathf.Clamp(popupStartScale, 0.1f, 1f);
            closeDuration = Mathf.Max(0.01f, closeDuration);
        }

        private readonly struct SortableLeaderboardRow
        {
            public readonly LeaderboardRowData Row;
            public readonly int SourceIndex;

            public SortableLeaderboardRow(LeaderboardRowData row, int sourceIndex)
            {
                Row = row;
                SourceIndex = sourceIndex;
            }
        }
    }
}
