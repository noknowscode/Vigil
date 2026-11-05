using System.Runtime.CompilerServices;

namespace Vigil.Services;

public interface IClock
{
    DateTimeOffset Now { get; }

    /// <summary>The current time now, then once per second.</summary>
    IAsyncEnumerable<DateTimeOffset> Ticks(CancellationToken ct);
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;

    public async IAsyncEnumerable<DateTimeOffset> Ticks([EnumeratorCancellation] CancellationToken ct)
    {
        yield return Now;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(ct))
        {
            yield return Now;
        }
    }
}

/// <summary>
/// The system clock moved by a fixed offset, so a verification run reads a chosen wall time (the prototype's 12:42:26)
/// and still ticks. Debug hook only (<c>--vigil-now=HH:mm:ss</c>, implied by <c>--clover-preview</c>).
/// </summary>
public sealed class ShiftedClock(TimeSpan offset) : IClock
{
    private readonly SystemClock _system = new();

    public static ShiftedClock At(TimeSpan timeOfDay)
    {
        var now = DateTimeOffset.Now;
        return new ShiftedClock(new DateTimeOffset(now.Date, now.Offset) + timeOfDay - now);
    }

    public DateTimeOffset Now => _system.Now + offset;

    public async IAsyncEnumerable<DateTimeOffset> Ticks([EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var t in _system.Ticks(ct))
        {
            yield return t + offset;
        }
    }
}
