namespace BuilderCatalogue.Api.Models.Contracts;

public record BuildSizeRecommendationResponse(string Username, int PieceThreshold, int UserCountConsidered, double Percentile);
