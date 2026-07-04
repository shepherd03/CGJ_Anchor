using System;
using System.Collections.Generic;

using AttributeRow = Anchor.Config.game.playerAttribute;

namespace Anchor.Character.Attributes
{
    /// <summary>
    /// 玩家属性配置表运行时索引。属性身份只使用配置表 ID，其他表和运行时代码都通过 ID 读写属性。
    /// </summary>
    public sealed class CharacterAttributeCatalog
    {
        private readonly Dictionary<int, AttributeRow> mRowsById = new();

        public CharacterAttributeCatalog(IEnumerable<AttributeRow> rows)
        {
            if (rows == null)
            {
                throw new ArgumentNullException(nameof(rows));
            }

            foreach (var row in rows)
            {
                if (row == null)
                {
                    continue;
                }

                if (row.Id <= 0)
                {
                    throw new InvalidOperationException($"Player attribute has invalid id: {row.Id}");
                }

                if (!mRowsById.TryAdd(row.Id, row))
                {
                    throw new InvalidOperationException($"Duplicate player attribute id: {row.Id}");
                }
            }
        }

        public IReadOnlyDictionary<int, AttributeRow> RowsById => mRowsById;

        public bool Contains(int attributeId)
        {
            return mRowsById.ContainsKey(attributeId);
        }

        public AttributeRow GetRequiredRow(int attributeId)
        {
            if (!mRowsById.TryGetValue(attributeId, out var row))
            {
                throw new KeyNotFoundException($"Player attribute id not found in config: {attributeId}");
            }

            return row;
        }

        public string GetDisplayName(int attributeId)
        {
            return mRowsById.TryGetValue(attributeId, out var row) ? row.DisplayName : attributeId.ToString();
        }

        public int GetDefaultValue(int attributeId)
        {
            return mRowsById.TryGetValue(attributeId, out var row) ? row.DefaultValue : 0;
        }

    }
}
