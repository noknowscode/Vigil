using Microsoft.Extensions.Logging.Abstractions;
using Vigil.Presentation;

namespace Vigil.Tests;

/// <summary>The record's drawers: each pane's model saves its own changes, reports its own failures and hands steps back.</summary>
public class PreopModelTests : ModelTestBase
{
    private PreopModel Preop(Case c, ICaseStore? store = null) =>
        new(new CaseRef(c.Id), store ?? Store, Clock, new Preferences(), Nav, NullLogger<PreopModel>.Instance);

    [Test]
    public async Task MarkReviewedSurvivesTheNextChecklistTick()
    {
        var oscar = Proto.Named("Oscar");
        await Store.SaveAsync(oscar, CancellationToken.None);

        await new LabsModel(new CaseRef(oscar.Id), Store, Clock, new Preferences(), Nav, NullLogger<LabsModel>.Instance)
            .ReviewLab("CBC and chemistry panel", CancellationToken.None);
        var model = Preop(oscar);
        (await model.Preop)!.BloodworkReviewed.Should().BeTrue("the checklist shows the check Mark reviewed set");
        await model.Preop.UpdateAsync(p => p! with { Fasted = true }, CancellationToken.None);
        await UntilAsync(async () => (await SavedAsync(oscar.Id)).Preop.Fasted);

        var saved = await SavedAsync(oscar.Id);
        saved.Preop.Fasted.Should().BeTrue();
        saved.Preop.BloodworkReviewed.Should().BeTrue("the checklist tick must not write back the check Mark reviewed set");
    }

    [Test]
    public async Task InduceStartsTheClockAndClosesTheDrawer()
    {
        var oscar = Proto.Named("Oscar") with { Preop = new PreopCheck(true, true, true, true, true, 2, false, "") };
        await Store.SaveAsync(oscar, CancellationToken.None);

        await Preop(oscar).Induce(CancellationToken.None);

        var saved = await SavedAsync(oscar.Id);
        saved.Status.Should().Be(CaseStatus.Anesthetized);
        saved.InducedAt.Should().Be(Proto.Now);
        Nav.Paths.Should().Equal("-");
    }

    [Test]
    public async Task AFailedInductionStaysOpenAndSaysSo()
    {
        var oscar = Proto.Named("Oscar");
        await Store.SaveAsync(oscar, CancellationToken.None);
        var model = Preop(oscar, FailingStore);

        await model.Induce(CancellationToken.None);

        (await SettledAsync(model.SaveError, e => e != "")).Should().Be("Not saved: It could not be written to this device. Try again.");
        (await model.HasSaveError).Should().BeTrue();
        Nav.Paths.Should().BeEmpty("the drawer stays open on the error");
        await model.DismissError(CancellationToken.None);
        (await model.SaveError).Should().BeEmpty();
    }

    [Test]
    public async Task PremedicationDetailsAndDeleteAreHandedToTheRecord()
    {
        var oscar = Proto.Named("Oscar");
        await Store.SaveAsync(oscar, CancellationToken.None);
        var model = Preop(oscar);

        await model.GivePremedication(CancellationToken.None);
        await model.EditDetails(CancellationToken.None);
        await model.DeleteCase(CancellationToken.None);

        Nav.Paths.Should().Equal("-", "-", "-");
        Nav.Data.Should().Equal(new PaneChoice(RecordPane.Dose), new PaneChoice(PaneChoice.EditCase), new PaneChoice(PaneChoice.DeleteCase));
        (await SavedAsync(oscar.Id)).Should().NotBeNull("the record deletes, after asking");
    }
}

public class VitalsModelTests : ModelTestBase
{
    private VitalsModel Vitals(Case c) =>
        new(new CaseRef(c.Id), Store, Clock, new Preferences(), new EmbeddedFormulary(), Nav, NullLogger<VitalsModel>.Instance);

    [Test]
    public async Task ThePadStartsFromTheLastReadingAndSteps()
    {
        var knuckles = Proto.Named("Knuckles");
        await Store.SaveAsync(knuckles, CancellationToken.None);
        var model = Vitals(knuckles);
        (await model.Draft)!.Hr.Should().Be("94");

        await model.Step("Hr|+", CancellationToken.None);

        (await SettledAsync(model.Draft, d => d?.Hr != "94"))!.Hr.Should().Be("96", "heart rate steps by 2");
        (await model.Timers)!.ClockText.Should().StartWith("overdue", "the last reading was at 12:36 and readings are due every 5 minutes");
    }

    [Test]
    public async Task ARecordedReadingIsSavedAndStartsTheNextOne()
    {
        var knuckles = Proto.Named("Knuckles");
        await Store.SaveAsync(knuckles, CancellationToken.None);
        var model = Vitals(knuckles);
        var draft = VitalsDraft.From(knuckles.Readings[^1]) with { Hr = "98" };

        await model.RecordReading(draft, CancellationToken.None);

        var saved = await SavedAsync(knuckles.Id);
        saved.Readings.Should().HaveCount(knuckles.Readings.Count + 1);
        saved.Readings[^1].Hr.Should().Be(98);
        saved.Readings[^1].At.Should().Be(Proto.Now);
        (await model.Draft)!.Hr.Should().Be("98");
        Nav.Paths.Should().BeEmpty("the drawer stays open for the next reading");
    }

