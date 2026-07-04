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

        /// <summary>发生变化的属性类型。</summary>
        public readonly CharacterAttributeType AttributeType;

        /// <summary>变化前的属性值。</summary>
        public readonly float PreviousValue;

        /// <summary>变化后的属性值。</summary>
        public readonly float CurrentValue;

        /// <summary>本次变化的差值。</summary>
        public float Delta => CurrentValue - PreviousValue;

        public CharacterAttributeChangedEvent(
            CharacterAttributeSet attributeSet,
            CharacterAttributeType attributeType,
            float previousValue,
            float currentValue)
        {
            AttributeSet = attributeSet;
            AttributeType = attributeType;
            PreviousValue = previousValue;
            CurrentValue = currentValue;
        }
    }

    /// <summary>
    /// 以枚举作为键、浮点数作为值的角色属性集合。
    /// </summary>
    public sealed class CharacterAttributeSet
    {
        private readonly Dictionary<CharacterAttributeType, float> mValues = new();

        /// <summary>
        /// 当前属性集合的实例级变化通知。
        /// </summary>
        public event Action<CharacterAttributeChangedEvent> Changed;

        /// <summary>
        /// 当前已记录的属性值。
        /// </summary>
        public IReadOnlyDictionary<CharacterAttributeType, float> Values => mValues;

        /// <summary>
        /// 当前已记录的属性数量。
        /// </summary>
        public int Count => mValues.Count;

        /// <summary>
        /// 判断指定属性是否已有显式记录。
        /// </summary>
        public bool Contains(CharacterAttributeType type)
        {
            return mValues.ContainsKey(type);
        }

        /// <summary>
        /// 读取指定属性值；未记录时返回给定默认值。
        /// </summary>
        public float Get(CharacterAttributeType type, float defaultValue = 0f)
        {
            return mValues.TryGetValue(type, out var value) ? value : defaultValue;
        }

        /// <summary>
        /// 设置指定属性值；数值实际变化时会触发通知。
        /// </summary>
        public void Set(CharacterAttributeType type, float value)
        {
            var previousValue = Get(type);
            if (previousValue.Equals(value))
            {
                return;
            }

            mValues[type] = value;
            NotifyChanged(type, previousValue, value);
        }

        /// <summary>
        /// 在当前属性值上叠加变化量，并返回变化后的值。
        /// </summary>
        public float Add(CharacterAttributeType type, float delta)
        {
            var value = Get(type) + delta;
            Set(type, value);
            return value;
        }

        /// <summary>
        /// 移除指定属性；成功移除时会以 0 作为当前值触发通知。
        /// </summary>
        public bool Remove(CharacterAttributeType type)
        {
            if (!mValues.TryGetValue(type, out var previousValue))
            {
                return false;
            }

            mValues.Remove(type);
            NotifyChanged(type, previousValue, 0f);
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

            var previousValues = new List<KeyValuePair<CharacterAttributeType, float>>(mValues);
            mValues.Clear();

            foreach (var previousValue in previousValues)
            {
                NotifyChanged(previousValue.Key, previousValue.Value, 0f);
            }
        }

        private void NotifyChanged(CharacterAttributeType type, float previousValue, float currentValue)
        {
            var changedEvent = new CharacterAttributeChangedEvent(this, type, previousValue, currentValue);
            Changed?.Invoke(changedEvent);
            EventKit.Type.Send(changedEvent);
        }
    }
}
