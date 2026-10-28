using System.Globalization;
using System.Text.Json.Nodes;

namespace Vigil.Tests;

/// <summary>The prototype moment: 12:42:26 on Wed 28 Oct 2026, Montreal.</summary>
internal static class Proto
{
    public static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 42, 26, TimeSpan.FromHours(-4));
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    public static IReadOnlyList<Case> Day => SampleDay.Create(Now);
    public static Case Named(string name) => Day.Single(c => c.Patient.Name == name);
    public static DateTimeOffset At(int h, int m) => new(2026, 10, 28, h, m, 0, TimeSpan.FromHours(-4));

    public static readonly Drug Cefazolin = new("cefazolin", "Cefazolin", "Antibiotic", 100, 22, 20, 25, 20, 25, DoseRoute.IV) { RedoseMinutes = 90 };
    public static readonly Drug Propofol = new("propofol", "Propofol", "Induction", 10, 4, 2, 6, 2, 8, DoseRoute.IV);
    public static readonly Drug[] Formulary = [Cefazolin, Propofol];
}

public class StageTests
{
    [Test]
    public void SampleDayStagesMatchThePrototypeCards()
    {
        Stage.Of(Proto.Named("Bella")).Should().Be(CareStage.Surgery);
        Stage.Of(Proto.Named("Milo")).Should().Be(CareStage.Recovery);
        Stage.Of(Proto.Named("Oscar")).Should().Be(CareStage.Booked);
        Stage.Of(Proto.Named("Pepper")).Should().Be(CareStage.Home);
    }

    [Test]
    public void PrepStartMovesABookedCaseToPrepUntilInduction()
    {
        var oscar = Proto.Named("Oscar");
        Stage.Of(oscar with { PrepStartedAt = Proto.Now }).Should().Be(CareStage.Prep);
        Stage.Of(oscar with { PrepStartedAt = Proto.Now, InducedAt = Proto.Now, Status = CaseStatus.Anesthetized }).Should().Be(CareStage.Surgery);
    }

    [Test]
    public void DischargeSendsAnUnsignedCaseHome()
    {
        Stage.Of(Proto.Named("Milo") with { DischargedAt = Proto.Now }).Should().Be(CareStage.Home);
        Stage.Number(CareStage.Recovery).Should().Be(4);
        Stage.AccessibleName(CareStage.Surgery).Should().Be("Stage 3 of 5: surgery");
    }
}

public class CarePhasesTests
{
    private static string Times(PhaseSpan s) => $"{s.Phase}{(s.Plans is { } p ? ":" + p : "")} {s.Start:HH:mm}-{(s.Ongoing ? "now" : s.End.ToString("HH:mm"))}";

    [Test]
    public void SpansReproduceThePrototypeGantt()
    {
        CarePhases.Of(Proto.Named("Pepper"), Proto.Now).Select(Times).Should().Equal("Prep 08:00-08:32", "Anesthesia 08:32-09:27", "Recovery 09:27-10:07");
        CarePhases.Of(Proto.Named("Milo"), Proto.Now).Select(Times).Should().Equal("Prep 09:30-10:02", "Anesthesia 10:02-12:08", "Recovery 12:08-now");
        CarePhases.Of(Proto.Named("Bella"), Proto.Now).Select(Times).Should().Equal("Prep 11:00-11:30", "Anesthesia 11:30-now");
        CarePhases.Of(Proto.Named("Oscar"), Proto.Now).Select(Times).Should().Equal(
            "Waiting 12:27-now", "Planned:Prep 13:30-14:00", "Planned:Anesthesia 14:00-15:00");
    }

    [Test]
    public void WaitingEndsAtPrepAndPlannedSlotsVanishOnceStarted()
    {
        var oscar = Proto.Named("Oscar") with { PrepStartedAt = Proto.Now.AddMinutes(-5) };
        var spans = CarePhases.Of(oscar, Proto.Now);
        spans.Single(s => s.Phase == CarePhaseKind.Waiting).End.Should().Be(oscar.PrepStartedAt!.Value);
        spans.Single(s => s.Phase == CarePhaseKind.Prep).Ongoing.Should().BeTrue();
        spans.Where(s => s.IsPlanned).Should().ContainSingle(s => s.Plans == CarePhaseKind.Anesthesia, "prep has started, only the surgery slot is planned");
        CarePhases.Of(oscar with { PlannedStart = Proto.Now.AddMinutes(-1) }, Proto.Now).Should().NotContain(s => s.IsPlanned);
    }

