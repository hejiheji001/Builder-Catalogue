using BuilderCatalogue.Api.Clients;
using BuilderCatalogue.Api.Models.Dto;
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

    public InventoryDto BuildUserInventory(UserDetailApiModel userDetail)
    {
        var pieces = userDetail.Collection
            .SelectMany(entry => entry.Variants.Select(variant => new PieceDto(entry.PieceId, variant.Color, variant.Count)
            {
                Users = [userDetail.Username]
            })).ToList();
        return new InventoryDto(pieces, userDetail.Id);
    }

    public ColorFlexibleInventoryDto BuildColorFlexibleInventory(UserDetailApiModel userDetail)
    {
        var colorFlexiblePieces = userDetail.Collection
            .Select(piece => new ColorFlexiblePieceDto(piece.PieceId, piece.Variants.ToDictionary(key => key.Color, value => value.Count))).ToList();

        return new ColorFlexibleInventoryDto(colorFlexiblePieces, userDetail.Id);
    }
}
