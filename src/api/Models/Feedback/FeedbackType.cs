namespace CommitApi.Models.Feedback;

/// <summary>Type of feedback a user provides on a commitment.</summary>
public enum FeedbackType
{
    /// <summary>The commitment was not a real commitment (false positive).</summary>
    FalsePositive,

    /// <summary>The commitment is confirmed correct.</summary>
    Confirm,

    /// <summary>The commitment was assigned to the wrong person.</summary>
    WrongOwner,

    /// <summary>This is a duplicate of another commitment.</summary>
    Duplicate,
}
