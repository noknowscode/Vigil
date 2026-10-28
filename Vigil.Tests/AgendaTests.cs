using Vigil.Presentation;
namespace Vigil.Tests;

public class AgendaTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 14, 0, 0, TimeSpan.FromHours(-4));

    [Test]
    public void TimelineUsesActualMilestonesAndKeepsPreparingVisitsUntimed()
    {
        var day = SampleDay.Create(Now);
        var agenda = BoardAgenda.From(day, Now);
        agenda.Events.Should().HaveCount(3).And.BeInAscendingOrder(x => x.At);
        agenda.Preparing.Should().HaveCount(1).And.OnlyContain(x => x.Time == "—");
        var bella = day.Single(c => c.Patient.Name == "Bella");
        agenda.Events.Single(e => e.Id == bella.Id).At.Should().Be(bella.InducedAt);
    }

    [Test]
    public void YesterdayCompletedVisitsStayOutButOngoingCareCarriesForward()
    {
        var agenda = BoardAgenda.From(SampleDay.Create(Now.AddDays(-1)), Now);
        agenda.Events.Should().HaveCount(2).And.OnlyContain(e => !e.IsComplete);
        agenda.Events.Should().OnlyContain(e => e.Time.Contains("26 Oct"));
    }

    [Test]
    public void ReminderPrioritizesTheEarliestDeadlineAcrossPatients()
    {
        var bella = SampleDay.Create(Now).Single(c => c.Patient.Name == "Bella");
        var later = bella with { Id = Guid.NewGuid(), Readings = [], InducedAt = Now };
        var reminder = AgendaReminder.From([later, bella], Now, TimeSpan.FromMinutes(5));
        reminder!.Id.Should().Be(bella.Id);
        reminder.IsOverdue.Should().BeTrue();
        AgendaReminder.From([later], Now, TimeSpan.FromMinutes(5))!.IsWaiting.Should().BeTrue();
    }

    [Test]
    public void EmptyAgendaDoesNotInventAppointmentsOrReminders()
    {
        var agenda = BoardAgenda.From([], Now);
        agenda.NoEvents.Should().BeTrue();
        agenda.NoPreparing.Should().BeTrue();
        AgendaReminder.From([], Now, TimeSpan.FromMinutes(5)).Should().BeNull();
    }
}
