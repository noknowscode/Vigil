using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Uno.Extensions.Reactive;
using Vigil.Domain;

namespace Vigil.Services;

public interface ICaseStore
{
    ValueTask<ImmutableList<Case>> GetAllAsync(CancellationToken ct);

    ValueTask<Case?> GetAsync(Guid id, CancellationToken ct);

    /// <summary>Raised after every change, and by <see cref="Reload"/>. Feeds use it as their refresh signal.</summary>
    Signal Changed { get; }

    /// <summary>Drops a failed load so the next read retries, then raises <see cref="Changed"/> (Retry).</summary>
    void Reload();

    /// <summary>
    /// Case files that could not be read this session. Each was moved to the "damaged" folder beside the cases so the
    /// other records still load; the shell shows a warning while the list is not empty.
    /// </summary>
    IReadOnlyList<string> SetAside { get; }

    /// <summary>Creates or replaces a case. Throws <see cref="SignedRecordException"/> when the stored case is signed.</summary>
    ValueTask SaveAsync(Case value, CancellationToken ct);

    /// <summary>Reads, transforms and saves one case. Returns the saved value.</summary>
    ValueTask<Case> UpdateAsync(Guid id, Func<Case, Case> update, CancellationToken ct);

    /// <summary>
    /// Deletes an unsigned case (booked by mistake, or the patient is not coming); a missing case is already gone.
    /// Throws <see cref="SignedRecordException"/> for a signed record.
    /// </summary>
    ValueTask DeleteAsync(Guid id, CancellationToken ct);

    ValueTask ClearAsync(CancellationToken ct);
}

public sealed class SignedRecordException(Guid id) : InvalidOperationException($"Case {id} is signed and can no longer change.");

public sealed class StoreReadException(string message, Exception? inner = null) : IOException(message, inner);

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(Case))]
internal partial class VigilJsonContext : JsonSerializerContext;

/// <summary>One JSON file per case under a folder (the app's LocalFolder), with an in-memory cache.</summary>
public sealed class JsonCaseStore : ICaseStore
{
    /// <summary>Subfolder that unreadable case files are moved to (see <see cref="SetAside"/>).</summary>
    public const string DamagedFolder = "damaged";

    private readonly string _folder;
    private readonly IFaultInjection _fault;
    private readonly ILogger? _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ImmutableDictionary<Guid, Case>? _cache;
    public Signal Changed { get; } = new();

    public void Reload() => Notify();

    public IReadOnlyList<string> SetAside { get; private set; } = [];

    public JsonCaseStore(string folder, IFaultInjection fault, ILogger? logger = null)
    {
        _folder = folder;
        _fault = fault;
        _logger = logger;
    }

    public async ValueTask<ImmutableList<Case>> GetAllAsync(CancellationToken ct) => Order(await LoadAsync(ct));

    public async ValueTask<Case?> GetAsync(Guid id, CancellationToken ct)
    {
        if (_fault.RecordDelayMs > 0)
        {
            await Task.Delay(_fault.RecordDelayMs, ct);
        }
        if (_fault.ConsumeRecordFault())
        {
            throw new StoreReadException("This record could not be read (injected fault).");
        }
        return (await LoadAsync(ct)).GetValueOrDefault(id);
    }

