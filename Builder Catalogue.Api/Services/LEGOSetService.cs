using BuilderCatalogue.Api.Clients;
using BuilderCatalogue.Api.Models.Contracts;
using BuilderCatalogue.Api.Models.Dto;
using BuilderCatalogue.Api.Models.External;
using BuilderCatalogue.Api.Services.Caching;

namespace BuilderCatalogue.Api.Services;

//TODO: Use AutoMapper or Mapperly in a real project

public class LEGOSetService(ICacheService cacheService, UserService userService, ICatalogueApiClient apiClient)
{
    public async Task<BuildableLEGOSetsResponse> GetBuildableSetsAsync(string username, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        var userDetail = await userService.GetUserDetailAsync(username, cancellationToken);

        var LEGOSets = ComputeBuildableSets(userDetail);

        userDetail.BuildableSets = [.. LEGOSets.Select(set => new LEGOSetResponse(set.Id, set.Name, set.SetNumber, set.TotalPieces))];

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
        var candidates = userSummaries.Where(user => !string.Equals(user.Username, username)).ToList();

        var collaborators = ImmutableArray.CreateBuilder<string[]>();

        // This is a simple greedy approach - in a real-world scenario, we might want to consider
        // more advanced algorithms to find optimal collaborator combinations.
        // For now, we just check each candidate individually.
        foreach (var candidate in candidates)
        {
            var candidateInfo = await userService.GetUserDetailAsync(candidate.Username, cancellationToken);
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
        var candidates = userSummaries.Where(user => !string.Equals(user.Username, username)).ToList();

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

    public async Task<BuildableLEGOSetsResponse> GetColorFlexibilityAsync(string username, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        var userDetail = await userService.GetUserDetailAsync(username, cancellationToken);
        var exactInventory = userService.BuildUserInventory(userDetail);
        var flexibleInventory = userService.BuildColorFlexibleUserInventory(userDetail);
        var flexibleSets = new List<ColorFlexibleLEGOSet>();

        foreach (var setDetail in cacheService.GetSetDetails())
        {
            var exactRequirements = BuildSetRequirements(setDetail);
            var isExactBuildable = IsBuildable(exactInventory, exactRequirements);

            if (TryCreateColorFlexibleAssignment(setDetail, flexibleInventory, out var assignments, out var hasSubstitution))
            {
                if (!isExactBuildable || hasSubstitution)
                {
                    flexibleSets.Add(new ColorFlexibleLEGOSet(setDetail.Id, setDetail.Name, setDetail.SetNumber, setDetail.TotalPieces, assignments));
                }
            }
        }

        return new BuildableLEGOSetsResponse(username, flexibleSets.Count, flexibleSets);
    }

    private static Dictionary<(string DesignId, string ColorId), int> BuildSetRequirements(LEGOSetDetailApiResponse setDetail)
    {
        return setDetail.Pieces
            .Where(piece => piece.Part is not null)
            .Select(piece => new
            {
                DesignId = piece.Part!.DesignID,
                ColorId = piece.Part.Material.ToString(),
                piece.Quantity
            })
            .GroupBy(item => (item.DesignId, item.ColorId))
            .ToDictionary(
                group => group.Key,
                group => group.Sum(item => item.Quantity));
    }

    private List<LEGOSetDto> ComputeBuildableSets(UserDetailApiModel userInfo)
    {
        var buildableSets = new List<LEGOSetDto>();

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
                    buildableSets.Add(new LEGOSetDto
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

    private static Dictionary<string, Dictionary<string, int>> BuildColorFlexibleSetRequirements(LEGOSetDetailApiResponse setDetail)
    {
        // Key - DesignID, Value - <Key - Material, Value - Count>
        return setDetail.Pieces
            .GroupBy(entry => entry.Part!.DesignID)
            .ToDictionary(group => group.Key, group => group
                .GroupBy(variant => variant.Part!.Material.ToString())
                .ToDictionary(key => key.Key, value => value
                    .Sum(x => x.Quantity)));
    }

    private static bool TryCreateColorFlexibleAssignment(LEGOSetDetailApiResponse setDetail, Dictionary<string, Dictionary<string, int>> flexibleInventory, out IReadOnlyList<ColorUsage> assignments, out bool hasSubstitution)
    {
        var flexibleRequirements = BuildColorFlexibleSetRequirements(setDetail);
        var result = new List<ColorUsage>();
        hasSubstitution = false;

        foreach (var (designId, colorRequirements) in flexibleRequirements)
        {
            if (!flexibleInventory.TryGetValue(designId, out var availableColors) || availableColors.Count == 0)
            {
                assignments = [];
                hasSubstitution = false;
                return false;
            }

            if (!TryAssignColorsForDesign(designId, colorRequirements, availableColors, result, ref hasSubstitution))
            {
                assignments = [];
                hasSubstitution = false;
                return false;
            }
        }

        assignments = result;

        return result.Count > 0;
    }

    private static bool TryAssignColorsForDesign(string designId, Dictionary<string, int> colorRequirements, Dictionary<string, int> availableColors, List<ColorUsage> assignments, ref bool hasSubstitution)
    {
        var assignment = new Dictionary<string, string>();

        if (!TryAssignColors(colorRequirements, availableColors, assignment, 0))
        {
            return false;
        }

        foreach (var (requiredColor, quantity) in colorRequirements)
        {
            var usedColor = assignment[requiredColor];
            assignments.Add(new ColorUsage(designId, requiredColor, usedColor, quantity));

            if (!string.Equals(requiredColor, usedColor))
            {
                hasSubstitution = true;
            }
        }

        return true;
    }

    private static bool TryAssignColors(Dictionary<string, int> colorRequirements, Dictionary<string, int> availableColors, Dictionary<string, string> assignment, int index)
    {
        if (index >= colorRequirements.Count)
        {
            return true;
        }

        var (requiredColorId, requiredQuantity) = colorRequirements.ElementAt(index);

        foreach (var availableColorId in availableColors.Keys)
        {
            if (availableColors.TryGetValue(availableColorId, out var availableQuantity) && availableQuantity >= requiredQuantity)
            {
                availableColors[availableColorId] = availableQuantity - requiredQuantity;
                assignment[requiredColorId] = availableColorId;

                if (TryAssignColors(colorRequirements, availableColors, assignment, index + 1))
                {
                    availableColors[availableColorId] = availableQuantity;
                    return true;
                }

                availableColors[availableColorId] = availableQuantity;
                assignment.Remove(requiredColorId);
            }
        }

        return false;
    }

    private static bool IsBuildable<TKey>(Dictionary<TKey, int> inventory, Dictionary<TKey, int> requirements) where TKey : notnull => ComputeMissingPieces(inventory, requirements).Count == 0;
}
