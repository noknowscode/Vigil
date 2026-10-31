using Vigil.Presentation;
namespace Vigil.Tests;

public class CareWorkspaceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 14, 0, 0, TimeSpan.FromHours(-4));
    [Test]
    public void UnifiedWorkspaceFollowsSavedCarePhaseAndKeepsSignedRecordsReadOnly()
    {
        foreach (var c in SampleDay.Create(Now))
        {
            var view = CaseView.From(c, Now, TimeSpan.FromMinutes(5));
            view.HasAnaesthesiaRecord.Should().Be(c.InducedAt is not null);
            view.IsAnesthetized.Should().Be(c.Status == CaseStatus.Anesthetized);
            view.CanEditRecovery.Should().Be(c.Status == CaseStatus.Recovery);
            view.CanChange.Should().Be(c.Status != CaseStatus.Signed);
        }
    }
    [Test]
    public void PatientCareShortcutPrioritizesActiveCareAndHandlesAnEmptyBoard()
    {
        var cases = SampleDay.Create(Now).Reverse().ToArray();
        BoardDestination.Patient(cases)!.Patient.Name.Should().Be("Knuckles");
        BoardDestination.Patient(cases.Where(c => c.Status != CaseStatus.Anesthetized))!.Patient.Name.Should().Be("Milo");
        BoardDestination.Patient([]).Should().BeNull();
    }
    [Test]
    public void ReminderUsesTheSelectedPatientsPhotoAndNeverGuessesFromTheirName()
    {
        var bella = SampleDay.Create(Now).First();
        var reminder = AgendaReminder.From([bella], Now, TimeSpan.FromMinutes(5))!;
        reminder.Photo.Should().Be(PatientMedia.Photo(bella.Patient));
        var noPhoto = bella with { Patient = bella.Patient with { PhotoAsset = null } };
        AgendaReminder.From([noPhoto], Now, TimeSpan.FromMinutes(5))!.HasNoPhoto.Should().BeTrue();
    }
}
