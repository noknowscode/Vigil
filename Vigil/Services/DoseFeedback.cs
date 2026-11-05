namespace Vigil.Services;

/// <summary>Session-only acknowledgement of a successfully persisted dose. Never a patient-state signal.</summary>
public sealed class DoseFeedback
{
    private Guid _caseId;
    private long _started;
    public void Recorded(Guid caseId) { _caseId = caseId; _started = Environment.TickCount64; }
    public long PulseFor(Guid caseId) => _caseId == caseId && Environment.TickCount64 - _started < 10_000 ? _started : 0;
}
