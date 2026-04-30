using MicaAudio.Core.Presets;

namespace App.WinUI.Services.Panels;

// DOCS: docs/wiki/modules/paineis.md#compositor-hub75
internal sealed class PanelsMediaCache
{
    private const int MaxPosterEntries = 32;
    private const int MaxAnimatedEntries = 8;

    private readonly object gate = new();
    private readonly Dictionary<string, CacheEntry<RgbaColor[]>> posterEntries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CacheEntry<AnimatedMediaSequence>> animatedEntries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task<RgbaColor[]>> posterInflight = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task<AnimatedMediaSequence>> animatedInflight = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<string> posterLru = [];
    private readonly LinkedList<string> animatedLru = [];

    public Task<RgbaColor[]> GetOrCreatePosterAsync(string key, Func<CancellationToken, Task<RgbaColor[]>> factory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        lock (gate)
        {
            if (posterEntries.TryGetValue(key, out var cached))
            {
                Touch(posterLru, cached.Node);
                return Task.FromResult(cached.Value);
            }

            if (posterInflight.TryGetValue(key, out var inflight))
            {
                return inflight;
            }

            var task = CreatePosterCoreAsync(key, factory, cancellationToken);
            posterInflight[key] = task;
            return task;
        }
    }

    public Task<AnimatedMediaSequence> GetOrCreateAnimatedAsync(string key, Func<CancellationToken, Task<AnimatedMediaSequence>> factory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        lock (gate)
        {
            if (animatedEntries.TryGetValue(key, out var cached))
            {
                Touch(animatedLru, cached.Node);
                return Task.FromResult(cached.Value);
            }

            if (animatedInflight.TryGetValue(key, out var inflight))
            {
                return inflight;
            }

            var task = CreateAnimatedCoreAsync(key, factory, cancellationToken);
            animatedInflight[key] = task;
            return task;
        }
    }

    private async Task<RgbaColor[]> CreatePosterCoreAsync(string key, Func<CancellationToken, Task<RgbaColor[]>> factory, CancellationToken cancellationToken)
    {
        try
        {
            var value = await factory(cancellationToken).ConfigureAwait(false);
            var normalized = value.Length == 0 ? Array.Empty<RgbaColor>() : value;

            lock (gate)
            {
                var node = posterLru.AddFirst(key);
                posterEntries[key] = new CacheEntry<RgbaColor[]>(normalized, node);
                Trim(posterEntries, posterLru, MaxPosterEntries);
            }

            return normalized;
        }
        finally
        {
            lock (gate)
            {
                posterInflight.Remove(key);
            }
        }
    }

    private async Task<AnimatedMediaSequence> CreateAnimatedCoreAsync(string key, Func<CancellationToken, Task<AnimatedMediaSequence>> factory, CancellationToken cancellationToken)
    {
        try
        {
            var value = await factory(cancellationToken).ConfigureAwait(false);
            var normalized = value.IsEmpty ? AnimatedMediaSequence.Empty : value;

            lock (gate)
            {
                var node = animatedLru.AddFirst(key);
                animatedEntries[key] = new CacheEntry<AnimatedMediaSequence>(normalized, node);
                Trim(animatedEntries, animatedLru, MaxAnimatedEntries);
            }

            return normalized;
        }
        finally
        {
            lock (gate)
            {
                animatedInflight.Remove(key);
            }
        }
    }

    private static void Trim<T>(Dictionary<string, CacheEntry<T>> entries, LinkedList<string> lru, int maxEntries)
    {
        while (entries.Count > maxEntries && lru.Last is not null)
        {
            var tail = lru.Last;
            lru.RemoveLast();
            entries.Remove(tail.Value);
        }
    }

    private static void Touch(LinkedList<string> lru, LinkedListNode<string> node)
    {
        if (node.List is null || ReferenceEquals(lru.First, node))
        {
            return;
        }

        lru.Remove(node);
        lru.AddFirst(node);
    }

    private sealed record CacheEntry<T>(T Value, LinkedListNode<string> Node);
}

internal sealed record AnimatedMediaFrame(RgbaColor[] Pixels, int DurationMs);

internal sealed class AnimatedMediaSequence
{
    public static AnimatedMediaSequence Empty { get; } = new([], 0);

    public AnimatedMediaSequence(AnimatedMediaFrame[] frames, int totalDurationMs)
    {
        Frames = frames ?? [];
        TotalDurationMs = Math.Max(0, totalDurationMs);
    }

    public AnimatedMediaFrame[] Frames { get; }

    public int TotalDurationMs { get; }

    public int Count => Frames.Length;

    public bool IsEmpty => Count == 0;
}
