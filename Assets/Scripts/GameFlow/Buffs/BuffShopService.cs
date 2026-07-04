using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Anchor.Character.Attributes;
using Orange.Extraction;

using BuffRow = Anchor.Config.game.buff;

namespace Anchor.GameFlow.Buffs
{
    public sealed class BuffShopService
    {
        public const int DefaultCostAttributeId = CharacterAttributeIds.Coins;

        private readonly List<BuffRow> mBuffRows = new();
        private readonly List<BuffRow> mCurrentOffers = new();
        private readonly ReadOnlyCollection<BuffRow> mReadOnlyCurrentOffers;
        private readonly int mCostAttributeId;

        public BuffShopService(IEnumerable<BuffRow> buffRows, int costAttributeId = DefaultCostAttributeId)
        {
            if (buffRows != null)
            {
                foreach (var row in buffRows)
                {
                    if (row != null)
                    {
                        mBuffRows.Add(row);
                    }
                }
            }

            mCostAttributeId = costAttributeId;
            mReadOnlyCurrentOffers = mCurrentOffers.AsReadOnly();
        }

        public int CostAttributeId => mCostAttributeId;
        public IReadOnlyList<BuffRow> CurrentOffers => mReadOnlyCurrentOffers;

        public IReadOnlyList<BuffRow> RefreshOffers(GameFlowBlackboard blackboard, int count)
        {
            if (blackboard == null)
            {
                throw new ArgumentNullException(nameof(blackboard));
            }

            mCurrentOffers.Clear();
            count = Math.Max(0, count);

            if (count == 0 || mBuffRows.Count == 0)
            {
                return CurrentOffers;
            }

            var pool = new WeightedExtractionPool<BuffRow, GameFlowBlackboard>();
            foreach (var row in mBuffRows)
            {
                if (row.Id <= 0 || row.Weight < 0)
                {
                    continue;
                }

                pool.AddEntry(
                    row.Id.ToString(),
                    row,
                    row.Weight,
                    IsDrawable);
            }

            foreach (var result in pool.DrawManyUnique(blackboard, count))
            {
                mCurrentOffers.Add(result.Item);
            }

            return CurrentOffers;
        }

        public bool TryPurchaseOfferedBuff(
            GameFlowBlackboard blackboard,
            int buffId,
            out BuffPurchaseResult result)
        {
            if (blackboard == null)
            {
                throw new ArgumentNullException(nameof(blackboard));
            }

            var buff = FindCurrentOffer(buffId);
            if (buff == null)
            {
                result = BuffPurchaseResult.Fail(
                    BuffPurchaseStatus.NotOffered,
                    buffId,
                    mCostAttributeId,
                    0,
                    $"Buff is not in current shop offers: {buffId}.");
                return false;
            }

            return TryPurchaseBuff(blackboard, buff, out result);
        }

        public bool CanPurchaseOfferedBuff(GameFlowBlackboard blackboard, int buffId)
        {
            if (blackboard == null)
            {
                return false;
            }

            var buff = FindCurrentOffer(buffId);
            return buff != null && CanPurchase(blackboard, buff);
        }

        private bool TryPurchaseBuff(
            GameFlowBlackboard blackboard,
            BuffRow buff,
            out BuffPurchaseResult result)
        {
            if (buff == null || buff.Id <= 0 || buff.Cost < 0)
            {
                result = BuffPurchaseResult.Fail(
                    BuffPurchaseStatus.InvalidBuff,
                    buff != null ? buff.Id : 0,
                    mCostAttributeId,
                    buff != null ? buff.Cost : 0,
                    "Buff row is invalid.",
                    buff);
                return false;
            }

            if (blackboard.HasActiveBuff(buff.Id))
            {
                result = BuffPurchaseResult.Fail(
                    BuffPurchaseStatus.AlreadyActive,
                    buff.Id,
                    mCostAttributeId,
                    buff.Cost,
                    $"Buff is already active: {buff.Id}.",
                    buff);
                return false;
            }

            if (!CanPay(blackboard, buff))
            {
                result = BuffPurchaseResult.Fail(
                    BuffPurchaseStatus.CannotAfford,
                    buff.Id,
                    mCostAttributeId,
                    buff.Cost,
                    $"Not enough attribute {mCostAttributeId} to purchase buff {buff.Id}.",
                    buff);
                return false;
            }

            if (!TryValidateEffects(blackboard, buff, out var invalidEffectsMessage))
            {
                result = BuffPurchaseResult.Fail(
                    BuffPurchaseStatus.InvalidEffects,
                    buff.Id,
                    mCostAttributeId,
                    buff.Cost,
                    invalidEffectsMessage,
                    buff);
                return false;
            }

            blackboard.PlayerAttributes.Add(mCostAttributeId, -buff.Cost);
            ApplyEffects(blackboard, buff);
            blackboard.AddActiveBuff(buff.Id);
            mCurrentOffers.Remove(buff);

            result = BuffPurchaseResult.Success(buff, mCostAttributeId);
            return true;
        }

        private bool CanPurchase(GameFlowBlackboard blackboard, BuffRow buff)
        {
            return buff != null
                && buff.Id > 0
                && buff.Weight > 0
                && !blackboard.HasActiveBuff(buff.Id)
                && CanPay(blackboard, buff)
                && TryValidateEffects(blackboard, buff, out _);
        }

        private bool CanPay(GameFlowBlackboard blackboard, BuffRow buff)
        {
            return buff.Cost >= 0 && blackboard.PlayerAttributes.Get(mCostAttributeId) >= buff.Cost;
        }

        private bool IsDrawable(WeightedExtractionEntry<BuffRow, GameFlowBlackboard> entry, GameFlowBlackboard blackboard)
        {
            return CanPurchase(blackboard, entry.Item);
        }

        private BuffRow FindCurrentOffer(int buffId)
        {
            for (var i = 0; i < mCurrentOffers.Count; i++)
            {
                var buff = mCurrentOffers[i];
                if (buff != null && buff.Id == buffId)
                {
                    return buff;
                }
            }

            return null;
        }

        private static void ApplyEffects(GameFlowBlackboard blackboard, BuffRow buff)
        {
            for (var i = 0; i < buff.Effects.Length; i++)
            {
                var pair = buff.Effects[i];
                blackboard.PlayerAttributes.Add(pair[0], pair[1]);
            }
        }

        private static bool TryValidateEffects(
            GameFlowBlackboard blackboard,
            BuffRow buff,
            out string message)
        {
            if (buff.Effects == null)
            {
                message = $"Buff {buff.Id} effects cannot be null.";
                return false;
            }

            for (var i = 0; i < buff.Effects.Length; i++)
            {
                var pair = buff.Effects[i];
                if (pair == null || pair.Length != 2)
                {
                    message = $"Buff {buff.Id} effect item {i} must be [attributeId, value].";
                    return false;
                }

                if (!blackboard.CanWriteAttribute(pair[0]))
                {
                    message = $"Buff {buff.Id} effect item {i} uses unknown writable attribute id: {pair[0]}.";
                    return false;
                }
            }

            message = string.Empty;
            return true;
        }
    }
}
