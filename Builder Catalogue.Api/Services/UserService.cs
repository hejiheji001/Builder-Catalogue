using BuilderCatalogue.Api.Clients;
using BuilderCatalogue.Api.Models.Dto;
using BuilderCatalogue.Api.Models.External;
using System.Collections.Frozen;

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

    public PieceInventoryDto BuildUserInventory(UserDetailApiModel userDetail)
    {
        var pieces = userDetail.Collection
            .SelectMany(entry => entry.Variants.Select(variant => new PieceInfo(entry.PieceId, variant.Color, variant.Count)));

        return PieceInventoryDto.Create(pieces, userDetail.Id);
    }

    public async Task<FrozenDictionary<PieceKey, FrozenDictionary<string, int>>> BuildUserInventoryIndexAsync(IEnumerable<UserSummaryApiModel> userSummaries, CancellationToken cancellationToken)
    {
        var indexTasks = userSummaries.Select(async summary =>
        {
            var detail = await GetUserDetailAsync(summary.Username, cancellationToken);
            var inventory = BuildUserInventory(detail);
            return BuildPieceAvailabilityIndex(inventory);
        });

        var index = await Task.WhenAll(indexTasks);
        return index.SelectMany(dic => dic).ToFrozenDictionary(kvp => kvp.Key, kvp => kvp.Value.ToFrozenDictionary());
    }

    public Dictionary<PieceKey, Dictionary<string, int>> BuildPieceAvailabilityIndex(PieceInventoryDto inventory)
    {
        var bucket = new Dictionary<PieceKey, Dictionary<string, int>>();

        foreach (var piece in inventory.Pieces)
        {
            var key = new PieceKey(piece.PieceId, piece.ColorId);

            if (!bucket.TryGetValue(key, out var colors))
            {
                colors = [];
                bucket[key] = colors;
            }

            colors[piece.ColorId] = piece.Count;
        }

        return bucket;
    }
}
