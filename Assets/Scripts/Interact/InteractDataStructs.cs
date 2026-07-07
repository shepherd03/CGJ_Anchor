using System;
using UnityEngine;

/// <summary>
/// 交互数据。
/// </summary>
[Serializable]
public struct InteractData
{
    [Tooltip("交互数值数组。")]
    public float[] value;

    [Tooltip("交互内容文本。")]
    public string content;
}

/// <summary>
/// Floating UI 目标开关状态变化事件。
/// </summary>
public readonly struct FloatingUITargetStateChangedEvent
{
    public readonly ButtonPrefabSpawner Source;
    public readonly bool IsOpen;

    public FloatingUITargetStateChangedEvent(ButtonPrefabSpawner source, bool isOpen)
    {
        Source = source;
        IsOpen = isOpen;
    }
}
