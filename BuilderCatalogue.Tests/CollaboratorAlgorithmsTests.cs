using System.Collections.Frozen;
using BuilderCatalogue.Api.Models.Dto;
using BuilderCatalogue.Api.Services;

namespace BuilderCatalogue.Tests;

public class CollaboratorAlgorithmsTests
{
    [Fact]
    public void FindMissingPiecesProvider_ReturnsUsersCoveringAllPieces()
    {
        var missingPieces = PieceInventoryDto.Create(
        [
            new PieceInfo("brick-1", "Red", 2),
            new PieceInfo("brick-2", "Blue", 1)
        ]);

        var index = BuildIndex(new Dictionary<(string Piece, string Color), Dictionary<string, int>>
        {
            [("brick-1", "Red")] = new(StringComparer.Ordinal)
            {
                ["userA"] = 3,
                ["userB"] = 2
            },
            [("brick-2", "Blue")] = new(StringComparer.Ordinal)
            {
                ["userA"] = 1
            }
        });

        var resultRow = LEGOSetService.FindMissingPiecesProvider(missingPieces, index);
        var resultColumn = LEGOSetService.FindMissingPiecesProviderColumnar(missingPieces, index);

        Assert.Equal(["userA"], resultRow.OrderBy(u => u));
        Assert.Equal(["userA"], resultColumn.OrderBy(u => u));
    }

    [Fact]
    public void FindMissingPiecesProvider_ReturnsEmptyWhenCountsInsufficient()
    {
        var missingPieces = PieceInventoryDto.Create(
        [
            new PieceInfo("brick-1", "Red", 5),
            new PieceInfo("brick-2", "Blue", 1)
        ]);

        var index = BuildIndex(new Dictionary<(string Piece, string Color), Dictionary<string, int>>
        {
            [("brick-1", "Red")] = new(StringComparer.Ordinal)
            {
                ["userA"] = 4
            },
            [("brick-2", "Blue")] = new(StringComparer.Ordinal)
            {
                ["userA"] = 1
            }
        });

        var resultRow = LEGOSetService.FindMissingPiecesProvider(missingPieces, index);
        var resultColumn = LEGOSetService.FindMissingPiecesProviderColumnar(missingPieces, index);

        Assert.Empty(resultRow);
        Assert.Empty(resultColumn);
    }

    private static FrozenDictionary<PieceKey, FrozenDictionary<string, int>> BuildIndex(Dictionary<(string Piece, string Color), Dictionary<string, int>> source)
    {
        return source.ToFrozenDictionary(
            entry => new PieceKey(entry.Key.Piece, entry.Key.Color),
            entry => entry.Value.ToFrozenDictionary(StringComparer.Ordinal));
    }
}
