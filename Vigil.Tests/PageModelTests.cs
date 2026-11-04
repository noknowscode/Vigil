using Microsoft.Extensions.Logging.Abstractions;
using Vigil.Presentation;

namespace Vigil.Tests;

/// <summary>The shell: sidebar picks, the highlight, the Anesthesia shortcut, New patient and the phone menu.</summary>
public class ShellModelTests : ModelTestBase
{
    private readonly FakeRouteNotifier _routes = new();

    private ShellModel Shell() => new(Nav, _routes, Store, new Preferences(), Clock);

    [Test]
    public async Task ASidebarPickHighlightsItAndClearsTheFrame()
    {
        var shell = Shell();

        await shell.Go(ShellSection.Agenda, CancellationToken.None);

        (await shell.Section).Should().Be(ShellSection.Agenda);
        (await shell.AgendaState).Should().Be(ShellSection.Active);
        (await shell.TodayState).Should().Be(ShellSection.Inactive);
        Nav.Paths.Should().Equal("./-/Agenda");
    }

    [Test]
    public async Task APageShownInTheFrameMovesTheHighlight()
    {
        var shell = Shell();
        (await shell.Section).Should().Be(ShellSection.Today);

        _routes.Show("Today/Record");
        await UntilAsync(async () => await shell.Section == ShellSection.Patients);

        (await shell.Section).Should().Be(ShellSection.Patients, "a record belongs to Patients");
    }

    [Test]
    public async Task AnesthesiaOpensTheLiveRecordOverToday()
    {
        var day = await SeedDayAsync();
        var shell = Shell();

        await shell.OpenAnesthesia(CancellationToken.None);

        Nav.Paths.Should().Equal("./-/Today", "./Record");
        Nav.Data[1].Should().Be(new CaseRef(day.Single(c => c.Status == CaseStatus.Anesthetized).Id));
    }

    [Test]
    public async Task AnesthesiaWithNoCaseUnderwayGoesNowhere()
    {
        await SeedAsync(Proto.Day.Where(c => c.Status != CaseStatus.Anesthetized));

        await Shell().OpenAnesthesia(CancellationToken.None);

        Nav.Paths.Should().BeEmpty();
    }

    [Test]
    public async Task NewPatientOpensTheCaseTheSheetCreated()
    {
        var created = new CaseRef(Guid.NewGuid());
        Nav.Answer(created);

        await Shell().NewPatient(CancellationToken.None);

        Nav.Paths.Should().Equal("!NewCase", "./-/Today", "./Record");
        Nav.Data[2].Should().Be(created);
    }

    [Test]
    public async Task ACancelledNewPatientStaysPut()
    {
        await Shell().NewPatient(CancellationToken.None);

        Nav.Paths.Should().Equal("!NewCase");
    }

    [Test]
    public async Task ThePhoneMenuHandsItsPickBackToTheShell()
    {
        var shell = Shell();
        Nav.Answer(new MenuChoice(ShellSection.Settings));

        await shell.OpenMenu(ShellSection.Today, CancellationToken.None);

        Nav.Paths.Should().Equal("!Menu", "./-/Settings");
        Nav.Data[0].Should().Be(new MenuContext(ShellSection.Today));
        (await shell.Section).Should().Be(ShellSection.Settings);
    }

    [Test]
    public async Task TheSidebarCollapsesToTheRailAndBack()
    {
        var shell = Shell();
        (await shell.IsExpanded).Should().BeTrue();

        await shell.ToggleSidebar(CancellationToken.None);

        (await shell.IsCollapsed).Should().BeTrue();
        await shell.ToggleSidebar(CancellationToken.None);
        (await shell.IsExpanded).Should().BeTrue();
    }

    [Test]
    public async Task AnUnreadableRecordRaisesTheWarningAndTheRestLoad()
    {
        await SeedAsync(Proto.Day);
        await File.WriteAllTextAsync(Path.Combine(Folder, $"{Guid.NewGuid()}.json"), "{ not json");

        // A fresh start over the folder: the store reads every file and sets the damaged one aside.
        var shell = new ShellModel(Nav, _routes, new JsonCaseStore(Folder, NoFaultInjection.Instance), new Preferences(), Clock);

        (await shell.StoreWarning).Should().StartWith("One saved record could not be read.");
        (await shell.HasStoreWarning).Should().BeTrue();
        (await shell.AnesthesiaCount).Should().Be("1");
    }
}

