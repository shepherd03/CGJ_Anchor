using Anchor.Character.Attributes;

namespace Anchor.Character
{
    public sealed class GamePlayer
    {
        public CharacterAttributeSet Attributes { get; } = new();
    }
}
