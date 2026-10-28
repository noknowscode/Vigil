using Vigil.Controls;
using Vigil.Presentation;

namespace Vigil.Tests;

/// <summary>Today view models against the prototype moment (12:42:26, today-dashboard-values.md).</summary>
public class TodayViewTests
{
    private static readonly IImmutableSet<Guid> None = ImmutableHashSet<Guid>.Empty;

    [Test]
    public void FilterTabsCountInClinicSignedOutAndAll()
    {
        Today.Tabs(Proto.Day, CardFilter.InClinic, Proto.Now)
            .Select(t => $"{t.Label} {t.Count} {t.IsSelected}")
            .Should().Equal("In clinic 3 True", "Signed out 1 False", "All 4 False");
    }

    [Test]
    public void CardsMatchThePrototypeInOrder()
    {
        var cards = Today.Cards(Proto.Day, CardFilter.InClinic, None, Proto.Now, reduceMotion: false);
        cards.Select(c => $"{c.Name} | {c.Sub} | {c.Asa} | {c.StripLabel} {c.StripValue} | {c.VisualStates}").Should().Equal(
            "Bella | English bulldog · 2 yrs | ASA II | ANESTHESIA 1:12 | Live,StageSurgery,Collapsed",
            "Milo | Domestic shorthair · 1 yr | ASA I | IN RECOVERY 0:34 | Recovery,StageRecovery,Collapsed",
            "Oscar | Maine coon · 4 yrs | ASA pending | BOOKED Not started | Booked,StageBooked,Collapsed");
        cards[2].AsaPending.Should().BeTrue();
        cards[0].HasSparkline.Should().BeTrue("the live card draws recorded heart rate");
        cards[1].HasSparkline.Should().BeFalse();
        cards.Select(c => c.StageIndex).Should().Equal(2, 3, 0);
        cards[0].StageName.Should().Be("Stage 3 of 5: surgery");
        Today.Cards(Proto.Day, CardFilter.SignedOut, None, Proto.Now, false).Single().StripLabel.Should().Be("SIGNED OUT");
    }

    [Test]
    public void DetailsOpenPerCardAndReduceMotionUsesTheStillStates()
    {
        var day = Proto.Day;
        var bella = day.Single(c => c.Patient.Name == "Bella");
        var cards = Today.Cards(day, CardFilter.InClinic, ImmutableHashSet.Create(bella.Id), Proto.Now, reduceMotion: true);
        cards[0].VisualStates.Should().Be("LiveStill,StageSurgery,ExpandedStill");
        cards[0].ToggleText.Should().Be("Hide details");
        cards[1].IsExpanded.Should().BeFalse();
    }

    [Test]
    public void SparklineScalesRecordedHeartRateIntoTheStrip()
    {
        var path = Today.Sparkline(Proto.Named("Bella").Readings);
        path.Should().StartWith("M0,").And.EndWith(",2", "the last reading (94) is the highest of the last 12");
        path.Split('L').Should().HaveCount(12);
        Today.Sparkline([]).Should().BeEmpty();
    }

    [Test]
    public void GanttRowsSegmentsAndNowDots()
    {
        var timeline = CareTimeline.From(Proto.Day, Proto.Now, 25)!;
        timeline.Rows.Select(r => r.Name).Should().Equal("Pepper", "Milo", "Bella", "Oscar");
        timeline.Ticks.Select(t => t.Label).Should().Equal("08:00", "09:00", "10:00", "11:00", "12:00", "13:00", "14:00", "15:00");
        timeline.NowText.Should().Be("Now 12:42");
        timeline.NowFraction.Should().BeApproximately(282.43 / 420, 0.001);
        timeline.TotalText.Should().Be("7h 16m");

        var pepper = timeline.Rows[0].Marks;
        pepper[0].Start.Should().BeApproximately(0, 0.002, "08:00:26 on the sample day");
        pepper[0].Length.Should().BeApproximately(32.0 / 420, 1e-6);
        pepper.Select(m => m.Gap).Should().Equal(2, 2, 0);
        pepper.Should().NotContain(m => m.IsDot, "Pepper went home");

        timeline.Rows[1].Marks.Single(m => m.IsDot).IsRecovery.Should().BeTrue();
        timeline.Rows[2].Marks.Single(m => m.IsDot).IsAnesthesia.Should().BeTrue();
        var oscar = timeline.Rows[3].Marks;
        oscar.Should().NotContain(m => m.IsDot, "waiting is not a running phase");
        oscar.Where(m => m.IsPlanned).Select(m => m.Start * 420).Should().BeEquivalentTo([330.0, 360.0], "planned prep 13:30, surgery 14:00");
        oscar.First(m => m.IsWaiting).Gap.Should().Be(0, "waiting ends now, the planned slot starts later");
    }

