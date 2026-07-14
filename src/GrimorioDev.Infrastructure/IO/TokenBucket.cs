using System;
using System.Threading;
using System.Threading.Tasks;

namespace GrimorioDev.Infrastructure.IO;

public sealed class TokenBucket
{
    private readonly long _maxBytesPerSecond;
    private long _tokens;
    private DateTime _lastRefill = DateTime.UtcNow;

    public TokenBucket(long maxBytesPerSecond)
    {
        _maxBytesPerSecond = maxBytesPerSecond;
        _tokens = maxBytesPerSecond;
    }

    public async Task WaitAsync(long bytes, CancellationToken cancellationToken = default)
    {
        while (bytes > 0)
        {
            Refill();

            var available = Interlocked.Read(ref _tokens);
            if (available <= 0)
            {
                var delayMs = Math.Min(100, (int)(-available * 1000 / _maxBytesPerSecond) + 1);
                await Task.Delay(Math.Max(1, delayMs), cancellationToken).ConfigureAwait(false);
                continue;
            }

            var take = Math.Min(bytes, available);
            Interlocked.Add(ref _tokens, -take);
            bytes -= take;
        }
    }

    private void Refill()
    {
        var now = DateTime.UtcNow;
        var elapsed = (now - _lastRefill).TotalSeconds;
        if (elapsed <= 0) return;

        var added = (long)(elapsed * _maxBytesPerSecond);
        if (added > 0)
        {
            Interlocked.Add(ref _tokens, added);
            var max = _maxBytesPerSecond * 2;
            var current = Interlocked.Read(ref _tokens);
            if (current > max)
                Interlocked.Exchange(ref _tokens, max);
            _lastRefill = now;
        }
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _tokens, _maxBytesPerSecond);
        _lastRefill = DateTime.UtcNow;
    }
}
