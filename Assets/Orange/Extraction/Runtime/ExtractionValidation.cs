using System;

namespace Orange.Extraction
{
    internal static class ExtractionValidation
    {
        public static void ThrowIfInvalidBaseWeight(float weight, string entryId)
        {
            if (float.IsNaN(weight) || float.IsInfinity(weight))
            {
                throw new ArgumentException($"Extraction entry '{entryId}' has a non-finite base weight: {weight}.");
            }

            if (weight < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(weight), weight, $"Extraction entry '{entryId}' base weight cannot be negative.");
            }
        }

        public static void ThrowIfInvalidFinalWeight(float weight, string entryId)
        {
            if (float.IsNaN(weight) || float.IsInfinity(weight))
            {
                throw new InvalidOperationException($"Extraction entry '{entryId}' produced a non-finite final weight: {weight}.");
            }
        }

        public static void ThrowIfInvalidRandomValue(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f || value >= 1f)
            {
                throw new InvalidOperationException($"Extraction random source returned {value}. Expected a finite value in [0, 1).");
            }
        }
    }
}
