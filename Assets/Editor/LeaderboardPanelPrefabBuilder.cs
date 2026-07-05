using System.IO;
using Anchor.UI.Panel;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Anchor.Editor
{
    public static class LeaderboardPanelPrefabBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/LeaderboardPanel.prefab";
        private const float RankColumnWidth = 84f;
        private const float QualityColumnWidth = 120f;
        private const float WishlistColumnWidth = 200f;
        private const float TimeColumnWidth = 190f;
        private const float FlexibleInfoMinWidth = 300f;

        [MenuItem("Anchor/UI/Rebuild Leaderboard Panel Prefab")]
        public static void RebuildPrefab()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));

            GameObject root = CreateRectObject("LeaderboardPanel", null);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            Stretch(rootRect, 0f, 0f, 0f, 0f);

            Image background = root.AddComponent<Image>();
            background.color = new Color(0.03f, 0.045f, 0.065f, 0.96f);

            CanvasGroup canvasGroup = root.AddComponent<CanvasGroup>();
            LeaderboardPanelManager manager = root.AddComponent<LeaderboardPanelManager>();

            CreateTitle(rootRect);
            Button closeButton = CreateCloseButton(rootRect);
            RectTransform content = CreateContent(rootRect);
            CreateColumnHeader(content);
            ScrollRect scrollRect = CreateScrollView(content, out RectTransform rowRoot);
            LeaderboardEntryView rowTemplate = CreateRowTemplate(rowRoot);
            TextMeshProUGUI emptyStateText = CreateEmptyState(content);

            AssignObject(manager, "panelRoot", rootRect);
            AssignObject(manager, "canvasGroup", canvasGroup);
            AssignObject(manager, "rowRoot", rowRoot);
            AssignObject(manager, "rowTemplate", rowTemplate);
            AssignObject(manager, "emptyStateText", emptyStateText);
            AssignObject(manager, "closeButton", closeButton);

            scrollRect.content = rowRoot;
            rowTemplate.gameObject.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"排行榜 Prefab 已生成：{PrefabPath}");
        }

        private static void CreateTitle(RectTransform parent)
        {
            TextMeshProUGUI title = CreateText("TitleText", parent, "排行榜", 54, FontStyles.Bold, TextAlignmentOptions.Center);
            RectTransform rect = title.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -42f);
            rect.sizeDelta = new Vector2(-220f, 74f);
            title.color = new Color(0.94f, 0.98f, 1f, 1f);
        }

        private static Button CreateCloseButton(RectTransform parent)
        {
            GameObject closeObject = CreateRectObject("CloseButton", parent);
            RectTransform rect = closeObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-56f, -54f);
            rect.sizeDelta = new Vector2(72f, 72f);

            Image image = closeObject.AddComponent<Image>();
            image.color = new Color(0.82f, 0.18f, 0.18f, 0.92f);

            Button button = closeObject.AddComponent<Button>();
            button.targetGraphic = image;

            TextMeshProUGUI label = CreateText("Label", rect, "X", 44, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(label.rectTransform, 0f, 0f, 0f, 2f);
            label.raycastTarget = false;
            label.color = Color.white;

            return button;
        }

        private static RectTransform CreateContent(RectTransform parent)
        {
            GameObject contentObject = CreateRectObject("Content", parent);
            RectTransform rect = contentObject.GetComponent<RectTransform>();
            Stretch(rect, 96f, 130f, 96f, 78f);

            Image image = contentObject.AddComponent<Image>();
            image.color = new Color(0.075f, 0.095f, 0.125f, 0.92f);

            return rect;
        }

        private static void CreateColumnHeader(RectTransform parent)
        {
            GameObject header = CreateRectObject("ColumnHeader", parent);
            RectTransform rect = header.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -22f);
            rect.sizeDelta = new Vector2(-48f, 44f);

            HorizontalLayoutGroup layout = header.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 0, 0);
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlHeight = true;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = false;

            AddHeaderLabel(rect, "名次", RankColumnWidth, TextAlignmentOptions.Center);
            AddHeaderLabel(rect, "结局", 0f, TextAlignmentOptions.Left);
            AddHeaderLabel(rect, "质量分", QualityColumnWidth, TextAlignmentOptions.Center);
            AddHeaderLabel(rect, "愿望单", WishlistColumnWidth, TextAlignmentOptions.Center);
            AddHeaderLabel(rect, "完成时间", TimeColumnWidth, TextAlignmentOptions.Center);
        }

        private static ScrollRect CreateScrollView(RectTransform parent, out RectTransform rowRoot)
        {
            GameObject scrollObject = CreateRectObject("Scroll View", parent);
            RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
            Stretch(scrollRectTransform, 24f, 86f, 24f, 32f);

            ScrollRect scrollRect = scrollObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 42f;

            GameObject viewportObject = CreateRectObject("Viewport", scrollRectTransform);
            RectTransform viewport = viewportObject.GetComponent<RectTransform>();
            Stretch(viewport, 0f, 0f, 0f, 0f);

            Image viewportImage = viewportObject.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.02f);
            Mask mask = viewportObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            GameObject rowRootObject = CreateRectObject("RowRoot", viewport);
            rowRoot = rowRootObject.GetComponent<RectTransform>();
            rowRoot.anchorMin = new Vector2(0f, 1f);
            rowRoot.anchorMax = new Vector2(1f, 1f);
            rowRoot.pivot = new Vector2(0.5f, 1f);
            rowRoot.anchoredPosition = Vector2.zero;
            rowRoot.sizeDelta = Vector2.zero;

            VerticalLayoutGroup rowLayout = rowRootObject.AddComponent<VerticalLayoutGroup>();
            rowLayout.padding = new RectOffset(0, 0, 0, 0);
            rowLayout.spacing = 10f;
            rowLayout.childAlignment = TextAnchor.UpperCenter;
            rowLayout.childControlHeight = false;
            rowLayout.childControlWidth = true;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childForceExpandWidth = true;

            ContentSizeFitter fitter = rowRootObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewport;
            return scrollRect;
        }

        private static LeaderboardEntryView CreateRowTemplate(RectTransform parent)
        {
            GameObject row = CreateRectObject("RowTemplate", parent);
            RectTransform rect = row.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, 92f);

            Image background = row.AddComponent<Image>();
            background.color = new Color(0.09f, 0.12f, 0.16f, 0.86f);

            LayoutElement element = row.AddComponent<LayoutElement>();
            element.preferredHeight = 92f;
            element.minHeight = 92f;

            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 8, 8);
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlHeight = true;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = false;

            TextMeshProUGUI rankText = AddRowText(rect, "RankText", "1", 36, RankColumnWidth, TextAlignmentOptions.Center);
            RectTransform infoStack = CreateInfoStack(rect);
            TextMeshProUGUI nameText = CreateText("NameText", infoStack, "好评如潮", 30, FontStyles.Bold, TextAlignmentOptions.Left);
            TextMeshProUGUI summaryText = CreateText("SummaryText", infoStack, "结局简介", 21, FontStyles.Normal, TextAlignmentOptions.Left);
            TextMeshProUGUI qualityText = AddRowText(rect, "QualityText", "82", 30, QualityColumnWidth, TextAlignmentOptions.Center);
            TextMeshProUGUI wishlistText = AddRowText(rect, "WishlistText", "4,500,000", 28, WishlistColumnWidth, TextAlignmentOptions.Center);
            TextMeshProUGUI timeText = AddRowText(rect, "TimeText", "2026/07/05", 22, TimeColumnWidth, TextAlignmentOptions.Center);

            nameText.color = Color.white;
            summaryText.color = new Color(0.76f, 0.84f, 0.92f, 0.86f);
            rankText.color = new Color(1f, 0.94f, 0.70f, 1f);
            qualityText.color = new Color(0.74f, 1f, 0.76f, 1f);
            wishlistText.color = new Color(1f, 0.78f, 0.88f, 1f);
            timeText.color = new Color(0.72f, 0.78f, 0.86f, 1f);

            LeaderboardEntryView view = row.AddComponent<LeaderboardEntryView>();
            AssignObject(view, "rankText", rankText);
            AssignObject(view, "nameText", nameText);
            AssignObject(view, "qualityText", qualityText);
            AssignObject(view, "wishlistText", wishlistText);
            AssignObject(view, "endingText", nameText);
            AssignObject(view, "timeText", timeText);
            AssignObject(view, "summaryText", summaryText);
            AssignObject(view, "backgroundImage", background);

            return view;
        }

        private static TextMeshProUGUI CreateEmptyState(RectTransform parent)
        {
            TextMeshProUGUI empty = CreateText("EmptyStateText", parent, "暂无排行数据", 36, FontStyles.Normal, TextAlignmentOptions.Center);
            RectTransform rect = empty.rectTransform;
            Stretch(rect, 0f, 0f, 0f, 0f);
            empty.color = new Color(0.78f, 0.86f, 0.94f, 0.72f);
            empty.gameObject.SetActive(false);
            return empty;
        }

        private static RectTransform CreateInfoStack(RectTransform parent)
        {
            GameObject stackObject = CreateRectObject("InfoStack", parent);
            RectTransform rect = stackObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, 76f);

            LayoutElement element = stackObject.AddComponent<LayoutElement>();
            element.flexibleWidth = 1f;
            element.minWidth = FlexibleInfoMinWidth;

            VerticalLayoutGroup layout = stackObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 3f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            return rect;
        }

        private static void AddHeaderLabel(RectTransform parent, string text, float width, TextAlignmentOptions alignment)
        {
            TextMeshProUGUI label = CreateText($"Header_{text}", parent, text, 24, FontStyles.Bold, alignment);
            label.color = new Color(0.70f, 0.82f, 0.94f, 0.92f);

            LayoutElement element = label.gameObject.AddComponent<LayoutElement>();
            if (width > 0f)
            {
                element.preferredWidth = width;
                element.minWidth = width;
            }
            else
            {
                element.flexibleWidth = 1f;
                element.minWidth = FlexibleInfoMinWidth;
            }
        }

        private static TextMeshProUGUI AddRowText(
            RectTransform parent,
            string name,
            string text,
            int fontSize,
            float width,
            TextAlignmentOptions alignment)
        {
            TextMeshProUGUI label = CreateText(name, parent, text, fontSize, FontStyles.Bold, alignment);
            LayoutElement element = label.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.minWidth = width;
            return label;
        }

        private static TextMeshProUGUI CreateText(
            string name,
            RectTransform parent,
            string text,
            int fontSize,
            FontStyles fontStyle,
            TextAlignmentOptions alignment)
        {
            GameObject textObject = CreateRectObject(name, parent);
            TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.alignment = alignment;
            label.enableWordWrapping = true;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            return label;
        }

        private static GameObject CreateRectObject(string name, RectTransform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = 5;

            RectTransform rect = gameObject.GetComponent<RectTransform>();
            if (parent != null)
            {
                rect.SetParent(parent, false);
            }

            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.anchoredPosition3D = Vector3.zero;
            return gameObject;
        }

        private static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void AssignObject(Object target, string propertyName, Object value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning($"Cannot find serialized property {propertyName} on {target.name}.", target);
                return;
            }

            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
