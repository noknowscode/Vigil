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

    /// <summary>The form filled from a saved case (Edit case details); times read in the local zone of <paramref name="now"/>.</summary>
    public static CaseDraft From(Case c, DateTimeOffset now)
    {
        static string Dec(decimal v, string format) => v.ToString(format, CultureInfo.InvariantCulture);
        string Time(DateTimeOffset? t) => t is { } v ? v.ToOffset(now.Offset).ToString("HH:mm", CultureInfo.InvariantCulture) : "";
        var p = c.Patient;
        return new(p.Name, p.Species switch { Species.Feline => 1, Species.Other => 2, _ => 0 }, p.Breed, Dec(p.WeightKg, "0.0"),
            p.AgeYears > 0 ? Dec(p.AgeYears, "0.#") : "", p.Owner, c.Procedure, c.Veterinarian, c.Technician, c.OperatingRoom,
            Time(c.PlannedStart), c.PlannedMinutes?.ToString(CultureInfo.InvariantCulture) ?? "", Time(c.FastingSince), c.ParentNotifiedAt is not null);
    }

    /// <summary>
    /// <paramref name="c"/> corrected to this form (<see cref="Case.WithDetailsFrom"/> logs each change). A time left as it
    /// was keeps its saved date; a changed planned start stays on the case's day, a changed fasting time is the last one.
    /// </summary>
    public Case ApplyTo(Case c, DateTimeOffset now)
    {
        var was = From(c, now);
        var day = (c.PlannedStart ?? c.CreatedAt).ToOffset(now.Offset);
        var edited = c with
        {
            Patient = c.Patient with { Name = Name.Trim(), Species = Species, Breed = Breed.Trim(), WeightKg = Math.Round(WeightKg ?? 0, 1), AgeYears = AgeYears ?? 0, Owner = Owner.Trim() },
            Procedure = Procedure.Trim(),
            Veterinarian = Veterinarian.Trim(),
            Technician = Technician.Trim(),
            OperatingRoom = OperatingRoom.Trim(),
            PlannedStart = PlannedStartText.Trim() == was.PlannedStartText ? c.PlannedStart : ParseTime(PlannedStartText) is TimeSpan start ? On(day, start) : null,
            PlannedMinutes = int.TryParse(PlannedMinutesText.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) ? minutes : null,
            FastingSince = FastingSinceText.Trim() == was.FastingSinceText ? c.FastingSince : ParseTime(FastingSinceText) is TimeSpan fast ? LastAt(now, fast) : null,
            ParentNotifiedAt = ParentNotified ? c.ParentNotifiedAt ?? now : null,
        };
        return c.WithDetailsFrom(edited, now);
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

/// <summary>
/// What the case form shows besides its fields, for both of its models: errors once the user has tried to save (then
/// live), the save failure, and whether the footer has anything to say.
/// </summary>
public sealed class CaseFormState
{
    public CaseFormState(object owner, IState<CaseDraft> draft)
    {
        Attempted = State.Value(owner, () => false);
        ErrorText = Feed.Combine(draft, Attempted).Select(x => x.Item2 ? string.Join(Environment.NewLine, x.Item1.Validate()) : "");
        SaveError = State.Value(owner, () => "");
        HasErrors = Feed.Combine(ErrorText, SaveError).Select(x => !string.IsNullOrEmpty(x.Item1) || !string.IsNullOrEmpty(x.Item2));
    }

    /// <summary>Set by the first save attempt so errors appear after the user tries, then update live.</summary>
    public IState<bool> Attempted { get; }

    public IFeed<string> ErrorText { get; }

    public IState<string> SaveError { get; }

    /// <summary>The footer's error lines take no space until there is something to say.</summary>
    public IFeed<bool> HasErrors { get; }

    /// <summary>Marks the attempt; true when the draft is valid to save.</summary>
    public async ValueTask<bool> AcceptsAsync(CaseDraft draft, CancellationToken ct)
    {
        await Attempted.UpdateAsync(_ => true, ct);
        return draft.Validate().Count == 0;
    }
}

/// <summary>New patient (route "!NewCase"): an empty form; Save books the case and hands it back to open its record.</summary>
public partial record NewCaseModel(ICaseStore Store, IClock Clock, INavigator Navigator, ILogger<NewCaseModel> Logger)
{
    public IState<CaseDraft> Draft => State.Value(this, () => CaseDraft.Empty);

    private CaseFormState Form => _form ??= new(this, Draft);
    private CaseFormState? _form;

    public string Heading => Loc.T("NewCase_Heading", "New patient");
    public string Intro => Loc.T("NewCase_Intro", "Weight sets every dose, so it is the one number to get right.");
    public string ActionLabel => Loc.T("NewCase_Action", "Create case");
    public string Note => "";
    public bool HasNote => false;

    public IState<bool> Attempted => Form.Attempted;
    public IFeed<string> ErrorText => Form.ErrorText;
    public IState<string> SaveError => Form.SaveError;
    public IFeed<bool> HasErrors => Form.HasErrors;

    public async ValueTask Save(CaseDraft draft, CancellationToken ct)
    {
        if (!await Form.AcceptsAsync(draft, ct))
        {
            return;
        }
        var created = draft.ToCase(Clock.Now);
        try
        {
            await Store.SaveAsync(created, ct);
        }
        catch (Exception ex) when (StoreErrors.IsReported(ex))
        {
            Logger.LogWarning(ex, "Saving a new case failed.");
            await SaveError.UpdateAsync(_ => Loc.T("NewCase_NotSaved", "The case could not be saved on this device. Your entries are kept; try again."), ct);
            return;
        }
        await Navigator.NavigateBackWithResultAsync(this, data: new CaseRef(created.Id), cancellation: ct);
    }

    public async ValueTask Cancel(CancellationToken ct) => await Navigator.NavigateBackAsync(this, cancellation: ct);
}
