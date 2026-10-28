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

    /// <summary>Creates or replaces a case. Throws <see cref="SignedRecordException"/> when the stored case is signed.</summary>
    ValueTask SaveAsync(Case value, CancellationToken ct);

    /// <summary>Reads, transforms and saves one case. Returns the saved value.</summary>
    ValueTask<Case> UpdateAsync(Guid id, Func<Case, Case> update, CancellationToken ct);

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
    private readonly string _folder;
    private readonly IFaultInjection _fault;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ImmutableDictionary<Guid, Case>? _cache;
    public Signal Changed { get; } = new();

    public void Reload() => Notify();

    public JsonCaseStore(string folder, IFaultInjection fault)
    {
        _folder = folder;
        _fault = fault;
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
            if (cache.TryGetValue(value.Id, out var existing) && existing.IsSigned)
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
            if (existing.IsSigned)
            {
                throw new SignedRecordException(id);
            }
            updated = update(existing);
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
            }
            _cache = ImmutableDictionary<Guid, Case>.Empty;
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
            foreach (var file in Directory.EnumerateFiles(_folder, "*.json"))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await using var stream = File.OpenRead(file);
                    if (await JsonSerializer.DeserializeAsync(stream, VigilJsonContext.Default.Case, ct) is { } c)
                    {
                        builder[c.Id] = c;
                    }
                }
                catch (JsonException ex)
                {
                    throw new StoreReadException($"Saved record {Path.GetFileName(file)} is damaged.", ex);
                }
            }
        }
        _cache = builder.ToImmutable();
        return _cache;
    }

    private async ValueTask WriteAsync(Case value, CancellationToken ct)
    {
        if (_fault.WriteFault)
        {
            throw new IOException("The record could not be saved (injected fault).");
        }
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, $"{value.Id:N}.json");
        var temp = path + ".tmp";
        await using (var stream = File.Create(temp))
        {
            await JsonSerializer.SerializeAsync(stream, value, VigilJsonContext.Default.Case, ct);
        }
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>Incremented on every change notification (observable in tests; the Signal itself is not).</summary>
    public long Version { get; private set; }

    private void Notify()
    {
        Version++;
        Changed.Raise();
    }
}
