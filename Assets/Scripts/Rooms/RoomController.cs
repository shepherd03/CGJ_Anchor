using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 管理单个房间内的多个交互点。
/// </summary>
[DisallowMultipleComponent]
public sealed class RoomController : MonoBehaviour
{
    [Header("Room")]
    [SerializeField, Tooltip("房间唯一标识。为空时默认使用当前物体名。")]
    private string roomId;

    [Header("Interaction Points")]
    [SerializeField, Tooltip("当前房间持有的交互点列表。为空时会从当前物体子级查找。")]
    private List<RoomInteractionPointManager> interactionPoints = new List<RoomInteractionPointManager>();

    public string RoomId => roomId;
    public IReadOnlyList<RoomInteractionPointManager> InteractionPoints => interactionPoints;
    public int InteractionPointCount => interactionPoints.Count;

    /// <summary>
    /// 添加组件时初始化房间标识并扫描子级交互点。
    /// </summary>
    private void Reset()
    {
        ResolveDefaultRoomId();
        RefreshInteractionPoints();
    }

    /// <summary>
    /// 运行时初始化房间交互点列表。
    /// </summary>
    private void Awake()
    {
        ResolveDefaultRoomId();
        RefreshInteractionPoints();
    }

    /// <summary>
    /// Inspector 修改后清理空引用和重复交互点。
    /// </summary>
    private void OnValidate()
    {
        ResolveDefaultRoomId();
        RemoveInvalidInteractionPoints();
    }

    /// <summary>
    /// 重新扫描当前房间子级中的所有交互点。
    /// </summary>
    [ContextMenu("Refresh Interaction Points")]
    public void RefreshInteractionPoints()
    {
        if (interactionPoints == null)
        {
            interactionPoints = new List<RoomInteractionPointManager>();
        }

        interactionPoints.Clear();

        RoomInteractionPointManager[] foundPoints = GetComponentsInChildren<RoomInteractionPointManager>(true);
        for (int i = 0; i < foundPoints.Length; i++)
        {
            RegisterInteractionPoint(foundPoints[i]);
        }
    }

    /// <summary>
    /// 向当前房间注册一个交互点。
    /// </summary>
    public bool RegisterInteractionPoint(RoomInteractionPointManager interactionPoint)
    {
        if (interactionPoint == null)
        {
            return false;
        }

        if (interactionPoints == null)
        {
            interactionPoints = new List<RoomInteractionPointManager>();
        }

        if (interactionPoints.Contains(interactionPoint))
        {
            return false;
        }

        interactionPoints.Add(interactionPoint);
        return true;
    }

    /// <summary>
    /// 从当前房间注销一个交互点。
    /// </summary>
    public bool UnregisterInteractionPoint(RoomInteractionPointManager interactionPoint)
    {
        if (interactionPoints == null || interactionPoint == null)
        {
            return false;
        }

        return interactionPoints.Remove(interactionPoint);
    }

    /// <summary>
    /// 根据交互点 ID 查找当前房间内的交互点。
    /// </summary>
    public bool TryGetInteractionPoint(string pointId, out RoomInteractionPointManager interactionPoint)
    {
        interactionPoint = null;

        if (string.IsNullOrWhiteSpace(pointId) || interactionPoints == null)
        {
            return false;
        }

        for (int i = 0; i < interactionPoints.Count; i++)
        {
            RoomInteractionPointManager currentPoint = interactionPoints[i];
            if (currentPoint != null && currentPoint.PointId == pointId)
            {
                interactionPoint = currentPoint;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 设置当前房间全部交互点的可交互状态。
    /// </summary>
    public void SetAllInteractionPointsInteractable(bool value)
    {
        if (interactionPoints == null)
        {
            return;
        }

        for (int i = 0; i < interactionPoints.Count; i++)
        {
            if (interactionPoints[i] != null)
            {
                interactionPoints[i].SetInteractable(value);
            }
        }
    }

    /// <summary>
    /// 关闭当前房间内所有交互点的 Floating UI。
    /// </summary>
    public void CloseAllFloatingUI()
    {
        if (interactionPoints == null)
        {
            return;
        }

        for (int i = 0; i < interactionPoints.Count; i++)
        {
            if (interactionPoints[i] != null)
            {
                interactionPoints[i].CloseFloatingUI();
            }
        }
    }

    /// <summary>
    /// 只在 ID 为空时使用物体名作为默认房间标识。
    /// </summary>
    private void ResolveDefaultRoomId()
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            roomId = name;
        }
    }

    /// <summary>
    /// 移除交互点列表中的空引用和重复项。
    /// </summary>
    private void RemoveInvalidInteractionPoints()
    {
        if (interactionPoints == null)
        {
            interactionPoints = new List<RoomInteractionPointManager>();
            return;
        }

        for (int i = interactionPoints.Count - 1; i >= 0; i--)
        {
            RoomInteractionPointManager currentPoint = interactionPoints[i];
            if (currentPoint == null || IndexOfInteractionPoint(currentPoint) != i)
            {
                interactionPoints.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 查找指定交互点第一次出现在列表中的索引。
    /// </summary>
    private int IndexOfInteractionPoint(RoomInteractionPointManager interactionPoint)
    {
        for (int i = 0; i < interactionPoints.Count; i++)
        {
            if (interactionPoints[i] == interactionPoint)
            {
                return i;
            }
        }

        return -1;
    }
}
