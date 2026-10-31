namespace Vigil.Tests;

public class CaseStoreTests
{
    private string _folder = "";

    [SetUp]
    public void SetUp() => _folder = Path.Combine(Path.GetTempPath(), "vigil-tests", Guid.NewGuid().ToString("N"));

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private JsonCaseStore Store(string? fault = null) => new(_folder, new FaultInjection(fault, 0));

    private static async Task SeedAsync(ICaseStore store)
    {
        foreach (var c in SampleDay.Create(DateTimeOffset.Now))
        {
            await store.SaveAsync(c, CancellationToken.None);
        }
    }

    [Test]
    public async Task SavedCasesRoundTripThroughDisk()
    {
        await SeedAsync(Store());

        var reloaded = await Store().GetAllAsync(CancellationToken.None);
        reloaded.Should().HaveCount(4);
        var bella = reloaded.Single(c => c.Patient.Name == "Knuckles");
        bella.Readings.Should().HaveCount(13);
        bella.Doses.Should().HaveCount(4);
        bella.Preop.Asa.Should().Be(2);
        bella.Status.Should().Be(CaseStatus.Anesthetized);
    }

    [Test]
    public async Task ActiveCasesSortFirst()
    {
        var store = Store();
        await SeedAsync(store);
        var all = await store.GetAllAsync(CancellationToken.None);
        all[0].Status.Should().Be(CaseStatus.Anesthetized);
        all[^1].Status.Should().Be(CaseStatus.Signed);
    }

    [Test]
    public async Task SignedRecordsRejectChanges()
    {
        var store = Store();
        var signed = SampleDay.Create(DateTimeOffset.Now).Single(c => c.IsSigned);
        await store.SaveAsync(signed, CancellationToken.None);

        var update = async () => await store.UpdateAsync(signed.Id, c => c with { Procedure = "Changed" }, CancellationToken.None);
        await update.Should().ThrowAsync<SignedRecordException>();
        var save = async () => await store.SaveAsync(signed with { Procedure = "Changed" }, CancellationToken.None);
        await save.Should().ThrowAsync<SignedRecordException>();
        (await store.GetAsync(signed.Id, CancellationToken.None))!.Procedure.Should().Be(signed.Procedure);
    }

    [Test]
    public async Task SignedRecordsStillTakeTheDischargeCallButNothingWithIt()
    {
        var store = Store();
        var signed = SampleDay.Create(DateTimeOffset.Now).Single(c => c.IsSigned);
        await store.SaveAsync(signed, CancellationToken.None);
        var at = DateTimeOffset.Now;

        var mixed = async () => await store.UpdateAsync(signed.Id, c => c.WithDischargeCallDone(at) with { Procedure = "Changed" }, CancellationToken.None);
        await mixed.Should().ThrowAsync<SignedRecordException>();
        await store.UpdateAsync(signed.Id, c => c.WithDischargeCallDone(at), CancellationToken.None);

        var stored = (await store.GetAsync(signed.Id, CancellationToken.None))!;
        stored.DischargeCall!.DoneAt.Should().Be(at);
        stored.Procedure.Should().Be(signed.Procedure);
        stored.IsSigned.Should().BeTrue();
    }

    [Test]
    public async Task ReadFaultOnceThenRecovers()
    {
        await Store().SaveAsync(SampleDay.Create(DateTimeOffset.Now)[0], CancellationToken.None);

        var store = Store("store-read-once");
        var first = async () => await store.GetAllAsync(CancellationToken.None);
        await first.Should().ThrowAsync<StoreReadException>();
        (await store.GetAllAsync(CancellationToken.None)).Should().HaveCount(1);
    }

    [Test]
    public async Task WriteFaultLeavesTheStoreUnchanged()
    {
        var store = Store("store-write");
        var save = async () => await store.SaveAsync(SampleDay.Create(DateTimeOffset.Now)[0], CancellationToken.None);
        await save.Should().ThrowAsync<IOException>();
        (await store.GetAllAsync(CancellationToken.None)).Should().BeEmpty();
    }

    [Test]
    public async Task EveryWriteRaisesTheChangeSignal()
    {
        var store = Store();
        await store.SaveAsync(SampleDay.Create(DateTimeOffset.Now)[0], CancellationToken.None);
        await store.ClearAsync(CancellationToken.None);
        store.Reload();
        store.Version.Should().Be(3);
    }

    [Test]
    public async Task ClearRemovesEveryCase()
    {
        var store = Store();
        await SeedAsync(store);
        await store.ClearAsync(CancellationToken.None);
        (await Store().GetAllAsync(CancellationToken.None)).Should().BeEmpty();
    }
}

public class FormularyTests
{
    [Test]
    public async Task EmbeddedFormularyLoadsSortedDrugs()
    {
        var drugs = await new EmbeddedFormulary().GetDrugsAsync(CancellationToken.None);
        drugs.Should().HaveCountGreaterThan(10);
        drugs.Select(d => d.Name).Should().BeInAscendingOrder();
        drugs.Should().OnlyContain(d => d.MgPerMl > 0 && d.CanineMinMgPerKg <= d.DefaultMgPerKg && d.DefaultMgPerKg <= d.CanineMaxMgPerKg);
    }
}
