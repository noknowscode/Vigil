using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vigil.Services;

public interface IFormulary
{
    ValueTask<ImmutableList<Drug>> GetDrugsAsync(CancellationToken ct);
}

internal sealed record FormularyFile(string Note, ImmutableList<Drug> Drugs);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(FormularyFile))]
internal partial class FormularyJsonContext : JsonSerializerContext;

/// <summary>Drug list from the embedded <c>Data/formulary.json</c> (sample reference data).</summary>
public sealed class EmbeddedFormulary : IFormulary
{
    private ImmutableList<Drug>? _drugs;

    public async ValueTask<ImmutableList<Drug>> GetDrugsAsync(CancellationToken ct)
    {
        if (_drugs is { } d)
        {
            return d;
        }
        await using var stream = typeof(EmbeddedFormulary).Assembly.GetManifestResourceStream("Vigil.Data.formulary.json")
            ?? throw new InvalidOperationException("Embedded formulary.json is missing.");
        var file = await JsonSerializer.DeserializeAsync(stream, FormularyJsonContext.Default.FormularyFile, ct)
            ?? throw new InvalidOperationException("formulary.json is empty.");
        _drugs = file.Drugs.OrderBy(x => x.Name).ToImmutableList();
        return _drugs;
    }
}
