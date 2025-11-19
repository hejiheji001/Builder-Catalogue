using BuilderCatalogue.Api.Models.External;

namespace BuilderCatalogue.Api.Services;

public class UserService(ICatalogueApiClient apiClient)
{
    public async Task<UserDetailApiModel> GetUserDetailAsync(string username, CancellationToken cancellationToken = default)
    {
        var userDetail = await apiClient.GetUserAsync(username, cancellationToken);
        return userDetail;
    }

    public async Task<IEnumerable<UserSummaryApiModel>> GetUsersAsync(CancellationToken cancellationToken)
    {
        return await apiClient.GetUsersAsync(cancellationToken);
    }

    public Dictionary<(string DesignId, string ColorId), int> BuildUserInventory(UserDetailApiModel userDetail)
    {
        return userDetail.Collection
            .SelectMany(entry => entry.Variants.Select(variant => new
            {
                DesignId = entry.PieceId,
                ColorId = variant.Color,
                variant.Count
            }))
            .GroupBy(item => (item.DesignId, item.ColorId))
            .ToDictionary(
                group => group.Key,
                group => group.Sum(item => item.Count));
    }

    public Dictionary<string, Dictionary<string, int>> BuildColorFlexibleUserInventory(UserDetailApiModel userDetail)
    {
        // Key - PieceId, Value - <Key - ColorId, Value - Count>
        return userDetail.Collection
            .GroupBy(entry => entry.PieceId)
            .ToDictionary(group => group.Key, group => group
                .SelectMany(entry => entry.Variants)
                .GroupBy(variant => variant.Color)
                .ToDictionary(key => key.Key, value => value
                    .Sum(x => x.Count)));
    }
}
