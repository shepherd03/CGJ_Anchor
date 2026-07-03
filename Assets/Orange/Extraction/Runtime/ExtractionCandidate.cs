namespace Orange.Extraction
{
    public sealed class ExtractionCandidate<TItem>
    {
        internal ExtractionCandidate(
            int entryIndex,
            string entryId,
            TItem item,
            float baseWeight,
            float finalWeight,
            ExtractionCandidateStatus status)
        {
            EntryIndex = entryIndex;
            EntryId = entryId;
            Item = item;
            BaseWeight = baseWeight;
            FinalWeight = finalWeight;
            Status = status;
        }

        public int EntryIndex { get; }
        public string EntryId { get; }
        public TItem Item { get; }
        public float BaseWeight { get; }
        public float FinalWeight { get; }
        public ExtractionCandidateStatus Status { get; }
        public bool IsDrawable => Status == ExtractionCandidateStatus.Drawable;
    }
}
