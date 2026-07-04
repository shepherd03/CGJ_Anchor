using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Anchor.UI.Panel
{
    [DisallowMultipleComponent]
    public sealed class MainPanelManager : MonoBehaviour
    {
        [Header("Status Text")]
        [SerializeField, Tooltip("显示 Gold 数值的 TextMeshProUGUI。")]
        private TextMeshProUGUI goldText;

        [SerializeField, Tooltip("显示 Bug 数值的 TextMeshProUGUI。")]
        private TextMeshProUGUI bugText;

        [SerializeField, Tooltip("显示 View 数值的 TextMeshProUGUI。")]
        private TextMeshProUGUI viewText;

        [SerializeField, Tooltip("显示 Audio 数值的 TextMeshProUGUI。")]
        private TextMeshProUGUI audioText;

        [Header("Button")]
        [SerializeField, Tooltip("点击后结束本周行动并关闭 MainPanel 的按钮。")]
        private Button nextWeekButton;

        /// <summary>
        /// Panel 启用时注册下一周按钮点击事件。
        /// </summary>
        private void OnEnable()
        {
            RegisterNextWeekButtonClick();
        }

        /// <summary>
        /// Panel 关闭时注销下一周按钮点击事件，避免重复绑定。
        /// </summary>
        private void OnDisable()
        {
            UnregisterNextWeekButtonClick();
        }

        /// <summary>
        /// 打开 MainPanel。
        /// </summary>
        public void Open()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// 关闭 MainPanel。
        /// </summary>
        public void Close()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 点击下一周按钮后交给流程 UI 编排器推进下一步。
        /// </summary>
        private void OnNextWeekButtonClicked()
        {
            GameFlowPanelCoordinator.GetOrCreate().NextStep();
        }

        /// <summary>
        /// 给下一周按钮注册点击事件。
        /// </summary>
        private void RegisterNextWeekButtonClick()
        {
            if (nextWeekButton == null)
            {
                Debug.LogWarning($"{nameof(MainPanelManager)} needs a next week button.", this);
                return;
            }

            nextWeekButton.onClick.RemoveListener(OnNextWeekButtonClicked);
            nextWeekButton.onClick.AddListener(OnNextWeekButtonClicked);
        }

        /// <summary>
        /// 移除下一周按钮点击事件。
        /// </summary>
        private void UnregisterNextWeekButtonClick()
        {
            if (nextWeekButton == null)
            {
                return;
            }

            nextWeekButton.onClick.RemoveListener(OnNextWeekButtonClicked);
        }

        /// <summary>
        /// 挂到 Button 本体时自动填充按钮引用。
        /// </summary>
        private void Reset()
        {
            if (nextWeekButton == null)
            {
                nextWeekButton = GetComponent<Button>();
            }
        }
    }
}
