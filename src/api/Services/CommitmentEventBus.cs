using System.Collections.Concurrent;
using System.Threading.Channels;

namespace CommitApi.Services;

/// <summary>
/// In-memory pub/sub bus that notifies connected SSE clients when new commitments
/// are stored for a user.
///
/// Each user gets a bounded Channel. ExtractionOrchestrator calls Publish() after
/// a successful extraction run. The SSE endpoint subscribes and streams events to
/// the Teams tab — which then re-fetches /commitments (the data never flows through
/// the event stream itself).
///
/// On reconnect Subscribe() replaces the old channel so stale events are dropped.
/// Thread-safe: ConcurrentDictionary + channel primitives.
/// </summary>
public sealed class CommitmentEventBus
{
    private readonly ConcurrentDictionary<string, Channel<int>> _channels =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Subscribes the caller to events for <paramref name="userId"/>.
    /// Any existing subscription for that user is closed first (handles tab reconnects).
    /// </summary>
    public ChannelReader<int> Subscribe(string userId)
    {
        var channel = Channel.CreateBounded<int>(
            new BoundedChannelOptions(20) { FullMode = BoundedChannelFullMode.DropOldest });

        _channels.AddOrUpdate(userId, channel, (_, prev) =>
        {
            prev.Writer.TryComplete(); // close old connection
            return channel;
        });

        return channel.Reader;
    }

    /// <summary>Removes the subscription for <paramref name="userId"/> on disconnect.</summary>
    public void Unsubscribe(string userId)
    {
        if (_channels.TryRemove(userId, out var ch))
            ch.Writer.TryComplete();
    }

    /// <summary>
    /// Notifies a connected tab that <paramref name="upserted"/> new items were stored.
    /// No-op if the user has no active SSE connection.
    /// </summary>
    public void Publish(string userId, int upserted)
    {
        if (upserted <= 0) return;
        if (_channels.TryGetValue(userId, out var channel))
            channel.Writer.TryWrite(upserted);
    }
}
