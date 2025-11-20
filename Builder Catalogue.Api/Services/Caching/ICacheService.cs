namespace BuilderCatalogue.Api.Services.Caching;

public interface ICacheService
{
    IReadOnlyCollection<T> GetCachedEntries<T>() where T : class;
    T? GetFromCache<T>(string id) where T : class;
    void UpdateCache<T>(string id, T value) where T : class;
}