/// <summary>The phone menu: the shell's sidebar in a drawer that hands each pick back.</summary>
public class MenuModelTests : ModelTestBase
{
    private MenuModel Menu(string section) => new(new MenuContext(section), Nav, Store, new Preferences(), Clock);

    [Test]
    public async Task TheDrawerHighlightsTheSectionItOpenedOver()
    {
        var menu = Menu(ShellSection.Agenda);

        (await menu.AgendaState).Should().Be(ShellSection.Active);
        (await menu.TodayState).Should().Be(ShellSection.Inactive);
        menu.IsExpanded.Should().BeTrue("the drawer always shows the full sidebar");
    }

    [Test]
    public async Task APickClosesTheDrawerWithTheChoice()
    {
        var menu = Menu(ShellSection.Today);

        await menu.Go(ShellSection.Patients, CancellationToken.None);
        await menu.NewPatient(CancellationToken.None);
        await menu.OpenAnesthesia(CancellationToken.None);

        Nav.Paths.Should().Equal("-", "-", "-");
        Nav.Data.Should().Equal(new MenuChoice(ShellSection.Patients), new MenuChoice(MenuChoice.NewPatient), new MenuChoice(ShellSection.Anesthesia));
    }

    [Test]
    public async Task CollapseClosesTheDrawerWithoutAChoice()
    {
        await Menu(ShellSection.Today).ToggleSidebar(CancellationToken.None);

        Nav.Paths.Should().Equal("-");
        Nav.Data.Should().Equal([null]);
    }

    [Test]
    public async Task TheCountsMatchTheShells()
    {
        await SeedAsync(Proto.Day);
        var menu = Menu(ShellSection.Today);

        (await menu.AnesthesiaCount).Should().Be("1");
        (await menu.HasLive).Should().BeTrue();
        (await menu.ClinicianName).Should().Be("Clinician", "no surname is set");
    }
}

/// <summary>Patients: every case on the device as cards, newest first, each with its own details toggle.</summary>
public class PatientsModelTests : ModelTestBase
{
    [Test]
    public async Task EveryCaseIsListedNewestFirst()
    {
        await SeedAsync(Proto.Day);
        var model = new PatientsModel(Store, Clock, new Preferences());

        var cards = await model.All;

        cards.Select(c => c.Name).Should().Equal(Proto.Day.OrderByDescending(c => c.CreatedAt).Select(c => c.Patient.Name));
        (await model.Summary).Should().Be("3 in clinic · 4 on this device", "Pepper is signed out");
    }

    [Test]
    public async Task DetailsOpenAndCloseOneCardAtATime()
    {
        await SeedAsync(Proto.Day);
        var model = new PatientsModel(Store, Clock, new Preferences());
        var milo = (await model.All).Single(c => c.Name == "Milo");

        await model.ToggleDetails(milo, CancellationToken.None);
        await UntilAsync(async () => (await model.All).Single(c => c.Name == "Milo").IsExpanded);

        (await model.All).Where(c => c.IsExpanded).Select(c => c.Name).Should().Equal("Milo");
        await model.ToggleDetails(milo, CancellationToken.None);
        await UntilAsync(async () => !(await model.All).Any(c => c.IsExpanded));
        (await model.All).Should().NotContain(c => c.IsExpanded);
    }

    [Test]
    public async Task RetryAfterAFailedReadLoadsTheCases()
    {
        await SeedAsync(Proto.Day);
        var flaky = new JsonCaseStore(Folder, new FaultInjection("store-read-once", 0));
        var model = new PatientsModel(flaky, Clock, new Preferences());

        var failed = () => model.All.Value(CancellationToken.None).AsTask();
        await failed.Should().ThrowAsync<IOException>("the first read of the folder fails");

        model.Retry();
        await UntilAsync(async () => (await model.All).Count == 4);
        (await model.All).Should().HaveCount(4);
    }
}

