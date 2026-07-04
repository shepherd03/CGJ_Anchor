using Cinemachine;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 根据鼠标滚轮在两个 Cinemachine Virtual Camera 之间平滑切换。
/// </summary>
[DisallowMultipleComponent]
public class CinemachineScrollZoomController : MonoBehaviour
{
    [Header("Cameras")]
    [SerializeField, Tooltip("主摄像机上的 CinemachineBrain。留空时自动从 Camera.main 查找。")]
    private CinemachineBrain cinemachineBrain;

    [FormerlySerializedAs("virtualCamera")]
    [SerializeField, Tooltip("滚轮后退时切回的原先虚拟镜头。留空时自动取当前物体上的 CinemachineVirtualCamera。")]
    private CinemachineVirtualCamera originalVirtualCamera;

    [SerializeField, Tooltip("滚轮向前时切换到的目标虚拟镜头。必须手动拖入另一个 CinemachineVirtualCamera。")]
    private CinemachineVirtualCamera targetVirtualCamera;

    [Header("Priority")]
    [SerializeField, Tooltip("被激活镜头的 Priority。必须高于未激活镜头。")]
    private int activePriority = 20;

    [SerializeField, Tooltip("未激活镜头的 Priority。数值低于激活镜头即可。")]
    private int inactivePriority = 0;

    [SerializeField, Tooltip("启动时是否初始化两个虚拟镜头的 Priority，并默认显示原先镜头。")]
    private bool initializePrioritiesOnAwake = true;

    [Header("Blend")]
    [SerializeField, Tooltip("是否用这里的设置覆盖 CinemachineBrain 的默认过渡效果。")]
    private bool overrideDefaultBlend = true;

    [SerializeField, Tooltip("镜头切换的过渡曲线类型。一般用 EaseInOut。")]
    private CinemachineBlendDefinition.Style blendStyle = CinemachineBlendDefinition.Style.EaseInOut;

    [SerializeField, Min(0f), Tooltip("镜头切换过渡时间，单位秒。0 表示瞬切。")]
    private float blendTime = 0.6f;

    [Header("Input")]
    [SerializeField, Tooltip("是否启用鼠标滚轮切换镜头。关闭后不会响应滚轮。")]
    private bool enableScrollSwitch = true;

    [SerializeField, Min(0f), Tooltip("滚轮输入阈值。滚轮增量绝对值小于该值时忽略。")]
    private float scrollThreshold = 0.01f;

    [SerializeField, Tooltip("反转滚轮方向。默认滚轮向前切到目标镜头，向后切回原先镜头。")]
    private bool invertScrollDirection;

    private Transform cachedTargetVirtualCameraFollow;
    private bool hasCachedTargetVirtualCameraFollow;
    private bool restoreTargetFollowWhenBlendEnds;
    private int restoreTargetFollowRequestFrame = -1;
    private bool showingTargetCamera;

    /// <summary>
    /// 添加组件时自动补齐同物体上的虚拟镜头和主摄像机上的 CinemachineBrain。
    /// </summary>
    private void Reset()
    {
        ResolveReferences();
    }

    /// <summary>
    /// 初始化依赖、过渡效果和默认镜头状态。
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
        CacheTargetVirtualCameraFollow();
        ApplyDefaultBlend();

