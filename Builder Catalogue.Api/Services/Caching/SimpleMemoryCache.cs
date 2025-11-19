using BuilderCatalogue.Api.Models.External;
using System.Collections.Concurrent;

namespace BuilderCatalogue.Api.Services.Caching;

public class SimpleMemoryCache : ICacheService
{
    private readonly string _cacheFilePath;
    private readonly ConcurrentDictionary<string, object> _commonCache = [];
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public SimpleMemoryCache(IHostEnvironment environment)
    {
        _cacheFilePath = Path.Combine(environment.ContentRootPath, "CacheData", "sets-cache.json");

        LoadInitialCache();
    }

    public IReadOnlyCollection<T> GetCachedEntries<T>() where T : class
    {
        return [.. _commonCache.Values.Where(value => value is T).Cast<T>()];
    }

    public T? GetFromCache<T>(string key) where T : class
    {
        return _commonCache.TryGetValue(key, out var value) ? value as T : null;
    }

    public void UpdateCache<T>(string key, T cache) where T : class
    {
        _commonCache.TryAdd(key, cache);
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

            foreach (var entry in entries)
            {
                _commonCache.TryAdd(entry.Name, entry);
            }

        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to load cache from {_cacheFilePath}: {e}");
        }
    }
}