/// <summary>Agenda: today's events and preparing list, the care ruler and the next observation reminder.</summary>
public class AgendaModelTests : ModelTestBase
{
    [Test]
    public async Task TheNextReadingIsTheLiveCasesOverdueOne()
    {
        await SeedAsync(Proto.Day);
        var model = new AgendaModel(Store, Clock, new Preferences());

        var next = await model.AgendaNext;

        next.Should().NotBeNull();
        next!.Name.Should().Be("Knuckles");
        next.IsOverdue.Should().BeTrue("the last reading was at 12:36 and the interval is 5 minutes");
        (await model.AgendaClock).Should().Be("12:42");
    }

    [Test]
    public async Task TheDayListsItsCasesAndTheirCareTime()
    {
        await SeedAsync(Proto.Day);
        var model = new AgendaModel(Store, Clock, new Preferences());

        var agenda = await model.Agenda;
        var ruler = await model.CareRhythm;

        agenda!.HasEvents.Should().BeTrue();
        agenda.Events.Select(e => e.Name).Should().Equal("Pepper", "Knuckles", "Milo");
        agenda.Preparing.Select(e => e.Name).Should().Equal("Oscar");
        ruler!.IsEmpty.Should().BeFalse();
    }

    [Test]
    public async Task AnEmptyDayHasNoReminder()
    {
        var model = new AgendaModel(Store, Clock, new Preferences());

        (await model.AgendaNext).Should().BeNull();
        (await model.CareRhythm)!.IsEmpty.Should().BeTrue();
    }
}

/// <summary>New patient: errors after the first try, a saved case handed back, a failed save that keeps the entries.</summary>
public class NewCaseModelTests : ModelTestBase
{
    private static readonly CaseDraft Ziggy = CaseDraft.Empty with
    {
        Name = "Ziggy", WeightText = "12.6", AgeText = "3", Owner = "R. Diaz", Procedure = "Dental cleaning",
        Veterinarian = "Dr. Okafor", Technician = "S. Tremblay", OperatingRoom = "OR 2", PlannedStartText = "14:30",
    };

    [Test]
    public async Task ErrorsWaitForTheFirstTry()
    {
        var model = new NewCaseModel(Store, Clock, Nav, NullLogger<NewCaseModel>.Instance);
        (await model.ErrorText).Should().BeEmpty();

        await model.Save(CaseDraft.Empty, CancellationToken.None);

        (await SettledAsync(model.ErrorText, e => e != "")).Should().Contain("Enter the patient's name.").And.Contain("every dose is calculated from it");
        (await model.HasErrors).Should().BeTrue();
        (await Store.GetAllAsync(CancellationToken.None)).Should().BeEmpty();
        Nav.Paths.Should().BeEmpty();
    }

    [Test]
    public async Task ACompleteFormSavesTheCaseAndHandsItBack()
    {
        var model = new NewCaseModel(Store, Clock, Nav, NullLogger<NewCaseModel>.Instance);

        await model.Save(Ziggy, CancellationToken.None);

        var saved = (await Store.GetAllAsync(CancellationToken.None)).Should().ContainSingle().Subject;
        saved.Patient.WeightKg.Should().Be(12.6m);
        saved.OperatingRoom.Should().Be("OR 2");
        saved.PlannedStart.Should().Be(Proto.At(14, 30));
        Nav.Paths.Should().Equal("-");
        Nav.Data.Should().Equal(new CaseRef(saved.Id));
    }

    [Test]
    public async Task AFailedSaveKeepsTheEntriesAndSaysSo()
    {
        var model = new NewCaseModel(FailingStore, Clock, Nav, NullLogger<NewCaseModel>.Instance);
        await model.Draft.UpdateAsync(_ => Ziggy, CancellationToken.None);

        await model.Save(Ziggy, CancellationToken.None);

        (await SettledAsync(model.SaveError, e => e != "")).Should().Be("The case could not be saved on this device. Your entries are kept; try again.");
        (await model.Draft).Should().Be(Ziggy);
        Nav.Paths.Should().BeEmpty();
    }

