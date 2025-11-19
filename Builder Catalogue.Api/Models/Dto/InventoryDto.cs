
namespace BuilderCatalogue.Api.Models.Dto;

public record InventoryDto(List<PieceDto> Pieces, string UserId)
{
    public PieceDto? GetPiece(string pieceId, string colorId)
    {
        return Pieces.FirstOrDefault(p => p.PieceId == pieceId && p.ColorId == colorId);
    }

    public List<PieceDto?> GetPieceVariants(string pieceId)
    {
        return [.. Pieces.Where(p => p.PieceId == pieceId)];
    }
}

public record ColorFlexibleInventoryDto(List<ColorFlexiblePieceDto> Pieces, string UserId)
{
    public List<PieceDto?> GetPieceVariants(string pieceId)
    {
        return [.. Pieces.Where(p => p.PieceId == pieceId).SelectMany(p => p.ColorInfo.Select(ci => new PieceDto(p.PieceId, ci.Key, ci.Value)))];
    }
}

public record RequirementsDto(List<PieceDto> Pieces) : InventoryDto(Pieces, string.Empty);

public record ColorFlexibleRequirementsDto(List<ColorFlexiblePieceDto> Pieces) : ColorFlexibleInventoryDto(Pieces, string.Empty);

public record PieceDto(string PieceId, string ColorId, int Count)
{
    public int Count { get; set; } = Count;
    public List<string> Users { get; set; } = [];
}

public record ColorFlexiblePieceDto(string PieceId, Dictionary<string, int> ColorInfo);