using System.Collections.Immutable;

namespace Vigil.Domain;

/// <summary>
/// A believable surgical day for demos and screenshots (Settings › Load sample day). Times are relative to
/// <c>now</c> and reproduce the v2 prototype when now is 12:42:26: Bella in OR 1 since 11:30, Milo recovering since
/// 12:08, Oscar booked for 14:00 and Pepper signed out at 10:07 with a discharge call at 14:00.
/// </summary>
public static class SampleDay
{
    public static IReadOnlyList<Case> Create(DateTimeOffset now)
    {
        var bella = Live(now);
        var milo = Recovering(now);
        var pepper = Signed(now);
        var oscar = Booked(now);
        return [bella, milo, oscar, pepper];
    }

    private static Case Booked(DateTimeOffset now)
    {
        var p = new Patient("Oscar", Species.Feline, "Maine coon", 6.8m, 4m, "L. Moreau") { PhotoAsset = "oscar-profile.jpg" };
        var planned = Minute(now.AddMinutes(78));
        return Case.New(p, "Mass removal, left flank", "Dr. Okafor", "S. Tremblay", now.AddMinutes(-15)) with
        {
            OperatingRoom = "OR 1",
            PlannedStart = planned,
            PlannedMinutes = 60,
            FastingSince = now.AddHours(-6).AddMinutes(-42),
            ParentNotifiedAt = now.AddMinutes(-12),
            Labs = [new LabResult("CBC and chemistry panel", LabState.Ready, Minute(now.AddMinutes(33)))],
        };
    }

    /// <summary>Planned times sit on whole minutes, as a booking would.</summary>
    private static DateTimeOffset Minute(DateTimeOffset t) => t.AddTicks(-(t.Ticks % TimeSpan.TicksPerMinute));

    private static PreopCheck FullCheck(int asa) => new(true, true, true, true, true, asa, false, "");

    private static Case Live(DateTimeOffset now)
    {
        var induced = now.AddMinutes(-72).AddSeconds(-26);
        var readings = ImmutableList.CreateBuilder<VitalsReading>();
        var hr = new[] { 104, 98, 92, 90, 88, 92, 94, 90, 88, 86, 90, 92, 92, 94 };
        for (var i = 0; i < hr.Length; i++)
        {
            var sys = 118 - i % 4 * 3;
            var dia = 64 - i % 3 * 2;
            readings.Add(new VitalsReading(induced.AddMinutes(5 * i + 1), hr[i], 12 - i % 3, 98 - i % 2, 38 + i % 4, sys, dia, null,
                37.8m - i * 0.05m, 2.0m + (i > 2 ? 0.5m : 0), 1.0m, i < 2 ? Plane.Light : Plane.Surgical, i == 4 ? "First incision" : ""));
        }
        var p = new Patient("Bella", Species.Canine, "English bulldog", 18.4m, 2m, "M. Lavoie") { PhotoAsset = "bella-english-bulldog.jpg", HeroAsset = "bella-outdoor-hero.jpg" };
        var c = Case.New(p, "Ovariohysterectomy (spay)", "Dr. Okafor", "S. Tremblay", induced.AddMinutes(-30));
        return c with
        {
            Status = CaseStatus.Anesthetized,
            OperatingRoom = "OR 1",
            PlannedStart = induced,
            PlannedMinutes = 90,
            PrepStartedAt = induced.AddMinutes(-30),
            Fluids = [new FluidRun("LRS", 5m, induced, null)],
            Preop = FullCheck(2),
            InducedAt = induced,
            Readings = readings.ToImmutable(),
            Doses =
            [
                new DoseGiven(Guid.NewGuid(), induced.AddMinutes(-30), "dexmedetomidine", "Dexmedetomidine", 0.005m, 0.5m, Dosing.VolumeMl(18.4m, 0.005m, 0.5m), DoseRoute.IM, false),
                new DoseGiven(Guid.NewGuid(), induced.AddMinutes(-30), "hydromorphone", "Hydromorphone", 0.1m, 2m, Dosing.VolumeMl(18.4m, 0.1m, 2m), DoseRoute.IM, false),
                new DoseGiven(Guid.NewGuid(), induced, "propofol", "Propofol", 4m, 10m, Dosing.VolumeMl(18.4m, 4m, 10m), DoseRoute.IV, false),
                new DoseGiven(Guid.NewGuid(), induced.AddMinutes(10), "cefazolin", "Cefazolin", 22m, 100m, Dosing.VolumeMl(18.4m, 22m, 100m), DoseRoute.IV, false),
            ],
        };
    }

    private static Case Recovering(DateTimeOffset now)
    {
        var induced = now.AddMinutes(-160);
        var ended = now.AddMinutes(-34);
        var readings = Enumerable.Range(0, 25).Select(i => new VitalsReading(induced.AddMinutes(5 * i + 1), 150 - i % 5 * 4, 14, 97, 40, 112, 66, null,
            37.6m - i * 0.02m, 1.5m, 1.0m, Plane.Surgical, "")).ToImmutableList();
        var p = new Patient("Milo", Species.Feline, "Domestic shorthair", 4.2m, 1m, "J. Roy") { PhotoAsset = "milo-profile.jpg" };
        return Case.New(p, "Castration", "Dr. Bergeron", "A. Nguyen", induced.AddMinutes(-32)) with
        {
            Status = CaseStatus.Recovery,
            OperatingRoom = "OR 2",
            PlannedStart = induced,
            PlannedMinutes = 120,
            PrepStartedAt = induced.AddMinutes(-32),
            Preop = FullCheck(1),
            InducedAt = induced,
            EndedAt = ended,
            Readings = readings,
            Recovery = new RecoveryLog(ended.AddMinutes(4), null, 1, 37.1m, "")
            {
                PainScores = [new PainScore(now.AddMinutes(-22), 2), new PainScore(now.AddMinutes(-7), 1)],
            },
        };
    }

    private static Case Signed(DateTimeOffset now)
    {
        var induced = now.AddHours(-4).AddMinutes(-10);
        var ended = induced.AddMinutes(55);
        var readings = Enumerable.Range(0, 11).Select(i => new VitalsReading(induced.AddMinutes(5 * i + 1), 96 - i, 10, 98, 37, 120, 70, null,
            37.9m, 2.0m, 1.0m, Plane.Surgical, "")).ToImmutableList();
        var p = new Patient("Pepper", Species.Canine, "Miniature schnauzer", 8.1m, 9m, "K. Gagnon");
        return Case.New(p, "Cystotomy", "Dr. Bergeron", "A. Nguyen", induced.AddMinutes(-32)) with
        {
            Status = CaseStatus.Signed,
            OperatingRoom = "OR 1",
            PlannedStart = induced,
            PlannedMinutes = 60,
            PrepStartedAt = induced.AddMinutes(-32),
            DischargeCall = new DischargeCall(Minute(now.AddMinutes(78)), null),
            Preop = FullCheck(2),
            InducedAt = induced,
            EndedAt = ended,
            Readings = readings,
            Recovery = new RecoveryLog(ended.AddMinutes(6), ended.AddMinutes(25), 1, 37.4m, "Smooth recovery."),
            SignedBy = "Dr. Bergeron",
            SignedAt = ended.AddMinutes(40),
        };
    }
}
