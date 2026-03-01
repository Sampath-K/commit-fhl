namespace CommitApi.Models.Graph;

/// <summary>
/// Represents a directed dependency edge between two commitments.
/// FromId blocks ToId (i.e., ToId cannot finish until FromId is done).
/// </summary>
/// <param name="FromId">Commitment that blocks the other (upstream).</param>
/// <param name="ToId">Commitment that is blocked (downstream).</param>
/// <param name="EdgeType">Signal that produced this link: "thread" | "people" | "title".</param>
/// <param name="Confidence">0–1 confidence in the dependency signal.</param>
public record GraphEdge(
    string FromId,
    string ToId,
    string EdgeType,
    double Confidence);
