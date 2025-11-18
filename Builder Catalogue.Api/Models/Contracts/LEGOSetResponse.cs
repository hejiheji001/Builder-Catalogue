
namespace BuilderCatalogue.Api.Models.Contracts;

public record BuildableLEGOSetsResponse(string Username, int Count, IReadOnlyList<LEGOSetResponse> BuildableSets);

public record LEGOSetResponse(string SetId, string Name, string SetNumber, int TotalPieces, string SetType = "Normal");

public record ColourFlexibleLEGOSet(string SetId, string Name, string SetNumber, int TotalPieces) : LEGOSetResponse(SetId, Name, SetNumber, TotalPieces, "ColourFlexible");