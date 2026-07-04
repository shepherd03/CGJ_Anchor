#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Anchor.UI.Editor
{
    [InitializeOnLoad]
    public static class BlackboardCollectingValuePrefabBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/UI/BlackboardCollectingValue.prefab";

        static BlackboardCollectingValuePrefabBuilder()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                EditorApplication.delayCall += Build;
        }

        [MenuItem("Tools/Anchor UI/Rebuild Blackboard Collecting Value Prefab")]
        public static void Build()
        {
            EnsureFolders();
            var prefab = BuildPrefab();
            BuildScene(prefab);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("Built BlackboardCollectingValue prefab and Budget/Wishlist instances.");
        }

        private static GameObject BuildPrefab()
        {
            var root = UIObject("Blackboard Collecting Value", null);
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(560f, 170f);
            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            var component = root.AddComponent<BlackboardCollectingValue>();

            var icon = UIObject("Target Icon", root.transform).AddComponent<Image>();
            icon.raycastTarget = false;
            SetRect(icon.rectTransform, new Vector2(-205f, 30f), new Vector2(72f, 72f));

            var value = UIObject("Animated Value", root.transform).AddComponent<TextMeshProUGUI>();
            value.font = TMP_Settings.defaultFontAsset;
            value.text = "0";
            value.fontSize = 68f;
            value.fontStyle = FontStyles.Bold;
            value.color = Color.white;
            value.alignment = TextAlignmentOptions.Center;
            value.enableAutoSizing = true;
            value.fontSizeMin = 30f;
            value.fontSizeMax = 68f;
            value.raycastTarget = false;
            SetRect(value.rectTransform, new Vector2(35f, 30f), new Vector2(380f, 90f));

            var input = BuildInput(root.transform);
            SetRect(input.GetComponent<RectTransform>(), new Vector2(0f, -55f), new Vector2(360f, 52f));

            var particle = UIObject("Particle Template", root.transform).AddComponent<Image>();
            particle.raycastTarget = false;
            particle.rectTransform.sizeDelta = new Vector2(42f, 42f);
            particle.gameObject.SetActive(false);

            var serialized = new SerializedObject(component);
            serialized.FindProperty("mTargetIcon").objectReferenceValue = icon;
            serialized.FindProperty("mAnimatedValue").objectReferenceValue = value;
            serialized.FindProperty("mTestInput").objectReferenceValue = input;
            serialized.FindProperty("mParticleTemplate").objectReferenceValue = particle;
            serialized.FindProperty("mFontAsset").objectReferenceValue = TMP_Settings.defaultFontAsset;
            serialized.FindProperty("mFallbackColor").colorValue = new Color(1f, 0.72f, 0.2f, 1f);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static TMP_InputField BuildInput(Transform parent)
        {
            var root = UIObject("Test Value Input", parent);
            var background = root.AddComponent<Image>();
            background.color = new Color(0.12f, 0.145f, 0.20f, 1f);
            var input = root.AddComponent<TMP_InputField>();
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            input.lineType = TMP_InputField.LineType.SingleLine;

            var viewport = UIObject("Text Area", root.transform);
            Stretch(viewport.GetComponent<RectTransform>());
            viewport.GetComponent<RectTransform>().offsetMin = new Vector2(18f, 4f);
            viewport.GetComponent<RectTransform>().offsetMax = new Vector2(-18f, -4f);
            viewport.AddComponent<RectMask2D>();

            var text = UIObject("Text", viewport.transform).AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = 24f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            Stretch(text.rectTransform);

            var placeholder = UIObject("Placeholder", viewport.transform).AddComponent<TextMeshProUGUI>();
            placeholder.font = TMP_Settings.defaultFontAsset;
            placeholder.text = "Enter test value + Return";
            placeholder.fontSize = 20f;
            placeholder.fontStyle = FontStyles.Italic;
            placeholder.color = new Color(0.62f, 0.68f, 0.78f, 1f);
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            Stretch(placeholder.rectTransform);

            input.textViewport = viewport.GetComponent<RectTransform>();
            input.textComponent = text;
            input.placeholder = placeholder;
            return input;
        }

        private static void BuildScene(GameObject prefab)
        {
            DestroyNamed("Persistent Collection Effects");
            DestroyNamed("Blackboard Number Animation Demo");
            DestroyNamed("Blackboard Collecting Values");

            var canvasGo = UIObject("Blackboard Collecting Values", null);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            Stretch(canvasGo.GetComponent<RectTransform>());

            CreateInstance(prefab, canvasGo.transform, canvasGo.GetComponent<RectTransform>(), true);
            CreateInstance(prefab, canvasGo.transform, canvasGo.GetComponent<RectTransform>(), false);
        }

        private static void CreateInstance(GameObject prefab, Transform parent, RectTransform layer, bool budget)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = budget ? "Budget Collecting Value" : "Wishlist Collecting Value";
            var rect = instance.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = budget ? new Vector2(-360f, 0f) : new Vector2(360f, 0f);
            rect.sizeDelta = new Vector2(560f, 170f);

            var color = budget ? new Color(1f, 0.72f, 0.2f, 1f) : new Color(0.4f, 0.82f, 1f, 1f);
            var component = instance.GetComponent<BlackboardCollectingValue>();
            var serialized = new SerializedObject(component);
            serialized.FindProperty("mValueKind").enumValueIndex = budget ? 0 : 1;
            serialized.FindProperty("mEffectLayer").objectReferenceValue = layer;
            serialized.FindProperty("mFallbackColor").colorValue = color;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            instance.transform.Find("Target Icon").GetComponent<Image>().color = color;
            instance.transform.Find("Particle Template").GetComponent<Image>().color = color;
        }

        private static void DestroyNamed(string name)
        {
            var go = GameObject.Find(name);
            if (go != null) Object.DestroyImmediate(go);
        }

        private static GameObject UIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI")) AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
        }
    }
}
#endif
