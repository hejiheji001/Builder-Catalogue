using System.Collections.Frozen;

namespace BuilderCatalogue.Api.Models.Dto;

/// <summary>
/// Columnar snapshot of a user's inventory. Aggregates duplicate pieces at creation
/// time, exposes fast lookups by (piece,color), and provides spans for
/// each design to support SIMD-friendly scans plus inverted-index construction.
/// </summary>
public record PieceInventoryDto
{
    public static PieceInventoryDto Empty { get; } = new([], null);

    public string? OwnerId { get; }

    public IReadOnlyList<PieceInfo> Pieces { get; }

    public IEnumerable<string> DistinctPieceIds => _pieceSlices.Keys;

    // Expose contiguous columns so callers can perform SIMD-friendly scans without touching the row objects.
    public ref readonly PieceColumns Columns => ref _columns;

    // Backing arrays + lookup tables. Piece data stays immutable so spans remain valid.
    private readonly PieceInfo[] _pieces;
    private readonly FrozenDictionary<PieceKey, PieceInfo> _lookup;
    private readonly FrozenDictionary<string, PieceSlice> _pieceSlices;
    private readonly PieceColumns _columns;

    private PieceInventoryDto(PieceInfo[] pieces, string? ownerId)
    {
        OwnerId = ownerId;
        _pieces = pieces;
        Pieces = Array.AsReadOnly(_pieces);
        _lookup = pieces.ToFrozenDictionary(piece => new PieceKey(piece.PieceId, piece.ColorId), piece => piece);
        _pieceSlices = BuildSlices(pieces);
        _columns = new PieceColumns(
            [.. pieces.Select(p => p.PieceId)],
            [.. pieces.Select(p => p.ColorId)],
            [.. pieces.Select(p => p.Count)]);
    }

    /// <summary>
    /// Normalize piece collection into sorted, distinct rows
    /// Create columnar arrays + slice metadata for quick scanning and grouping.
    /// </summary>
    public static PieceInventoryDto Create(IEnumerable<PieceInfo> entries, string? ownerId = null)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var aggregated = Aggregate(entries);
        if (aggregated.Length == 0)
        {
            return ownerId is null ? Empty : new PieceInventoryDto([], ownerId);
        }

        return new PieceInventoryDto(aggregated, ownerId);
    }

    public bool TryGetCount(string pieceId, string colorId, out int owned)
    {
        if (_lookup.TryGetValue(new PieceKey(pieceId, colorId), out var piece))
        {
            owned = piece.Count;
            return true;
        }

        owned = 0;
        return false;
    }

    public ReadOnlySpan<PieceInfo> GetVariants(string pieceId)
    {
        if (_pieceSlices.TryGetValue(pieceId, out var slice))
        {
            return new ReadOnlySpan<PieceInfo>(_pieces, slice.Offset, slice.Length);
        }

        return [];
    }

    public bool IsEmpty => _pieces.Length == 0;

    /// <summary>
    /// Aggregate pieces by (piece,color) to avoid duplicate rows and enforce stable ordering.
    /// </summary>
    private static PieceInfo[] Aggregate(IEnumerable<PieceInfo> entries)
    {
        var totals = new Dictionary<PieceKey, int>();

        foreach (var entry in entries)
        {
            var key = new PieceKey(entry.PieceId, entry.ColorId);
            if (totals.TryGetValue(key, out var current))
            {
                totals[key] = current + entry.Count;
            }
            else
            {
                totals[key] = entry.Count;
            }
        }

        return [.. totals
            .Select(kvp => new PieceInfo(kvp.Key.PieceId, kvp.Key.ColorId, kvp.Value))
            .OrderBy(piece => piece.PieceId)
            .ThenBy(piece => piece.ColorId)];
    }

    /// <summary>
    /// Precompute offsets for each design so GetVariants can return slices without allocating.
    /// </summary>
    private static FrozenDictionary<string, PieceSlice> BuildSlices(PieceInfo[] pieces)
    {
        var slices = new Dictionary<string, PieceSlice>();
        var index = 0;

        while (index < pieces.Length)
        {
            var pieceId = pieces[index].PieceId;
            var start = index;

            do
            {
                index++;
            }
            while (index < pieces.Length && string.Equals(pieces[index].PieceId, pieceId));

            slices[pieceId] = new PieceSlice(start, index - start);
        }

        return slices.ToFrozenDictionary();
    }

    private readonly record struct PieceSlice(int Offset, int Length);
}

public record PieceInfo(string PieceId, string ColorId, int Count)
{
    public int Count { get; set; } = Count;
}

public readonly record struct PieceKey(string PieceId, string ColorId = "");

public readonly struct PieceColumns(string[] pieceIds, string[] colorIds, int[] counts)
{
    public ReadOnlySpan<string> PieceIds => pieceIds;

    public ReadOnlySpan<string> ColorIds => colorIds;

    public ReadOnlySpan<int> Counts => counts;
}