namespace Vigil.Tests;

public class DosingTests
{
    private static readonly Drug Propofol = new("propofol", "Propofol", "Induction", 10m, 4m, 2m, 6m, 2m, 8m, DoseRoute.IV);
    private static readonly Drug Dexmed = new("dexmedetomidine", "Dexmedetomidine", "Sedative", 0.5m, 0.005m, 0.001m, 0.01m, 0.001m, 0.015m, DoseRoute.IM);

    [Test]
    public void VolumeIsWeightTimesDoseOverConcentration()
    {
        Dosing.VolumeMl(18.4m, 4m, 10m).Should().Be(7.36m);
    }

    [Test]
    public void VolumeRoundsToHundredthOfAMillilitre()
    {
        // 18.4 × 0.005 ÷ 0.5 = 0.184 → 0.18 ; 3.3 × 0.1 ÷ 2 = 0.165 → 0.17 (midpoint away from zero)
        Dosing.VolumeMl(18.4m, 0.005m, 0.5m).Should().Be(0.18m);
        Dosing.VolumeMl(3.3m, 0.1m, 2m).Should().Be(0.17m);
    }

    [Test]
    public void ZeroConcentrationGivesZeroVolumeInsteadOfThrowing()
    {
        Dosing.VolumeMl(10m, 1m, 0m).Should().Be(0m);
    }

    [TestCase(Species.Canine, 1.9, RangeClass.Low)]
    [TestCase(Species.Canine, 4.0, RangeClass.Normal)]
    [TestCase(Species.Canine, 7.0, RangeClass.High)]
    [TestCase(Species.Feline, 7.0, RangeClass.Normal)]
    public void RangeIsClassifiedPerSpecies(Species species, decimal mgPerKg, RangeClass expected)
    {
        Dosing.Calculate(species, 5m, Propofol, mgPerKg).Range.Should().Be(expected);
    }

    [Test]
    public void CalculationCarriesFormulaAndReference()
    {
        var calc = Dosing.Calculate(Species.Canine, 18.4m, Dexmed, 0.005m);
        calc.Formula.Should().Be("18.4 kg × 0.005 mg/kg = 0.092 mg ÷ 0.5 mg/mL");
        calc.VolumeText.Should().Be("0.18 mL");
        calc.ReferenceText.Should().Be("0.001–0.01 mg/kg");
        calc.IsOutOfRange.Should().BeFalse();
    }
}

public class VitalsTests
{
    [Test]
    public void MapIsDerivedFromSystolicAndDiastolic()
    {
        Vitals.DeriveMap(120, 60).Should().Be(80);
        Vitals.DeriveMap(110, 62).Should().Be(78);
        Vitals.DeriveMap(null, 60).Should().BeNull();
    }

    [Test]
    public void EnteredMapWinsOverDerived()
    {
        var r = Reading(sys: 120, dia: 60, map: 85);
        r.EffectiveMap.Should().Be(85);
        r.MapIsCalculated.Should().BeFalse();
        (r with { Map = null }).MapIsCalculated.Should().BeTrue();
    }

    [Test]
    public void FelineHeartRateRangeDiffersFromCanine()
    {
        Vitals.Classify(Species.Canine, VitalKind.Hr, 180).Should().Be(RangeClass.High);
        Vitals.Classify(Species.Feline, VitalKind.Hr, 180).Should().Be(RangeClass.Normal);
    }

    [Test]
    public void AlarmsListEveryOutOfRangeValueAndIgnoreBlanks()
    {
        var r = Reading(hr: 45, spo2: 91, sys: 85, dia: 40) with { EtCo2 = null };
        var alarms = Vitals.Alarms(Species.Canine, r);
        alarms.Should().Contain((VitalKind.Hr, RangeClass.Low));
        alarms.Should().Contain((VitalKind.SpO2, RangeClass.Low));
        alarms.Should().Contain((VitalKind.Sys, RangeClass.Low));
        alarms.Should().Contain((VitalKind.Map, RangeClass.Low)); // (85 + 80) / 3 = 55
        alarms.Select(a => a.Kind).Should().NotContain(VitalKind.EtCo2);
    }

