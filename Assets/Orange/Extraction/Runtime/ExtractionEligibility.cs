namespace Orange.Extraction
{
    public delegate bool ExtractionEligibility<TItem, TContext>(
        WeightedExtractionEntry<TItem, TContext> entry,
        TContext context);
}
