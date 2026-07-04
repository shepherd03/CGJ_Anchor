using System.Collections.Generic;
using UnityEngine;

namespace Anchor.UI
{
    /// <summary>
    /// 根据行动点数量动态生成行动点 UI 实例。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class APContainerController : MonoBehaviour
    {
        [SerializeField, Tooltip("单个行动点 UI 预制体，会按传入数量动态生成。")]
        private RectTransform actionPointPrefab;

        [SerializeField, Tooltip("行动点实例的父节点。为空时默认使用当前物体的 RectTransform。")]
        private RectTransform contentRoot;

        // 缓存已生成的行动点实例，刷新数量时复用，避免频繁创建销毁。
        private readonly List<RectTransform> actionPointInstances = new List<RectTransform>();

        // 避免预制体未配置时每次刷新都刷屏警告。
        private bool hasWarnedMissingPrefab;

        /// <summary>
        /// 添加组件或点击 Reset 时自动补齐默认父节点。
        /// </summary>
        private void Reset()
        {
            CacheMissingContentRoot();
        }

        /// <summary>
        /// 运行时初始化行动点实例父节点。
        /// </summary>
        private void Awake()
        {
            CacheMissingContentRoot();
        }

        /// <summary>
        /// Inspector 改动后补齐默认父节点，减少手动配置成本。
        /// </summary>
        private void OnValidate()
        {
            CacheMissingContentRoot();
        }

        /// <summary>
        /// 接收当前剩余行动点数量，并同步生成对应数量的 UI 实例。
        /// </summary>
        public void SetActionPointCount(int actionPointCount)
        {
            actionPointCount = Mathf.Max(0, actionPointCount);
            CacheMissingContentRoot();
            RemoveMissingInstances();

            if (actionPointPrefab == null)
            {
                SetActiveInstanceCount(0);
                WarnMissingPrefabOnce(actionPointCount);
                return;
            }

            if (contentRoot == null)
            {
                Debug.LogWarning($"{nameof(APContainerController)} needs a content root.", this);
                return;
            }

            EnsureInstanceCount(actionPointCount);
            SetActiveInstanceCount(actionPointCount);
        }

        /// <summary>
        /// 清空当前显示的行动点 UI。
        /// </summary>
        public void Clear()
        {
            SetActiveInstanceCount(0);
        }

        /// <summary>
        /// 只在父节点未配置时自动使用当前 RectTransform。
        /// </summary>
        private void CacheMissingContentRoot()
        {
            if (contentRoot == null)
            {
                contentRoot = transform as RectTransform;
            }
        }

        /// <summary>
        /// 确保缓存池里至少有目标数量的行动点实例。
        /// </summary>
        private void EnsureInstanceCount(int targetCount)
        {
            while (actionPointInstances.Count < targetCount)
            {
                RectTransform instance = Instantiate(actionPointPrefab, contentRoot);
                instance.name = $"{actionPointPrefab.name}_{actionPointInstances.Count + 1:00}";
                instance.gameObject.SetActive(false);
                actionPointInstances.Add(instance);
            }
        }

        /// <summary>
        /// 按目标数量显示或隐藏已生成的行动点实例。
        /// </summary>
        private void SetActiveInstanceCount(int activeCount)
        {
            activeCount = Mathf.Max(0, activeCount);

            for (int i = 0; i < actionPointInstances.Count; i++)
            {
                RectTransform instance = actionPointInstances[i];
                if (instance != null)
                {
                    instance.gameObject.SetActive(i < activeCount);
                }
            }
        }

        /// <summary>
        /// 移除已经被外部销毁的实例引用。
        /// </summary>
        private void RemoveMissingInstances()
        {
            for (int i = actionPointInstances.Count - 1; i >= 0; i--)
            {
                if (actionPointInstances[i] == null)
                {
                    actionPointInstances.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 预制体未配置且需要显示行动点时只警告一次。
        /// </summary>
        private void WarnMissingPrefabOnce(int actionPointCount)
        {
            if (actionPointCount <= 0 || hasWarnedMissingPrefab)
            {
                return;
            }

            hasWarnedMissingPrefab = true;
            Debug.LogWarning($"{nameof(APContainerController)} needs an action point prefab.", this);
        }
    }
}
