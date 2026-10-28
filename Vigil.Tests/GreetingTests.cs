using Vigil.Presentation;
namespace Vigil.Tests;

public class GreetingTests
{
    private static readonly DateTimeOffset Afternoon = new(2026, 10, 27, 14, 0, 0, TimeSpan.FromHours(-4));
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    [Test]
    public void SurgeryLeadsTheRecapAndNamesTheOverdueReading()
    {
        var greeting = BoardGreeting.From(SampleDay.Create(Afternoon), Afternoon, Interval, "Okafor");
        greeting.Title.Should().Be("Good afternoon, Dr. Okafor");
        greeting.Lead.Should().Be("1 patient in surgery.");
        greeting.Rest.Should().Be(" Bella's reading is overdue · 2 in preparation · 1 recovering · 1 signed today.");
    }

    [Test]
    public void TitleFollowsTheClockAndNeverDoublesTheTitle()
    {
        var morning = Afternoon.AddHours(-5);
        BoardGreeting.From([], morning, Interval, "Dr. Okafor").Title.Should().Be("Good morning, Dr. Okafor");
        BoardGreeting.From([], Afternoon.AddHours(5), Interval, "dr okafor").Title.Should().Be("Good evening, Dr. okafor");
        BoardGreeting.From([], morning, Interval, "  ").Title.Should().Be("Good morning");
    }

    [Test]
    public void WithoutSurgeryTheNextActiveGroupLeadsAndIsNotRepeated()
    {
        var day = SampleDay.Create(Afternoon).Where(c => c.Status != CaseStatus.Anesthetized).ToList();
        var greeting = BoardGreeting.From(day, Afternoon, Interval, "");
        greeting.Lead.Should().Be("2 waiting for preparation.");
        greeting.Rest.Should().Be(" 1 recovering · 1 signed today.");
    }

    [Test]
    public void AnEmptyBoardSaysSoWithoutCounts()
    {
        var greeting = BoardGreeting.From([], Afternoon, Interval, "");
        greeting.Lead.Should().Be("No patients on the board yet.");
        greeting.Rest.Should().BeEmpty();
    }
}
