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

        var buildableSets = LEGOSets.Select(set => new LEGOSetResponse(set.Id, set.Name, set.SetNumber, set.TotalPieces)).ToList();

        return new BuildableLEGOSetsResponse(username, buildableSets.Count, buildableSets);
    }

    public async Task<CollaborationResponse> FindCollaboratorsAsync(string username, string setName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(setName);

        var setDetail = cacheService.GetFromCache<LEGOSetDetailApiResponse>(setName);
        if (setDetail is null)
        {
            setDetail = await apiClient.GetSetAsync(setName, cancellationToken) ?? throw new ArgumentException($"Set with name '{setName}' not found.", nameof(setName));
            cacheService.UpdateCache(setName, setDetail);
        }
         
        var targetUser = await userService.GetUserDetailAsync(username, cancellationToken);
        var targetInventory = userService.BuildUserInventory(targetUser);
        var requirements = BuildSetRequirements(setDetail);

        // Treated as a sub requirement - what is missing from the target user?
        var missingPieces = ComputeMissingPieces(targetInventory, requirements);

        if (missingPieces.Pieces.Count == 0)
        {
            return new CollaborationResponse(username, setDetail.Id, setDetail.Name, []);
        }

        var userSummaries = await userService.GetUsersAsync(cancellationToken);
        var candidates = userSummaries.Where(user => !string.Equals(user.Username, username)).ToList();

        var collaborators = ImmutableArray.CreateBuilder<string[]>();

        // This is a simple greedy approach - in a real-world scenario, we might want to consider
        // more advanced algorithms to find optimal collaborator combinations. e.g. find a group of users as collaborators.
        // For now, we just check each candidate individually and make up a 2 user collaboration.
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

    public async Task<CollaborationResponse> FindCollaboratorsInvertedIndexAsync(string username, string setName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(setName);

        var setDetail = cacheService.GetFromCache<LEGOSetDetailApiResponse>(setName);
        if (setDetail is null)
        {
            setDetail = await apiClient.GetSetAsync(setName, cancellationToken) ?? throw new ArgumentException($"Set with name '{setName}' not found.", nameof(setName));
            cacheService.UpdateCache(setName, setDetail);
        }

        var targetUser = await userService.GetUserDetailAsync(username, cancellationToken);
        var targetInventory = userService.BuildUserInventory(targetUser);
        var requirements = BuildSetRequirements(setDetail);

        // Treated as a sub requirement - what is missing from the target user?
        var missingPieces = ComputeMissingPieces(targetInventory, requirements);

        if (missingPieces.Pieces.Count == 0)
        {
            return new CollaborationResponse(username, setDetail.Id, setDetail.Name, []);
        }

        //Introducing an inverted index (piece → list of users with quantity) turns candidate selection into intersection/filtering,
        //lowering effective complexity to O(P_missing + hits)
        //Get missingPieces, for each piece, point to a list of users then Union all users
        foreach (var piece in missingPieces.Pieces)
        {

        }


        return null;
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
        var flexibleInventory = userService.BuildColorFlexibleInventory(userDetail);
        var flexibleSets = new List<ColorFlexibleLEGOSet>();

        foreach (var setDetail in cacheService.GetCachedEntries<LEGOSetDetailApiResponse>())
        {
            var exactRequirements = BuildSetRequirements(setDetail);
            var flexibleRequirements = BuildColorFlexibleSetRequirements(setDetail);

            var isExactBuildable = IsBuildable(exactInventory, exactRequirements);

            if (TryCreateColorFlexibleAssignment(flexibleRequirements, flexibleInventory, out var assignments, out var hasSubstitution))
            {
                if (!isExactBuildable || hasSubstitution)
                {
                    flexibleSets.Add(new ColorFlexibleLEGOSet(setDetail.Id, setDetail.Name, setDetail.SetNumber, setDetail.TotalPieces, assignments));
                }
            }
        }

        return new BuildableLEGOSetsResponse(username, flexibleSets.Count, flexibleSets);
    }

    private RequirementsDto BuildSetRequirements(LEGOSetDetailApiResponse setDetail)
    {
        var cache = cacheService.GetFromCache<RequirementsDto>(setDetail.Id);

        if (cache is null)
        {
            var pieces = setDetail.Pieces
            .Where(piece => piece.Part is not null)
            .Select(piece => new PieceDto(piece.Part!.DesignID, piece.Part.Material.ToString(), piece.Quantity))
            .ToList();

            cache = new RequirementsDto(pieces);

            cacheService.UpdateCache(setDetail.Id, cache);
        }

        return cache;
    }

    private static ColorFlexibleRequirementsDto BuildColorFlexibleSetRequirements(LEGOSetDetailApiResponse setDetail)
    {
        var colorFlexibleSetRequirements = setDetail.Pieces
            .GroupBy(piece => piece.Part!.DesignID)
            .Select(group => new ColorFlexiblePieceDto(
                group.Key,
                group.ToDictionary(piece => piece.Part!.Material.ToString(), piece => piece.Quantity)))
            .ToList();

        return new ColorFlexibleRequirementsDto(colorFlexibleSetRequirements);
    }

    private List<LEGOSetDto> ComputeBuildableSets(UserDetailApiModel userInfo)
    {
        var buildableSets = new List<LEGOSetDto>();

        var inventory = userService.BuildUserInventory(userInfo);

        // This call is intentionally routed through a cache abstraction so we can
        // plug in alternative providers (distributed cache, Redis, etc.)
        // without touching the evaluation flow in the future.
        var setInfo = cacheService.GetCachedEntries<LEGOSetDetailApiResponse>();

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

    private static RequirementsDto ComputeMissingPieces<T>(InventoryDto inventory, T requirements) where T : InventoryDto
    {
        var missing = new List<PieceDto>();

        foreach (var piece in requirements.Pieces)
        {
            var owned = inventory.GetPiece(piece.PieceId, piece.ColorId)!;
            var ownedCount = owned.Count;
            if (piece.Count > ownedCount)
            {
                missing.Add(owned with
                {
                    Count = piece.Count - ownedCount
                });
            }
        }

        return new RequirementsDto(missing);
    }

    private static bool TryCreateColorFlexibleAssignment(ColorFlexibleRequirementsDto flexibleRequirements, ColorFlexibleInventoryDto flexibleInventory, out IReadOnlyList<ColorUsage> assignments, out bool hasSubstitution)
    {
        var result = new List<ColorUsage>();
        hasSubstitution = false;

        foreach (var piece in flexibleRequirements.Pieces)
        {
            var availableColors = flexibleInventory.GetPieceVariants(piece.PieceId);
            var colorRequirements = flexibleRequirements.GetPieceVariants(piece.PieceId);

            if (availableColors.Count == 0)
            {
                assignments = [];
                hasSubstitution = false;
                return false;
            }

            // Order available colors by quantity descending to try larger stocks first
            var orderedColors = availableColors.OrderByDescending(color => color.Count).ToList();

            if (!TryAssignColorsForDesign(colorRequirements, orderedColors, result, ref hasSubstitution))
            {
                assignments = [];
                hasSubstitution = false;
                return false;
            }
        }

        assignments = result;
        return result.Count > 0;
    }

    private static bool TryAssignColorsForDesign(List<PieceDto> colorRequirements, List<PieceDto> availableColors, List<ColorUsage> assignments, ref bool hasSubstitution)
    {
        var assignment = new Dictionary<string, string>();

        if (!TryAssignColors(colorRequirements, availableColors, assignment, 0))
        {
            return false;
        }

        foreach (var piece in colorRequirements)
        {
            var usedColor = assignment[piece.ColorId];
            assignments.Add(new ColorUsage(piece.PieceId, piece.ColorId, usedColor, piece.Count));

            if (!string.Equals(piece.ColorId, usedColor))
            {
                hasSubstitution = true;
            }
        }

        return true;
    }

    private static bool TryAssignColors(List<PieceDto> colorRequirements, List<PieceDto> availableColors, Dictionary<string, string> assignment, int index)
    {
        if (index >= colorRequirements.Count)
        {
            return true;
        }

        var requiredPiece = colorRequirements.ElementAt(index);
        var requiredColorId = requiredPiece.ColorId;
        var requiredQuantity = requiredPiece.Count;

        var availableColorsDic = availableColors.ToDictionary(color => color.ColorId, color => color);

        foreach (var availableColorId in availableColorsDic.Keys)
        {
            var availablePiece = availableColorsDic[availableColorId];
            var availableQuantity = availablePiece.Count;
            if (availableQuantity >= requiredQuantity)
            {
                availablePiece.Count = availableQuantity - requiredQuantity;

                assignment[requiredColorId] = availableColorId;

                if (TryAssignColors(colorRequirements, availableColors, assignment, index + 1))
                {
                    availablePiece.Count = availableQuantity;
                    return true;
                }

                availablePiece.Count = availableQuantity;
                assignment.Remove(requiredColorId);
            }
        }

        return false;
    }

    private static bool IsBuildable<T>(InventoryDto inventory, T requirements) where T : InventoryDto => ComputeMissingPieces(inventory, requirements).Pieces.Count == 0;
}
