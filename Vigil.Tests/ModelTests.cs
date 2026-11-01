using System.Runtime.CompilerServices;
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

    protected static readonly IClock Clock = new FixedClock(Proto.Now);

    [SetUp]
    public void SetUpStore()
    {
        _folder = Path.Combine(Path.GetTempPath(), "vigil-tests", Guid.NewGuid().ToString("N"));
        Store = new JsonCaseStore(_folder, new FaultInjection(null, 0));
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
    private RecordModel Open(Case c) =>
        new(new CaseRef(c.Id), Store, Clock, new Preferences(), null!, null!, new DoseFeedback(), new EmbeddedFormulary());

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