    [Test]
    public void RecoveryEndsAtTheEarlierOfSignOffAndDischarge()
    {
        var milo = Proto.Named("Milo") with { DischargedAt = Proto.Now.AddMinutes(-4) };
        var recovery = CarePhases.Of(milo, Proto.Now).Single(s => s.Phase == CarePhaseKind.Recovery);
        recovery.End.Should().Be(milo.DischargedAt!.Value);
        recovery.Ongoing.Should().BeFalse();
    }

    [Test]
    public void TotalsMatchWhereCareTimeWent()
    {
        var totals = CarePhases.Totals(Proto.Day, Proto.Now);
        totals.TotalText.Should().Be("7h 16m");
        totals.Rows.Select(r => $"{r.Label} {r.DurationText} {r.PercentText}").Should().Equal(
            "Anesthesia 4h 13m 58%", "Prep 1h 34m 22%", "Recovery 1h 14m 17%", "Waiting 0h 15m 3%");
        totals.PatientCount.Should().Be(4);
        totals.ProcedureCount.Should().Be(3);
        totals.Summary.Should().Be("7h 16m across 4 patients today");
        using (new CultureScope("en-US"))
        {
            totals.DayLine.Should().Be("4 patients · 3 procedures · Wed 28 Oct");
        }
        totals.Rows[0].Tooltip.Should().Be("Anesthesia: 4h 13m, 58% of care time");
    }

    [Test]
    public void TotalsClipToTodayAndIgnorePlannedSlots()
    {
        var yesterday = CarePhases.Totals(SampleDay.Create(Proto.Now.AddDays(-1)), Proto.Now);
        yesterday.Rows.Single(r => r.Phase == CarePhaseKind.Prep).Duration.Should().Be(TimeSpan.Zero);
        // Bella under anesthesia, Milo in recovery and Oscar waiting since yesterday: midnight to now each.
        var sinceMidnight = Proto.Now - new DateTimeOffset(Proto.Now.Date, Proto.Now.Offset);
        yesterday.Total.Should().Be(sinceMidnight * 3);
        yesterday.PatientCount.Should().Be(3);
        yesterday.ProcedureCount.Should().Be(0);
        CarePhases.Totals([], Proto.Now).Rows.Should().OnlyContain(r => r.Percent == 0);
    }

    [Test]
    public void WindowSpansWholeHoursAroundTheDay()
    {
        var spans = Proto.Day.SelectMany(c => CarePhases.Of(c, Proto.Now));
        CarePhases.Window(spans, Proto.Now).Should().Be((Proto.At(8, 0), Proto.At(15, 0)));
    }
}

public class DueTasksTests
{
    private static ImmutableList<DueTask> Rows(IEnumerable<Case>? cases = null) =>
        DueTasks.Of(cases ?? Proto.Day, Proto.Now, Proto.Interval, Proto.Formulary);

    [Test]
    public void SampleDayReproducesDueNextInOrder()
    {
        var rows = Rows();
        rows.Select(r => $"{r.DueLabel} {r.Time} | {r.Title} | {r.PatientName} · {r.Meta} | {r.Status} | {r.Action}").Should().Equal(
            "Late 12:41 | Record observations | Bella · Anesthesia · 5-minute strip | Overdue 1:26 | Record",
            "Due 12:50 | Pain score | Milo · Recovery · every 15 min | In 8 min | Score",
            $"Due 13:10 | Cefazolin redose | Bella · {4.05m.ToString("0.00", CultureInfo.CurrentCulture)} mL · IV | In 28 min | Prepare",
            "Due 13:15 | Pre-op bloodwork | Oscar · CBC and chemistry panel | Results ready | Review",
            "Due 14:00 | Discharge call | Pepper · Pet parent · K. Gagnon | Scheduled | Call");
        rows.Select(r => r.Tone).Should().Equal(DueTone.Overdue, DueTone.Neutral, DueTone.Neutral, DueTone.Ready, DueTone.Neutral);
        rows.Select(r => r.IsPrimary).Should().Equal(true, false, false, true, false);
        rows.Select(r => r.Stage).Should().Equal(CareStage.Surgery, CareStage.Recovery, CareStage.Surgery, CareStage.Booked, CareStage.Home);
        DueTasks.Summary(rows).Should().Be("5 tasks before 14:00 · 1 overdue");
    }

