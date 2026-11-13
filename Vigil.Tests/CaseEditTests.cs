using Microsoft.Extensions.Logging.Abstractions;
using Vigil.Presentation;

namespace Vigil.Tests;

/// <summary>Case details stay correctable until sign-off: every change is logged, and what is calculated from the case follows.</summary>
public class CaseEditTests : ModelTestBase
{
    private EditCaseModel Edit(Case c, ICaseStore? store = null) =>
        new(new CaseRef(c.Id), store ?? Store, Clock, Nav, NullLogger<EditCaseModel>.Instance);

    [Test]
    public void TheFormFillsFromTheCaseAndAnUnchangedFormChangesNothing()
    {
        var oscar = Proto.Named("Oscar");

        var draft = CaseDraft.From(oscar, Proto.Now);

        draft.Name.Should().Be("Oscar");
        draft.SpeciesIndex.Should().Be(1, "Oscar is feline");
        draft.WeightText.Should().Be("6.8");
        draft.OperatingRoom.Should().Be("OR 1");
        draft.PlannedStartText.Should().Be("14:00");
        draft.ParentNotified.Should().BeTrue();
        draft.Validate().Should().BeEmpty();
        draft.ApplyTo(oscar, Proto.Now).Should().Be(oscar, "saving the form as it was logs no correction and moves no time");
    }

    [Test]
    public void EachChangedDetailIsLoggedAndTheRecordStaysAsRecorded()
    {
        var knuckles = Proto.Named("Knuckles");
        var draft = CaseDraft.From(knuckles, Proto.Now) with { WeightText = "14.8", OperatingRoom = "OR 2", Procedure = "Spay" };

        var corrected = draft.ApplyTo(knuckles, Proto.Now.AddMinutes(8));

        corrected.Patient.WeightKg.Should().Be(14.8m);
        corrected.OperatingRoom.Should().Be("OR 2");
        corrected.Readings.Should().Equal(knuckles.Readings);
        corrected.Doses.Should().Equal(knuckles.Doses, "doses given keep the volume drawn up");
        corrected.Status.Should().Be(knuckles.Status);
        corrected.Corrections.Select(c => (c.Field, c.Before, c.After)).Should().Equal(
            (CaseDetail.Weight, "18.4", "14.8"), (CaseDetail.Procedure, "Ovariohysterectomy (spay)", "Spay"), (CaseDetail.Room, "OR 1", "OR 2"));
        corrected.Corrections.Should().OnlyContain(c => c.At == Proto.Now.AddMinutes(8));
    }

    [Test]
    public void ANewPlannedStartStaysOnTheCasesDay()
    {
        var oscar = Proto.Named("Oscar");

        var moved = (CaseDraft.From(oscar, Proto.Now) with { PlannedStartText = "15:30" }).ApplyTo(oscar, Proto.Now);

        moved.PlannedStart.Should().Be(Proto.At(15, 30));
        moved.Corrections.Should().ContainSingle(c => c.Field == CaseDetail.PlannedStart && c.Before == "14:00" && c.After == "15:30");
    }

    [Test]
    public async Task AWeightCorrectionSavesAndTheDoseCalculatorFollowsIt()
    {
        var knuckles = Proto.Named("Knuckles");
        await Store.SaveAsync(knuckles, CancellationToken.None);
        var edit = Edit(knuckles);
        var draft = (await edit.Draft)!;
        (await edit.Note).Should().Contain("New doses use the corrected weight", "Knuckles already had doses");

        await edit.Save(draft with { WeightText = "14.8" }, CancellationToken.None);

        var saved = await SavedAsync(knuckles.Id);
        saved.Patient.WeightKg.Should().Be(14.8m);
        saved.Corrections.Should().ContainSingle(c => c.Field == CaseDetail.Weight);
        Nav.Paths.Should().Equal("-");

        var dose = new DoseModel(new CaseRef(knuckles.Id), Store, new EmbeddedFormulary(), Clock, Nav, new DoseFeedback(), NullLogger<DoseModel>.Instance);
        await dose.Selected.UpdateAsync(_ => Proto.Cefazolin, CancellationToken.None);
        var calc = await SettledAsync(dose.Calc, c => c is not null);
        calc!.VolumeMl.Should().Be(Dosing.Calculate(Species.Canine, 14.8m, Proto.Cefazolin, 22).VolumeMl, "the calculator reads the corrected weight");
    }

