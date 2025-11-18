using BuilderCatalogue.Api.Models.Contracts;
using BuilderCatalogue.Api.Models.External;
using BuilderCatalogue.Api.Services.Caching;
using BuilderCatalogue.Data.Domain;

namespace BuilderCatalogue.Api.Services;

//TODO: Use AutoMapper or Mapperly in a real project

public class LEGOSetService(ICacheService cacheService, UserService userService, CatalogueDbContext dbContext, ICatalogueApiClient apiClient)
{
    public async Task<BuildableLEGOSetsResponse> GetBuildableSetsAsync(string username, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        var userDetail = await userService.GetUserDetailAsync(username, cancellationToken);

        //if (userDetail.BuildableSets.Count == 0)
        //{
            var LEGOSets = ComputeBuildableSets(userDetail);

        //    var id = ComputeSignature(userDetail);
        //    await PersistCacheAsync(username, id, LEGOSets, cancellationToken);

            userDetail.BuildableSets = [.. LEGOSets.Select(set => new LEGOSetResponse(set.Id, set.Name, set.SetNumber, set.TotalPieces))];
        //}

        return new BuildableLEGOSetsResponse(username, userDetail.BuildableSets.Count, userDetail.BuildableSets);
    }

    public async Task<CollaborationResponse> FindCollaboratorsAsync(string username, string setName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(setName);

        var setDetail = cacheService.GetSetDetailByName(setName);
        if (setDetail is null)
        {
            setDetail = await apiClient.GetSetAsync(setName, cancellationToken);
            // Update Cache - in a real-world scenario, we might want to have a background job to refresh the cache periodically.
        }
         
        if (setDetail is null)
        {
            throw new ArgumentException($"Set with name '{setName}' not found.", nameof(setName));
        }

        var targetUser = await userService.GetUserDetailAsync(username, cancellationToken);
        var targetInventory = userService.BuildUserInventory(targetUser);
        var requirements = BuildSetRequirements(setDetail);

        // Treated as a sub requirement - what is missing from the target user?
        var missingPieces = ComputeMissingPieces(targetInventory, requirements);

        if (missingPieces.Count == 0)
        {
            return new CollaborationResponse(username, setDetail.Id, setDetail.Name, []);
        }

        var userSummaries = await userService.GetUsersAsync(cancellationToken);
        var candidates = userSummaries.Where(user => !string.Equals(user.Username, username, StringComparison.OrdinalIgnoreCase)).ToList();

        var collaborators = ImmutableArray.CreateBuilder<string[]>();

        // This is a simple greedy approach - in a real-world scenario, we might want to consider
        // more advanced algorithms to find optimal collaborator combinations.
        // For now, we just check each candidate individually.
        foreach (var candidate in candidates)
        {
            var candidateInfo = await userService.GetUserDetailAsync(username, cancellationToken);
            var candidateInventory = userService.BuildUserInventory(candidateInfo);

            if (IsBuildable(candidateInventory, missingPieces))
            {
                collaborators.Add([username, candidate.Username]);
            }
        }

        return new CollaborationResponse(username, setDetail.Id, setDetail.Name, collaborators.ToImmutable());
    }

    public async Task<BuildSizeRecommendationResponse> GetBuildSizeRecommendationAsync(string username, double percentile, CancellationToken cancellationToken = default)
    {
        if (percentile is <= 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(percentile), percentile, "Percentile must be within (0, 1].");
        }

        var userSummaries = await userService.GetUsersAsync(cancellationToken);
        var candidates = userSummaries.Where(user => !string.Equals(user.Username, username, StringComparison.OrdinalIgnoreCase)).ToList();

        // Assume the larger the user's brickcount is, the more build options they have.
        // e.g. 15 candidates, order by brickcount asc, skip (int) Math.Floor(15 * 0.5) = 7, take the 8th
        var userSkiped = (int)Math.Floor(candidates.Count * percentile);
        var candidate = candidates.OrderBy(user => user.BrickCount).Skip(userSkiped).First();

        var candidateInfo = await userService.GetUserDetailAsync(candidate.Username, cancellationToken);
        var candidateInventory = userService.BuildUserInventory(candidateInfo);

        var userDetail = await userService.GetUserDetailAsync(username, cancellationToken);
        var userInventory = userService.BuildUserInventory(userDetail);

        // Assume the new custom build can be built by the user
        // Which means, if the userInventory < candidateInventory, any custom build made by the user is able to be build by the candidate
        // And if the userInventory > candidateInventory, the user should restrict the pieces of materials to candidateInventory
        var threshold = IsBuildable(userInventory, candidateInventory) ? candidate.BrickCount : userDetail.BrickCount;

