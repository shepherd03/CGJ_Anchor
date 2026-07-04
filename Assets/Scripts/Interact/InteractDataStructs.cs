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
