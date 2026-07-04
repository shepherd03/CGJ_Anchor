using BuffRow = Anchor.Config.game.buff;

namespace Anchor.GameFlow.Buffs
{
    public enum BuffPurchaseStatus
    {
        None = 0,
        Success = 1,
        NotInBudgetShop = 2,
        NotOffered = 3,
        AlreadyActive = 4,
        CannotAfford = 5,
        InvalidBuff = 6,
        InvalidEffects = 7
    }

    public readonly struct BuffPurchaseResult
    {
        public BuffPurchaseResult(
            BuffPurchaseStatus status,
            BuffRow buff,
            int buffId,
            int costAttributeId,
            int cost,
            string message)
        {
            Status = status;
            Buff = buff;
            BuffId = buffId;
            CostAttributeId = costAttributeId;
            Cost = cost;
            Message = message;
        }

        public BuffPurchaseStatus Status { get; }
        public BuffRow Buff { get; }
        public int BuffId { get; }
        public int CostAttributeId { get; }
        public int Cost { get; }
        public string Message { get; }
        public bool Succeeded => Status == BuffPurchaseStatus.Success;

        public static BuffPurchaseResult Success(BuffRow buff, int costAttributeId)
        {
            return new BuffPurchaseResult(
                BuffPurchaseStatus.Success,
                buff,
                buff != null ? buff.Id : 0,
                costAttributeId,
                buff != null ? buff.Cost : 0,
                string.Empty);
        }

        public static BuffPurchaseResult Fail(
            BuffPurchaseStatus status,
            int buffId,
            int costAttributeId,
            int cost,
            string message,
            BuffRow buff = null)
        {
            return new BuffPurchaseResult(status, buff, buffId, costAttributeId, cost, message);
        }
    }
}
