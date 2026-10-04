using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MadlanExplorer;

public class IsraeliLocalityCatalog
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _localitiesFilePath;
    private IReadOnlyList<IsraeliLocality> _localities = [];
    private IReadOnlyDictionary<string, IsraeliLocality> _localitiesByName =
        new Dictionary<string, IsraeliLocality>(StringComparer.OrdinalIgnoreCase);

    public IsraeliLocalityCatalog(IOptions<DatasetOptions> options, IHostEnvironment environment)
    {
        var settings = options.Value;
        var contentRootLocalitiesFilePath = Path.Combine(environment.ContentRootPath, settings.LocalitiesFile);
        _localitiesFilePath = File.Exists(contentRootLocalitiesFilePath)
            ? contentRootLocalitiesFilePath
            : Path.Combine(AppContext.BaseDirectory, settings.LocalitiesFile);
    }

    public IReadOnlyList<IsraeliLocality> Localities => _localities;

    public void Load()
    {
        if (_localities.Count > 0)
        {
            return;
        }

        if (!File.Exists(_localitiesFilePath))
        {
            throw new InvalidOperationException($"Israeli localities file was not found at '{_localitiesFilePath}'.");
        }

        var localities = JsonSerializer.Deserialize<List<IsraeliLocality>>(File.ReadAllText(_localitiesFilePath), SerializerOptions)
            ?? throw new InvalidOperationException("Israeli localities file contains no localities.");

        if (localities.Count == 0 || localities.Any(locality => locality.Code <= 0 || string.IsNullOrWhiteSpace(locality.NameHe)))
        {
            throw new InvalidOperationException("Israeli localities file contains invalid localities.");
        }

        _localities = localities;
        _localitiesByName = BuildNameIndex(localities);
    }

    public string? FindCanonicalHebrewName(string name)
    {
        if (_localitiesByName.TryGetValue(NormalizeName(name), out var locality))
        {
            return locality.NameHe;
        }

        return null;
    }

    public IsraeliLocality? FindByCode(int code) => _localities.FirstOrDefault(locality => locality.Code == code);

    private static IReadOnlyDictionary<string, IsraeliLocality> BuildNameIndex(IEnumerable<IsraeliLocality> localities)
    {
        var names = new Dictionary<string, IsraeliLocality>(StringComparer.OrdinalIgnoreCase);
        foreach (var locality in localities)
        {
            names.TryAdd(NormalizeName(locality.NameHe), locality);

            if (!string.IsNullOrWhiteSpace(locality.NameEn))
            {
                names.TryAdd(NormalizeName(locality.NameEn), locality);
            }
        }

        return names;
    }

    private static string NormalizeName(string value) => string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}

public class IsraeliLocality
{
    public int Code { get; init; }

    public string NameHe { get; init; } = string.Empty;

    public string? NameEn { get; init; }

    public int? DistrictCode { get; init; }

    public string? DistrictNameHe { get; init; }

    public int? RegionalCouncilCode { get; init; }

    public string? RegionalCouncilNameHe { get; init; }
}
