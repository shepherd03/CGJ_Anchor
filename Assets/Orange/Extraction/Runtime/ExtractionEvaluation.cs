using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Orange.Extraction
{
    public sealed class ExtractionEvaluation<TItem>
    {
        internal ExtractionEvaluation(IList<ExtractionCandidate<TItem>> candidates, double totalWeight)
        {
            Candidates = new ReadOnlyCollection<ExtractionCandidate<TItem>>(candidates ?? throw new ArgumentNullException(nameof(candidates)));
            TotalWeight = totalWeight;
        }

        public IReadOnlyList<ExtractionCandidate<TItem>> Candidates { get; }
        public double TotalWeight { get; }
        public bool HasDrawableCandidates => TotalWeight > 0d;
    }
}
