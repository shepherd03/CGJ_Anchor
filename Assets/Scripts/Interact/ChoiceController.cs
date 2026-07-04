using TMPro;
using UnityEngine;

/// <summary>
/// 管理单个选项的数据，并把数据内容显示到 TextMeshProUGUI。
/// </summary>
[DisallowMultipleComponent]
public class ChoiceController : MonoBehaviour
{
    [Header("View")]
    [SerializeField, Tooltip("显示选项内容的 TextMeshProUGUI。由 Inspector 手动配置。")]
    private TextMeshProUGUI contentText;

    [Header("Data")]
    [SerializeField, Tooltip("当前持有的交互数据。运行时可通过 InjectData 从外部注入。")]
    private InteractData currentData;

    /// <summary>
    /// 当前持有的交互数据，只读暴露给外部查询。
    /// </summary>
    public InteractData CurrentData => currentData;

    /// <summary>
    /// 从外部注入交互数据，并立即刷新文本显示。
    /// </summary>
    public void InjectData(InteractData data)
    {
        currentData = data;
        RefreshContentText();
    }

    /// <summary>
    /// 根据当前交互数据刷新 TextMeshProUGUI 文本。
    /// </summary>
    private void RefreshContentText()
    {
        if (contentText == null)
        {
            Debug.LogWarning($"{nameof(ChoiceController)} needs a content text.", this);
            return;
        }

        contentText.text = currentData.content;
    }
}
