using UnityEngine;

/// <summary>
/// 让当前物体跟随鼠标在世界空间中的位置。
/// </summary>
[DisallowMultipleComponent]
public class MouseWorldFollower : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField, Tooltip("用于把鼠标屏幕坐标转换为世界坐标的 Camera。留空时自动使用 Camera.main。")]
    private Camera targetCamera;

    [Header("Bounds")]
    [SerializeField, Tooltip("是否把当前物体限制在以启动位置为中心的正方体范围内。")]
    private bool enableCubeBounds = true;

    [SerializeField, Min(0f), Tooltip("可活动正方体的边长。0 表示锁死在中心点。")]
    private float cubeSideLength = 10f;

    // 运行时记录启动位置作为活动正方体中心，避免边界跟着物体移动。
    private Vector3 cubeCenter;

    [Header("Follow")]
    [SerializeField, Tooltip("是否启用鼠标跟随。关闭后物体保持当前位置。")]
    private bool enableFollow = true;

    [SerializeField, Tooltip("是否使用当前物体的 Z 坐标作为鼠标投射平面。2D 项目一般保持开启。")]
    private bool useCurrentZAsPlane = true;

    [SerializeField, Tooltip("鼠标射线投射到的世界 Z 平面。关闭 Use Current Z As Plane 时使用。")]
    private float worldPlaneZ;

    [SerializeField, Min(0f), Tooltip("跟随鼠标的平滑时间。0 表示立即移动到鼠标位置。")]
    private float smoothTime;

    [SerializeField, Tooltip("是否使用不受 Time.timeScale 影响的时间。暂停时仍需要跟随鼠标就开启。")]
    private bool useUnscaledTime = true;

    private Vector3 followVelocity;

    /// <summary>
    /// 添加组件时自动补齐主相机引用。
    /// </summary>
    private void Reset()
    {
        ResolveCamera();
    }

    /// <summary>
    /// 初始化鼠标坐标转换所需的相机和活动正方体中心。
    /// </summary>
    private void Awake()
    {
        ResolveCamera();
        CaptureCubeCenter();
    }

    /// <summary>
    /// 每帧把当前物体移动到鼠标对应的世界坐标。
    /// </summary>
    private void Update()
    {
        if (!enableFollow)
        {
            return;
        }

        if (targetCamera == null)
        {
            ResolveCamera();
        }

        if (!TryGetMouseWorldPosition(out Vector3 targetPosition))
        {
            return;
        }

        MoveToTargetPosition(targetPosition);
    }

    /// <summary>
    /// 查找用于屏幕坐标转换的相机。
    /// </summary>
    private void ResolveCamera()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    /// <summary>
    /// 将鼠标屏幕坐标投射到指定 Z 平面，得到世界坐标。
    /// </summary>
    private bool TryGetMouseWorldPosition(out Vector3 worldPosition)
    {
        worldPosition = transform.position;

        if (targetCamera == null)
        {
            return false;
        }

        if (!TryReadMouseScreenPosition(out Vector3 screenPosition))
        {
            return false;
        }

        float planeZ = GetWorldPlaneZ();
        Plane mousePlane = new Plane(Vector3.forward, new Vector3(0f, 0f, planeZ));
        Ray mouseRay = targetCamera.ScreenPointToRay(screenPosition);

        if (!mousePlane.Raycast(mouseRay, out float enter))
        {
            return false;
        }

        worldPosition = mouseRay.GetPoint(enter);
        worldPosition.z = planeZ;
        return true;
    }

    /// <summary>
    /// 读取旧版 Input Manager 的鼠标屏幕坐标。
    /// </summary>
    private bool TryReadMouseScreenPosition(out Vector3 screenPosition)
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        screenPosition = Input.mousePosition;
        return true;
#else
        screenPosition = default;
        return false;
#endif
    }

    /// <summary>
    /// 获取鼠标投射使用的世界 Z 平面。
    /// </summary>
    private float GetWorldPlaneZ()
    {
        if (useCurrentZAsPlane)
        {
            return transform.position.z;
        }

        return worldPlaneZ;
    }

    /// <summary>
    /// 按配置立即移动或平滑移动到正方体限制后的目标位置。
    /// </summary>
    private void MoveToTargetPosition(Vector3 targetPosition)
    {
        targetPosition = ClampToCubeBounds(targetPosition);

        if (smoothTime <= 0f)
        {
            transform.position = targetPosition;
            followVelocity = Vector3.zero;
            return;
        }

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        Vector3 nextPosition = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref followVelocity,
            smoothTime,
            Mathf.Infinity,
            deltaTime);

        Vector3 clampedPosition = ClampToCubeBounds(nextPosition);
        ResetBlockedVelocity(nextPosition, clampedPosition);
        transform.position = clampedPosition;
    }

    /// <summary>
    /// 记录当前物体位置作为正方体活动范围中心。
    /// </summary>
    private void CaptureCubeCenter()
    {
        cubeCenter = transform.position;
    }

    /// <summary>
    /// 将目标位置限制到以启动位置为中心的正方体范围内。
    /// </summary>
    private Vector3 ClampToCubeBounds(Vector3 targetPosition)
    {
        if (!enableCubeBounds)
        {
            return targetPosition;
        }

        float halfSideLength = cubeSideLength * 0.5f;
        targetPosition.x = ClampAxis(targetPosition.x, cubeCenter.x, halfSideLength);
        targetPosition.y = ClampAxis(targetPosition.y, cubeCenter.y, halfSideLength);
        targetPosition.z = ClampAxis(targetPosition.z, cubeCenter.z, halfSideLength);
        return targetPosition;
    }

    /// <summary>
    /// 按单轴计算正方体范围内的合法坐标。
    /// </summary>
    private static float ClampAxis(float value, float center, float halfSideLength)
    {
        return Mathf.Clamp(value, center - halfSideLength, center + halfSideLength);
    }

    /// <summary>
    /// 平滑跟随被边界挡住时清掉对应轴速度，避免持续顶边抖动。
    /// </summary>
    private void ResetBlockedVelocity(Vector3 nextPosition, Vector3 clampedPosition)
    {
        if (!Mathf.Approximately(nextPosition.x, clampedPosition.x))
        {
            followVelocity.x = 0f;
        }

        if (!Mathf.Approximately(nextPosition.y, clampedPosition.y))
        {
            followVelocity.y = 0f;
        }

        if (!Mathf.Approximately(nextPosition.z, clampedPosition.z))
        {
            followVelocity.z = 0f;
        }
    }

    /// <summary>
    /// 在 Inspector 修改参数时修正非法范围。
    /// </summary>
    private void OnValidate()
    {
        cubeSideLength = Mathf.Max(0f, cubeSideLength);
        smoothTime = Mathf.Max(0f, smoothTime);
    }
}
