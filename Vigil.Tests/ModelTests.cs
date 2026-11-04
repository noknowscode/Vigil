using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using Uno.Extensions.Reactive;
using Uno.Extensions.Reactive.Core;
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

/// <summary>A clock that moves one second every 20 ms, from <paramref name="start"/>.</summary>
internal sealed class FastClock(DateTimeOffset start) : IClock
{
    private int _ticks;

    public DateTimeOffset Now => start.AddSeconds(_ticks);

    public async IAsyncEnumerable<DateTimeOffset> Ticks([EnumeratorCancellation] CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            yield return Now;
            await Task.Delay(20, ct);
            Interlocked.Increment(ref _ticks);
        }
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

    /// <summary>The store's folder, for a second store over the same files or a damaged file.</summary>
    protected string Folder => _folder;

    /// <summary>A second store over the same folder whose writes fail (the sample day is saved through <see cref="Store"/>).</summary>
    protected JsonCaseStore FailingStore => new(_folder, new FaultInjection("store-write", 0));

    protected static readonly IClock Clock = new FixedClock(Proto.Now);

    /// <summary>Records the navigation the model under test asks for (a new one per test).</summary>
    internal FakeNavigator Nav { get; private set; } = null!;

    [SetUp]
    public void SetUpStore()
    {
        _folder = Path.Combine(Path.GetTempPath(), "vigil-tests", Guid.NewGuid().ToString("N"));
        Store = new JsonCaseStore(_folder, NoFaultInjection.Instance);
        Nav = new FakeNavigator();
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

    /// <summary>Saves the sample day (one set of ids) and returns it.</summary>
    protected async Task<IReadOnlyList<Case>> SeedDayAsync()
    {
        var day = Proto.Day;
        await SeedAsync(day);
        return day;
    }

    /// <summary>
    /// The feed's value once <paramref name="settled"/> holds, or its last value after 5 s. A command's state update can
    /// land after the command returns on a loaded machine, so an assertion on a message reads it this way.
    /// </summary>
    protected static async Task<T?> SettledAsync<T>(IFeed<T> feed, Func<T?, bool> settled) where T : notnull
    {
        T? value = default;
        for (var i = 0; i < 100; i++)
        {
            value = await feed;
            if (settled(value))
            {
                break;
            }
            await Task.Delay(50);
        }
        return value;
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
    private static RecordModel Open(Case c, ICaseStore store, IClock? clock = null) =>
        new(new CaseRef(c.Id), store, clock ?? Clock, new Preferences(), null!, null!, new DoseFeedback(), new EmbeddedFormulary(), NullLogger<RecordModel>.Instance);

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
    public async Task TheClockMovesTheTimersNotTheRecordView()
    {
        var knuckles = Proto.Named("Knuckles");
        await Store.SaveAsync(knuckles, CancellationToken.None);
        var model = Open(knuckles, Store, new FastClock(Proto.Now));
        var context = SourceContext.GetOrCreate(model);
        int views = 0, timers = 0;

        Count(model.Case, context, () => Interlocked.Increment(ref views));
        Count(model.Timers, context, () => Interlocked.Increment(ref timers));
        await Task.Delay(1000);
        await context.DisposeAsync();

        views.Should().Be(1, "the readings, doses and labs are only rebuilt when the case changes");
        timers.Should().BeGreaterThan(5, "the overdue countdown moves every (fast) second");
    }

    /// <summary>Calls <paramref name="onValue"/> for each new value of <paramref name="feed"/> until the context is disposed.</summary>
    private static void Count<T>(IFeed<T> feed, SourceContext context, Action onValue) => _ = Task.Run(async () =>
    {
        await foreach (var message in feed.GetSource(context, CancellationToken.None))
        {
            if (message.Changes.Contains(MessageAxis.Data) && message.Current.Data.IsSome(out _))
            {
                onValue();
            }
        }
    });

    [Test]
    public async Task ARuleOfTheRecordIsShownAsWritten()
    {
        var oscar = Proto.Named("Oscar");
        await Store.SaveAsync(oscar, CancellationToken.None);
        var model = Open(oscar);

        await model.RecordReading(VitalsDraft.From(Proto.Named("Knuckles").Readings[^1]), CancellationToken.None);

        (await SettledAsync(model.SaveError, e => e != "")).Should().Be("Not saved: Induce before recording vitals.");
        (await SavedAsync(oscar.Id)).Readings.Should().BeEmpty();
    }

    [Test]
    public async Task AFailedWriteSaysSoWithoutPathsOrIds()
    {
        var knuckles = Proto.Named("Knuckles");
        await Store.SaveAsync(knuckles, CancellationToken.None);
        var model = Open(knuckles, FailingStore);

        await model.EndAnesthesia(CancellationToken.None);

        (await SettledAsync(model.SaveError, e => e != "")).Should().Be("Not saved: It could not be written to this device. Try again.");
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

        var error = await SettledAsync(model.Error, e => e != "");
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

        (await SettledAsync(model.ActionError, e => e != "")).Should().Be("Not saved: It could not be written to this device. Try again.");
        (await SavedAsync(hero.Id)).PrepStartedAt.Should().BeNull();
    }
}

public class ShellSectionTests
{
    [TestCase("/Today", ShellSection.Today)]
    [TestCase("Main/Agenda", ShellSection.Agenda)]
    [TestCase("/Settings", ShellSection.Settings)]
    [TestCase("./-/Patients?filter=all", ShellSection.Patients)]
    [TestCase("/Today/Record", ShellSection.Patients)]
    [TestCase("/Today/Record/Dose", ShellSection.Patients)]
    [TestCase("/Today/!NewCase", ShellSection.Today)]
    [TestCase("/Board", ShellSection.Today)]
    public void ARouteHighlightsTheSidebarItemOfItsLastFramePage(string path, string section) =>
        ShellSection.FromPath(path).Should().Be(section);

    [TestCase(null)]
    [TestCase("")]
    [TestCase("/Main")]
    [TestCase("!NewCase")]
    public void ARouteWithoutAFramePageKeepsTheHighlight(string? path) =>
        ShellSection.FromPath(path).Should().BeNull();

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

public class SettingsModelTests : ModelTestBase
{
    [Test]
    public async Task PlannedPrepIsSavedClampedOrPutBack()
    {
        var preferences = new Preferences();
        var model = new SettingsModel(null!, null!, preferences, Store, Clock, null!, NullLogger<SettingsModel>.Instance);
        (await model.PlannedPrepMinutes).Should().Be("25");

        await model.PlannedPrepMinutes.UpdateAsync(_ => "40", CancellationToken.None);
        await UntilAsync(() => Task.FromResult(preferences.PlannedPrepMinutes == 40));
        preferences.PlannedPrepMinutes.Should().Be(40);

        await model.PlannedPrepMinutes.UpdateAsync(_ => "500", CancellationToken.None);
        await UntilAsync(async () => await model.PlannedPrepMinutes == "180");
        preferences.PlannedPrepMinutes.Should().Be(180, "the practice preference is clamped to 5-180 minutes");

        await model.PlannedPrepMinutes.UpdateAsync(_ => "soon", CancellationToken.None);
        await UntilAsync(async () => await model.PlannedPrepMinutes == "180");
        (await model.PlannedPrepMinutes).Should().Be("180", "text that is not a number puts back the saved value");
    }
}
