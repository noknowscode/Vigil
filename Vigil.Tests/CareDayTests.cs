using Vigil.Presentation;
namespace Vigil.Tests;

public class CareDayTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 14, 0, 0, TimeSpan.FromHours(-4));

    [Test]
    public void RulerUsesActualIntervalsAndExcludesUntimedPreparation()
    {
        var cases = SampleDay.Create(Now);
        var day = CareDay.From(cases, Now);
        day.Spans.Should().HaveCount(5);
        foreach (var c in cases.Where(c => c.InducedAt is not null))
        {
            var anaesthesia = day.Spans.Single(x => x.CaseId == c.Id && x.Phase == CarePhase.Anaesthesia);
            anaesthesia.Start.Should().Be(c.InducedAt);
            anaesthesia.End.Should().Be(c.EndedAt ?? Now);
            anaesthesia.Ongoing.Should().Be(c.EndedAt is null);
        }
        day.Spans.Should().NotContain(x => cases.Single(c => c.Id == x.CaseId).Status == CaseStatus.Scheduled);
    }

    [Test]
    public void RecoveryEndsAtSignOffOrNow()
    {
        var cases = SampleDay.Create(Now);
        var day = CareDay.From(cases, Now);
        foreach (var c in cases.Where(c => c.EndedAt is not null))
        {
            var recovery = day.Spans.Single(x => x.CaseId == c.Id && x.Phase == CarePhase.Recovery);
            recovery.Start.Should().Be(c.EndedAt);
            recovery.End.Should().Be(c.SignedAt ?? Now);
            recovery.Ongoing.Should().Be(c.SignedAt is null);
        }
    }

    [Test]
    public void OvernightCareKeepsItsOriginalTimestampWhenClipped()
    {
        var cases = SampleDay.Create(Now.AddDays(-1));
        var day = CareDay.From(cases, Now);
        var midnight = new DateTimeOffset(Now.Date, Now.Offset);
        day.Spans.Should().HaveCount(2).And.OnlyContain(x => x.Start == midnight && x.End == Now && x.Continued);
        day.Spans.Should().OnlyContain(x => x.RecordedStart < midnight);
        day.Start.Should().Be(midnight);
    }

    [Test]
    public void OverlappingIntervalsUseSeparateLanesWithoutFalseExtraColumns()
    {
        var day = CareDay.From(SampleDay.Create(Now), Now);
        var slots = day.Layout();
        slots.Max(x => x.Lane).Should().Be(1);
        foreach (var lane in slots.GroupBy(x => x.Lane))
        {
            var ordered = lane.OrderBy(x => x.Top).ToArray();
            for (var i = 1; i < ordered.Length; i++)
                (ordered[i].Top - ordered[i-1].Top - ordered[i-1].Height).Should().BeGreaterThanOrEqualTo(3.99);
        }
    }

    [Test]
    public void ShortIntervalsRemainAccessibleAndKeepExactTimes()
    {
        var c = SampleDay.Create(Now).First() with { InducedAt = Now.AddMinutes(-1), EndedAt = Now.AddSeconds(-30), SignedAt = Now };
        var day = CareDay.From([c], Now);
        day.Layout().Should().OnlyContain(x => x.Height >= 48);
        day.Spans.First().End.Subtract(day.Spans.First().Start).Should().Be(TimeSpan.FromSeconds(30));
        day.Layout().Select(x => x.Lane).Should().OnlyHaveUniqueItems();
    }

    [Test]
    public void EmptyAndFutureRecordsDoNotInventCare()
    {
        var future = SampleDay.Create(Now.AddDays(1));
        var day = CareDay.From(future, Now);
        day.IsEmpty.Should().BeTrue();
        day.Start.Should().Be(Now.AddHours(-1));
        day.End.Should().Be(Now.AddHours(1));
        day.NowTop.Should().Be(CareDay.PixelsPerHour);
    }
}