    [Test]
    public void HeroShowsTheLiveCaseThenTheNextSurgery()
    {
        var live = HeroCard.From(Proto.Day, Proto.Now, Proto.Interval, 25)!;
        live.IsLive.Should().BeTrue();
        live.Title.Should().Be("In surgery · Bella");
        live.Subtitle.Should().Be("Ovariohysterectomy (spay) · OR 1");
        live.Timer.Should().Be("1:12");
        live.ChipText.Should().Be("Reading overdue 1:26");
        live.ShowErrorChip.Should().BeTrue();
        new[] { live.First, live.Second, live.Third }.Select(s => $"{s.Label} {s.Value} {s.Sub}").Should().Equal(
            "Induced 11:30 1h 12m ago", "Last reading 12:36 HR 94 · SpO₂ 97%", "Next due 12:41 1:26 late");

        var next = HeroCard.From(Proto.Day.Where(c => c.Patient.Name != "Bella"), Proto.Now, Proto.Interval, 25)!;
        next.IsNext.Should().BeTrue();
        next.Title.Should().Be("Next surgery · Oscar");
        next.Timer.Should().Be("1:18");
        next.TimerCaption.Should().Be("until 14:00");
        next.ShowReadyChip.Should().BeTrue();
        next.ChipNote.Should().Be("Fasting since 06:00");
        next.First.Value.Should().Be("13:30");
        next.First.Sub.Should().Be("in 48 min");
        next.Third.Sub.Should().Be("notified 12:30");
        next.CanStartPrep.Should().BeTrue();

        HeroCard.From([Proto.Named("Pepper")], Proto.Now, Proto.Interval, 25).Should().BeNull();
    }

    [Test]
    public void DueRowsRouteToTheirScreensAndEmptyReadsAllCaughtUp()
    {
        var rows = DueTasks.Of(Proto.Day, Proto.Now, Proto.Interval, Proto.Formulary).Select(DueRow.From).ToList();
        rows.Select(r => r.Route).Should().Equal("Record", "Record", "Dose", "Record", "SignOff");
        rows[3].IsReady.Should().BeTrue();
        rows[4].IsHome.Should().BeTrue();

        var empty = DueHeader.From([], Proto.Day, Proto.Now, 25);
        empty.Summary.Should().Be("Nothing due before 14:00");
        empty.NextUp.Should().Be("Next: Oscar prep at 13:30");
    }

    [Test]
    public void CareTimeAndEmptyCopy()
    {
        var care = CareTimeCard.From(Proto.Day, Proto.Now, 25)!;
        care.Bars.Select(b => $"{b.Label} {b.DurationText} {b.PercentText}").Should().Equal(
            "Anesthesia 4h 13m 58%", "Prep 1h 34m 22%", "Recovery 1h 14m 17%", "Waiting 0h 15m 3%");
        care.Insight.Should().Be("Prep ran 31 minutes on average today, 6 minutes over plan.");
        CareTimeCard.From([], Proto.Now, 25).Should().BeNull();

        CardsEmpty.From([], CardFilter.InClinic, Proto.Now).ShowActions.Should().BeTrue();
        CardsEmpty.From(Proto.Day, CardFilter.SignedOut, Proto.Now).ShowActions.Should().BeFalse();
    }

    [Test]
    public void CardGridFitsThreeTwoOrOneColumns()
    {
        CardColumnsLayout.ColumnsFor(1090, 320, 24, 3).Should().Be(3);
        CardColumnsLayout.ColumnsFor(834, 320, 24, 3).Should().Be(2);
        CardColumnsLayout.ColumnsFor(655, 320, 24, 3).Should().Be(1);
        CardColumnsLayout.ColumnsFor(2400, 320, 24, 3).Should().Be(3);
    }
}