    internal static VitalsReading Reading(int? hr = 90, int? spo2 = 98, int? sys = 120, int? dia = 60, int? map = null, DateTimeOffset? at = null) =>
        new(at ?? DateTimeOffset.Now, hr, 12, spo2, 40, sys, dia, map, 37.5m, 2m, 1m, Plane.Surgical, "");
}

public class ScheduleTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 7, 9, 0, 0, TimeSpan.FromHours(-4));
    private static readonly TimeSpan Five = TimeSpan.FromMinutes(5);

    private static Case Anesthetized(params int[] readingMinutes) =>
        Case.New(new Patient("Knuckles", Species.Canine, "Lab", 18.4m, 2m, "M"), "Spay", "Dr", "Tech", T0.AddHours(-1)) with
        {
            Status = CaseStatus.Anesthetized,
            InducedAt = T0,
            Readings = readingMinutes.Select(m => VitalsTests.Reading(at: T0.AddMinutes(m))).ToImmutableList(),
        };

    [Test]
    public void FirstReadingIsDueOneIntervalAfterInduction()
    {
        Schedule.NextDue(Anesthetized(), Five).Should().Be(T0.AddMinutes(5));
    }

    [Test]
    public void NextReadingIsDueOneIntervalAfterTheLastOne()
    {
        Schedule.NextDue(Anesthetized(1, 6), Five).Should().Be(T0.AddMinutes(11));
    }

    [TestCase(2, DueState.Waiting)]
    [TestCase(4.6, DueState.Due)]
    [TestCase(5, DueState.Due)]
    [TestCase(5.1, DueState.Overdue)]
    public void DueStateFollowsTheClock(double minutes, DueState expected)
    {
        Schedule.State(Anesthetized(), Five, T0.AddMinutes(minutes)).Should().Be(expected);
    }

    [Test]
    public void NoScheduleWhenNotUnderAnesthesia()
    {
        var c = Anesthetized() with { Status = CaseStatus.Recovery };
        Schedule.State(c, Five, T0.AddMinutes(30)).Should().Be(DueState.None);
        Schedule.Progress(c, Five, T0.AddMinutes(30)).Should().Be(0);
    }

    [Test]
    public void ProgressFillsFromZeroToOneAndClamps()
    {
        var c = Anesthetized();
        Schedule.Progress(c, Five, T0.AddSeconds(150)).Should().BeApproximately(0.5, 1e-9);
        Schedule.Progress(c, Five, T0.AddMinutes(9)).Should().Be(1);
    }

    [Test]
    public void ClockFormatsMinutesAndHours()
    {
        Schedule.Clock(TimeSpan.FromSeconds(161)).Should().Be("2:41");
        Schedule.Clock(TimeSpan.FromSeconds(-40)).Should().Be("-0:40");
        Schedule.HoursMinutes(TimeSpan.FromMinutes(72)).Should().Be("01:12");
    }
}

public class CsvExportTests
{
    [Test]
    public void CsvHasOneRowPerEventInTimeOrder()
    {
        var c = SampleDay.Create(DateTimeOffset.Now).Single(x => x.Patient.Name == "Knuckles");
        var lines = CsvExport.ToCsv(c).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(1 + c.Readings.Count + c.Doses.Count);
        lines[1].Should().Contain(",dose,").And.Contain("Dexmedetomidine");
        lines.Count(l => l.Contains(",reading,")).Should().Be(c.Readings.Count);
    }

    [Test]
    public void NotesWithCommasAndQuotesAreEscaped()
    {
        var c = SampleDay.Create(DateTimeOffset.Now)[0];
        c = c with { Readings = [VitalsTests.Reading() with { Note = "Moved, \"repositioned\"" }], Doses = [] };
        CsvExport.ToCsv(c).Should().Contain("\"Moved, \"\"repositioned\"\"\"");
    }

    [Test]
    public void SummaryNamesThePatientAndSigner()
    {
        var signed = SampleDay.Create(DateTimeOffset.Now).Single(x => x.IsSigned);
        var text = CsvExport.ToSummary(signed);
        text.Should().Contain("Pepper").And.Contain("Signed by Dr. Bergeron");
    }
}
