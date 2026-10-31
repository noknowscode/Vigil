namespace Vigil.Presentation;

public partial record SignOffSummary(
    string Patient,
    string Procedure,
    string Duration,
    int Readings,
    int Doses,
    int AlarmReadings,
    int OutOfRangeDoses,
    string Recovery,
    string Veterinarian,
    bool CanSign,
    string Blocker)
{
    public string Counts =>
        Loc.F("SignOff_Counts", "{0} · {1} out of range, confirmed · {2} with an alarm", Plural(Doses, "dose"), OutOfRangeDoses, Plural(AlarmReadings, "reading"));

    private static string Plural(int n, string noun) => noun == "dose"
        ? (n == 1 ? Loc.T("Count_OneDose", "1 dose") : Loc.F("Count_Doses", "{0} doses", n))
        : (n == 1 ? Loc.T("Count_OneReading", "1 reading") : Loc.F("Count_Readings", "{0} readings", n));

    public static SignOffSummary From(Case c)
    {
        var alarms = c.Readings.Count(r => Vitals.Alarms(c.Patient.Species, r).Count > 0);
        var blocker = c.Status switch
        {
            CaseStatus.Recovery => "",
            CaseStatus.Signed => Loc.F("SignOff_AlreadySigned", "Already signed by {0}.", c.SignedBy),
            _ => Loc.T("SignOff_EndFirst", "End anesthesia before signing."),
        };
        var r = c.Recovery;
        return new SignOffSummary(
            $"{c.Patient.Name} · {c.Patient.SpeciesAndBreed} · {c.Patient.WeightKg:0.0} kg · {c.Preop.AsaLabel}",
            c.Procedure,
            // Only an ended anesthesia has a length; a live one used CreatedAt as its end and read "00:-30".
            c.EndedAt is { } ended ? Schedule.HoursMinutes(Schedule.Elapsed(c, ended)) : "—",
            c.Readings.Count,
            c.Doses.Count,
            alarms,
            c.Doses.Count(d => d.OutOfRangeConfirmed),
            Loc.F("SignOff_Recovery", "Extubated {0} · sternal {1} · pain {2}/4", r.ExtubatedAt?.ToString("HH:mm") ?? "—", r.SternalAt?.ToString("HH:mm") ?? "—", r.PainScore?.ToString() ?? "—"),
            c.Veterinarian,
            c.Status == CaseStatus.Recovery,
            blocker);
    }
}

/// <summary>The veterinarian's sign-off: a typed name and an explicit confirmation on a shared device (no authentication).</summary>
public partial record SignOffModel(CaseRef Ref, ICaseStore Store, IClock Clock, INavigator Navigator)
{
    public IFeed<SignOffSummary> Summary => Feed<SignOffSummary>.Async(async ct =>
        await Store.GetAsync(Ref.Id, ct) is { } c ? Option.Some(SignOffSummary.From(c)) : Option.None<SignOffSummary>(), Store.Changed);

    public IState<string> Veterinarian => State.Async(this, async ct => (await Store.GetAsync(Ref.Id, ct))?.Veterinarian ?? "");

    public IState<bool> Confirmed => State.Value(this, () => false);

    public IState<string> Error => State.Value(this, () => "");

    public void Retry() => Store.Reload();

    public async ValueTask Sign(string veterinarian, bool confirmed, CancellationToken ct)
    {
        if (!confirmed || string.IsNullOrWhiteSpace(veterinarian))
        {
            await Error.UpdateAsync(_ => Loc.T("SignOff_Incomplete", "Enter the veterinarian's name and confirm the record is complete."), ct);
            return;
        }
        try
        {
            await Store.UpdateAsync(Ref.Id, c => c.Status == CaseStatus.Recovery
                ? c with { Status = CaseStatus.Signed, SignedBy = veterinarian.Trim(), SignedAt = Clock.Now }
                : throw new InvalidOperationException(Loc.T("SignOff_EndFirst", "End anesthesia before signing.")), ct);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or KeyNotFoundException)
        {
            await Error.UpdateAsync(_ => Loc.F("SignOff_NotSigned", "Not signed: {0}", ex.Message), ct);
            return;
        }
        await Navigator.NavigateBackAsync(this, cancellation: ct);
    }

    public async ValueTask Cancel(CancellationToken ct) => await Navigator.NavigateBackAsync(this, cancellation: ct);
}
