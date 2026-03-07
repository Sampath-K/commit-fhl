namespace CommitApi.Models.Feedback;

/// <summary>
/// Per-user learned signal profile derived from feedback history.
/// Used to tune extraction thresholds and NLP prompts.
/// </summary>
public sealed record UserSignalProfile(
    /// <summary>
    /// Adjustment to add to the baseline MinConfidence (e.g. +0.1 if user rejects many items).
    /// Range: [-0.2, +0.2].
    /// </summary>
    double ConfidenceAdjustment,

    /// <summary>
    /// SHA-256 fingerprints of titles the user has marked as false positives.
    /// Items matching these fingerprints are suppressed on re-extraction.
    /// </summary>
    IReadOnlySet<string> SuppressedFingerprints,

    /// <summary>Example phrases for the NLP system prompt (negative examples from FP feedback).</summary>
    IReadOnlyList<string> NlpNegativeExamples,

    /// <summary>Example phrases for the NLP system prompt (positive examples from confirmed commitments).</summary>
    IReadOnlyList<string> NlpPositiveExamples
)
{
    /// <summary>Returns a default profile with no adjustments (used when there is no feedback history).</summary>
    public static UserSignalProfile Default { get; } = new(
        ConfidenceAdjustment:   0.0,
        SuppressedFingerprints: new HashSet<string>(),
        NlpNegativeExamples:    [],
        NlpPositiveExamples:    []);
}