    [Test]
    public void ObservationsFollowTheReadingIntervalAndTurnLate()
    {
        var bella = Proto.Named("Bella");
        var fresh = bella with { Readings = bella.Readings.Add(bella.Readings[^1] with { At = Proto.Now.AddMinutes(-1) }) };
        var row = Rows([fresh]).Single(r => r.Kind == DueTaskKind.Observations);
        row.IsLate.Should().BeFalse();
        row.DueAt.Should().Be(Proto.Now.AddMinutes(4));
        row.Status.Should().Be("In 4 min");
        Rows([bella]).Single(r => r.Kind == DueTaskKind.Observations).IsLate.Should().BeTrue();
    }

    [Test]
    public void PainScoreIsDueFifteenMinutesAfterTheLastScoreOrTheEndOfAnesthesia()
    {
        var milo = Proto.Named("Milo");
        Rows([milo]).Single().DueAt.Should().Be(milo.Recovery.PainScores[^1].At.AddMinutes(15));
        var unscored = milo with { Recovery = milo.Recovery with { PainScores = [] } };
        var late = Rows([unscored]).Single();
        late.DueAt.Should().Be(milo.EndedAt!.Value.AddMinutes(15));
        late.IsLate.Should().BeTrue();
        late.Status.Should().Be("Overdue 19:00");
        Rows([Proto.Named("Bella")]).Should().NotContain(r => r.Kind == DueTaskKind.PainScore);
    }

    [Test]
    public void RedoseFollowsTheLastDoseAndOnlyRedosedDrugs()
    {
        var bella = Proto.Named("Bella");
        var redose = Rows([bella]).Single(r => r.Kind == DueTaskKind.Redose);
        redose.DueAt.Should().Be(Proto.At(13, 10));
        var again = bella with { Doses = bella.Doses.Add(bella.Doses.Single(d => d.DrugId == "cefazolin") with { At = Proto.At(12, 30) }) };
        Rows([again]).Single(r => r.Kind == DueTaskKind.Redose).DueAt.Should().Be(Proto.At(14, 0));
        DueTasks.Of([bella], Proto.Now, Proto.Interval, [Proto.Propofol]).Should().NotContain(r => r.Kind == DueTaskKind.Redose);
        Redose.Describe(Redose.Next(bella, Proto.Formulary)!, Proto.Now).Should().Be("Cefazolin redose · in 28 min");
    }

    [Test]
    public void BloodworkAppearsOnlyWhenReadyAndDischargeCallUntilDone()
    {
        var oscar = Proto.Named("Oscar");
        Rows([oscar with { Labs = [oscar.Labs[0] with { State = LabState.Pending }] }]).Should().BeEmpty();
        Rows([oscar with { Labs = [oscar.Labs[0] with { State = LabState.Reviewed }] }]).Should().BeEmpty();
        var pepper = Proto.Named("Pepper");
        Rows([pepper with { DischargeCall = pepper.DischargeCall! with { DoneAt = Proto.Now } }]).Should().BeEmpty();
        var soon = Rows([pepper with { DischargeCall = new DischargeCall(Proto.Now.AddMinutes(30), null) }]).Single();
        soon.Status.Should().Be("In 30 min", "within the hour a countdown replaces Scheduled");
        Rows([pepper with { DischargeCall = new DischargeCall(Proto.Now.AddMinutes(-2), null) }]).Single().IsLate.Should().BeTrue();
    }

    [Test]
    public void SummaryCountsAndHandlesEmpty()
    {
        DueTasks.Summary([]).Should().Be("Nothing due");
        DueTasks.Summary(Rows([Proto.Named("Milo")])).Should().Be("1 task before 12:50");
    }
}

public class CareInsightTests
{
    [Test]
    public void AveragePrepAgainstPlan()
    {
        var insight = CareInsight.Of(Proto.Day, Proto.Now, 25)!;
        insight.AverageMinutes.Should().Be(31);
        insight.Cases.Should().Be(3);
        insight.Text.Should().Be("Prep ran 31 minutes on average today, 6 minutes over plan.");
        CareInsight.Of(Proto.Day, Proto.Now, 40)!.Text.Should().Be("Prep ran 31 minutes on average today, 9 minutes under plan.");
        CareInsight.Of(Proto.Day, Proto.Now, 31)!.Text.Should().Be("Prep ran 31 minutes on average today, on plan.");
    }

