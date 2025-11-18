using BuilderCatalogue.Api.Models.Contracts;

namespace BuilderCatalogue.Api.Models.External;

public record UserApiModel
{
    public List<UserSummaryApiModel> Users { get; init; } = [];
}

public record UserSummaryApiModel
{
    public string Id { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public int BrickCount { get; init; }
}

public record UserDetailApiModel
{
    public string Id { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public int BrickCount { get; init; }
    public List<LEGOSetResponse> BuildableSets { get; set; } = [];
    public List<UserCollectionApiModel> Collection { get; init; } = [];
}

public record UserCollectionApiModel
{
    public string PieceId { get; init; } = string.Empty;
    public List<UserPieceVariantApiModel> Variants { get; init; } = [];
}

public record UserPieceVariantApiModel
{
    public string Color { get; init; } = string.Empty;
    public int Count { get; init; }
}