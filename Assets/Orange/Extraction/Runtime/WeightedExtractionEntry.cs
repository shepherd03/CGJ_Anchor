using System;
using System.Collections.Generic;

namespace Orange.Extraction
{
    public class WeightedExtractionEntry<TItem, TContext>
    {
        public WeightedExtractionEntry(
            string entryId,
            TItem item,
            float baseWeight,
            ExtractionEligibility<TItem, TContext> eligibility = null,
            IExtractionWeightModifier<TItem, TContext> weightModifier = null)
        {
            if (string.IsNullOrWhiteSpace(entryId))
            {
                throw new ArgumentException("Extraction entry id cannot be null, empty, or whitespace.", nameof(entryId));
            }

            if (item is null)
            {
                throw new ArgumentNullException(nameof(item), $"Extraction entry '{entryId}' cannot hold a null item.");
            }

            ExtractionValidation.ThrowIfInvalidBaseWeight(baseWeight, entryId);

            EntryId = entryId;
            Item = item;
            BaseWeight = baseWeight;
            Eligibility = eligibility;
            WeightModifiers = new LinkedList<IExtractionWeightModifier<TItem, TContext>>();

            if (weightModifier != null)
            {
                WeightModifiers.AddLast(weightModifier);
            }
        }

        public string EntryId { get; }
        public TItem Item { get; }
        public float BaseWeight { get; }
        public ExtractionEligibility<TItem, TContext> Eligibility { get; }
        /// <summary>
        /// The current staged weight used while modifiers are being evaluated.
        /// </summary>
        public float CurrentWeight { get; set; }
        public LinkedList<IExtractionWeightModifier<TItem, TContext>> WeightModifiers { get; }

        public void AddWeightModifier(IExtractionWeightModifier<TItem, TContext> weightModifier)
        {
            WeightModifiers.AddLast(weightModifier);
        }

        public void AddWeightModifiers(IEnumerable<IExtractionWeightModifier<TItem, TContext>> weightModifiers)
        {
            if (weightModifiers == null)
            {
                throw new ArgumentNullException(nameof(weightModifiers));
            }

            if (ReferenceEquals(weightModifiers, WeightModifiers))
            {
                weightModifiers = new List<IExtractionWeightModifier<TItem, TContext>>(WeightModifiers);
            }

            foreach (IExtractionWeightModifier<TItem, TContext> weightModifier in weightModifiers)
            {
                WeightModifiers.AddLast(weightModifier);
            }
        }

        public bool IsEligible(TContext context)
        {
            return Eligibility == null || Eligibility(this, context);
        }
    }

    public sealed class WeightedExtractionEntry<TItem> : WeightedExtractionEntry<TItem, EmptyExtractionContext>
    {
        public WeightedExtractionEntry(
            string entryId,
            TItem item,
            float baseWeight,
            ExtractionEligibility<TItem, EmptyExtractionContext> eligibility = null,
            IExtractionWeightModifier<TItem, EmptyExtractionContext> weightModifier = null)
            : base(entryId, item, baseWeight, eligibility, weightModifier)
        {
        }
    }
}
