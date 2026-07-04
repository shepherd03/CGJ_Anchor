using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 管理当前场景内所有房间控制器。
/// </summary>
[DisallowMultipleComponent]
public sealed class CompanyController : MonoBehaviour
{
    [Header("Company")]
    [SerializeField, Tooltip("公司唯一标识。为空时默认使用当前物体名。")]
    private string companyId;

    [Header("Rooms")]
    [SerializeField, Tooltip("当前公司持有的房间列表。为空或需要刷新时会从当前场景查找。")]
    private List<RoomController> rooms = new List<RoomController>();

    public string CompanyId => companyId;
    public IReadOnlyList<RoomController> Rooms => rooms;
    public int RoomCount => rooms.Count;

    /// <summary>
    /// 添加组件时初始化公司标识并扫描场景房间。
    /// </summary>
    private void Reset()
    {
        ResolveDefaultCompanyId();
        RefreshRooms();
    }

    /// <summary>
    /// 运行时初始化公司持有的房间列表。
    /// </summary>
    private void Awake()
    {
        ResolveDefaultCompanyId();
        RefreshRooms();
    }

    /// <summary>
    /// Inspector 修改后清理空引用和重复房间。
    /// </summary>
    private void OnValidate()
    {
        ResolveDefaultCompanyId();
        RemoveInvalidRooms();
    }

    /// <summary>
    /// 重新扫描当前场景内所有房间控制器。
    /// </summary>
    [ContextMenu("Refresh Rooms")]
    public void RefreshRooms()
    {
        if (rooms == null)
        {
            rooms = new List<RoomController>();
        }

        rooms.Clear();

        RoomController[] foundRooms = FindObjectsOfType<RoomController>(true);
        for (int i = 0; i < foundRooms.Length; i++)
        {
            RegisterRoom(foundRooms[i]);
        }
    }

    /// <summary>
    /// 向当前公司注册一个房间。
    /// </summary>
    public bool RegisterRoom(RoomController room)
    {
        if (room == null)
        {
            return false;
        }

        if (rooms == null)
        {
            rooms = new List<RoomController>();
        }

        if (rooms.Contains(room))
        {
            return false;
        }

        rooms.Add(room);
        return true;
    }

    /// <summary>
    /// 从当前公司注销一个房间。
    /// </summary>
    public bool UnregisterRoom(RoomController room)
    {
        if (rooms == null || room == null)
        {
            return false;
        }

        return rooms.Remove(room);
    }

    /// <summary>
    /// 根据房间 ID 查找当前公司持有的房间。
    /// </summary>
    public bool TryGetRoom(string roomId, out RoomController room)
    {
        room = null;

        if (string.IsNullOrWhiteSpace(roomId) || rooms == null)
        {
            return false;
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            RoomController currentRoom = rooms[i];
            if (currentRoom != null && currentRoom.RoomId == roomId)
            {
                room = currentRoom;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 刷新当前公司持有的所有房间交互点列表。
    /// </summary>
    public void RefreshAllRoomInteractionPoints()
    {
        if (rooms == null)
        {
            return;
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i] != null)
            {
                rooms[i].RefreshInteractionPoints();
            }
        }
    }

    /// <summary>
    /// 设置当前公司全部房间内交互点的可交互状态。
    /// </summary>
    public void SetAllRoomsInteractable(bool value)
    {
        if (rooms == null)
        {
            return;
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i] != null)
            {
                rooms[i].SetAllInteractionPointsInteractable(value);
            }
        }
    }

    /// <summary>
    /// 关闭当前公司全部房间内的 Floating UI。
    /// </summary>
    public void CloseAllFloatingUI()
    {
        if (rooms == null)
        {
            return;
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i] != null)
            {
                rooms[i].CloseAllFloatingUI();
            }
        }
    }

    /// <summary>
    /// 只在 ID 为空时使用物体名作为默认公司标识。
    /// </summary>
    private void ResolveDefaultCompanyId()
    {
        if (string.IsNullOrWhiteSpace(companyId))
        {
            companyId = name;
        }
    }

    /// <summary>
    /// 移除房间列表中的空引用和重复项。
    /// </summary>
    private void RemoveInvalidRooms()
    {
        if (rooms == null)
        {
            rooms = new List<RoomController>();
            return;
        }

        for (int i = rooms.Count - 1; i >= 0; i--)
        {
            RoomController currentRoom = rooms[i];
            if (currentRoom == null || IndexOfRoom(currentRoom) != i)
            {
                rooms.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 查找指定房间第一次出现在列表中的索引。
    /// </summary>
    private int IndexOfRoom(RoomController room)
    {
        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i] == room)
            {
                return i;
            }
        }

        return -1;
    }
}
