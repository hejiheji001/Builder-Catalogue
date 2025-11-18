namespace BuilderCatalogue.Api.Models.Contracts;

public record CollaborationResponse(string Username, string SetId, string SetName, IReadOnlyCollection<string[]> CollaboratorGroups);
