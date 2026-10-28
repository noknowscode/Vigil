using Vigil.Presentation;

namespace Vigil.Tests;

/// <summary>Patient care view models against the prototype moment (12:42:26, patient-care-values.md).</summary>
public class RecordViewTests
{
    private static CaseView View(string name, string? pane = null) =>
        CaseView.From(Proto.Named(name), Proto.Now, Proto.Interval, Proto.Formulary, pane);

    [Test]
    public void ChartReproducesThePrototypeGeometry()
    {
        var chart = RecordChart.From(Proto.Named("Bella"), Proto.Now, Proto.Interval);
        chart.XLabels.Should().Equal("11:30", "11:45", "12:00", "12:15", "12:30", "12:45");
        chart.HrTicks.Should().Equal("110", "95", "80");
        chart.BpTicks.Should().Equal("130", "85", "40");
        chart.HrLine.Should().StartWith("M61.52,36.27 L84.45,39.73").And.EndWith("L336.72,39.73", "§3.4: 11:36 HR 96 at (61.5, 36.3), 12:36 HR 94 at (336.7, 39.7)");
        chart.BpBars.Should().StartWith("M61.52,110.8V140.93", "112/64: bar 108.8-142.9, inset 2 for the round caps");
        chart.DueLine.Should().Be("M359.65,8V160", "12:41 is 71 minutes after 11:30");
        chart.DueLabel.Should().Be("Due 12:41");
        chart.AccessibleName.Should().Be("Anesthesia record, 11:36 to 12:36. Heart rate 90 to 96 bpm. Blood pressure 102/58 to 112/64 mmHg. Next reading due at 12:41.");
    }

    [Test]
    public void ChartBandWidensForValuesOutsideThePrototypeRange()
    {
        RecordChart.Band([134, 150], 80, 110, 15).Should().Be((125, 155));
        RecordChart.Band([90, 96], 80, 110, 15).Should().Be((80, 110));
        RecordChart.Band([], 40, 130, 45).Should().Be((40, 130));
        var milo = RecordChart.From(Proto.Named("Milo"), Proto.Now, Proto.Interval);
        milo.HasDue.Should().BeFalse("recovery has no next reading");
        milo.HrTicks.Should().Equal("155", "140", "125");
    }

    [Test]
    public void ChartWindowSlidesToKeepTheLatestTimeInView()
    {
        var bella = Proto.Named("Bella");
        RecordChart.WindowStart(bella, Proto.Now, Proto.Interval).Should().Be(Proto.At(11, 30));
        RecordChart.WindowStart(bella, Proto.Now.AddMinutes(20), Proto.Interval).Should().Be(Proto.At(11, 30), "an overdue reading does not move the window");
        RecordChart.WindowStart(Proto.Named("Milo"), Proto.Now, Proto.Interval).Should().Be(Proto.At(11, 0), "readings to 12:03 end the window at 12:15, 75 minutes after 11:00");
    }

    [Test]
    public void SparklinesLandOnThePrototypeCoordinates()
    {
        var bella = View("Bella");
        bella.HrSpark.Data.Should().Be("M4,8.8 L16.67,13.6 L29.33,18.4 L42,23.2 L54.67,20.8 L67.33,16 L80,11.2 L92.67,13.6 L105.33,18.4 L118,16 L130.67,11.2 L143.33,8.8 L156,13.6");
        bella.HrSpark.Top.Should().BeApproximately(8.8, 0.001);
        bella.HrSpark.Height.Should().BeApproximately(14.4, 0.001);
        bella.HrSpark.EndY.Should().BeApproximately(13.6, 0.001);
        bella.SpO2Spark.Data.Should().Be("M4,12 L16.67,12 L29.33,16 L42,16 L54.67,12 L67.33,16 L80,20 L92.67,16 L105.33,16 L118,12 L130.67,16 L143.33,16 L156,16");
        Spark.From([97], 4).HasLine.Should().BeFalse();
        var flat = Spark.From([97, 97, 97], 4);
        flat.Height.Should().BeApproximately(4, 0.001, "a flat line still has height to stretch");
        flat.Data.Should().EndWith("M156,14 V18");
    }

    [Test]
    public void BellaMatchesThePrototypeCopy()
    {
        var v = View("Bella");
        v.Care.Chip.Should().Be("Under anesthesia");
        $"{v.Care.Label} {v.Care.Since} {v.Care.Timer}".Should().Be("UNDER ANESTHESIA since 11:30 1:12");
        $"{v.Care.FirstLabel} {v.Care.FirstValue} · {v.Care.SecondLabel} {v.Care.SecondValue}".Should().Be("Last reading 12:36 · Next due 12:41");
        v.Care.Alert.Should().Be("Reading overdue 1:26");
        v.Care.CtaPane.Should().Be("Monitor");
        v.OverdueTitle.Should().Be("Reading overdue by 1:26");
        v.OverdueDetail.Should().Be("Last recorded 28 Oct, 12:36 · readings due every 5 minutes");
        v.ProcedureMeta.Should().Be("Started 11:30 · OR 1 · Dr. Okafor, S. Tremblay");
        v.AgeOwner.Should().Be("2 years · M. Lavoie");
        $"{v.MedicationSummary} | {v.MedicationDetail}".Should().Be("Cefazolin | 4.05 mL · IV · 11:40");
        $"{v.NextDose.Time} | {v.NextDose.Detail}".Should().Be("13:10 | Cefazolin redose · in 28 min");
        v.Fluids.Should().Be(new FluidsText("Fluids running · LRS 5 mL/kg/h", true));
        v.CirculationDetail.Should().Be("BP 109/63 · MAP 78");
        v.RespirationDetail.Should().Be("EtCO₂ 38 mmHg · RR 12");
        $"{v.LastHr} {v.LastSpo2}".Should().Be("94 97");
    }

    [Test]
    public void RecordNavFollowsTheOpenPaneElseTheStage()
    {
        View("Bella").ActiveSection.Should().Be("Monitor", "Vitals is the page itself while the patient is asleep");
        View("Bella", "Drugs").ActiveSection.Should().Be("Drugs");
        View("Oscar").ActiveSection.Should().Be("Preop", "before induction the record opens on preparation");
    }

    [Test]
    public void StatusCardCoversEveryStage()
    {
        var milo = View("Milo").Care;
        $"{milo.Tone} {milo.Label} {milo.Since} {milo.Timer} {milo.SecondValue} {milo.CtaPane}".Should().Be("Recovery IN RECOVERY since 12:08 0:34 1 of 4 Recovery");
        var oscar = View("Oscar").Care;
        $"{oscar.Tone} {oscar.Label} {oscar.Since} {oscar.Timer} {oscar.FirstValue} {oscar.CtaPane}".Should().Be("Neutral BOOKED starts 14:00 1:18 0 of 5 Preop");
        var pepper = View("Pepper").Care;
        $"{pepper.Tone} {pepper.Label} {pepper.FirstValue} {pepper.CtaPane}".Should().Be("Neutral SIGNED OUT Dr. Bergeron History");
        View("Oscar").Fluids.IsRunning.Should().BeFalse();
        View("Oscar").NextDose.Detail.Should().Be("No redose scheduled");
        View("Oscar").Chart.HasReadings.Should().BeFalse();
    }
}
