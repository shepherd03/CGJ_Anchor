using System.Collections.Generic;
using Anchor.Window;
using UnityEngine;
using UnityEngine.UI;

namespace Anchor.UI.Panel
{
    [DisallowMultipleComponent]
    public sealed class WindowShopPanelManager : MonoBehaviour
    {
        [Header("Buff Window")]
        [SerializeField, Tooltip("BuffWindow 上的下落回弹动画组件。为空时会从当前物体或子物体查找。")]
        private WindowDropBounceAnimator buffWindowAnimator;

        [SerializeField, Tooltip("动态生成 BuffCard 的父节点。为空时默认使用当前 UI 节点。")]
        private RectTransform buffCardRoot;

        [SerializeField, Tooltip("用于动态生成的 BuffCard 预制体。")]
        private BuffCardController buffCardPrefab;

        [Header("Button")]
        [SerializeField, Tooltip("点击后关闭当前 BuffWindow 的按钮。")]
        private Button closeButton;

        // 当前由面板动态生成的 BuffCard，用于重新注入数据时清理旧实例。
        private readonly List<BuffCardController> generatedBuffCards = new List<BuffCardController>();

        /// <summary>
        /// Panel 启用时注册关闭按钮点击事件。
        /// </summary>
        private void OnEnable()
        {
            RegisterCloseButtonClick();
        }

        /// <summary>
        /// Panel 关闭时注销关闭按钮点击事件，避免重复绑定。
        /// </summary>
        private void OnDisable()
        {
            UnregisterCloseButtonClick();
        }

        /// <summary>
        /// 打开 BuffWindow，实际动画入口交给 WindowDropBounceAnimator.Open。
        /// </summary>
        public void Open()
        {
            EnsureBuffWindowAnimator();

            if (buffWindowAnimator == null)
            {
                Debug.LogWarning($"{nameof(WindowShopPanelManager)} cannot find {nameof(WindowDropBounceAnimator)}.", this);
                return;
            }

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            buffWindowAnimator.Open();
        }

        /// <summary>
        /// 关闭 BuffWindow，实际动画入口交给 WindowDropBounceAnimator.Close。
        /// </summary>
        public void Close()
        {
            EnsureBuffWindowAnimator();

            if (buffWindowAnimator == null)
            {
                gameObject.SetActive(false);
                return;
            }

            buffWindowAnimator.Close();
        }

        /// <summary>
        /// 从外部注入 BuffCard 生成数量，并刷新当前 BuffWindow 的 BuffCard 列表。
        /// </summary>
        public void InjectData(int buffCardCount)
        {
            ClearGeneratedBuffCards();
            EnsureBuffCardRoot();

            if (buffCardRoot == null)
            {
                Debug.LogWarning($"{nameof(WindowShopPanelManager)} needs a buff card root.", this);
                return;
            }

            if (buffCardPrefab == null)
            {
                Debug.LogWarning($"{nameof(WindowShopPanelManager)} needs a buff card prefab.", this);
                return;
            }

            int safeCount = Mathf.Max(0, buffCardCount);
            for (int i = 0; i < safeCount; i++)
            {
                CreateBuffCard(i);
            }
        }

        /// <summary>
        /// 给关闭按钮注册点击事件。
        /// </summary>
        private void RegisterCloseButtonClick()
        {
            if (closeButton == null)
            {
                Debug.LogWarning($"{nameof(WindowShopPanelManager)} needs a close button.", this);
                return;
            }

            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.RemoveListener(OnCloseButtonClicked);
            closeButton.onClick.AddListener(OnCloseButtonClicked);
        }

        /// <summary>
        /// 点击关闭按钮后交给流程 UI 编排器推进下一步。
        /// </summary>
        private void OnCloseButtonClicked()
        {
            GameFlowPanelCoordinator.GetOrCreate().NextStep();
        }

        /// <summary>
        /// 移除关闭按钮点击事件。
        /// </summary>
        private void UnregisterCloseButtonClick()
        {
            if (closeButton == null)
            {
                return;
            }

            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.RemoveListener(OnCloseButtonClicked);
        }

        /// <summary>
        /// 查找并缓存 BuffWindow 动画组件。
        /// </summary>
        private void EnsureBuffWindowAnimator()
        {
            if (buffWindowAnimator != null)
            {
                return;
            }

            buffWindowAnimator = GetComponent<WindowDropBounceAnimator>();

            if (buffWindowAnimator == null)
            {
                buffWindowAnimator = GetComponentInChildren<WindowDropBounceAnimator>(true);
            }
        }

        /// <summary>
        /// 查找并缓存动态 BuffCard 的父节点。
        /// </summary>
        private void EnsureBuffCardRoot()
        {
            if (buffCardRoot != null)
            {
                return;
            }

            buffCardRoot = transform as RectTransform;
        }

        /// <summary>
        /// 生成单个 BuffCard，并用临时序号文本填充卡片内容。
        /// </summary>
        private void CreateBuffCard(int index)
        {
            BuffCardController buffCard = Instantiate(buffCardPrefab, buffCardRoot);
            buffCard.name = $"{buffCardPrefab.name}_{index + 1}";
            buffCard.InjectData((index + 1).ToString());
            generatedBuffCards.Add(buffCard);
        }

        /// <summary>
        /// 清理上一次由当前面板动态生成的 BuffCard。
        /// </summary>
        private void ClearGeneratedBuffCards()
        {
            for (int i = generatedBuffCards.Count - 1; i >= 0; i--)
            {
                BuffCardController buffCard = generatedBuffCards[i];
                if (buffCard == null)
                {
                    continue;
                }

                DestroyGeneratedBuffCard(buffCard);
            }

            generatedBuffCards.Clear();
        }

        /// <summary>
        /// 按当前运行环境销毁动态生成的 BuffCard。
        /// </summary>
        private static void DestroyGeneratedBuffCard(BuffCardController buffCard)
        {
            if (Application.isPlaying)
            {
                Destroy(buffCard.gameObject);
                return;
            }

            DestroyImmediate(buffCard.gameObject);
        }

        /// <summary>
        /// 编辑器添加组件时自动填充当前物体上的 BuffWindow 动画组件和 BuffCard 父节点。
        /// </summary>
        private void Reset()
        {
            EnsureBuffWindowAnimator();
            EnsureBuffCardRoot();
        }
    }
}
