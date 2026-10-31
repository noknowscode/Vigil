using Vigil.Presentation;

namespace Vigil.Tests;

public class SignOffTests
{
    [Test]
    public void OnlyARecoveryCaseCanBeSigned()
    {
        var day = SampleDay.Create(DateTimeOffset.Now);
        SignOffSummary.From(day.Single(c => c.Status == CaseStatus.Recovery)).CanSign.Should().BeTrue();
        var live = SignOffSummary.From(day.Single(c => c.Status == CaseStatus.Anesthetized));
        live.CanSign.Should().BeFalse();
        live.Blocker.Should().Be("End anesthesia before signing.");
        live.Duration.Should().Be("—");
        SignOffSummary.From(day.Single(c => c.Status == CaseStatus.Recovery)).Duration.Should().MatchRegex(@"^\d\d:\d\d$");
        SignOffSummary.From(day.Single(c => c.IsSigned)).Blocker.Should().StartWith("Already signed by");
    }

    [Test]
    public void CountsUseSingularForOne()
    {
        var milo = SampleDay.Create(DateTimeOffset.Now).Single(c => c.Patient.Name == "Milo");
        var oneDose = milo with { Doses = [new DoseGiven(Guid.NewGuid(), DateTimeOffset.Now, "x", "X", 1, 1, 1, DoseRoute.IV, false)] };
        SignOffSummary.From(oneDose).Counts.Should().StartWith("1 dose ·");
    }

    [Test]
    public void RecoveryDraftAppliesTemperatureAndNotesAndLeavesThePainScore()
    {
        var scored = RecoveryLog.Empty.WithPainScore(2, DateTimeOffset.Now);
        var log = new RecoveryDraft("37,2", " ok ").ApplyTo(scored);
        log.TempC.Should().Be(37.2m);
        log.Notes.Should().Be("ok");
        log.PainScore.Should().Be(2);
        log.PainScores.Should().HaveCount(1);
        RecoveryDraft.From(log).Should().Be(new RecoveryDraft("37.2", "ok"));
    }

    [Test]
    public void EveryPainScoreIsTimedEvenWhenUnchanged()
    {
        var at = DateTimeOffset.Now;
        var log = RecoveryLog.Empty.WithPainScore(1, at).WithPainScore(1, at.AddMinutes(15));
        log.PainScore.Should().Be(1);
        log.PainScores.Select(p => p.At).Should().Equal(at, at.AddMinutes(15));
        FluentActions.Invoking(() => log.WithPainScore(5, at)).Should().Throw<ArgumentOutOfRangeException>();
    }
}
