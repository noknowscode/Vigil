using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using Vigil.Presentation;

namespace Vigil.Tests;

/// <summary>A wall clock that stays at one moment: <see cref="Ticks"/> yields it once and then waits.</summary>
internal sealed class FixedClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset Now => now;

    public async IAsyncEnumerable<DateTimeOffset> Ticks([EnumeratorCancellation] CancellationToken ct)
    {
        yield return now;
        await Task.Delay(Timeout.Infinite, ct);
    }
}

/// <summary>
/// Model commands against a store in a temporary folder and a fixed clock (SPEC.md, Testing). MVUX runs the states'
/// ForEach callbacks in the background, so a test waits for the store to show the result.
/// </summary>
public abstract class ModelTestBase
{
    private string _folder = "";

    protected JsonCaseStore Store { get; private set; } = null!;

    /// <summary>A second store over the same folder whose writes fail (the sample day is saved through <see cref="Store"/>).</summary>
    protected JsonCaseStore FailingStore => new(_folder, new FaultInjection("store-write", 0));

    protected static readonly IClock Clock = new FixedClock(Proto.Now);

    [SetUp]
    public void SetUpStore()
    {
        _folder = Path.Combine(Path.GetTempPath(), "vigil-tests", Guid.NewGuid().ToString("N"));
        Store = new JsonCaseStore(_folder, NoFaultInjection.Instance);
    }

    [TearDown]
    public void DeleteStore()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    protected async Task<Case> SavedAsync(Guid id) => (await Store.GetAsync(id, CancellationToken.None))!;

    protected async Task SeedAsync(IEnumerable<Case> cases)
    {
        foreach (var c in cases)
        {
            await Store.SaveAsync(c, CancellationToken.None);
        }
    }

    /// <summary>Polls <paramref name="condition"/> until it holds, for up to 5 s.</summary>
    protected static async Task UntilAsync(Func<Task<bool>> condition)
    {
        for (var i = 0; i < 100 && !await condition(); i++)
        {
            await Task.Delay(50);
        }
    }
}

public class RecordModelTests : ModelTestBase
{
    private static RecordModel Open(Case c, ICaseStore store) =>
        new(new CaseRef(c.Id), store, Clock, new Preferences(), null!, null!, new DoseFeedback(), new EmbeddedFormulary(), NullLogger<RecordModel>.Instance);

    private RecordModel Open(Case c) => Open(c, Store);

    [Test]
    public async Task MarkReviewedSurvivesTheNextChecklistTick()
    {
        var oscar = Proto.Named("Oscar");
        await Store.SaveAsync(oscar, CancellationToken.None);
        var model = Open(oscar);
        (await model.Preop)!.BloodworkReviewed.Should().BeFalse();

        await model.ReviewLab("CBC and chemistry panel", CancellationToken.None);
        await model.Preop.UpdateAsync(p => p! with { Fasted = true }, CancellationToken.None);
        await UntilAsync(async () => (await SavedAsync(oscar.Id)).Preop.Fasted);

        var saved = await SavedAsync(oscar.Id);
        saved.Preop.Fasted.Should().BeTrue();
        saved.Preop.BloodworkReviewed.Should().BeTrue("the checklist tick must not write back the check Mark reviewed set");
        (await model.Preop)!.BloodworkReviewed.Should().BeTrue("the checklist shows the check Mark reviewed set");
    }

    [Test]
    public async Task ARuleOfTheRecordIsShownAsWritten()
    {
        var oscar = Proto.Named("Oscar");
        await Store.SaveAsync(oscar, CancellationToken.None);
        var model = Open(oscar);

        await model.RecordReading(VitalsDraft.From(Proto.Named("Knuckles").Readings[^1]), CancellationToken.None);

        (await model.SaveError).Should().Be("Not saved: Induce before recording vitals.");
        (await SavedAsync(oscar.Id)).Readings.Should().BeEmpty();
    }

    [Test]
    public async Task AFailedWriteSaysSoWithoutPathsOrIds()
    {
        var knuckles = Proto.Named("Knuckles");
        await Store.SaveAsync(knuckles, CancellationToken.None);
        var model = Open(knuckles, FailingStore);

        await model.EndAnesthesia(CancellationToken.None);

        (await model.SaveError).Should().Be("Not saved: It could not be written to this device. Try again.");
        (await SavedAsync(knuckles.Id)).Status.Should().Be(CaseStatus.Anesthetized);
    }
}

public class DoseModelTests : ModelTestBase
{
    [Test]
    public async Task ASignedRecordRefusesADoseWithoutShowingItsId()
    {
        var pepper = Proto.Named("Pepper");
        await Store.SaveAsync(pepper, CancellationToken.None);
        var model = new DoseModel(new CaseRef(pepper.Id), Store, new EmbeddedFormulary(), Clock, null!, new DoseFeedback(), NullLogger<DoseModel>.Instance);
        var calc = Dosing.Calculate(pepper.Patient.Species, pepper.Patient.WeightKg, Proto.Cefazolin, 22);

        await model.Give(calc, Proto.Cefazolin, 0, CancellationToken.None);

        var error = await model.Error;
        error.Should().Be("Not saved: This record is signed and can no longer change.");
        error.Should().NotContain(pepper.Id.ToString());
        (await SavedAsync(pepper.Id)).Doses.Should().BeEmpty();
    }
}

public class BoardModelTests : ModelTestBase
{
    [Test]
    public async Task StartPrepThatCannotBeSavedSaysSo()
    {
        var day = Proto.Day.Where(c => c.Status != CaseStatus.Anesthetized).ToList();
        await SeedAsync(day);
        var model = new BoardModel(FailingStore, Clock, new Preferences(), new EmbeddedFormulary(), null!, NullLogger<BoardModel>.Instance);
        var hero = HeroCard.From(day, Proto.Now, Proto.Interval, 25)!;

        await model.StartPrep(hero, CancellationToken.None);

        (await model.ActionError).Should().Be("Not saved: It could not be written to this device. Try again.");
        (await SavedAsync(hero.Id)).PrepStartedAt.Should().BeNull();
    }
}

public class ShellSectionTests
{
    [Test]
    public void SetAsideFilesRaiseAWarning()
    {
        ShellSection.SetAsideText(0).Should().BeEmpty();
        ShellSection.SetAsideText(1).Should().StartWith("One saved record could not be read.");
        ShellSection.SetAsideText(3).Should().StartWith("3 saved records could not be read.");
    }
}

public class StoreErrorsTests
{
    [Test]
    public void UsersReadWhatWentWrongNotTheExceptionText()
    {
        var id = Guid.NewGuid();
        StoreErrors.Describe(new SignedRecordException(id)).Should().Be("This record is signed and can no longer change.");
        StoreErrors.Describe(new KeyNotFoundException($"Case {id} does not exist.")).Should().Be("This record no longer exists.");
        StoreErrors.Describe(new IOException("/home/x/cases/a.json: disk full")).Should().Be("It could not be written to this device. Try again.");
        StoreErrors.Describe(new CaseRuleException("End anesthesia before signing.")).Should().Be("End anesthesia before signing.");
    }
}
