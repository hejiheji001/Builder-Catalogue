using System.ComponentModel.DataAnnotations;

namespace BuilderCatalogue.Data.Domain;

public record LEGOSet
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SetNumber { get; set; } = string.Empty;
    public int TotalPieces { get; set; }
}

public record BuildableLEGOSets
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public DateTimeOffset ComputedAt { get; set; } = DateTimeOffset.UnixEpoch;
    public List<LEGOSet> LEGOSets { get; set; } = [];
}