    [Test]
    public async Task ARuleOfTheRecordIsShownAsWritten()
    {
        var oscar = Proto.Named("Oscar");
        await Store.SaveAsync(oscar, CancellationToken.None);
        var model = Vitals(oscar);

        await model.RecordReading(VitalsDraft.From(Proto.Named("Knuckles").Readings[^1]), CancellationToken.None);

        (await SettledAsync(model.SaveError, e => e != "")).Should().Be("Not saved: Induce before recording vitals.");
        (await SavedAsync(oscar.Id)).Readings.Should().BeEmpty();
    }

    [Test]
    public async Task AnEmptyPadSaysWhatIsMissing()
    {
        var knuckles = Proto.Named("Knuckles");
        await Store.SaveAsync(knuckles, CancellationToken.None);
        var model = Vitals(knuckles);

        await model.RecordReading(VitalsDraft.Empty, CancellationToken.None);

        (await SettledAsync(model.SaveError, e => e != "")).Should().NotBeEmpty();
        (await SavedAsync(knuckles.Id)).Readings.Should().HaveCount(knuckles.Readings.Count);
    }
}

public class MedicationsModelTests : ModelTestBase
{
    private MedicationsModel Medications(Case c) =>
        new(new CaseRef(c.Id), Store, Clock, new Preferences(), Nav, NullLogger<MedicationsModel>.Instance);

    [Test]
    public async Task FluidsStartChangeRateAndStop()
    {
        var knuckles = Proto.Named("Knuckles") with { Fluids = [] };
        await Store.SaveAsync(knuckles, CancellationToken.None);
        var model = Medications(knuckles);
        (await model.FluidName).Should().Be("LRS");

        await model.StartFluids("LRS", "5", CancellationToken.None);
        await model.StartFluids("LRS", "10", CancellationToken.None);
        (await SavedAsync(knuckles.Id)).Fluids.Should().HaveCount(2).And.ContainSingle(f => f.IsRunning && f.RateMlKgH == 10);

        await model.StopFluids(CancellationToken.None);
        (await SavedAsync(knuckles.Id)).Fluids.Should().OnlyContain(f => !f.IsRunning);
    }

    [Test]
    public async Task ARateOutOfBoundsIsRefused()
    {
        var knuckles = Proto.Named("Knuckles") with { Fluids = [] };
        await Store.SaveAsync(knuckles, CancellationToken.None);
        var model = Medications(knuckles);

        await model.StartFluids("LRS", "45", CancellationToken.None);

        (await SettledAsync(model.SaveError, e => e != "")).Should().Be("Enter a fluid and a rate above 0 and up to 30 mL/kg/h.");
        (await SavedAsync(knuckles.Id)).Fluids.Should().BeEmpty();
    }

    [Test]
    public async Task GiveADoseHandsTheDosePaneBack()
    {
        var knuckles = Proto.Named("Knuckles");
        await Store.SaveAsync(knuckles, CancellationToken.None);

        await Medications(knuckles).GiveDose(CancellationToken.None);

        Nav.Paths.Should().Equal("-");
        Nav.Data.Should().Equal(new PaneChoice(RecordPane.Dose));
    }
}

public class RecoveryModelTests : ModelTestBase
{
    private RecoveryModel Recovery(Case c, ICaseStore? store = null, IRecordExporter? exporter = null) =>
        new(new CaseRef(c.Id), store ?? Store, Clock, new Preferences(), new EmbeddedFormulary(), exporter!, Nav, NullLogger<RecoveryModel>.Instance);

    [TestCase(ExportStatus.Saved, "Saved Pepper-2026-10-28-anesthesia.csv.")]
    [TestCase(ExportStatus.Cancelled, "Export cancelled.")]
    [TestCase(ExportStatus.Unavailable, "No save dialog could open on this device, so nothing was exported.")]
    public async Task AnExportSaysHowItEnded(ExportStatus status, string notice)
    {
        var pepper = Proto.Named("Pepper");
        await Store.SaveAsync(pepper, CancellationToken.None);
        var model = Recovery(pepper, exporter: new Exporter(new ExportResult(status, status == ExportStatus.Saved ? RecordExporter.SuggestedFileName(pepper) : "")));

        await model.Export(CancellationToken.None);

        (await SettledAsync(model.Notice, n => n != "")).Should().Be(notice);
    }

    [Test]
    public void TheSuggestedFileNameCarriesItsExtension() =>
        RecordExporter.SuggestedFileName(Proto.Named("Pepper") with { Patient = Proto.Named("Pepper").Patient with { Name = "Mr. Pepper" } })
            .Should().Be("MrPepper-2026-10-28-anesthesia.csv");

