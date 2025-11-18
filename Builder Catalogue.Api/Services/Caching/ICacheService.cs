using BuilderCatalogue.Api.Models.External;

namespace BuilderCatalogue.Api.Services.Caching;

public interface ICacheService
{
    IReadOnlyCollection<LEGOSetDetailApiResponse> GetSetDetails();

    public LEGOSetDetailApiResponse? GetSetDetailByName(string setName);
}