        return new BuildSizeRecommendationResponse(username, threshold, candidates.Count - userSkiped, percentile);
    }

    public async Task<BuildableLEGOSetsResponse> GetColourFlexibilityAsync(string username, CancellationToken cancellationToken = default)
    {
        var userDetail = await userService.GetUserDetailAsync(username, cancellationToken);
        var buildableSets = ComputeBuildableSets(userDetail);

        var flexibleInventory = userService.BuildColourFelixibleUserInventory(userDetail);
        var flexibleSets = new List<ColourFlexibleLEGOSet>();

        // Do not exclude the buildable sets since the user can build a buildable set with exact colours,
        var setInfo = cacheService.GetSetDetails();

        foreach (var setDetail in setInfo)
        {
            var flexibleRequirements = BuildColourFelixibleSetRequirements(setDetail);

            if (IsBuildable(flexibleInventory, flexibleRequirements))
            {
                flexibleSets.Add(new ColourFlexibleLEGOSet(setDetail.Id, setDetail.Name, setDetail.SetNumber, setDetail.TotalPieces));
            }
        }

        return new BuildableLEGOSetsResponse(username, flexibleSets.Count, flexibleSets);
    }

    private static Dictionary<(string DesignId, string ColourId), int> BuildSetRequirements(LEGOSetDetailApiResponse setDetail)
    {
        return setDetail.Pieces
            .Where(piece => piece.Part is not null)
            .Select(piece => new
            {
                DesignId = piece.Part!.DesignID,
                ColourId = piece.Part.Material.ToString(),
                piece.Quantity
            })
            .GroupBy(item => (item.DesignId, item.ColourId))
            .ToDictionary(
                group => group.Key,
                group => group.Sum(item => item.Quantity));
    }

    private List<LEGOSet> ComputeBuildableSets(UserDetailApiModel userInfo)
    {
        var buildableSets = new List<LEGOSet>();

        var inventory = userService.BuildUserInventory(userInfo);

        // This call is intentionally routed through a cache abstraction so we can
        // plug in alternative providers (distributed cache, Redis, etc.)
        // without touching the evaluation flow in the future.
        var setInfo = cacheService.GetSetDetails();

        foreach (var detail in setInfo)
        {
            if (detail is not null && detail.Pieces.Count != 0)
            {
                var requirements = BuildSetRequirements(detail);

                if (IsBuildable(inventory, requirements))
                {
                    buildableSets.Add(new LEGOSet
                    {
                        Id = detail.Id,
                        Name = detail.Name,
                        SetNumber = detail.SetNumber,
                        TotalPieces = detail.TotalPieces
                    });
                }
            }
        }

        return buildableSets;
    }

    private static Dictionary<string, int> BuildColourFelixibleSetRequirements(LEGOSetDetailApiResponse setDetail)
    {
         return setDetail.Pieces
            .Where(piece => piece.Part is not null)
            .GroupBy(piece => piece.Part!.DesignID)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(item => item.Quantity));
    }

    private async Task PersistCacheAsync(string username, string id, IReadOnlyList<LEGOSet> buildableSets, CancellationToken cancellationToken)
    {
        var existingCache = await dbContext.BuildableSets
            .Where(cache => string.Equals(cache.Username, username, StringComparison.OrdinalIgnoreCase))
            .ToListAsync(cancellationToken);

        if (existingCache.Count > 0)
        {
            dbContext.BuildableSets.RemoveRange(existingCache);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var newCache = new BuildableLEGOSets()
        {
            Username = username,
            Id = id,
            ComputedAt = DateTimeOffset.UtcNow,
            LEGOSets = [.. buildableSets
                .Select(set => new LEGOSet() {
                    Username = username,
                    Id = set.Id,
                    Name = set.Name,
                    SetNumber = set.SetNumber,
                    TotalPieces = set.TotalPieces,
                    BuildableLEGOSetsId = id
                })]
        };

        dbContext.BuildableSets.Add(newCache);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // Simple signature computation based on the user's collection state.
    private static string ComputeSignature(UserDetailApiModel user)
    {
        var normalizedEntries = user.Collection
            .SelectMany(entry => entry.Variants.Select(variant => new
            {
                entry.PieceId,
                variant.Color,
                variant.Count
            }))
            .OrderBy(item => item.PieceId)
            .ThenBy(item => item.Color)
            .Select(item => $"{item.PieceId}:{item.Color}:{item.Count}");

        var concatenated = string.Join('|', normalizedEntries);
        var bytes = Encoding.UTF8.GetBytes(concatenated);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static bool IsBuildable<TKey>(Dictionary<TKey, int> inventory, Dictionary<TKey, int> requirements) where TKey : notnull => ComputeMissingPieces(inventory, requirements).Count == 0;

    private static Dictionary<TKey, int> ComputeMissingPieces<TKey>(Dictionary<TKey, int> inventory, Dictionary<TKey, int> requirements) where TKey : notnull
    {
        var missing = new Dictionary<TKey, int>();

        foreach (var (key, requiredQuantity) in requirements)
        {
            inventory.TryGetValue(key, out var owned);
            if (requiredQuantity > owned)
            {
                missing[key] = requiredQuantity - owned;
            }
        }

        return missing;
    }
}
