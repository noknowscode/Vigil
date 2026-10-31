using Vigil.Presentation;

namespace Vigil.Tests;

public class FluidTests
{
    private static Case Knuckles => Proto.Named("Knuckles");

    [Test]
    public void StoppingEndsTheRunAndTheCardSaysNoneRunning()
    {
        var stopped = Knuckles.WithFluidsStopped(Proto.Now);
        stopped.Fluids.Should().ContainSingle().Which.StoppedAt.Should().Be(Proto.Now);
        RecordText.Fluids(stopped).IsRunning.Should().BeFalse();
    }

    [Test]
    public void StartingWhileRunningIsARateChange()
    {
        var changed = Knuckles.WithFluidsStarted("LRS", 10m, Proto.Now);
        changed.Fluids.Should().HaveCount(2);
        changed.Fluids[0].StoppedAt.Should().Be(Proto.Now);
        changed.Fluids[1].Should().Be(new FluidRun("LRS", 10m, Proto.Now, null));
        RecordText.Fluids(changed).Text.Should().Be("Fluids running · LRS 10 mL/kg/h");
    }

    [Test]
    public void StoppingWithNothingRunningChangesNothing()
    {
        var milo = Proto.Named("Milo");
        milo.WithFluidsStopped(Proto.Now).Should().BeSameAs(milo);
    }

    [Test]
    public void RateOutsideTheRangeOrNoNameIsRejected()
    {
        FluentActions.Invoking(() => Knuckles.WithFluidsStarted("LRS", 0m, Proto.Now)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => Knuckles.WithFluidsStarted("LRS", 31m, Proto.Now)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => Knuckles.WithFluidsStarted(" ", 5m, Proto.Now)).Should().Throw<ArgumentException>();
    }
}

public class CaseDraftTests
{
    private static readonly CaseDraft Valid = new("Nova", 0, "Beagle", "12.5", "4", "J. Roy", "Dental", "Dr. Okafor", "S. Tremblay");

    [Test]
    public void SchedulingFieldsAreOptional()
    {
        Valid.Validate().Should().BeEmpty();
        var c = Valid.ToCase(Proto.Now);
        c.OperatingRoom.Should().BeEmpty();
        c.PlannedStart.Should().BeNull();
        c.PlannedMinutes.Should().BeNull();
        c.FastingSince.Should().BeNull();
        c.ParentNotifiedAt.Should().BeNull();
    }

    [Test]
    public void SchedulingFieldsReachTheCase()
    {
        var c = (Valid with { OperatingRoom = " OR 2 ", PlannedStartText = "14h30", PlannedMinutesText = "45", FastingSinceText = "6:15", ParentNotified = true }).ToCase(Proto.Now);
        c.OperatingRoom.Should().Be("OR 2");
        c.PlannedStart.Should().Be(Proto.At(14, 30));
        c.PlannedMinutes.Should().Be(45);
        c.FastingSince.Should().Be(Proto.At(6, 15));
        c.ParentNotifiedAt.Should().Be(Proto.Now);
    }

    [Test]
    public void AFastingTimeLaterThanNowWasLastNight()
    {
        var c = (Valid with { FastingSinceText = "2000" }).ToCase(Proto.Now);
        c.FastingSince.Should().Be(Proto.At(20, 0).AddDays(-1));
    }

    [Test]
    public void BadTimesAndLengthsAreReported()
    {
        (Valid with { PlannedStartText = "25:00" }).Validate().Should().Contain("Planned start is a time like 14:00.");
        (Valid with { FastingSinceText = "soon" }).Validate().Should().Contain("Fasting since is a time like 20:00.");
        (Valid with { PlannedStartText = "14:00", PlannedMinutesText = "2" }).Validate().Should().Contain("Planned length is 5 to 600 minutes.");
        (Valid with { PlannedMinutesText = "60" }).Validate().Should().Contain("Enter a planned start for the planned length.");
    }

    [Test]
    public void TimesAcceptTheUsualSpellings()
    {
        CaseDraft.ParseTime("14:00").Should().Be(new TimeSpan(14, 0, 0));
        CaseDraft.ParseTime("14h00").Should().Be(new TimeSpan(14, 0, 0));
        CaseDraft.ParseTime("930").Should().Be(new TimeSpan(9, 30, 0));
        CaseDraft.ParseTime("9:61").Should().BeNull();
        CaseDraft.ParseTime("noon").Should().BeNull();
    }
}

public class InitialsTests
{
    [Test]
    public void FirstNameAndSurnameGiveTwoInitials()
    {
        ShellSection.Initials("Amara", "Okafor").Should().Be("AO");
        ShellSection.Initials("amara", "van der Berg").Should().Be("AB");
        ShellSection.Initials("", "Okafor").Should().Be("O");
        ShellSection.Initials("Amara", "").Should().Be("A");
        ShellSection.Initials(" ", " ").Should().Be("C");
    }
}
