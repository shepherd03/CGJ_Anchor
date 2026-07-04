using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 控制单条弹幕的文本绑定、从右到左移动和离屏回收。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class BulletBugController : MonoBehaviour
{
    [Header("View")]
    [SerializeField, Tooltip("弹幕文本；留空时自动查找自身或子物体上的第一个 TextMeshProUGUI。")]
    private TextMeshProUGUI contentText;

    [SerializeField, Tooltip("播放前是否把锚点改到父节点中心，保证屏幕坐标计算稳定。")]
    private bool useCenteredAnchors = true;

    private RectTransform rectTransform;
    private Action<BulletBugController> onFinished;
    private float despawnX;
    private float moveSpeed;
    private bool useUnscaledTime;
    private bool isPlaying;

    /// <summary>
    /// 当前弹幕使用的 TextMeshProUGUI，只读暴露给屏幕控制器取默认文本。
    /// </summary>
    public TextMeshProUGUI ContentText
    {
        get
        {
            EnsureReferences();
            return contentText;
        }
    }

    /// <summary>
    /// 初始化 RectTransform 和文本引用。
    /// </summary>
    private void Awake()
    {
        EnsureReferences();
    }

    /// <summary>
    /// 每帧按固定速度向左平滑移动，完全离屏后结束生命周期。
    /// </summary>
    private void Update()
    {
        if (!isPlaying)
        {
            return;
        }

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        Vector2 position = rectTransform.anchoredPosition;
        position.x -= moveSpeed * deltaTime;
        rectTransform.anchoredPosition = position;

        if (position.x <= despawnX)
        {
            Finish();
        }
    }

    /// <summary>
    /// 写入弹幕文本，并刷新 TextMeshProUGUI 的网格数据。
    /// </summary>
    public void SetContent(string content)
    {
        EnsureReferences();

        if (contentText == null)
        {
            Debug.LogWarning($"{nameof(BulletBugController)} cannot find TextMeshProUGUI on self or children.", this);
            return;
        }

        contentText.text = content;
        contentText.ForceMeshUpdate();
    }

    /// <summary>
    /// 从指定起点开始向左播放弹幕。
    /// </summary>
    public void Play(Vector2 startPosition, float targetDespawnX, float speed, bool shouldUseUnscaledTime, Action<BulletBugController> finishCallback)
    {
        EnsureReferences();
        ApplyCenteredAnchors();

        rectTransform.anchoredPosition = startPosition;
        despawnX = targetDespawnX;
        moveSpeed = Mathf.Max(0f, speed);
        useUnscaledTime = shouldUseUnscaledTime;
        onFinished = finishCallback;
        isPlaying = true;
        gameObject.SetActive(true);
    }

    /// <summary>
    /// 计算弹幕视觉宽度，优先取 RectTransform，文本更宽时用文本首选宽度。
    /// </summary>
    public float GetVisualWidth()
    {
        EnsureReferences();

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);

        float width = rectTransform.rect.width;
        if (contentText != null)
        {
            contentText.ForceMeshUpdate();
            width = Mathf.Max(width, contentText.GetPreferredValues(contentText.text).x);
        }

        return Mathf.Max(0f, width);
    }

    /// <summary>
    /// 立即结束当前弹幕并销毁自身物体。
    /// </summary>
    public void Finish()
    {
        if (!isPlaying)
        {
            return;
        }

        isPlaying = false;
        Action<BulletBugController> callback = onFinished;
        onFinished = null;
        callback?.Invoke(this);
        Destroy(gameObject);
    }

    /// <summary>
    /// 缓存 RectTransform，并查找自身或子物体上的第一个 TextMeshProUGUI。
    /// </summary>
    private void EnsureReferences()
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        if (contentText == null)
        {
            contentText = GetComponent<TextMeshProUGUI>();
            if (contentText == null)
            {
                contentText = GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }
    }

    /// <summary>
    /// 运行时统一弹幕锚点，让 anchoredPosition 对应父节点中心坐标。
    /// </summary>
    private void ApplyCenteredAnchors()
    {
        if (!useCenteredAnchors)
        {
            return;
        }

        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }
}
