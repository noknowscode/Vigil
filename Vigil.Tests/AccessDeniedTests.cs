using Microsoft.Extensions.Logging.Abstractions;
using Vigil.Presentation;

namespace Vigil.Tests;

/// <summary>
/// A store whose writes the device refuses (File.Create throws UnauthorizedAccessException on a read-only folder):
/// every caller reports it like any other failed write, instead of letting it escape the command unseen.
/// </summary>
public class AccessDeniedTests : ModelTestBase
{
    private const string NotWritten = "It could not be written to this device. Try again.";

    private DeniedStore Denied => new(Store);

    [Test]
    public void TheSharedFilterReportsAccessErrors()
    {
        StoreErrors.IsReported(new UnauthorizedAccessException("/cases/a.json")).Should().BeTrue();
        StoreErrors.Describe(new UnauthorizedAccessException("/cases/a.json")).Should().Be(NotWritten);
        StoreErrors.IsReported(new NullReferenceException()).Should().BeFalse("a bug is not a store failure");
    }

    [Test]
    public async Task ADrawerSaveSaysNotSaved()
    {
        var oscar = Proto.Named("Oscar");
        await Store.SaveAsync(oscar, CancellationToken.None);
        var model = new PreopModel(new CaseRef(oscar.Id), Denied, Clock, new Preferences(), Nav, NullLogger<PreopModel>.Instance);

        await model.Induce(CancellationToken.None);

        (await SettledAsync(model.SaveError, e => e != "")).Should().Be($"Not saved: {NotWritten}");
    }

    [Test]
    public async Task ADoseSaysNotSaved()
    {
        var knuckles = Proto.Named("Knuckles");
        await Store.SaveAsync(knuckles, CancellationToken.None);
        var model = new DoseModel(new CaseRef(knuckles.Id), Denied, new EmbeddedFormulary(), Clock, Nav, new DoseFeedback(), NullLogger<DoseModel>.Instance);
        var calc = Dosing.Calculate(knuckles.Patient.Species, knuckles.Patient.WeightKg, Proto.Cefazolin, 22);

        await model.Give(calc, Proto.Cefazolin, 0, CancellationToken.None);

        (await SettledAsync(model.Error, e => e != "")).Should().Be($"Not saved: {NotWritten}");
    }

    [Test]
    public async Task SignOffSaysNotSigned()
    {
        var milo = (await SeedDayAsync()).Single(c => c.Patient.Name == "Milo");
        var model = new SignOffModel(new CaseRef(milo.Id), Denied, Clock, Nav, NullLogger<SignOffModel>.Instance);

        await model.Sign("Dr. Okafor", true, CancellationToken.None);

        (await SettledAsync(model.Error, e => e != "")).Should().Be($"Not signed: {NotWritten}");
    }

    [Test]
    public async Task ANewCaseKeepsItsEntriesAndSaysSo()
    {
        var model = new NewCaseModel(Denied, Clock, Nav, NullLogger<NewCaseModel>.Instance);
        var draft = CaseDraft.From(Proto.Named("Oscar"), Proto.Now);

        await model.Save(draft, CancellationToken.None);

        (await SettledAsync(model.SaveError, e => e != "")).Should().Be("The case could not be saved on this device. Your entries are kept; try again.");
        Nav.Paths.Should().BeEmpty();
    }

    [Test]
    public async Task ACorrectionKeepsItsEntriesAndSaysSo()
    {
        var oscar = Proto.Named("Oscar");
        await Store.SaveAsync(oscar, CancellationToken.None);
        var model = new EditCaseModel(new CaseRef(oscar.Id), Denied, Clock, Nav, NullLogger<EditCaseModel>.Instance);

        await model.Save(CaseDraft.From(oscar, Proto.Now) with { WeightText = "5.0" }, CancellationToken.None);

        (await SettledAsync(model.SaveError, e => e != "")).Should().Be($"Not saved: {NotWritten} Your entries are kept.");
    }

    [Test]
    public async Task StartPrepSaysNotSaved()
    {
        var day = Proto.Day.Where(c => c.Status != CaseStatus.Anesthetized).ToList();
        await SeedAsync(day);
        var model = new BoardModel(Denied, Clock, new Preferences(), new EmbeddedFormulary(), null!, NullLogger<BoardModel>.Instance);

        await model.StartPrep(HeroCard.From(day, Proto.Now, Proto.Interval, 25)!, CancellationToken.None);

        (await SettledAsync(model.ActionError, e => e != "")).Should().Be($"Not saved: {NotWritten}");
    }

    [Test]
    public async Task ADeleteSaysNotDeleted()
    {
        var oscar = Proto.Named("Oscar");
        await Store.SaveAsync(oscar, CancellationToken.None);
        Nav.Answer(true);
        var model = new RecordModel(new CaseRef(oscar.Id), Denied, Clock, new Preferences(), Nav, new DoseFeedback(), new EmbeddedFormulary(), NullLogger<RecordModel>.Instance);

        await model.DeleteCase(CancellationToken.None);

        (await SettledAsync(model.SaveError, e => e != "")).Should().Be($"Not deleted: {NotWritten}");
        (await SavedAsync(oscar.Id)).Should().NotBeNull();
    }

    [Test]
    public async Task LoadingTheSampleDayAndClearingSaySo()
    {
        Nav.Answer(true).Answer(true);
        var model = new SettingsModel(null!, null!, new Preferences(), Denied, Clock, Nav, NullLogger<SettingsModel>.Instance);

        await model.LoadSampleDay(CancellationToken.None);
        (await SettledAsync(model.Notice, e => e != "")).Should().Be("The sample day could not be loaded. Your records are unchanged.");

        await model.ClearAll(CancellationToken.None);
        (await SettledAsync(model.Notice, e => e is { Length: > 0 } && !e.StartsWith("The sample", StringComparison.Ordinal)))
            .Should().Be("Some records could not be deleted. Try again.");
    }

    /// <summary>Reads go to the real store; every write throws as File.Create does on a folder the app may not write.</summary>
    private sealed class DeniedStore(ICaseStore inner) : ICaseStore
    {
        private static UnauthorizedAccessException Denied() => new("Access to the path '/cases/a.json' is denied.");

        public ValueTask<ImmutableList<Case>> GetAllAsync(CancellationToken ct) => inner.GetAllAsync(ct);
        public ValueTask<Case?> GetAsync(Guid id, CancellationToken ct) => inner.GetAsync(id, ct);
        public Signal Changed => inner.Changed;
        public void Reload() => inner.Reload();
        public IReadOnlyList<string> SetAside => inner.SetAside;
        public ValueTask SaveAsync(Case value, CancellationToken ct) => throw Denied();
        public ValueTask<Case> UpdateAsync(Guid id, Func<Case, Case> update, CancellationToken ct) => throw Denied();
        public ValueTask DeleteAsync(Guid id, CancellationToken ct) => throw Denied();
        public ValueTask ClearAsync(CancellationToken ct) => throw Denied();
        public ValueTask ReplaceAllAsync(IReadOnlyList<Case> cases, CancellationToken ct) => throw Denied();
    }
}
