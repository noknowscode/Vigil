using System.Text.Json.Nodes;
using Vigil.Presentation;

namespace Vigil.Tests;

public class CloverTests
{
    [Test]
    public async Task LegacyPatientWithoutPhotoFieldsKeepsItsClinicalRecord()
    {
        var folder = Path.Combine(Path.GetTempPath(), "clover-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonCaseStore(folder, new FaultInjection(null, 0));
            var sample = SampleDay.Create(DateTimeOffset.Now).Single(c => c.Patient.Name == "Bella");
            await store.SaveAsync(sample, CancellationToken.None);
            var file = Directory.GetFiles(folder, "*.json").Single();
            var json = JsonNode.Parse(await File.ReadAllTextAsync(file))!;
            json["Patient"]!.AsObject().Remove("PhotoAsset");
            json["Patient"]!.AsObject().Remove("HeroAsset");
            await File.WriteAllTextAsync(file, json.ToJsonString());
            var loaded = await new JsonCaseStore(folder, new FaultInjection(null, 0)).GetAsync(sample.Id, CancellationToken.None);
            loaded!.Readings.Should().BeEquivalentTo(sample.Readings);
            loaded.Doses.Should().BeEquivalentTo(sample.Doses);
            loaded.Patient.WeightKg.Should().Be(sample.Patient.WeightKg);
            PatientMedia.Photo(loaded.Patient).Should().BeNull("a familiar name must never imply a patient photograph");
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    [Test]
    public void SignedParentPreviewDoesNotPromiseDischargeOrInventVitals()
    {
        var signed = SampleDay.Create(DateTimeOffset.Now).Single(c => c.Status == CaseStatus.Signed);
        var preview = ParentSnapshot.From(signed);
        preview.Status.Should().Be("Record complete");
        preview.Next.Should().Contain("confirm");
        preview.Description.Should().Contain("wait");
        preview.Milestones.Should().HaveCount(5);
        preview.Milestones[^1].Time.Should().Be(signed.SignedAt!.Value.ToString("HH:mm"));
    }

    [Test]
    public void ScheduledParentPreviewKeepsFutureMilestonesPending()
    {
        var scheduled = SampleDay.Create(DateTimeOffset.Now).First(c => c.Status == CaseStatus.Scheduled);
        var preview = ParentSnapshot.From(scheduled);
        preview.Milestones.Skip(2).Should().OnlyContain(m => !m.Done && m.Time == "Pending");
        preview.Milestones[3].Detail.Should().Contain("will");
    }

    [Test]
    public void MediaCannotResolveUnlistedFilesOrAssignImagesByPatientName()
    {
        var patient = new Patient("Bella", Species.Canine, "English bulldog", 18.4m, 2m, "Owner");
        PatientMedia.Photo(patient).Should().BeNull();
        PatientMedia.Photo(patient with { PhotoAsset = "../preferences.json" }).Should().BeNull();
    }

    [Test]
    public void DoseAcknowledgementBelongsOnlyToTheRecordedPatient()
    {
        var feedback = new DoseFeedback();
        var id = Guid.NewGuid();
        feedback.PulseFor(id).Should().Be(0);
        feedback.Recorded(id);
        feedback.PulseFor(id).Should().BeGreaterThan(0);
        feedback.PulseFor(Guid.NewGuid()).Should().Be(0);
    }
}