    [Test]
    public void NoFinishedPrepTodayMeansNoInsight()
    {
        CareInsight.Of([Proto.Named("Oscar")], Proto.Now, 25).Should().BeNull();
        CareInsight.Of(SampleDay.Create(Proto.Now.AddDays(-1)), Proto.Now, 25).Should().BeNull();
        CareInsight.Of(Proto.Day, Proto.Now, new Preferences())!.PlannedMinutes.Should().Be(25);
    }
}

public class NextSurgeryTests
{
    [Test]
    public void NextBookedCaseWithCountdownPrepFastingAndNotice()
    {
        var next = NextSurgery.Of(Proto.Day, Proto.Now, 25)!;
        next.Case.Patient.Name.Should().Be("Oscar");
        next.Title.Should().Be("Next surgery · Oscar");
        next.Subtitle.Should().Be("Mass removal, left flank · OR 1");
        next.CountdownText.Should().Be("1:18");
        next.UntilText.Should().Be("until 14:00");
        next.PrepStartsAt.Should().Be(next.PlannedStart.AddMinutes(-30), "Oscar's own planned prep (30) overrides the practice's 25");
        next.PrepStartsText.Should().Be("13:30");
        next.PrepStartsSub.Should().Be("in 48 min");
        next.FastingText.Should().Be("Fasting since 06:00");
        next.NotifiedText.Should().Be("notified 12:30");
        next.BloodworkReady.Should().BeTrue();
        next.Team.Should().Be("Dr. Okafor · S. Tremblay");
    }

    [Test]
    public void EarliestPlannedStartWinsAndNoneWithoutAPlan()
    {
        var oscar = Proto.Named("Oscar");
        var earlier = oscar with { Id = Guid.NewGuid(), PlannedStart = Proto.Now.AddMinutes(20), Patient = oscar.Patient with { Name = "Luna" } };
        NextSurgery.Of([oscar, earlier], Proto.Now, 25)!.Case.Patient.Name.Should().Be("Luna");
        NextSurgery.Of([oscar with { PlannedStart = null }, Proto.Named("Bella")], Proto.Now, 25).Should().BeNull();
        var preparing = NextSurgery.Of([oscar with { PrepStartedAt = Proto.Now.AddMinutes(-3) }], Proto.Now, 25)!;
        preparing.PrepStartsAt.Should().Be(Proto.Now.AddMinutes(-3));
        preparing.PrepStarted.Should().BeTrue();
    }

    [Test]
    public void ThePracticePlannedPrepAppliesWhenTheCaseHasNone()
    {
        var oscar = Proto.Named("Oscar") with { PlannedPrepMinutes = null };
        NextSurgery.Of([oscar], Proto.Now, 25)!.PrepStartsText.Should().Be("13:35");
        CarePhases.Of(oscar, Proto.Now, 25).Single(s => s.Plans == CarePhaseKind.Prep).Start.Should().Be(Proto.At(13, 35));
        CarePhases.Of(Proto.Named("Oscar"), Proto.Now, 25).Single(s => s.Plans == CarePhaseKind.Prep).Start.Should().Be(Proto.At(13, 30));
    }
}

public class SampleDayV2Tests
{
    [Test]
    public void SampleDayCarriesThePrototypeRecordFields()
    {
        Proto.Day.Select(c => c.Patient.Name).Should().Equal("Bella", "Milo", "Oscar", "Pepper");
        var bella = Proto.Named("Bella");
        bella.OperatingRoom.Should().Be("OR 1");
        bella.InducedAt!.Value.ToString("HH:mm").Should().Be("11:30");
        Schedule.HoursMinutes(Schedule.Elapsed(bella, Proto.Now)).Should().Be("01:12");
        bella.Fluids.Single(f => f.IsRunning).Label.Should().Be($"LRS 5 mL/kg/h");
        Proto.Named("Pepper").DischargeCall!.DueAt.ToString("HH:mm").Should().Be("14:00");
        Proto.Named("Oscar").Labs.Single().State.Should().Be(LabState.Ready);
    }

