namespace BuilderCatalogue.Api.Models.Contracts;

public record BuildableLEGOSetsResponse(string Username, int Count, IReadOnlyList<LEGOSetResponse> BuildableSets);

public record ColorUsage(string DesignId, string RequiredColorId, string UsedColorId, int Quantity);

public record LEGOSetResponse(
	string SetId,
	string Name,
	string SetNumber,
	int TotalPieces,
	string SetType = "Normal",
	IReadOnlyList<ColorUsage>? ColorAssignments = null);

public record ColorFlexibleLEGOSet(
	string SetId,
	string Name,
	string SetNumber,
	int TotalPieces,
	IReadOnlyList<ColorUsage> ColorAssignments)
		: LEGOSetResponse(SetId, Name, SetNumber, TotalPieces, "ColorFlexible", ColorAssignments);