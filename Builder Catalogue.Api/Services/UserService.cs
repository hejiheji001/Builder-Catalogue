using BuilderCatalogue.Api.Models.Contracts;
using BuilderCatalogue.Api.Models.External;

namespace BuilderCatalogue.Api.Services;

public class UserService(CatalogueDbContext dbContext, ICatalogueApiClient apiClient)
{
    public async Task<UserDetailApiModel> GetUserDetailAsync(string username, CancellationToken cancellationToken = default)
    {
        var userDetail = await apiClient.GetUserAsync(username, cancellationToken);

        //var cachedSets = await dbContext.BuildableSets
        //    .AsNoTracking()
        //    .Include(cache => cache.LEGOSets)
        //    .FirstOrDefaultAsync(cache => string.Equals(cache.Id, userDetail.Id), cancellationToken);

        //userDetail.BuildableSets = cachedSets?.LEGOSets.Select(set => new LEGOSetResponse(set.Id, set.Name, set.SetNumber, set.TotalPieces)).ToList() ?? [];

        return userDetail;
    }

    public async Task<IEnumerable<UserSummaryApiModel>> GetUsersAsync(CancellationToken cancellationToken)
    {
        return await apiClient.GetUsersAsync(cancellationToken);
    }

    public Dictionary<(string DesignId, string ColourId), int> BuildUserInventory(UserDetailApiModel userDetail)
    {
        return userDetail.Collection
            .SelectMany(entry => entry.Variants.Select(variant => new
            {
                DesignId = entry.PieceId,
                ColourId = variant.Color,
                variant.Count
            }))
            .GroupBy(item => (item.DesignId, item.ColourId))
            .ToDictionary(
                group => group.Key,
                group => group.Sum(item => item.Count));
    }

    public Dictionary<string, int> BuildColourFelixibleUserInventory(UserDetailApiModel userDetail)
    {
        return userDetail.Collection
            .GroupBy(entry => entry.PieceId)
            .ToDictionary(
                group => group.Key,
                group => group.SelectMany(entry => entry.Variants).Sum(variant => variant.Count));
    }
}