    [Test]
    public async Task CancelClosesTheSheet()
    {
        await new NewCaseModel(Store, Clock, Nav, NullLogger<NewCaseModel>.Instance).Cancel(CancellationToken.None);

        Nav.Paths.Should().Equal("-");
    }
}

/// <summary>Pet-parent preview: saved milestones only, and nothing for a record that is gone.</summary>
public class ParentPreviewModelTests : ModelTestBase
{
    [Test]
    public async Task TheParentSeesOnlyRecordedMilestones()
    {
        var knuckles = (await SeedDayAsync()).Single(c => c.Patient.Name == "Knuckles");

        var snapshot = await new ParentPreviewModel(new CaseRef(knuckles.Id), Store, Clock).Patient;

        snapshot.Should().NotBeNull();
        snapshot!.Status.Should().Be("In surgery");
        snapshot.Milestones.Select(m => m.Done).Should().Equal(true, true, true, false, false);
        snapshot.Milestones[2].Time.Should().Be("11:30");
    }

    [Test]
    public async Task ARecordThatIsGoneShowsNothing()
    {
        await SeedAsync(Proto.Day);

        (await new ParentPreviewModel(new CaseRef(Guid.NewGuid()), Store, Clock).Patient).Should().BeNull();
    }
}

/// <summary>Sign-off: a typed name and a confirmation, recovery cases only, then the sheet closes.</summary>
public class SignOffModelTests : ModelTestBase
{
    private SignOffModel SignOff(Case c, ICaseStore? store = null) =>
        new(new CaseRef(c.Id), store ?? Store, Clock, Nav, NullLogger<SignOffModel>.Instance);

    [Test]
    public async Task TheCasesVeterinarianIsPrefilled()
    {
        var milo = (await SeedDayAsync()).Single(c => c.Patient.Name == "Milo");
        var model = SignOff(milo);

        (await model.Veterinarian).Should().Be(milo.Veterinarian);
        (await model.Summary)!.CanSign.Should().BeTrue();
    }

    [Test]
    public async Task SigningNeedsANameAndTheConfirmation()
    {
        var milo = (await SeedDayAsync()).Single(c => c.Patient.Name == "Milo");
        var model = SignOff(milo);

        await model.Sign("Dr. Okafor", false, CancellationToken.None);
        await model.Sign("  ", true, CancellationToken.None);

        (await SettledAsync(model.Error, e => e != "")).Should().Be("Enter the veterinarian's name and confirm the record is complete.");
        (await SavedAsync(milo.Id)).Status.Should().Be(CaseStatus.Recovery);
        Nav.Paths.Should().BeEmpty();
    }

    [Test]
    public async Task SigningLocksTheRecordAndClosesTheSheet()
    {
        var milo = (await SeedDayAsync()).Single(c => c.Patient.Name == "Milo");

        await SignOff(milo).Sign(" Dr. Okafor ", true, CancellationToken.None);

        var saved = await SavedAsync(milo.Id);
        saved.Status.Should().Be(CaseStatus.Signed);
        saved.SignedBy.Should().Be("Dr. Okafor");
        saved.SignedAt.Should().Be(Proto.Now);
        Nav.Paths.Should().Equal("-");
    }

    [Test]
    public async Task ACaseStillUnderAnesthesiaIsNotSigned()
    {
        var knuckles = (await SeedDayAsync()).Single(c => c.Patient.Name == "Knuckles");
        var model = SignOff(knuckles);

        await model.Sign("Dr. Okafor", true, CancellationToken.None);

        (await SettledAsync(model.Error, e => e != "")).Should().Be("Not signed: End anesthesia before signing.");
        (await SavedAsync(knuckles.Id)).Status.Should().Be(CaseStatus.Anesthetized);
        Nav.Paths.Should().BeEmpty();
    }

    [Test]
    public async Task AFailedWriteIsReportedAndTheSheetStays()
    {
        var milo = (await SeedDayAsync()).Single(c => c.Patient.Name == "Milo");
        var model = SignOff(milo, FailingStore);

        await model.Sign("Dr. Okafor", true, CancellationToken.None);

        (await SettledAsync(model.Error, e => e != "")).Should().Be("Not signed: It could not be written to this device. Try again.");
        Nav.Paths.Should().BeEmpty();
    }
}
