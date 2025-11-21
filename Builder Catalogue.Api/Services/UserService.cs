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

    public async Task<FrozenDictionary<PieceKey, FrozenDictionary<string, int>>> BuildUserInventoryIndexColumnarAsync(IEnumerable<UserSummaryApiModel> userSummaries, CancellationToken cancellationToken)
    {
        var index = new Dictionary<PieceKey, Dictionary<string, int>>();

        foreach (var summary in userSummaries)
        {
            var detail = await GetUserDetailAsync(summary.Username, cancellationToken);
            var inventory = BuildUserInventory(detail);
            ref readonly var columns = ref inventory.Columns;
            var pieceIds = columns.PieceIds;
            var colorIds = columns.ColorIds;
            var counts = columns.Counts;

            for (var i = 0; i < pieceIds.Length; i++)
            {
                var key = new PieceKey(pieceIds[i], colorIds[i]);
                if (!index.TryGetValue(key, out var userCounts))
                {
                    userCounts = [];
                    index[key] = userCounts;
                }

                userCounts[summary.Username] = counts[i];
            }
        }

        return index.ToFrozenDictionary(kvp => kvp.Key, kvp => kvp.Value.ToFrozenDictionary());
    }

    public async Task<FrozenDictionary<PieceKey, FrozenDictionary<string, int>>> BuildUserInventoryIndexAsync(IEnumerable<UserSummaryApiModel> userSummaries, CancellationToken cancellationToken)
    {
        var index = new Dictionary<PieceKey, Dictionary<string, int>>();

        foreach (var summary in userSummaries)
        {
            var detail = await GetUserDetailAsync(summary.Username, cancellationToken);
            var inventory = BuildUserInventory(detail);
            var userIndex = BuildPieceAvailabilityIndex(inventory, summary.Username);

            foreach (var (pieceKey, userCounts) in userIndex)
            {
                if (!index.TryGetValue(pieceKey, out var bucket))
                {
                    bucket = [];
                    index[pieceKey] = bucket;
                }

                foreach (var (user, count) in userCounts)
                {
                    bucket[user] = count;
                }
            }
        }

        return index.ToFrozenDictionary(kvp => kvp.Key, kvp => kvp.Value.ToFrozenDictionary());
    }

    public Dictionary<PieceKey, Dictionary<string, int>> BuildPieceAvailabilityIndex(PieceInventoryDto inventory, string username = "")
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

            if (username != "")
            {
                colors[username] = piece.Count;
            } else
            {
                colors[piece.ColorId] = piece.Count;
            }
        }

        return bucket;
    }
}
