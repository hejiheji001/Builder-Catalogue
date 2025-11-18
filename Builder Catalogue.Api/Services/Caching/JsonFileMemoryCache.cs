using BuilderCatalogue.Api.Models.External;

namespace BuilderCatalogue.Api.Services.Caching;

public class JsonFileMemoryCache : ICacheService
{
    private readonly string _cacheFilePath;
    private ImmutableArray<LEGOSetDetailApiResponse> _cache = [];
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public JsonFileMemoryCache(IHostEnvironment environment)
    {
        _cacheFilePath = Path.Combine(environment.ContentRootPath, "Data", "sets-cache.json");

        LoadInitialCache();
    }

    public IReadOnlyCollection<LEGOSetDetailApiResponse> GetSetDetails()
    {
        return _cache;
    }

    public LEGOSetDetailApiResponse? GetSetDetailByName(string setName)
    {
        return _cache.Where(set => string.Equals(set.Name, setName, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
    }

    private void LoadInitialCache()
    {
        if (!File.Exists(_cacheFilePath))
        {
            return;
        }

        try
        {
            using var stream = File.OpenRead(_cacheFilePath);
            var entries = JsonSerializer.Deserialize<List<LEGOSetDetailApiResponse>>(stream, _serializerOptions);
            if (entries is null || entries.Count == 0)
            {
                return;
            }

            _cache = [.. entries];
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to load cache from {_cacheFilePath}: {e}");
        }
    }
}
