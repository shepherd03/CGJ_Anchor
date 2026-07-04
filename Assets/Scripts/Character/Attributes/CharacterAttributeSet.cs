using System;
using System.Collections.Generic;
using YokiFrame;

namespace Anchor.Character.Attributes
{
    /// <summary>
    /// 单个角色属性变化时广播的事件数据。
    /// </summary>
    public readonly struct CharacterAttributeChangedEvent
    {
        /// <summary>发生变化的属性集合。</summary>
        public readonly CharacterAttributeSet AttributeSet;

        /// <summary>发生变化的属性 ID，来源于玩家属性配置表。</summary>
        public readonly int AttributeId;

        /// <summary>变化前的属性值。</summary>
        public readonly int PreviousValue;

        /// <summary>变化后的属性值。</summary>
        public readonly int CurrentValue;

        /// <summary>本次变化的差值。</summary>
        public int Delta => CurrentValue - PreviousValue;

        public CharacterAttributeChangedEvent(
            CharacterAttributeSet attributeSet,
            int attributeId,
            int previousValue,
            int currentValue)
        {
            AttributeSet = attributeSet;
            AttributeId = attributeId;
            PreviousValue = previousValue;
            CurrentValue = currentValue;
        }
    }

    /// <summary>
    /// 以属性配置表 ID 作为键、整数作为值的角色属性集合。
    /// </summary>
    public sealed class CharacterAttributeSet
    {
        private readonly Dictionary<int, int> mValues = new();

        /// <summary>
        /// 当前属性集合的实例级变化通知。
        /// </summary>
        public event Action<CharacterAttributeChangedEvent> Changed;

        /// <summary>
        /// 当前已记录的属性值。
        /// </summary>
        public IReadOnlyDictionary<int, int> Values => mValues;

        /// <summary>
        /// 当前已记录的属性数量。
        /// </summary>
        public int Count => mValues.Count;

        /// <summary>
        /// 判断指定属性是否已有显式记录。
        /// </summary>
        public bool Contains(int attributeId)
        {
            return mValues.ContainsKey(attributeId);
        }

        /// <summary>
        /// 读取指定属性值；未记录时返回给定默认值。
        /// </summary>
        public int Get(int attributeId, int defaultValue = 0)
        {
            return mValues.TryGetValue(attributeId, out var value) ? value : defaultValue;
        }

        /// <summary>
        /// 设置指定属性值；数值实际变化时会触发通知。
        /// </summary>
        public void Set(int attributeId, int value)
        {
            var previousValue = Get(attributeId);
            if (previousValue.Equals(value))
            {
                return;
            }

            mValues[attributeId] = value;
            NotifyChanged(attributeId, previousValue, value);
        }

        /// <summary>
        /// 在当前属性值上叠加变化量，并返回变化后的值。
        /// </summary>
        public int Add(int attributeId, int delta)
        {
            var value = Get(attributeId) + delta;
            Set(attributeId, value);
            return value;
        }

        /// <summary>
        /// 移除指定属性；成功移除时会以 0 作为当前值触发通知。
        /// </summary>
        public bool Remove(int attributeId)
        {
            if (!mValues.TryGetValue(attributeId, out var previousValue))
            {
                return false;
            }

            mValues.Remove(attributeId);
            NotifyChanged(attributeId, previousValue, 0);
            return true;
        }

        /// <summary>
        /// 清空全部属性；每个被清空的属性都会独立触发一次通知。
        /// </summary>
        public void Clear()
        {
            if (mValues.Count == 0)
            {
                return;
            }

            var previousValues = new List<KeyValuePair<int, int>>(mValues);
            mValues.Clear();

            foreach (var previousValue in previousValues)
            {
                NotifyChanged(previousValue.Key, previousValue.Value, 0);
            }
        }

        private void NotifyChanged(int attributeId, int previousValue, int currentValue)
        {
            var changedEvent = new CharacterAttributeChangedEvent(this, attributeId, previousValue, currentValue);
            Changed?.Invoke(changedEvent);
            EventKit.Type.Send(changedEvent);
        }
    }
}
