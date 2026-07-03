namespace Orange.Extraction
{
    /// <summary>
    /// Provides normalized random values so extraction rolls can be replayed in tests or with a fixed seed.
    /// </summary>
    public interface IExtractionRandom
    {
        float NextNormalizedValue();
    }
}
