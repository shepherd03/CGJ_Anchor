using Anchor.GameFlow;
using UnityEngine;
using UnityEngine.UI;

namespace Anchor.UI.Panel
{
    [DisallowMultipleComponent]
    public sealed class MainPanelManager : MonoBehaviour
    {
        [Header("Button")]
        [SerializeField, Tooltip("点击后结束本周行动并关闭 MainPanel 的按钮。")]
        private Button nextWeekButton;

        private GameFlowRunner gameFlowRunner;

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
        /// 点击下一周按钮后结束本周行动，并隐藏当前 MainPanel。
        /// </summary>
        private void OnNextWeekButtonClicked()
        {
            EnsureGameFlowRunner();

            if (gameFlowRunner == null)
            {
                Debug.LogWarning($"{nameof(MainPanelManager)} cannot find {nameof(GameFlowRunner)} in the scene.", this);
                return;
            }

            gameFlowRunner.FinishWeekAction();
            Close();
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
        /// 查找并缓存场景中的 GameFlowRunner。
        /// </summary>
        private void EnsureGameFlowRunner()
        {
            if (gameFlowRunner == null)
            {
                gameFlowRunner = FindObjectOfType<GameFlowRunner>();
            }
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
