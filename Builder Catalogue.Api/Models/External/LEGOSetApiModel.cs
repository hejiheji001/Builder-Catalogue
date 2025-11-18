namespace BuilderCatalogue.Api.Models.External;

public record LEGOSetsApiResponse
{
    public List<LEGOSetSummaryApiResponse> Sets { get; init; } = [];
}

public record LEGOSetSummaryApiResponse
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string SetNumber { get; init; } = string.Empty;
    public int TotalPieces { get; init; }
}

public record LEGOSetDetailApiResponse
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string SetNumber { get; init; } = string.Empty;
    public int TotalPieces { get; init; }
    public List<LEGOSetPieceApiResponse> Pieces { get; init; } = [];
}

public record LEGOSetPieceApiResponse
{
    public LEGOSetPartApiResponse? Part { get; init; }
    public int Quantity { get; init; }
}

public record LEGOSetPartApiResponse
{
    public string DesignID { get; init; } = string.Empty;
    public int Material { get; init; }
    public string PartType { get; init; } = string.Empty;
}