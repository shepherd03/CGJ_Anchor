using System;

namespace Anchor.GameFlow
{
    public enum WishlistModifierKind
    {
        Flat = 0,
        Multiplier = 1
    }

    public readonly struct WishlistModifier
    {
        public readonly string SourceName;
        public readonly WishlistModifierKind Kind;
        public readonly int Value;
        public readonly int SortOrder;

        public WishlistModifier(string sourceName, WishlistModifierKind kind, int value, int sortOrder)
        {
            SourceName = string.IsNullOrWhiteSpace(sourceName) ? "未知来源" : sourceName;
            Kind = kind;
            Value = value;
            SortOrder = sortOrder;
        }
    }

    public readonly struct WishlistModifierResult
    {
        public readonly string SourceName;
        public readonly WishlistModifierKind Kind;
        public readonly int Value;
        public readonly int BeforeValue;
        public readonly int AfterValue;
        public readonly int BeforeWishlistCount;
        public readonly int AfterWishlistCount;
        public readonly int Delta;

        public WishlistModifierResult(
            string sourceName,
            WishlistModifierKind kind,
            int value,
            int beforeValue,
            int afterValue,
            int beforeWishlistCount,
            int afterWishlistCount)
        {
            SourceName = string.IsNullOrWhiteSpace(sourceName) ? "未知来源" : sourceName;
            Kind = kind;
            Value = value;
            BeforeValue = beforeValue;
            AfterValue = afterValue;
            BeforeWishlistCount = beforeWishlistCount;
            AfterWishlistCount = afterWishlistCount;
            Delta = afterValue - beforeValue;
        }

        public bool IsMultiplier => Kind == WishlistModifierKind.Multiplier;
        public bool IsFlat => Kind == WishlistModifierKind.Flat;

        public string DisplayValue => Kind == WishlistModifierKind.Multiplier
            ? $"{Value:+0;-0;0}%"
            : $"{Value:+0;-0;0}";

        public string DeltaText => $"{Delta:+0;-0;0}";
    }
}