    [Test]
    public async Task ASpeciesCorrectionChangesTheAlarmRanges()
    {
        var knuckles = Proto.Named("Knuckles");
        await Store.SaveAsync(knuckles, CancellationToken.None);
        var edit = Edit(knuckles);

        await edit.Save((await edit.Draft)! with { SpeciesIndex = 1 }, CancellationToken.None);

        var saved = await SavedAsync(knuckles.Id);
        saved.Patient.Species.Should().Be(Species.Feline);
        CaseView.From(saved, Proto.Now, Proto.Interval).Signalment.Should().StartWith("Feline");
    }

    [Test]
    public async Task AnInvalidFormSaysWhyAndSavesNothing()
    {
        var knuckles = Proto.Named("Knuckles");
        await Store.SaveAsync(knuckles, CancellationToken.None);
        var edit = Edit(knuckles);
        var draft = (await edit.Draft)! with { WeightText = "0" };
        await edit.Draft.UpdateAsync(_ => draft, CancellationToken.None);

        await edit.Save(draft, CancellationToken.None);

        (await SettledAsync(edit.ErrorText, e => e != "")).Should().Contain("every dose is calculated from it");
        (await SavedAsync(knuckles.Id)).Patient.WeightKg.Should().Be(18.4m);
        Nav.Paths.Should().BeEmpty();
    }

    [Test]
    public async Task ASignedRecordCannotBeCorrected()
    {
        var pepper = Proto.Named("Pepper");
        await Store.SaveAsync(pepper, CancellationToken.None);
        var edit = Edit(pepper);

        await edit.Save((await edit.Draft)! with { WeightText = "5.0" }, CancellationToken.None);

        (await SettledAsync(edit.SaveError, e => e != "")).Should().Be("Not saved: This record is signed and can no longer change. Your entries are kept.");
        (await SavedAsync(pepper.Id)).Corrections.Should().BeEmpty();
        Nav.Paths.Should().BeEmpty();
    }

    [Test]
    public async Task CorrectionsShowInHistoryAndTheExport()
    {
        var knuckles = Proto.Named("Knuckles");
        var corrected = (CaseDraft.From(knuckles, Proto.Now) with { WeightText = "14.8" }).ApplyTo(knuckles, Proto.At(12, 50));

        var view = CaseView.From(corrected, Proto.Now, Proto.Interval);

        view.HasCorrections.Should().BeTrue();
        view.Corrections.Single().Text.Should().Be("Weight (kg): 18.4 → 14.8");
        CsvExport.ToCsv(corrected).Should().Contain("correction").And.Contain("Weight: 18.4 -> 14.8");
        CsvExport.ToSummary(corrected).Should().Contain("Corrected 2026-10-28 12:50: Weight 18.4 -> 14.8");
        await Task.CompletedTask;
    }

    [Test]
    public async Task ACorrectedCaseSurvivesTheStore()
    {
        var knuckles = (CaseDraft.From(Proto.Named("Knuckles"), Proto.Now) with { WeightText = "14.8" }).ApplyTo(Proto.Named("Knuckles"), Proto.Now);
        await Store.SaveAsync(knuckles, CancellationToken.None);

        var reread = await new JsonCaseStore(Folder, NoFaultInjection.Instance).GetAsync(knuckles.Id, CancellationToken.None);

        reread!.Corrections.Should().Equal(knuckles.Corrections);
    }
}