        if (initializePrioritiesOnAwake)
        {
            SwitchToOriginalCamera();
        }
    }

    /// <summary>
    /// 每帧读取鼠标滚轮，根据方向切换虚拟镜头。
    /// </summary>
    private void Update()
    {
        RestoreTargetVirtualCameraFollowIfReady();

        if (!enableScrollSwitch)
        {
            return;
        }

        float scrollDelta = ReadScrollDelta();
        if (Mathf.Abs(scrollDelta) <= scrollThreshold)
        {
            return;
        }

        bool shouldSwitchToTarget = invertScrollDirection ? scrollDelta < 0f : scrollDelta > 0f;
        if (shouldSwitchToTarget)
        {
            SwitchToTargetCamera();
        }
        else
        {
            SwitchToOriginalCamera();
        }
    }

    /// <summary>
    /// 切换到滚轮向前对应的目标虚拟镜头。
    /// </summary>
    public void SwitchToTargetCamera()
    {
        if (targetVirtualCamera == null)
        {
            Debug.LogWarning($"{nameof(CinemachineScrollZoomController)} needs a target virtual camera.", this);
            return;
        }

        SwitchToCamera(targetVirtualCamera);
        showingTargetCamera = true;
        restoreTargetFollowWhenBlendEnds = false;
        ClearTargetVirtualCameraFollow();
    }

    /// <summary>
    /// 切回滚轮后退对应的原先虚拟镜头。
    /// </summary>
    public void SwitchToOriginalCamera()
    {
        if (originalVirtualCamera == null)
        {
            Debug.LogWarning($"{nameof(CinemachineScrollZoomController)} needs an original virtual camera.", this);
            return;
        }

        SwitchToCamera(originalVirtualCamera);
        showingTargetCamera = false;
        ScheduleTargetVirtualCameraFollowRestore();
    }

    /// <summary>
    /// 查找脚本依赖的 CinemachineBrain 和默认原先镜头。
    /// </summary>
    private void ResolveReferences()
    {
        if (originalVirtualCamera == null)
        {
            originalVirtualCamera = GetComponent<CinemachineVirtualCamera>();
        }

        if (cinemachineBrain != null)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            cinemachineBrain = mainCamera.GetComponent<CinemachineBrain>();
        }
    }

    /// <summary>
    /// 应用 CinemachineBrain 的默认镜头切换过渡。
    /// </summary>
    private void ApplyDefaultBlend()
    {
        if (!overrideDefaultBlend || cinemachineBrain == null)
        {
            return;
        }

        cinemachineBrain.m_DefaultBlend = new CinemachineBlendDefinition(blendStyle, blendTime);
    }

    /// <summary>
    /// 缓存目标虚拟镜头原本的 Follow，避免切到目标镜头后清空时丢失引用。
    /// </summary>
    private void CacheTargetVirtualCameraFollow()
    {
        if (hasCachedTargetVirtualCameraFollow || targetVirtualCamera == null)
        {
            return;
        }

        cachedTargetVirtualCameraFollow = targetVirtualCamera.Follow;
        hasCachedTargetVirtualCameraFollow = true;
    }

    /// <summary>
    /// 切换到目标虚拟镜头后清空它的 Follow，让它停在当前镜头位置参与过渡。
    /// </summary>
    private void ClearTargetVirtualCameraFollow()
    {
        if (targetVirtualCamera == null)
        {
            return;
        }

        CacheTargetVirtualCameraFollow();
        targetVirtualCamera.Follow = null;
    }

    /// <summary>
    /// 切回原先虚拟镜头后恢复目标虚拟镜头原本的 Follow。
    /// </summary>
    private void RestoreTargetVirtualCameraFollow()
    {
        if (targetVirtualCamera == null || !hasCachedTargetVirtualCameraFollow)
        {
            return;
        }

        targetVirtualCamera.Follow = cachedTargetVirtualCameraFollow;
    }

    /// <summary>
    /// 切回原先虚拟镜头后安排恢复目标虚拟镜头的 Follow，避免混合期间目标镜头继续跟随。
    /// </summary>
    private void ScheduleTargetVirtualCameraFollowRestore()
    {
        restoreTargetFollowWhenBlendEnds = true;
        restoreTargetFollowRequestFrame = Time.frameCount;
    }

    /// <summary>
    /// 在镜头混合结束后恢复目标虚拟镜头的 Follow。
    /// </summary>
    private void RestoreTargetVirtualCameraFollowIfReady()
    {
        if (!restoreTargetFollowWhenBlendEnds)
        {
            return;
        }

        if (Time.frameCount <= restoreTargetFollowRequestFrame)
        {
            return;
        }

        if (cinemachineBrain != null && cinemachineBrain.IsBlending)
        {
            return;
        }

        RestoreTargetVirtualCameraFollow();
        restoreTargetFollowWhenBlendEnds = false;
    }

    /// <summary>
    /// 通过 Priority 激活指定虚拟镜头，其余配置镜头降为未激活。
    /// </summary>
    private void SwitchToCamera(CinemachineVirtualCamera activeCamera)
    {
        if (activeCamera == null)
        {
            return;
        }

        SetCameraPriority(originalVirtualCamera, inactivePriority);
        SetCameraPriority(targetVirtualCamera, inactivePriority);
        SetCameraPriority(activeCamera, activePriority);
    }

    /// <summary>
    /// 设置单个虚拟镜头的 Priority。
    /// </summary>
    private void SetCameraPriority(CinemachineVirtualCamera camera, int priority)
    {
        if (camera == null)
        {
            return;
        }

        camera.Priority = priority;
    }

    /// <summary>
    /// 读取旧版 Input Manager 的鼠标滚轮增量。
    /// </summary>
    private float ReadScrollDelta()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.mouseScrollDelta.y;
#else
        return 0f;
#endif
    }

    /// <summary>
    /// 在 Inspector 修改参数时修正非法范围。
    /// </summary>
    private void OnValidate()
    {
        blendTime = Mathf.Max(0f, blendTime);
        scrollThreshold = Mathf.Max(0f, scrollThreshold);

        if (activePriority <= inactivePriority)
        {
            activePriority = inactivePriority + 1;
        }

        if (Application.isPlaying && showingTargetCamera)
        {
            SwitchToTargetCamera();
        }
        else if (Application.isPlaying)
        {
            SwitchToOriginalCamera();
        }
    }
}
