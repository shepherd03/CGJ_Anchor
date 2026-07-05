using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Anchor.UI.Panel
{
    [DisallowMultipleComponent]
    public sealed class LeaderboardEntryView : MonoBehaviour
    {
        [Header("Text")]
        [SerializeField, Tooltip("显示名次的文本。")]
        private TextMeshProUGUI rankText;

        [SerializeField, Tooltip("显示玩家、项目或结局名称的主文本。")]
        private TextMeshProUGUI nameText;

        [SerializeField, Tooltip("显示质量分的文本。")]
        private TextMeshProUGUI qualityText;

        [SerializeField, Tooltip("显示愿望单数量的文本。")]
        private TextMeshProUGUI wishlistText;

        [SerializeField, Tooltip("显示结局的文本。")]
        private TextMeshProUGUI endingText;

        [SerializeField, Tooltip("显示完成时间的文本。")]
        private TextMeshProUGUI timeText;

        [SerializeField, Tooltip("显示简短说明的文本。")]
        private TextMeshProUGUI summaryText;

        [Header("Visual")]
        [SerializeField, Tooltip("条目背景。前三名会使用不同底色。")]
        private Image backgroundImage;

        [SerializeField] private Color normalColor = new Color(0.09f, 0.12f, 0.16f, 0.86f);
        [SerializeField] private Color firstPlaceColor = new Color(0.95f, 0.68f, 0.20f, 0.96f);
        [SerializeField] private Color secondPlaceColor = new Color(0.58f, 0.66f, 0.74f, 0.92f);
        [SerializeField] private Color thirdPlaceColor = new Color(0.75f, 0.43f, 0.24f, 0.92f);

        public void InjectData(int rank, LeaderboardRowData data)
        {
            SetText(rankText, rank.ToString());
            SetText(nameText, FormatName(rank, data));
            SetText(qualityText, FormatOptionalNumber(data.QualityScore));
            SetText(wishlistText, FormatOptionalNumber(data.WishlistCount));
            SetText(endingText, string.IsNullOrWhiteSpace(data.EndingName) ? "未记录结局" : data.EndingName);
            SetText(timeText, FormatCompletedTime(data.CompletedAtUtcTicks));
            SetSummary(data.Summary);
            ApplyRankColor(rank);
        }

        private void SetSummary(string summary)
        {
            if (summaryText == null)
            {
                return;
            }

            bool hasSummary = !string.IsNullOrWhiteSpace(summary);
            summaryText.gameObject.SetActive(hasSummary);
            summaryText.text = hasSummary ? summary : string.Empty;
        }

        private void ApplyRankColor(int rank)
        {
            if (backgroundImage == null)
            {
                return;
            }

            backgroundImage.color = rank switch
            {
                1 => firstPlaceColor,
                2 => secondPlaceColor,
                3 => thirdPlaceColor,
                _ => normalColor
            };
        }

        private static string FormatName(int rank, LeaderboardRowData data)
        {
            if (!string.IsNullOrWhiteSpace(data.PlayerName))
            {
                return data.PlayerName;
            }

            if (!string.IsNullOrWhiteSpace(data.EndingName))
            {
                return data.EndingName;
            }

            return $"第 {rank} 名";
        }

        private static string FormatOptionalNumber(int value)
        {
            return value > 0 ? value.ToString("N0") : "--";
        }

        private static string FormatCompletedTime(long completedAtUtcTicks)
        {
            if (completedAtUtcTicks <= 0)
            {
                return "--";
            }

            try
            {
                return new DateTime(completedAtUtcTicks, DateTimeKind.Utc)
                    .ToLocalTime()
                    .ToString("yyyy/MM/dd HH:mm");
            }
            catch (ArgumentOutOfRangeException)
            {
                return "--";
            }
        }

        private static void SetText(TextMeshProUGUI text, string value)
        {
            if (text != null)
            {
                text.text = value;
            }
        }

        private void Reset()
        {
            if (backgroundImage == null)
            {
                backgroundImage = GetComponent<Image>();
            }

            TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TextMeshProUGUI text = texts[i];
                switch (text.name)
                {
                    case "RankText":
                        rankText = text;
                        break;
                    case "NameText":
                        nameText = text;
                        break;
                    case "QualityText":
                        qualityText = text;
                        break;
                    case "WishlistText":
                        wishlistText = text;
                        break;
                    case "EndingText":
                        endingText = text;
                        break;
                    case "TimeText":
                        timeText = text;
                        break;
                    case "SummaryText":
                        summaryText = text;
                        break;
                }
            }
        }
    }
}
