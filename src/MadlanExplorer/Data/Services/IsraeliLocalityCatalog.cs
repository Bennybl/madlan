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
        var resolution = Resolve(name);
        return resolution.OfficialCode.HasValue ? resolution.ResolvedValue : null;
    }

    public LocalityResolution Resolve(string value)
    {
        var original = value;
        var normalized = NormalizeName(value);
        if (_localitiesByName.TryGetValue(normalized, out var exact))
        {
            return CreateResolution(original, exact, "exact");
        }

        var candidates = _localities
            .Select(locality => new
            {
                Locality = locality,
                Distance = Math.Min(
                    EditDistance(normalized, NormalizeName(locality.NameHe)),
                    string.IsNullOrWhiteSpace(locality.NameEn) ? int.MaxValue : EditDistance(normalized, NormalizeName(locality.NameEn)))
            })
            .OrderBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.Locality.Code)
            .Take(2)
            .ToList();

        var isWithinTypoThreshold = candidates.Count > 0 && candidates[0].Distance <= MaximumTypoDistance(normalized);
        if (isWithinTypoThreshold && (candidates.Count == 1 || candidates[1].Distance - candidates[0].Distance >= 1))
        {
            return CreateResolution(original, candidates[0].Locality, "typo");
        }

        return new LocalityResolution
        {
            OriginalValue = original,
            ResolvedValue = normalized,
            Method = isWithinTypoThreshold && candidates.Count > 1 && candidates[0].Distance == candidates[1].Distance ? "ambiguous" : "unresolved"
        };
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

    private static LocalityResolution CreateResolution(string original, IsraeliLocality locality, string method) => new()
    {
        OfficialCode = locality.Code,
        OriginalValue = original,
        ResolvedValue = locality.NameHe,
        Method = method
    };

    private static int MaximumTypoDistance(string name) => name.Length <= 5 ? 1 : 2;

    private static int EditDistance(string first, string second)
    {
        var previous = Enumerable.Range(0, second.Length + 1).ToArray();
        for (var i = 1; i <= first.Length; i++)
        {
            var current = new int[second.Length + 1];
            current[0] = i;
            for (var j = 1; j <= second.Length; j++)
            {
                var substitutionCost = first[i - 1] == second[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + substitutionCost);
            }

            previous = current;
        }

        return previous[second.Length];
    }

    private static string NormalizeName(string value)
    {
        var withoutExtraWhitespace = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return withoutExtraWhitespace.Replace("-", string.Empty, StringComparison.Ordinal);
    }
}
