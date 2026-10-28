namespace Vigil.Services;

/// <summary>
/// Test hook that proves Loading and Error states at runtime. Reads <c>VIGIL_FAULT</c> / <c>VIGIL_SLOW_MS</c>
/// from the environment or <c>--vigil-fault=</c> / <c>--vigil-slow-ms=</c> from the command line (the App MCP
/// launcher passes args, not env vars). Inert when unset; not a user feature.
/// </summary>
public interface IFaultInjection
{
    int SlowMs { get; }

    /// <summary>True once for <c>store-read-once</c>, for the first N reads with <c>store-read-xN</c>, always for <c>store-read</c>.</summary>
    bool ConsumeReadFault();

    bool WriteFault { get; }

    /// <summary>
    /// Single-case reads (the record page): true for the first N calls with <c>record-read-xN</c>. The record page
    /// reads its case several times as it opens (feed and edit states), so N covers all of them; Retry reads again.
    /// </summary>
    bool ConsumeRecordFault() => false;

    /// <summary>Delay before a single-case read with <c>record-slow</c> (4 s), to show the record skeleton.</summary>
    int RecordDelayMs => 0;
}

public sealed class FaultInjection : IFaultInjection
{
    private readonly string _fault;
    private int _readFaults;

    public FaultInjection(string? fault, int slowMs)
    {
        _fault = fault ?? "";
        SlowMs = slowMs;
    }

    public static FaultInjection FromEnvironment()
    {
        string? Arg(string name) => Environment.GetCommandLineArgs()
            .FirstOrDefault(a => a.StartsWith($"--{name}=", StringComparison.OrdinalIgnoreCase))?.Split('=', 2)[1];

        var fault = Environment.GetEnvironmentVariable("VIGIL_FAULT") ?? Arg("vigil-fault");
        var slow = Environment.GetEnvironmentVariable("VIGIL_SLOW_MS") ?? Arg("vigil-slow-ms");
        return new FaultInjection(fault, int.TryParse(slow, out var ms) ? ms : 0);
    }

    public int SlowMs { get; }

    public bool WriteFault => _fault.Contains("store-write", StringComparison.OrdinalIgnoreCase);

    private int _recordFaults;

    public bool ConsumeRecordFault()
    {
        var times = System.Text.RegularExpressions.Regex.Match(_fault, @"record-read-x(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return times.Success && Interlocked.Increment(ref _recordFaults) <= int.Parse(times.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    public int RecordDelayMs => _fault.Contains("record-slow", StringComparison.OrdinalIgnoreCase) ? 4000 : 0;

    public bool ConsumeReadFault()
    {
        if (_fault.Contains("store-read-once", StringComparison.OrdinalIgnoreCase))
        {
            return Interlocked.Increment(ref _readFaults) == 1;
        }
        // store-read-xN: the shell and the page each read the store at launch, so a single failure only reaches one of them.
        var times = System.Text.RegularExpressions.Regex.Match(_fault, @"store-read-x(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (times.Success)
        {
            return Interlocked.Increment(ref _readFaults) <= int.Parse(times.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }
        return _fault.Equals("store-read", StringComparison.OrdinalIgnoreCase);
    }
}
