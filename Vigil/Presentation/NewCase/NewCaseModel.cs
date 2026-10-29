using System.Globalization;

namespace Vigil.Presentation;

/// <summary>
/// The new-case form. Numbers and times stay text while typing so partial input ("18.", "14:") is never rejected.
/// The scheduling fields are optional: a walk-in case has no room, slot or fasting time.
/// </summary>
public partial record CaseDraft(
    string Name,
    int SpeciesIndex,
    string Breed,
    string WeightText,
    string AgeText,
    string Owner,
    string Procedure,
    string Veterinarian,
    string Technician,
    string OperatingRoom = "",
    string PlannedStartText = "",
    string PlannedMinutesText = "",
    string FastingSinceText = "",
    bool ParentNotified = false)
{
    public static CaseDraft Empty { get; } = new("", 0, "", "", "", "", "", "", "");

    public Species Species => SpeciesIndex switch { 1 => Species.Feline, 2 => Species.Other, _ => Species.Canine };

    public decimal? WeightKg => ParseDecimal(WeightText);

    public decimal? AgeYears => ParseDecimal(AgeText);

    public IImmutableList<string> Validate()
    {
        var errors = ImmutableList.CreateBuilder<string>();
        if (string.IsNullOrWhiteSpace(Name))
        {
            errors.Add(Loc.T("NewCase_NameRequired", "Enter the patient's name."));
        }
        if (WeightKg is not decimal w || w is < 0.1m or > 150m)
        {
            errors.Add(Loc.T("NewCase_WeightRequired", "Enter a weight between 0.1 and 150 kg: every dose is calculated from it."));
        }
        if (!string.IsNullOrWhiteSpace(AgeText) && AgeYears is null)
        {
            errors.Add(Loc.T("NewCase_AgeNumber", "Age must be a number of years."));
        }
        if (string.IsNullOrWhiteSpace(Procedure))
        {
            errors.Add(Loc.T("NewCase_ProcedureRequired", "Enter the procedure."));
        }
        if (string.IsNullOrWhiteSpace(Veterinarian))
        {
            errors.Add(Loc.T("NewCase_VetRequired", "Enter the veterinarian."));
        }
        if (string.IsNullOrWhiteSpace(Technician))
        {
            errors.Add(Loc.T("NewCase_TechRequired", "Enter the technician running anesthesia."));
        }
        if (!string.IsNullOrWhiteSpace(PlannedStartText) && ParseTime(PlannedStartText) is null)
        {
            errors.Add(Loc.T("NewCase_StartTime", "Planned start is a time like 14:00."));
        }
        if (!string.IsNullOrWhiteSpace(PlannedMinutesText) && (int.TryParse(PlannedMinutesText.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) ? minutes is < 5 or > 600 : true))
        {
            errors.Add(Loc.T("NewCase_Duration", "Planned length is 5 to 600 minutes."));
        }
        if (!string.IsNullOrWhiteSpace(PlannedMinutesText) && string.IsNullOrWhiteSpace(PlannedStartText))
        {
            errors.Add(Loc.T("NewCase_DurationNeedsStart", "Enter a planned start for the planned length."));
        }
        if (!string.IsNullOrWhiteSpace(FastingSinceText) && ParseTime(FastingSinceText) is null)
        {
            errors.Add(Loc.T("NewCase_FastingTime", "Fasting since is a time like 20:00."));
        }
        return errors.ToImmutable();
    }

    public Case ToCase(DateTimeOffset now) => Case.New(
        new Patient(Name.Trim(), Species, Breed.Trim(), Math.Round(WeightKg ?? 0, 1), AgeYears ?? 0, Owner.Trim()),
        Procedure.Trim(), Veterinarian.Trim(), Technician.Trim(), now) with
    {
        OperatingRoom = OperatingRoom.Trim(),
        PlannedStart = ParseTime(PlannedStartText) is TimeSpan start ? On(now, start) : null,
        PlannedMinutes = int.TryParse(PlannedMinutesText.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) ? minutes : null,
        FastingSince = ParseTime(FastingSinceText) is TimeSpan fast ? LastAt(now, fast) : null,
        ParentNotifiedAt = ParentNotified ? now : null,
    };

    /// <summary>"14:00", "14h00", "1400" and "9:5" are times of day; anything else is not.</summary>
    public static TimeSpan? ParseTime(string text)
    {
        var t = text.Trim().ToLowerInvariant().Replace('h', ':').Replace('.', ':');
        if (!t.Contains(':') && t.Length is 3 or 4 && t.All(char.IsAsciiDigit))
        {
            t = t[..^2] + ":" + t[^2..];
        }
        var parts = t.Split(':');
        return parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var h) && h is >= 0 and <= 23
            && (parts[1].Length == 0 ? 0 : int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var m) ? m : -1) is var min && min is >= 0 and <= 59
            ? new TimeSpan(h, min, 0)
            : null;
    }

    private static DateTimeOffset On(DateTimeOffset now, TimeSpan time) => new(now.Date + time, now.Offset);

    /// <summary>Fasting starts before the visit: a time later than now was last night.</summary>
    private static DateTimeOffset LastAt(DateTimeOffset now, TimeSpan time) => On(now, time) is var at && at > now ? at.AddDays(-1) : at;

    private static decimal? ParseDecimal(string text)
    {
        var t = text.Trim().Replace(',', '.');
        return decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}

public partial record NewCaseModel(ICaseStore Store, IClock Clock, INavigator Navigator)
{
    public IState<CaseDraft> Draft => State.Value(this, () => CaseDraft.Empty);

    /// <summary>Set by the first Create attempt so errors appear after the user tries, then update live.</summary>
    public IState<bool> Attempted => State.Value(this, () => false);

    public IFeed<string> ErrorText => Feed.Combine(Draft, Attempted)
        .Select(x => x.Item2 ? string.Join(Environment.NewLine, x.Item1.Validate()) : "");

    public IState<string> SaveError => State.Value(this, () => "");

    /// <summary>The footer's error lines take no space until there is something to say.</summary>
    public IFeed<bool> HasErrors => Feed.Combine(ErrorText, SaveError).Select(x => !string.IsNullOrEmpty(x.Item1) || !string.IsNullOrEmpty(x.Item2));

    public async ValueTask Create(CaseDraft draft, CancellationToken ct)
    {
        await Attempted.UpdateAsync(_ => true, ct);
        if (draft.Validate().Count > 0)
        {
            return;
        }
        var created = draft.ToCase(Clock.Now);
        try
        {
            await Store.SaveAsync(created, ct);
        }
        catch (IOException ex)
        {
            await SaveError.UpdateAsync(_ => Loc.F("NewCase_SaveFailed", "The case could not be saved: {0} Your entries are kept; try again.", ex.Message), ct);
            return;
        }
        await Navigator.NavigateBackWithResultAsync(this, data: new CaseRef(created.Id), cancellation: ct);
    }

    public async ValueTask Cancel(CancellationToken ct) => await Navigator.NavigateBackAsync(this, cancellation: ct);
}
