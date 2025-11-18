using BuilderCatalogue.Api.Models.External;

namespace BuilderCatalogue.Api.Clients;

public interface ICatalogueApiClient
{
    Task<IReadOnlyList<UserSummaryApiModel>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<UserDetailApiModel> GetUserAsync(string username, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LEGOSetSummaryApiResponse>> GetSetsAsync(CancellationToken cancellationToken = default);
    Task<LEGOSetDetailApiResponse> GetSetAsync(string setId, CancellationToken cancellationToken = default);
}

public sealed class CatalogueApiClient(HttpClient httpClient) : ICatalogueApiClient
{
    public async Task<IReadOnlyList<UserSummaryApiModel>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetFromJsonAsync<UserApiModel>("/api/users", cancellationToken)
            ?? throw new InvalidOperationException("Unable to retrieve users catalogue.");

        return response.Users;
    }

    public async Task<UserDetailApiModel> GetUserAsync(string username, CancellationToken cancellationToken = default)
    {
        var summary = await httpClient.GetFromJsonAsync<UserSummaryApiModel>($"/api/user/by-username/{username}", cancellationToken)
            ?? throw new InvalidOperationException($"Unable to retrieve summary for user '{username}'.");

        var detail = await httpClient.GetFromJsonAsync<UserDetailApiModel>($"/api/user/by-id/{summary.Id}", cancellationToken)
            ?? throw new InvalidOperationException($"Unable to retrieve detail for user '{username}'.");

        return detail;
    }

    public async Task<IReadOnlyList<LEGOSetSummaryApiResponse>> GetSetsAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetFromJsonAsync<LEGOSetsApiResponse>("/api/sets", cancellationToken)
            ?? throw new InvalidOperationException("Unable to retrieve sets catalogue.");

        return response.Sets;
    }

    public async Task<LEGOSetDetailApiResponse> GetSetAsync(string setId, CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<LEGOSetDetailApiResponse>($"/api/set/by-id/{setId}", cancellationToken)
            ?? throw new InvalidOperationException($"Unable to retrieve set '{setId}'.");
    }
}
