using System.Text.Json;
using CommitApi.Config;
using CommitApi.Entities;
using CommitApi.Models.Graph;
using CommitApi.Repositories;

namespace CommitApi.Graph;

/// <summary>
/// Links commitments into a dependency graph using three heuristic signals:
///   1. Thread signal  — same SourceId (meeting / chat / email thread)
///   2. People signal  — overlapping watcher sets (≥ 1 shared person)
///   3. Title signal   — Jaccard title similarity ≥ 0.7
///
/// When a dependency is detected, the upstream task's BlocksJson and the downstream
/// task's BlockedByJson are updated in storage.  Direction = earlier CommittedAt blocks later.
/// </summary>
public class DependencyLinker : IDependencyLinker
{
    private const double TitleSimilarityThreshold = 0.70;
    private const double ThreadConfidence           = 0.90;
    private const double PeopleBaseConfidence       = 0.60;
    private const double PeopleBonus                = 0.30;

    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "and", "or", "but", "for", "of", "in", "on", "to",
        "by", "is", "it", "at", "as", "be", "do", "if", "we", "up", "so"
    };

    private readonly ICommitmentRepository _repo;
    private readonly ILogger<DependencyLinker> _log;

    public DependencyLinker(ICommitmentRepository repo, ILogger<DependencyLinker> log)
    {
        _repo = repo;
        _log  = log;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GraphEdge>> BuildGraphAsync(
        string userId, string bearerToken, CancellationToken ct = default)
    {
        var entities = await _repo.ListByOwnerAsync(userId, ct: ct);
        if (entities.Count < 2)
            return Array.Empty<GraphEdge>();

        var edges = new List<GraphEdge>();
        var items = entities.OrderBy(e => e.CommittedAt).ToList();

        for (var i = 0; i < items.Count; i++)
        {
            for (var j = i + 1; j < items.Count; j++)
            {
                var earlier = items[i];
                var later   = items[j];

                var edge = DetectEdge(earlier, later);
                if (edge is null)
                    continue;

                edges.Add(edge);
                await UpdateDependencyAsync(earlier, later, ct);
                _log.LogDebug("Edge detected: {From} → {To} ({Type}, conf={Conf:F2})",
                    edge.FromId, edge.ToId, edge.EdgeType, edge.Confidence);
            }
        }

        _log.LogInformation("DependencyLinker built {Count} edges for user (hashed={Hash})",
            edges.Count, PiiScrubber.HashValue(userId));

        return edges;
    }

    // ── Signal detection ──────────────────────────────────────────────────────

    private static GraphEdge? DetectEdge(CommitmentEntity from, CommitmentEntity to)
    {
        // Signal 1: same conversation thread (strongest signal)
        if (!string.IsNullOrEmpty(from.SourceId) &&
            !string.IsNullOrEmpty(to.SourceId) &&
            from.SourceId == to.SourceId)
        {
            return new GraphEdge(from.RowKey, to.RowKey, "thread", ThreadConfidence);
        }

        // Signal 2: overlapping watcher sets
        var fromWatchers = Deserialize(from.WatchersJson);
        var toWatchers   = Deserialize(to.WatchersJson);
        var intersection = fromWatchers.Intersect(toWatchers, StringComparer.OrdinalIgnoreCase).Count();
        if (intersection >= 1 && (fromWatchers.Length > 0 || toWatchers.Length > 0))
        {
            var minSize     = Math.Max(1, Math.Min(fromWatchers.Length, toWatchers.Length));
            var confidence  = PeopleBaseConfidence + PeopleBonus * ((double)intersection / minSize);
            return new GraphEdge(from.RowKey, to.RowKey, "people", confidence);
        }

        // Signal 3: NLP title similarity (Jaccard ≥ threshold)
        var similarity = JaccardSimilarity(from.Title, to.Title);
        if (similarity >= TitleSimilarityThreshold)
        {
            return new GraphEdge(from.RowKey, to.RowKey, "title", similarity);
        }

        return null;
    }

    // ── Storage update ────────────────────────────────────────────────────────

    private async Task UpdateDependencyAsync(
        CommitmentEntity upstream, CommitmentEntity downstream, CancellationToken ct)
    {
        // upstream.BlocksJson adds downstream.RowKey
        var upstreamBlocks = Deserialize(upstream.BlocksJson).ToHashSet();
        if (upstreamBlocks.Add(downstream.RowKey))
        {
            upstream.BlocksJson = JsonSerializer.Serialize(upstreamBlocks.ToArray());
            await _repo.UpsertAsync(upstream, ct);
        }

        // downstream.BlockedByJson adds upstream.RowKey
        var downstreamBlockedBy = Deserialize(downstream.BlockedByJson).ToHashSet();
        if (downstreamBlockedBy.Add(upstream.RowKey))
        {
            downstream.BlockedByJson = JsonSerializer.Serialize(downstreamBlockedBy.ToArray());
            await _repo.UpsertAsync(downstream, ct);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string[] Deserialize(string json)
    {
        try { return JsonSerializer.Deserialize<string[]>(json) ?? []; }
        catch { return []; }
    }

    public static double JaccardSimilarity(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            return 0.0;

        var tokensA = Tokenize(a);
        var tokensB = Tokenize(b);
        if (tokensA.Count == 0 || tokensB.Count == 0)
            return 0.0;

        var intersection = tokensA.Intersect(tokensB, StringComparer.OrdinalIgnoreCase).Count();
        var union        = tokensA.Union(tokensB, StringComparer.OrdinalIgnoreCase).Count();
        return union == 0 ? 0.0 : (double)intersection / union;
    }

    private static HashSet<string> Tokenize(string text) =>
        text.ToLowerInvariant()
            .Split([' ', '\t', '\r', '\n', '.', ',', '!', '?', ';', ':'], StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 1 && !Stopwords.Contains(t))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
