using BuilderCatalogue.Api.Models.Dto;
using BuilderCatalogue.Api.Services;

namespace BuilderCatalogue.Tests;

public class ColorFlexAlgorithmsTests
{
    [Fact]
    public void InventoryBasedAssignment_ReturnsAssignmentsWithSubstitutions()
    {
        var requirements = PieceInventoryDto.Create(
        [
            new PieceInfo("wall", "White", 2),
            new PieceInfo("roof", "Red", 1)
        ]);

        var inventory = PieceInventoryDto.Create(
        [
            new PieceInfo("wall", "Blue", 3),
            new PieceInfo("roof", "Red", 1)
        ]);

        var success = LEGOSetService.TryCreateColorFlexibleAssignment(requirements, inventory, out var assignments, out var hasSubstitution);

        Assert.True(success);
        Assert.True(hasSubstitution);
        Assert.Equal(requirements.Pieces.Count, assignments.Count);
        Assert.Contains(assignments, usage => usage.RequiredColorId == "White" && usage.UsedColorId == "Blue");
    }

    [Fact]
    public void InventoryBasedAssignment_FailsWhenReplacementUnavailable()
    {
        var requirements = PieceInventoryDto.Create(
        [
            new PieceInfo("wall", "White", 4)
        ]);

        var inventory = PieceInventoryDto.Create(
        [
            new PieceInfo("wall", "Blue", 3)
        ]);

        var success = LEGOSetService.TryCreateColorFlexibleAssignment(requirements, inventory, out var assignments, out var hasSubstitution);

        Assert.False(success);
        Assert.False(hasSubstitution);
        Assert.Empty(assignments);
    }

    [Fact]
    public void InvertedIndexAssignment_ReusesAvailabilityBuckets()
    {
        var requirements = PieceInventoryDto.Create(
        [
            new PieceInfo("wall", "White", 2),
            new PieceInfo("roof", "Red", 1)
        ]);

        var inventory = PieceInventoryDto.Create(
        [
            new PieceInfo("wall", "Blue", 3),
            new PieceInfo("roof", "Red", 1)
        ]);

        var availabilityIndex = BuildAvailabilityIndex(inventory);

        var success = LEGOSetService.TryCreateColorFlexibleAssignment(requirements, availabilityIndex, out var assignments, out var hasSubstitution);

        Assert.True(success);
        Assert.True(hasSubstitution);
        Assert.Equal(requirements.Pieces.Count, assignments.Count);
        Assert.Contains(assignments, usage => usage.RequiredColorId == "White" && usage.UsedColorId == "Blue");
    }

    [Fact]
    public void InvertedIndexAssignment_FailsWhenCountsTooLow()
    {
        var requirements = PieceInventoryDto.Create(
        [
            new PieceInfo("wall", "White", 4)
        ]);

        var inventory = PieceInventoryDto.Create(
        [
            new PieceInfo("wall", "Blue", 3)
        ]);

        var availabilityIndex = BuildAvailabilityIndex(inventory);

        var success = LEGOSetService.TryCreateColorFlexibleAssignment(requirements, availabilityIndex, out var assignments, out var hasSubstitution);

        Assert.False(success);
        Assert.False(hasSubstitution);
        Assert.Empty(assignments);
    }

    private static Dictionary<PieceKey, Dictionary<string, int>> BuildAvailabilityIndex(PieceInventoryDto inventory)
    {
        var map = new Dictionary<PieceKey, Dictionary<string, int>>();

        foreach (var piece in inventory.Pieces)
        {
            var key = new PieceKey(piece.PieceId, piece.ColorId);
            if (!map.TryGetValue(key, out var counts))
            {
                counts = new Dictionary<string, int>(StringComparer.Ordinal);
                map[key] = counts;
            }

            counts[piece.ColorId] = piece.Count;
        }

        return map;
    }
}
