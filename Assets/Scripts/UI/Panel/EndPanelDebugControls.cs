using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Anchor.UI.Panel
{
    [DisallowMultipleComponent]
    public sealed class EndPanelDebugControls : MonoBehaviour
    {
        [SerializeField] private EndPanelAnimator panelAnimator;
        [SerializeField] private TMP_InputField wishlistInput;
        [SerializeField] private TMP_InputField qualityInput;
        [SerializeField] private Button applyButton;

        private void OnEnable()
        {
            if (applyButton != null)
            {
                applyButton.onClick.RemoveListener(ApplyDebugValues);
                applyButton.onClick.AddListener(ApplyDebugValues);
            }
        }

        private void OnDisable()
        {
            if (applyButton != null)
            {
                applyButton.onClick.RemoveListener(ApplyDebugValues);
            }
        }

        public void ApplyDebugValues()
        {
            if (panelAnimator == null || wishlistInput == null || qualityInput == null)
            {
                Debug.LogWarning($"{nameof(EndPanelDebugControls)} is missing UI references.", this);
                return;
            }

            if (!int.TryParse(wishlistInput.text, out int wishlistValue) ||
                !int.TryParse(qualityInput.text, out int qualityScore))
            {
                Debug.LogWarning("EndPanel Debug 请输入有效整数。", this);
                return;
            }

            panelAnimator.PlayDebug(wishlistValue, qualityScore);
        }
    }
}
