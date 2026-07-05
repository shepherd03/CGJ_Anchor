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

        [MenuItem("Anchor/UI/Bind Begin Panel Leaderboard")]
        public static void Bind()
        {
            BindLeaderboardPanelPrefab();
            BindBeginPanelPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("BeginPanel 排行榜按钮和排行榜面板引用已绑定。");
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
                LeaderboardPanelManager leaderboardPanel = LoadLeaderboardPanelManagerAsset();

                var serializedObject = new SerializedObject(manager);
                AssignObject(serializedObject, "startButton", startButton);
                AssignObject(serializedObject, "leaderboardButton", leaderboardButton);
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
