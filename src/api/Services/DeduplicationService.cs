using CommitApi.Models.Extraction;

namespace CommitApi.Services;

/// <summary>
/// Merges duplicate <see cref="RawCommitment"/> objects using fuzzy title matching
/// plus same-owner + close-in-time heuristics.
/// </summary>
public sealed class DeduplicationService : IDeduplicationService
{
    private readonly ILogger<DeduplicationService> _logger;

    // Jaccard similarity threshold: titles above this are considered the same task
    private const double SimilarityThreshold = 0.55;

    public DeduplicationService(ILogger<DeduplicationService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public IReadOnlyList<RawCommitment> Deduplicate(IEnumerable<RawCommitment> rawCommitments)
    {
        var items    = rawCommitments.ToList();
        var clusters = new List<List<RawCommitment>>();

        foreach (var item in items)
        {
            var matched = false;
            foreach (var cluster in clusters)
            {
                var representative = cluster[0];
                if (AreDuplicates(representative, item))
                {
                    cluster.Add(item);
                    matched = true;
                    break;
                }
            }

            if (!matched)
                clusters.Add([item]);
        }

        var merged = clusters.Select(Merge).ToList();
        _logger.LogInformation("Deduplication: {In} → {Out} commitments", items.Count, merged.Count);
        return merged;
    }

    private static bool AreDuplicates(RawCommitment a, RawCommitment b)
    {
        // Must be same owner
        if (!string.Equals(a.OwnerUserId, b.OwnerUserId, StringComparison.OrdinalIgnoreCase))
            return false;

        // Must come from within 3 days of each other
        if (Math.Abs((a.ExtractedAt - b.ExtractedAt).TotalDays) > 3)
            return false;

        // Title similarity (Jaccard on word tokens)
        return JaccardSimilarity(a.Title, b.Title) >= SimilarityThreshold;
    }

    private static RawCommitment Merge(List<RawCommitment> cluster)
    {
        if (cluster.Count == 1) return cluster[0];

        // Keep the highest-confidence item as the representative
        var best = cluster.MaxBy(c => c.Confidence)!;

        // Merge watcher lists
        var allWatchers = cluster
            .SelectMany(c => c.WatcherUserIds)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Keep earliest due date
        var earliestDue = cluster
            .Where(c => c.DueAt.HasValue)
            .Select(c => c.DueAt!.Value)
            .OrderBy(d => d)
            .FirstOrDefault();

        return best with
        {
            WatcherUserIds = allWatchers,
            DueAt          = earliestDue == default ? best.DueAt : earliestDue
        };
    }

    /// <summary>
    /// Jaccard similarity on word tokens (case-insensitive, stopwords removed).
    /// </summary>
    private static double JaccardSimilarity(string a, string b)
    {
        var tokensA = Tokenize(a);
        var tokensB = Tokenize(b);

        if (tokensA.Count == 0 && tokensB.Count == 0) return 1.0;
        if (tokensA.Count == 0 || tokensB.Count == 0) return 0.0;

        var intersection = tokensA.Intersect(tokensB, StringComparer.OrdinalIgnoreCase).Count();
        var union        = tokensA.Union(tokensB, StringComparer.OrdinalIgnoreCase).Count();
        return (double)intersection / union;
    }

    private static readonly HashSet<string> Stopwords =
    [
        "a", "an", "the", "and", "or", "but", "in", "on", "at", "to",
        "for", "of", "with", "by", "from", "as", "is", "it", "its", "be"
    ];

    private static List<string> Tokenize(string text)
    {
        return text
            .ToLowerInvariant()
            .Split([' ', '-', '_', '/', '.', ':', ';', ','], StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 2 && !Stopwords.Contains(t))
            .ToList();
    }
}