    [Test]
    public async Task EmbeddedFormularyRedosesCefazolinOnly()
    {
        var drugs = await new EmbeddedFormulary().GetDrugsAsync(CancellationToken.None);
        drugs.Single(d => d.Id == "cefazolin").RedoseMinutes.Should().Be(90);
        drugs.Where(d => d.Id != "cefazolin").Should().OnlyContain(d => d.RedoseMinutes == null);
    }
}

public class BackCompatTests
{
    [Test]
    public async Task CaseFileWithoutV2FieldsLoadsWithDefaults()
    {
        var folder = Path.Combine(Path.GetTempPath(), "vigil-v2-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var bella = Proto.Named("Bella");
            await new JsonCaseStore(folder, new FaultInjection(null, 0)).SaveAsync(bella, CancellationToken.None);
            var file = Directory.GetFiles(folder, "*.json").Single();
            var json = JsonNode.Parse(await File.ReadAllTextAsync(file))!.AsObject();
            foreach (var key in new[] { "OperatingRoom", "PlannedStart", "PlannedMinutes", "PrepStartedAt", "DischargedAt", "Fluids", "Labs", "DischargeCall", "FastingSince", "ParentNotifiedAt" })
            {
                json.Remove(key).Should().BeTrue($"{key} is written by v2");
            }
            json["Recovery"]!.AsObject().Remove("PainScores").Should().BeTrue();
            await File.WriteAllTextAsync(file, json.ToJsonString());

            var loaded = (await new JsonCaseStore(folder, new FaultInjection(null, 0)).GetAsync(bella.Id, CancellationToken.None))!;
            loaded.OperatingRoom.Should().BeEmpty();
            loaded.PlannedStart.Should().BeNull();
            loaded.PrepStartedAt.Should().BeNull();
            loaded.Fluids.Should().NotBeNull().And.BeEmpty();
            loaded.Labs.Should().NotBeNull().And.BeEmpty();
            loaded.Recovery.PainScores.Should().NotBeNull().And.BeEmpty();
            loaded.DischargeCall.Should().BeNull();
            loaded.Readings.Should().BeEquivalentTo(bella.Readings);
            Stage.Of(loaded).Should().Be(CareStage.Surgery);
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    [Test]
    public async Task V2FieldsRoundTrip()
    {
        var folder = Path.Combine(Path.GetTempPath(), "vigil-v2-tests", Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var c in Proto.Day)
            {
                await new JsonCaseStore(folder, new FaultInjection(null, 0)).SaveAsync(c, CancellationToken.None);
            }
            var loaded = await new JsonCaseStore(folder, new FaultInjection(null, 0)).GetAllAsync(CancellationToken.None);
            loaded.Single(c => c.Patient.Name == "Oscar").Should().BeEquivalentTo(Proto.Named("Oscar"), o => o.Excluding(c => c.Id).Excluding(c => c.Patient));
            loaded.Single(c => c.Patient.Name == "Milo").Recovery.PainScores.Should().HaveCount(2);
            loaded.Single(c => c.Patient.Name == "Bella").Fluids.Single().RateMlKgH.Should().Be(5m);
            loaded.Single(c => c.Patient.Name == "Pepper").DischargeCall.Should().NotBeNull();
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    [Test]
    public void PreferencesFileWithoutPlannedPrepDefaultsTo25()
    {
        var path = Path.Combine(Path.GetTempPath(), "vigil-v2-tests", Guid.NewGuid().ToString("N"), "preferences.json");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, """{"ReadingIntervalMinutes":3,"ReduceMotion":false,"ClinicianName":"Okafor"}""");
            var prefs = new Preferences(path);
            prefs.PlannedPrepMinutes.Should().Be(25);
            prefs.ReadingInterval.Should().Be(TimeSpan.FromMinutes(3));
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Test]
    public async Task PlannedPrepMinutesPersists()
    {
        var path = Path.Combine(Path.GetTempPath(), "vigil-v2-tests", Guid.NewGuid().ToString("N"), "preferences.json");
        try
        {
            await new Preferences(path).SetPlannedPrepMinutesAsync(30, CancellationToken.None);
            new Preferences(path).PlannedPrepMinutes.Should().Be(30);
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }
}

internal sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public CultureScope(string name) => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);

    public void Dispose() => CultureInfo.CurrentCulture = _culture;
}
