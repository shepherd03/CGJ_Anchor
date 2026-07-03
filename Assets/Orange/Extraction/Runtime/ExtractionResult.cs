using System;

namespace Orange.Extraction
{
    public sealed class ExtractionResult<TItem>
    {
        internal ExtractionResult(
            ExtractionCandidate<TItem> selectedCandidate,
            double totalWeight,
            double rollValue,
            ExtractionEvaluation<TItem> evaluation)
        {
            if (selectedCandidate == null)
            {
                throw new ArgumentNullException(nameof(selectedCandidate));
            }

            EntryIndex = selectedCandidate.EntryIndex;
            EntryId = selectedCandidate.EntryId;
            Item = selectedCandidate.Item;
            BaseWeight = selectedCandidate.BaseWeight;
            FinalWeight = selectedCandidate.FinalWeight;
            TotalWeight = totalWeight;
            RollValue = rollValue;
            Evaluation = evaluation ?? throw new ArgumentNullException(nameof(evaluation));
        }

        public int EntryIndex { get; }
        public string EntryId { get; }
        public TItem Item { get; }
        public float BaseWeight { get; }
        public float FinalWeight { get; }
        public double TotalWeight { get; }
        public double RollValue { get; }
        public ExtractionEvaluation<TItem> Evaluation { get; }
    }
}
