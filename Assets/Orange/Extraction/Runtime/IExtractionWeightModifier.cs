namespace Orange.Extraction
{
    /// <summary>
    /// Returns the adjusted weight for one entry under a business-defined context.
    /// </summary>
    public interface IExtractionWeightModifier<TItem, TContext>
    {
        /// <param name="entry">
        /// The source extraction entry.
        /// <see cref="WeightedExtractionEntry{TItem, TContext}.BaseWeight"/> remains the original base weight.
        /// <see cref="WeightedExtractionEntry{TItem, TContext}.CurrentWeight"/> is the current staged weight before this modifier is applied.
        /// </param>
        /// <param name="context">The business context used by the modifier.</param>
        float ModifyWeight(WeightedExtractionEntry<TItem, TContext> entry, TContext context);
    }
}
