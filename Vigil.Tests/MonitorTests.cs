using Vigil.Presentation;

namespace Vigil.Tests;

public class VitalsDraftTests
{
    private static readonly VitalsReading Last = new(DateTimeOffset.Now, 92, 12, 98, 40, 118, 64, null, 37.6m, 2.5m, 1m, Plane.Surgical, "note");

    [Test]
    public void PrefillCopiesTheLastReadingButNotItsNote()
    {
        var d = VitalsDraft.From(Last);
        d.Hr.Should().Be("92");
        d.TempC.Should().Be("37.6");
        d.PlaneIndex.Should().Be((int)Plane.Surgical);
        d.Note.Should().BeEmpty();
    }

    [Test]
    public void AnUnchangedPadRecordsTheSameValues()
    {
        var r = VitalsDraft.From(Last).ToReading(DateTimeOffset.Now);
        r.Hr.Should().Be(92);
        r.Sys.Should().Be(118);
        r.Map.Should().BeNull();
        r.EffectiveMap.Should().Be(82);
    }

    [TestCase("Hr|+", "94")]
    [TestCase("Hr|-", "90")]
    [TestCase("Sys|+", "123")]
    public void StepsMoveByTheFieldIncrement(string arg, string expected)
    {
        var d = VitalsDraft.From(Last).Step(arg);
        (arg.StartsWith("Hr") ? d.Hr : d.Sys).Should().Be(expected);
    }

    [Test]
    public void TemperatureStepsByATenth()
    {
        VitalsDraft.From(Last).Step("TempC|-").TempC.Should().Be("37.5");
    }

    [Test]
    public void ABlankFieldStartsAtATypicalValueOnItsFirstTap()
    {
        VitalsDraft.Empty.Step("SpO2|+").SpO2.Should().Be("98");
        VitalsDraft.Empty.Step("Hr|-").Hr.Should().Be("80");
    }

    [Test]
    public void StepsClampToPhysicalLimits()
    {
        (VitalsDraft.From(Last) with { SpO2 = "100" }).Step("SpO2|+").SpO2.Should().Be("100");
    }

    [Test]
    public void ValidationRejectsTextAndAnEmptyPad()
    {
        (VitalsDraft.From(Last) with { Hr = "9x" }).Validate().Should().ContainSingle().Which.Should().Contain("Heart rate");
        VitalsDraft.Empty.Validate().Should().ContainSingle().Which.Should().Contain("at least one");
    }

    [Test]
    public void CommaDecimalsAreAccepted()
    {
        VitalsDraft.ParseDec("37,4").Should().Be(37.4m);
    }

    [Test]
    public void FlagsAreTextAndUseSpeciesRanges()
    {
        var d = VitalsDraft.From(Last) with { Hr = "180", Sys = "85", Map = "" };
        var dog = DraftFlags.From(d, Species.Canine);
        dog.Hr.Should().Be("▲ High");
        dog.Sys.Should().Be("▼ Low");
        dog.MapHint.Should().Be("calc 71");
        DraftFlags.From(d, Species.Feline).Hr.Should().BeEmpty();
    }
}
