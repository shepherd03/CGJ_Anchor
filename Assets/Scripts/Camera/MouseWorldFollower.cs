using Cinemachine;
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
    [SerializeField, Tooltip("是否把当前物体限制在主虚拟镜头视野内。")]
    private bool enableCameraBounds = true;

    [SerializeField, Tooltip("提供外层可移动范围的主 Cinemachine Virtual Camera。")]
    private CinemachineVirtualCamera mainVirtualCamera;

    [SerializeField, Tooltip("需要完整留在主镜头范围内的另一个 Cinemachine Virtual Camera。边界会按它的半宽半高向内收缩。")]
    private CinemachineVirtualCamera containedVirtualCamera;

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
    /// 初始化鼠标坐标转换所需的相机。
    /// </summary>
    private void Awake()
    {
        ResolveCamera();
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
    /// 按配置立即移动或平滑移动到限制后的目标位置。
    /// </summary>
    private void MoveToTargetPosition(Vector3 targetPosition)
    {
        targetPosition = ClampToCameraBounds(targetPosition);

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

        Vector3 clampedPosition = ClampToCameraBounds(nextPosition);
        ResetBlockedVelocity(nextPosition, clampedPosition);
        transform.position = clampedPosition;
    }

    /// <summary>
    /// 将目标位置限制到主虚拟镜头视野内缩后的范围。
    /// </summary>
    private Vector3 ClampToCameraBounds(Vector3 targetPosition)
    {
        if (!enableCameraBounds || !TryCalculateCameraBounds(out Vector2 center, out Vector2 outerHalfSize, out Vector2 innerHalfSize))
        {
            return targetPosition;
        }

        targetPosition.x = ClampAxis(targetPosition.x, center.x, outerHalfSize.x, innerHalfSize.x);
        targetPosition.y = ClampAxis(targetPosition.y, center.y, outerHalfSize.y, innerHalfSize.y);
        return targetPosition;
    }

    /// <summary>
    /// 计算主虚拟镜头中心、主镜头半尺寸和被包含镜头半尺寸。
    /// </summary>
    private bool TryCalculateCameraBounds(out Vector2 center, out Vector2 outerHalfSize, out Vector2 innerHalfSize)
    {
        center = Vector2.zero;
        outerHalfSize = Vector2.zero;
        innerHalfSize = Vector2.zero;

        if (mainVirtualCamera == null || containedVirtualCamera == null)
        {
            return false;
        }

        if (!TryGetVirtualCameraHalfSize(mainVirtualCamera, out outerHalfSize))
        {
            return false;
        }

        if (!TryGetVirtualCameraHalfSize(containedVirtualCamera, out innerHalfSize))
        {
            return false;
        }

        Vector3 mainCenter = GetVirtualCameraCenter(mainVirtualCamera);
        center = new Vector2(mainCenter.x, mainCenter.y);
        return true;
    }

    /// <summary>
    /// 读取正交虚拟镜头的世界空间半宽半高。
    /// </summary>
    private bool TryGetVirtualCameraHalfSize(CinemachineVirtualCamera virtualCamera, out Vector2 halfSize)
    {
        halfSize = Vector2.zero;

        if (virtualCamera == null)
        {
            return false;
        }

        LensSettings lens = GetVirtualCameraLens(virtualCamera);
        bool isOrthographic = lens.Orthographic || (targetCamera != null && targetCamera.orthographic);
        if (!isOrthographic)
        {
            return false;
        }

        float halfHeight = Mathf.Max(0f, lens.OrthographicSize);
        float aspect = GetVirtualCameraAspect(virtualCamera, lens);
        if (halfHeight <= 0f || aspect <= 0f)
        {
            return false;
        }

        halfSize = new Vector2(halfHeight * aspect, halfHeight);
        return true;
    }

    /// <summary>
    /// 读取虚拟镜头当前状态的 Lens，状态无效时退回 Inspector 配置。
    /// </summary>
    private LensSettings GetVirtualCameraLens(CinemachineVirtualCamera virtualCamera)
    {
        if (virtualCamera.PreviousStateIsValid)
        {
            return virtualCamera.State.Lens;
        }

        return virtualCamera.m_Lens;
    }

    /// <summary>
    /// 获取虚拟镜头宽高比，优先使用 Cinemachine 当前状态，其次使用实际渲染相机。
    /// </summary>
    private float GetVirtualCameraAspect(CinemachineVirtualCamera virtualCamera, LensSettings lens)
    {
        if (virtualCamera.PreviousStateIsValid && lens.Aspect > 0f)
        {
            return lens.Aspect;
        }

        if (targetCamera != null && targetCamera.aspect > 0f)
        {
            return targetCamera.aspect;
        }

        return Mathf.Max(1f, lens.Aspect);
    }

    /// <summary>
    /// 获取虚拟镜头的当前世界中心，状态无效时退回 Transform 位置。
    /// </summary>
    private Vector3 GetVirtualCameraCenter(CinemachineVirtualCamera virtualCamera)
    {
        if (virtualCamera.PreviousStateIsValid)
        {
            return virtualCamera.State.FinalPosition;
        }

        return virtualCamera.transform.position;
    }

    /// <summary>
    /// 按单轴计算主镜头边界内缩后的合法范围。
    /// </summary>
    private float ClampAxis(float value, float center, float outerHalfSize, float innerHalfSize)
    {
        float min = center - outerHalfSize + innerHalfSize;
        float max = center + outerHalfSize - innerHalfSize;

        if (min > max)
        {
            return center;
        }

        return Mathf.Clamp(value, min, max);
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
    }

    /// <summary>
    /// 在 Inspector 修改参数时修正非法范围。
    /// </summary>
    private void OnValidate()
    {
        smoothTime = Mathf.Max(0f, smoothTime);
    }
}