    /// <summary>An exporter that ends every export the same way.</summary>
    private sealed class Exporter(ExportResult result) : IRecordExporter
    {
        public ValueTask<ExportResult> ExportCsvAsync(Case record, CancellationToken ct) => ValueTask.FromResult(result);
        public ValueTask CopySummaryAsync(Case record, CancellationToken ct) => ValueTask.CompletedTask;
    }

    [Test]
    public async Task EndingAnesthesiaStartsRecovery()
    {
        var knuckles = Proto.Named("Knuckles");
        await Store.SaveAsync(knuckles, CancellationToken.None);

        await Recovery(knuckles).EndAnesthesia(CancellationToken.None);

        var saved = await SavedAsync(knuckles.Id);
        saved.Status.Should().Be(CaseStatus.Recovery);
        saved.EndedAt.Should().Be(Proto.Now);
    }

    [Test]
    public async Task AFailedWriteSaysSoWithoutPathsOrIds()
    {
        var knuckles = Proto.Named("Knuckles");
        await Store.SaveAsync(knuckles, CancellationToken.None);
        var model = Recovery(knuckles, FailingStore);

        await model.EndAnesthesia(CancellationToken.None);

        (await SettledAsync(model.SaveError, e => e != "")).Should().Be("Not saved: It could not be written to this device. Try again.");
        (await SavedAsync(knuckles.Id)).Status.Should().Be(CaseStatus.Anesthetized);
    }

    [Test]
    public async Task TheRecoveryLogRecordsItsMilestones()
    {
        var milo = Proto.Named("Milo") with { Recovery = RecoveryLog.Empty };
        await Store.SaveAsync(milo, CancellationToken.None);
        var model = Recovery(milo);

        await model.Extubate(CancellationToken.None);
        await model.Sternal(CancellationToken.None);
        await model.ScorePain("2", CancellationToken.None);
        await model.ScorePain("9", CancellationToken.None);

        var log = (await SavedAsync(milo.Id)).Recovery;
        log.ExtubatedAt.Should().Be(Proto.Now);
        log.SternalAt.Should().Be(Proto.Now);
        log.PainScores.Should().ContainSingle().Which.Score.Should().Be(2, "a score outside 0 to 4 is ignored");
    }

    [Test]
    public async Task TemperatureAndNotesSaveAsTheyAreTyped()
    {
        var milo = Proto.Named("Milo");
        await Store.SaveAsync(milo, CancellationToken.None);
        var model = Recovery(milo);
        await model.Recovery;

        await model.Recovery.UpdateAsync(d => d! with { TempC = "37.9", Notes = "Calm" }, CancellationToken.None);
        await UntilAsync(async () => (await SavedAsync(milo.Id)).Recovery.Notes == "Calm");

        var log = (await SavedAsync(milo.Id)).Recovery;
        log.TempC.Should().Be(37.9m);
        log.Notes.Should().Be("Calm");
    }

    [Test]
    public async Task SignOffIsHandedToTheRecord()
    {
        var milo = Proto.Named("Milo");
        await Store.SaveAsync(milo, CancellationToken.None);

        await Recovery(milo).SignOff(CancellationToken.None);

        Nav.Data.Should().Equal(new PaneChoice(PaneChoice.SignOff));
    }
}

public class LabsAndHistoryModelTests : ModelTestBase
{
    [Test]
    public async Task MarkReviewedClearsTheReadyPanelAndViewPreparationHandsBack()
    {
        var oscar = Proto.Named("Oscar");
        await Store.SaveAsync(oscar, CancellationToken.None);
        var model = new LabsModel(new CaseRef(oscar.Id), Store, Clock, new Preferences(), Nav, NullLogger<LabsModel>.Instance);
        (await model.Case)!.LabRows.Should().ContainSingle(l => l.IsReady);

        await model.ReviewLab("CBC and chemistry panel", CancellationToken.None);
        await model.ViewPreparation(CancellationToken.None);

        (await SavedAsync(oscar.Id)).Labs.Should().OnlyContain(l => l.State == LabState.Reviewed);
        Nav.Data.Should().Equal(new PaneChoice(RecordPane.Preop));
    }

    [Test]
    public async Task HistoryListsTheReadingsNewestFirstAndClosesOnClose()
    {
        var knuckles = Proto.Named("Knuckles");
        await Store.SaveAsync(knuckles, CancellationToken.None);
        var model = new HistoryModel(new CaseRef(knuckles.Id), Store, Clock, new Preferences(), Nav, NullLogger<HistoryModel>.Instance);

        var view = await model.Case;
        await model.Close(CancellationToken.None);

        view!.Readings.Should().HaveCount(13);
        view.Readings[0].Time.Should().Be("12:36");
        Nav.Paths.Should().Equal("-");
    }

    [Test]
    public async Task ADrawerOverADeletedCaseShowsNone()
    {
        var model = new HistoryModel(new CaseRef(Guid.NewGuid()), Store, Clock, new Preferences(), Nav, NullLogger<HistoryModel>.Instance);

        (await model.Case).Should().BeNull();
    }
}
