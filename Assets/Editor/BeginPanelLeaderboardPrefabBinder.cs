using Anchor.UI.Panel;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Anchor.Editor
{
    public static class BeginPanelLeaderboardPrefabBinder
    {
        private const string BeginPanelPath = "Assets/Prefabs/BeginPanel.prefab";
        private const string LeaderboardPanelPath = "Assets/Prefabs/LeaderboardPanel.prefab";
        private const string AboutUsSpritePath = "Assets/ArtRes/AbountUs.jpg";
        private static readonly string[] AboutUsNames =
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

        [MenuItem("Anchor/UI/Bind Begin Panel Leaderboard")]
        public static void Bind()
        {
            BindLeaderboardPanelPrefab();
            BindBeginPanelPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("BeginPanel 排行榜和关于我们引用已绑定。");
        }

        private static void BindBeginPanelPrefab()
        {
            GameObject beginRoot = PrefabUtility.LoadPrefabContents(BeginPanelPath);
            try
            {
                BeginPanelManager manager = beginRoot.GetComponent<BeginPanelManager>();
                if (manager == null)
                {
                    Debug.LogError($"{BeginPanelPath} 缺少 {nameof(BeginPanelManager)}。");
                    return;
                }

                Button startButton = beginRoot.transform.Find("Start")?.GetComponent<Button>();
                Button leaderboardButton = beginRoot.transform.Find("Leaderboard")?.GetComponent<Button>();
                Button runAwayButton = FindButton(beginRoot.transform, "RunAway")
                    ?? FindButton(beginRoot.transform, "跑路")
                    ?? FindButton(beginRoot.transform, "RunImg");
                Button aboutUsButton = EnsureAboutUsButton(beginRoot.transform, leaderboardButton);
                Sprite aboutUsSprite = LoadAboutUsSpriteAsset();
                GameObject aboutUsOverlay = EnsureAboutUsOverlay(
                    beginRoot,
                    leaderboardButton != null ? leaderboardButton.GetComponentInChildren<TextMeshProUGUI>(true) : null,
                    aboutUsSprite,
                    out CanvasGroup aboutUsOverlayCanvasGroup,
                    out Image aboutUsDimImage,
                    out Button aboutUsBackgroundButton,
                    out TextMeshProUGUI aboutUsNameLabel,
                    out CanvasGroup aboutUsNameCanvasGroup,
                    out Image aboutUsImage,
                    out CanvasGroup aboutUsImageCanvasGroup);
                LeaderboardPanelManager leaderboardPanel = LoadLeaderboardPanelManagerAsset();

                var serializedObject = new SerializedObject(manager);
                AssignObject(serializedObject, "startButton", startButton);
                AssignObject(serializedObject, "leaderboardButton", leaderboardButton);
                AssignObject(serializedObject, "runAwayButton", runAwayButton);
                AssignObject(serializedObject, "aboutUsButton", aboutUsButton);
                AssignObject(serializedObject, "aboutUsSprite", aboutUsSprite);
                AssignObject(serializedObject, "aboutUsOverlay", aboutUsOverlay);
                AssignObject(serializedObject, "aboutUsOverlayCanvasGroup", aboutUsOverlayCanvasGroup);
                AssignObject(serializedObject, "aboutUsDimImage", aboutUsDimImage);
                AssignObject(serializedObject, "aboutUsBackgroundButton", aboutUsBackgroundButton);
                AssignObject(serializedObject, "aboutUsNameLabel", aboutUsNameLabel);
                AssignObject(serializedObject, "aboutUsNameCanvasGroup", aboutUsNameCanvasGroup);
                AssignObject(serializedObject, "aboutUsImage", aboutUsImage);
                AssignObject(serializedObject, "aboutUsImageCanvasGroup", aboutUsImageCanvasGroup);
                AssignStringArray(serializedObject, "aboutUsNames", AboutUsNames);
                AssignObject(serializedObject, "leaderboardPanelPrefab", leaderboardPanel);
                serializedObject.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(beginRoot, BeginPanelPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(beginRoot);
            }
        }

        private static void BindLeaderboardPanelPrefab()
        {
            GameObject leaderboardRoot = PrefabUtility.LoadPrefabContents(LeaderboardPanelPath);
            try
            {
                LeaderboardPanelManager manager = leaderboardRoot.GetComponent<LeaderboardPanelManager>();
                if (manager == null)
                {
                    Debug.LogError($"{LeaderboardPanelPath} 缺少 {nameof(LeaderboardPanelManager)}。");
                    return;
                }

                RectTransform rootRect = leaderboardRoot.transform as RectTransform;
                CanvasGroup canvasGroup = leaderboardRoot.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = leaderboardRoot.AddComponent<CanvasGroup>();
                }

                RectTransform rowRoot = leaderboardRoot.transform.Find("Content/Scroll View/Viewport/RowRoot") as RectTransform;
                LeaderboardEntryView rowTemplate = leaderboardRoot.GetComponentInChildren<LeaderboardEntryView>(true);
                TextMeshProUGUI emptyStateText = leaderboardRoot.transform
                    .Find("Content/EmptyStateText")
                    ?.GetComponent<TextMeshProUGUI>();
                Button closeButton = leaderboardRoot.transform.Find("CloseButton")?.GetComponent<Button>();

                var serializedObject = new SerializedObject(manager);
                AssignObject(serializedObject, "panelRoot", rootRect);
                AssignObject(serializedObject, "canvasGroup", canvasGroup);
                AssignObject(serializedObject, "rowRoot", rowRoot);
                AssignObject(serializedObject, "rowTemplate", rowTemplate);
                AssignObject(serializedObject, "emptyStateText", emptyStateText);
                AssignObject(serializedObject, "closeButton", closeButton);
                serializedObject.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(leaderboardRoot, LeaderboardPanelPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(leaderboardRoot);
            }
        }

        private static Button EnsureAboutUsButton(Transform beginRoot, Button templateButton)
        {
            Button aboutUsButton = beginRoot.Find("AboutUs")?.GetComponent<Button>()
                ?? beginRoot.Find("关于我们")?.GetComponent<Button>();

            if (aboutUsButton == null && templateButton != null)
            {
                GameObject buttonObject = Object.Instantiate(templateButton.gameObject, templateButton.transform.parent);
                buttonObject.name = "AboutUs";
                SetLayerRecursively(buttonObject, beginRoot.gameObject.layer);
                aboutUsButton = buttonObject.GetComponent<Button>();

                RectTransform aboutRect = buttonObject.transform as RectTransform;
                RectTransform templateRect = templateButton.transform as RectTransform;
                if (aboutRect != null && templateRect != null)
                {
                    CopyRectTransform(templateRect, aboutRect);
                    aboutRect.anchoredPosition = templateRect.anchoredPosition + new Vector2(-520f, 0f);
                }

                buttonObject.transform.SetAsLastSibling();
            }

            if (aboutUsButton == null)
            {
                GameObject buttonObject = new GameObject("AboutUs", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                buttonObject.layer = beginRoot.gameObject.layer;
                buttonObject.transform.SetParent(beginRoot, false);

                RectTransform rect = buttonObject.transform as RectTransform;
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(-520f, 80f);
                rect.sizeDelta = new Vector2(320f, 92f);

                Image image = buttonObject.GetComponent<Image>();
                image.color = new Color(1f, 1f, 1f, 0.95f);

                aboutUsButton = buttonObject.GetComponent<Button>();
                aboutUsButton.targetGraphic = image;
            }

            aboutUsButton.onClick.RemoveAllListeners();
            SetButtonLabel(aboutUsButton, "关于我们", templateButton != null ? templateButton.GetComponentInChildren<TextMeshProUGUI>(true) : null);
            return aboutUsButton;
        }

        private static GameObject EnsureAboutUsOverlay(
            GameObject beginRoot,
            TextMeshProUGUI templateText,
            Sprite aboutUsSprite,
            out CanvasGroup overlayCanvasGroup,
            out Image dimImage,
            out Button backgroundButton,
            out TextMeshProUGUI nameLabel,
            out CanvasGroup nameCanvasGroup,
            out Image finalImage,
            out CanvasGroup finalImageCanvasGroup)
        {
            Transform rootTransform = beginRoot.transform;
            Transform overlayTransform = rootTransform.Find("AboutUsOverlay");
            GameObject overlayObject = overlayTransform != null
                ? overlayTransform.gameObject
                : new GameObject("AboutUsOverlay", typeof(RectTransform), typeof(CanvasGroup));
            overlayObject.layer = beginRoot.layer;
            overlayObject.transform.SetParent(rootTransform, false);
            StretchToParent(overlayObject.transform as RectTransform);
            overlayObject.transform.SetAsLastSibling();

            overlayCanvasGroup = EnsureComponent<CanvasGroup>(overlayObject);
            overlayCanvasGroup.alpha = 0f;
            overlayCanvasGroup.interactable = false;
            overlayCanvasGroup.blocksRaycasts = false;

            GameObject dimObject = GetOrCreateRectChild(overlayObject.transform, "DimBackground", beginRoot.layer);
            EnsureComponent<CanvasRenderer>(dimObject);
            dimImage = EnsureComponent<Image>(dimObject);
            dimImage.color = new Color(0f, 0f, 0f, 0f);
            dimImage.raycastTarget = true;
            backgroundButton = EnsureComponent<Button>(dimObject);
            backgroundButton.transition = Selectable.Transition.None;
            backgroundButton.targetGraphic = dimImage;
            backgroundButton.onClick.RemoveAllListeners();
            StretchToParent(dimObject.transform as RectTransform);

            GameObject nameObject = GetOrCreateRectChild(overlayObject.transform, "NameCarousel", beginRoot.layer);
            EnsureComponent<CanvasRenderer>(nameObject);
            nameCanvasGroup = EnsureComponent<CanvasGroup>(nameObject);
            nameCanvasGroup.alpha = 0f;
            nameLabel = EnsureComponent<TextMeshProUGUI>(nameObject);
            ApplyAboutUsNameTextStyle(nameLabel, templateText);

            RectTransform nameRect = nameObject.transform as RectTransform;
            nameRect.anchorMin = new Vector2(0.5f, 0.5f);
            nameRect.anchorMax = new Vector2(0.5f, 0.5f);
            nameRect.pivot = new Vector2(0.5f, 0.5f);
            nameRect.anchoredPosition = Vector2.zero;
            nameRect.sizeDelta = new Vector2(1320f, 220f);

            GameObject imageObject = GetOrCreateRectChild(overlayObject.transform, "FinalImage", beginRoot.layer);
            EnsureComponent<CanvasRenderer>(imageObject);
            finalImageCanvasGroup = EnsureComponent<CanvasGroup>(imageObject);
            finalImageCanvasGroup.alpha = 0f;
            finalImage = EnsureComponent<Image>(imageObject);
            finalImage.sprite = aboutUsSprite;
            finalImage.color = Color.white;
            finalImage.preserveAspect = true;
            finalImage.raycastTarget = true;
            ApplyAboutUsImageLayout(finalImage.rectTransform, aboutUsSprite);
            imageObject.SetActive(false);

            dimObject.transform.SetSiblingIndex(0);
            imageObject.transform.SetSiblingIndex(1);
            nameObject.transform.SetSiblingIndex(2);
            overlayObject.SetActive(false);
            return overlayObject;
        }

        private static void SetButtonLabel(Button button, string label, TextMeshProUGUI templateText)
        {
            TextMeshProUGUI tmpLabel = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmpLabel == null)
            {
                GameObject textObject = new GameObject("Text (TMP)", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                textObject.layer = button.gameObject.layer;
                textObject.transform.SetParent(button.transform, false);
                StretchToParent(textObject.transform as RectTransform);
                tmpLabel = textObject.GetComponent<TextMeshProUGUI>();
            }

            if (templateText != null)
            {
                tmpLabel.font = templateText.font;
                tmpLabel.fontSharedMaterial = templateText.fontSharedMaterial;
            }

            tmpLabel.text = label;
            tmpLabel.color = Color.black;
            tmpLabel.fontSize = 36f;
            tmpLabel.alignment = TextAlignmentOptions.Center;
            tmpLabel.raycastTarget = false;

            Text[] legacyLabels = button.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < legacyLabels.Length; i++)
            {
                legacyLabels[i].text = label;
            }
        }

        private static void ApplyAboutUsNameTextStyle(TextMeshProUGUI target, TextMeshProUGUI templateText)
        {
            if (templateText != null)
            {
                target.font = templateText.font;
                target.fontSharedMaterial = templateText.fontSharedMaterial;
            }

            target.text = string.Empty;
            target.color = Color.white;
            target.fontSize = 84f;
            target.enableAutoSizing = true;
            target.fontSizeMin = 32f;
            target.fontSizeMax = 96f;
            target.alignment = TextAlignmentOptions.Center;
            target.raycastTarget = false;
        }

        private static void ApplyAboutUsImageLayout(RectTransform rect, Sprite sprite)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            Vector2 sourceSize = sprite != null
                ? new Vector2(sprite.rect.width, sprite.rect.height)
                : new Vector2(800f, 565f);
            float scale = Mathf.Min(960f / sourceSize.x, 680f / sourceSize.y);
            rect.sizeDelta = sourceSize * Mathf.Max(0.01f, scale);
        }

        private static GameObject GetOrCreateRectChild(Transform parent, string name, int layer)
        {
            Transform child = parent.Find(name);
            if (child != null)
            {
                child.gameObject.layer = layer;
                return child.gameObject;
            }

            GameObject childObject = new GameObject(name, typeof(RectTransform));
            childObject.layer = layer;
            childObject.transform.SetParent(parent, false);
            return childObject;
        }

        private static T EnsureComponent<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private static Button FindButton(Transform root, string childName)
        {
            Transform found = root.Find(childName);
            return found != null ? found.GetComponent<Button>() : null;
        }

        private static Sprite LoadAboutUsSpriteAsset()
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AboutUsSpritePath);
            if (sprite == null)
            {
                Debug.LogError($"找不到关于我们图片：{AboutUsSpritePath}");
            }

            return sprite;
        }

        private static void AssignStringArray(SerializedObject serializedObject, string propertyName, string[] values)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning($"Cannot find serialized property {propertyName} on {serializedObject.targetObject.name}.");
                return;
            }

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).stringValue = values[i];
            }
        }

        private static void CopyRectTransform(RectTransform source, RectTransform target)
        {
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.sizeDelta = source.sizeDelta;
            target.localScale = source.localScale;
            target.localRotation = source.localRotation;
        }

        private static void StretchToParent(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private static LeaderboardPanelManager LoadLeaderboardPanelManagerAsset()
        {
            GameObject leaderboardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LeaderboardPanelPath);
            if (leaderboardPrefab == null)
            {
                Debug.LogError($"找不到排行榜 Prefab：{LeaderboardPanelPath}");
                return null;
            }

            LeaderboardPanelManager manager = leaderboardPrefab.GetComponent<LeaderboardPanelManager>();
            if (manager == null)
            {
                Debug.LogError($"{LeaderboardPanelPath} 缺少 {nameof(LeaderboardPanelManager)}。");
            }

            return manager;
        }

        private static void AssignObject(SerializedObject serializedObject, string propertyName, Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning($"Cannot find serialized property {propertyName} on {serializedObject.targetObject.name}.");
                return;
            }

            property.objectReferenceValue = value;
        }
    }
}