    public async ValueTask SaveAsync(Case value, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var cache = await LoadCoreAsync(ct);
            if (cache.TryGetValue(value.Id, out var existing) && existing.IsSigned && !existing.OnlyFollowUpDiffers(value))
            {
                throw new SignedRecordException(value.Id);
            }
            await WriteAsync(value, ct);
            _cache = cache.SetItem(value.Id, value);
        }
        finally
        {
            _gate.Release();
        }
        Notify();
    }

    public async ValueTask<Case> UpdateAsync(Guid id, Func<Case, Case> update, CancellationToken ct)
    {
        Case updated;
        await _gate.WaitAsync(ct);
        try
        {
            var cache = await LoadCoreAsync(ct);
            if (!cache.TryGetValue(id, out var existing))
            {
                throw new KeyNotFoundException($"Case {id} does not exist.");
            }
            updated = update(existing);
            if (existing.IsSigned && !existing.OnlyFollowUpDiffers(updated))
            {
                throw new SignedRecordException(id);
            }
            await WriteAsync(updated, ct);
            _cache = cache.SetItem(id, updated);
        }
        finally
        {
            _gate.Release();
        }
        Notify();
        return updated;
    }

    public async ValueTask DeleteAsync(Guid id, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var cache = await LoadCoreAsync(ct);
            if (!cache.TryGetValue(id, out var existing))
            {
                return;
            }
            if (existing.IsSigned)
            {
                throw new SignedRecordException(id);
            }
            if (_fault.WriteFault)
            {
                throw new IOException("The record could not be deleted (injected fault).");
            }
            File.Delete(PathOf(id));
            _cache = cache.Remove(id);
        }
        finally
        {
            _gate.Release();
        }
        Notify();
    }

    public async ValueTask ClearAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (Directory.Exists(_folder))
            {
                foreach (var file in Directory.EnumerateFiles(_folder, "*.json"))
                {
                    File.Delete(file);
                }
                if (Directory.Exists(Path.Combine(_folder, DamagedFolder)))
                {
                    Directory.Delete(Path.Combine(_folder, DamagedFolder), recursive: true);
                }
            }
            _cache = ImmutableDictionary<Guid, Case>.Empty;
            SetAside = [];
        }
        finally
        {
            _gate.Release();
        }
        Notify();
    }

    private static ImmutableList<Case> Order(ImmutableDictionary<Guid, Case> cases) =>
        cases.Values.OrderBy(c => StatusOrder(c.Status)).ThenByDescending(c => c.CreatedAt).ToImmutableList();

    private static int StatusOrder(CaseStatus s) => s switch
    {
        CaseStatus.Anesthetized => 0,
        CaseStatus.Recovery => 1,
        CaseStatus.Scheduled => 2,
        _ => 3,
    };

    private async ValueTask<ImmutableDictionary<Guid, Case>> LoadAsync(CancellationToken ct)
    {
        if (_cache is { } cached)
        {
            return cached;
        }
        await _gate.WaitAsync(ct);
        try
        {
            return await LoadCoreAsync(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async ValueTask<ImmutableDictionary<Guid, Case>> LoadCoreAsync(CancellationToken ct)
    {
        if (_cache is { } cached)
        {
            return cached;
        }
        if (_fault.SlowMs > 0)
        {
            await Task.Delay(_fault.SlowMs, ct);
        }
        if (_fault.ConsumeReadFault())
        {
            throw new StoreReadException("The saved records could not be read (injected fault).");
        }

        var builder = ImmutableDictionary.CreateBuilder<Guid, Case>();
        if (Directory.Exists(_folder))
        {
            // ToList: a damaged file moves out of the folder during the loop.
            foreach (var file in Directory.EnumerateFiles(_folder, "*.json").ToList())
            {
                ct.ThrowIfCancellationRequested();
                if (await ReadAsync(file, ct) is { } c)
                {
                    builder[c.Id] = c;
                }
            }
        }
        _cache = builder.ToImmutable();
        return _cache;
    }

    /// <summary>
    /// One case file. A file that is not a readable case is moved to <see cref="DamagedFolder"/> and skipped, so one bad
    /// file never keeps the other records from loading (and Retry does not trip over it again).
    /// </summary>
    private async ValueTask<Case?> ReadAsync(string file, CancellationToken ct)
    {
        try
        {
            await using var stream = File.OpenRead(file);
            return await JsonSerializer.DeserializeAsync(stream, VigilJsonContext.Default.Case, ct);
        }
        catch (JsonException ex)
        {
            var damaged = Path.Combine(_folder, DamagedFolder);
            Directory.CreateDirectory(damaged);
            var target = Path.Combine(damaged, $"{Path.GetFileNameWithoutExtension(file)}-{DateTime.UtcNow:yyyyMMddHHmmss}.json");
            File.Move(file, target, overwrite: true);
            SetAside = [.. SetAside, Path.GetFileName(file)];
            _logger?.LogWarning(ex, "Case file {File} could not be read and was moved to {Target}.", file, target);
            return null;
        }
    }

    private async ValueTask WriteAsync(Case value, CancellationToken ct)
    {
        if (_fault.WriteFault)
        {
            throw new IOException("The record could not be saved (injected fault).");
        }
        Directory.CreateDirectory(_folder);
        var path = PathOf(value.Id);
        var temp = path + ".tmp";
        await using (var stream = File.Create(temp))
        {
            await JsonSerializer.SerializeAsync(stream, value, VigilJsonContext.Default.Case, ct);
        }
        File.Move(temp, path, overwrite: true);
    }

    private string PathOf(Guid id) => Path.Combine(_folder, $"{id:N}.json");

    /// <summary>Incremented on every change notification (observable in tests; the Signal itself is not).</summary>
    public long Version { get; private set; }

    private void Notify()
    {
        Version++;
        Changed.Raise();
    }
}
