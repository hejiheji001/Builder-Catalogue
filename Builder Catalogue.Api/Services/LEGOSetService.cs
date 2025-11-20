using BuilderCatalogue.Api.Clients;
using BuilderCatalogue.Api.Models.Contracts;
using BuilderCatalogue.Api.Models.Dto;
using BuilderCatalogue.Api.Models.External;
using BuilderCatalogue.Api.Services.Caching;
using System.Collections.Frozen;

namespace BuilderCatalogue.Api.Services;

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

        if (missingPieces.IsEmpty)
        {
            return new CollaborationResponse(username, setDetail.Id, setDetail.Name, []);
        }

        var userSummaries = await userService.GetUsersAsync(cancellationToken);
        var candidates = userSummaries.Where(user => !string.Equals(user.Username, username)).ToList();

        if (candidates.Count == 0)
        {
            return new CollaborationResponse(username, setDetail.Id, setDetail.Name, []);
        }

        //var collaborators = await FindCollaboratorsNaiveAsync(candidates, username, missingPieces, cancellationToken);

        //var collaborators = await FindCollaboratorsInvertedIndexAsync(candidates, username, missingPieces, cancellationToken);

        var collaborators = await FindCollaboratorsInvertedIndexColumnarAsync(candidates, username, missingPieces, cancellationToken);

        return new CollaborationResponse(username, setDetail.Id, setDetail.Name, collaborators);
    }

    // Baseline O(U * P) scan: check each candidate inventory and verify it covers every missing piece.
    private async Task<ImmutableArray<string[]>> FindCollaboratorsNaiveAsync(List<UserSummaryApiModel> candidates, string username, PieceInventoryDto missingPieces, CancellationToken cancellationToken)
    {
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

        return collaborators.ToImmutable();
    }

    // Optimized path: build a piece -> users lookup once, then intersect eligible sets per missing piece.
    private async Task<ImmutableArray<string[]>> FindCollaboratorsInvertedIndexAsync(List<UserSummaryApiModel> candidates, string username, PieceInventoryDto missingPieces, CancellationToken cancellationToken)
    {
        var collaborators = ImmutableArray.CreateBuilder<string[]>();

        var invertedIndex = await userService.BuildUserInventoryIndexAsync(candidates, cancellationToken);
        var eligibleUsers = FindMissingPiecesProvider(missingPieces, invertedIndex);

        if (eligibleUsers.Count > 0)
        {
            collaborators.AddRange(eligibleUsers.Select(candidate => new[] { username, candidate }));
        }

        return collaborators.ToImmutable();
    }

    // Columnar alternative that iterates the flattened columns instead of row objects while building/intersecting.
    private async Task<ImmutableArray<string[]>> FindCollaboratorsInvertedIndexColumnarAsync(List<UserSummaryApiModel> candidates, string username, PieceInventoryDto missingPieces, CancellationToken cancellationToken)
    {
        var collaborators = ImmutableArray.CreateBuilder<string[]>();

        var invertedIndex = await BuildUserInventoryIndexColumnarAsync(candidates, cancellationToken);
        var eligibleUsers = FindMissingPiecesProviderColumnar(missingPieces, invertedIndex);

        if (eligibleUsers.Count > 0)
        {
            collaborators.AddRange(eligibleUsers.Select(candidate => new[] { username, candidate }));
        }

        return collaborators.ToImmutable();
    }

    public async Task<BuildableLEGOSetsResponse> GetColorFlexibilityAsync(string username, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        var userDetail = await userService.GetUserDetailAsync(username, cancellationToken);
        var userInventory = userService.BuildUserInventory(userDetail);
        
        var flexibleSets1 = GetColorFlexibleSetsNaive(userInventory);
        var flexibleSets = GetColorFlexibleSetsInvertedIndex(userInventory);

        // TODO: Use columnar version for better performance with large inventories.

        return new BuildableLEGOSetsResponse(username, flexibleSets.Length, flexibleSets);
    }

    // Evaluate every cached set using the full flexible inventory each time.
    private ImmutableArray<ColorFlexibleLEGOSet> GetColorFlexibleSetsNaive(PieceInventoryDto userInventory)
    {
        var flexibleSets = ImmutableArray.CreateBuilder<ColorFlexibleLEGOSet>();

        foreach (var setDetail in cacheService.GetCachedEntries<LEGOSetDetailApiResponse>())
        {
            var requirements = BuildSetRequirements(setDetail);

            var isExactBuildable = IsBuildable(userInventory, requirements);

            if (TryCreateColorFlexibleAssignment(requirements, userInventory, out var assignments, out var hasSubstitution))
            {
                if (!isExactBuildable || hasSubstitution)
                {
                    flexibleSets.Add(new ColorFlexibleLEGOSet(setDetail.Id, setDetail.Name, setDetail.SetNumber, setDetail.TotalPieces, assignments));
                }
            }
        }

        return flexibleSets.ToImmutable();
    }

    // Reuses a per-piece color availability index so each set only scans relevant colors.
    private ImmutableArray<ColorFlexibleLEGOSet> GetColorFlexibleSetsInvertedIndex(PieceInventoryDto userInventory)
    {
        var flexibleSets = ImmutableArray.CreateBuilder<ColorFlexibleLEGOSet>();

        var availabilityIndex = userService.BuildPieceAvailabilityIndex(userInventory);

        var setDetails = cacheService.GetCachedEntries<LEGOSetDetailApiResponse>();

        foreach (var setDetail in setDetails)
        {
            var requirements = BuildSetRequirements(setDetail);
            var isExactBuildable = IsBuildable(userInventory, requirements);

            if (TryCreateColorFlexibleAssignment(requirements, availabilityIndex, out var assignments, out var hasSubstitution))
            {
                if (!isExactBuildable || hasSubstitution)
                {
                    flexibleSets.Add(new ColorFlexibleLEGOSet(setDetail.Id, setDetail.Name, setDetail.SetNumber, setDetail.TotalPieces, assignments));
                }
            }
        }

        return flexibleSets.ToImmutable();
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

    private PieceInventoryDto BuildSetRequirements(LEGOSetDetailApiResponse setDetail)
    {
        var cache = cacheService.GetFromCache<PieceInventoryDto>(setDetail.Id);

        if (cache is null)
        {
            var pieces = setDetail.Pieces
            .Where(piece => piece.Part is not null)
            .Select(piece => new PieceInfo(piece.Part!.DesignID, piece.Part.Material.ToString(), piece.Quantity));

            cache = PieceInventoryDto.Create(pieces);

            cacheService.UpdateCache(setDetail.Id, cache);
        }

        return cache;
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

    // Intersects candidate user sets (one per missing piece) until the remaining users cover every deficit.
    internal static IReadOnlyCollection<string> FindMissingPiecesProvider(PieceInventoryDto missingPieces, FrozenDictionary<PieceKey, FrozenDictionary<string, int>> index)
    {
        HashSet<string>? intersection = null;

        // Start with the larger count of pieces for faster intersection.
        var orderedPieces = missingPieces.Pieces.OrderBy(p => p.Count).ToList();
        foreach (var piece in orderedPieces)
        {
            if (!index.TryGetValue(new PieceKey(piece.PieceId, piece.ColorId), out var userCounts))
            {
                return [];
            }

            var eligible = new HashSet<string>(userCounts.Where(pair => pair.Value >= piece.Count).Select(pair => pair.Key));
            if (eligible.Count == 0)
            {
                return [];
            }

            if (intersection is null)
            {
                intersection = eligible;
            }
            else
            {
                intersection.IntersectWith(eligible);

                if (intersection.Count == 0)
                {
                    return [];
                }
            }
        }

        return intersection is null ? Array.Empty<string>() : intersection;
    }

    internal static IReadOnlyCollection<string> FindMissingPiecesProviderColumnar(PieceInventoryDto missingPieces, FrozenDictionary<PieceKey, FrozenDictionary<string, int>> index)
    {
        HashSet<string>? intersection = null;

        ref readonly var columns = ref missingPieces.Columns;
        var pieceIds = columns.PieceIds;
        var colorIds = columns.ColorIds;
        var counts = columns.Counts;

        // Reorder indices by ascending counts for better intersection performance.

        //Insertion sort implementation, better performance than LINQ
        //var ordering = Enumerable.Range(0, pieceIds.Length).ToArray();
        //for (var i = 1; i < ordering.Length; i++)
        //{
        //    var keyIndex = ordering[i];
        //    var keyCount = counts[keyIndex];
        //    var j = i - 1;

        //    while (j >= 0 && counts[ordering[j]] > keyCount)
        //    {
        //        ordering[j + 1] = ordering[j];
        //        j--;
        //    }

        //    ordering[j + 1] = keyIndex;
        //}

        //LINQ based ordering, simpler but slightly less performant
        var ordering = counts.ToArray().Select((value, index) => (value, index)).OrderBy(x => x.value).Select(x => x.index).ToArray();

        foreach (var idx in ordering)
        {
            if (!index.TryGetValue(new PieceKey(pieceIds[idx], colorIds[idx]), out var userCounts))
            {
                return [];
            }

            var required = counts[idx];
            var eligible = new HashSet<string>(userCounts.Where(pair => pair.Value >= required).Select(pair => pair.Key));
            if (eligible.Count == 0)
            {
                return [];
            }

            if (intersection is null)
            {
                intersection = eligible;
                continue;
            }

            intersection.IntersectWith(eligible);
            if (intersection.Count == 0)
            {
                return [];
            }
        }

        return intersection is null ? Array.Empty<string>() : intersection;
    }

    private static PieceInventoryDto ComputeMissingPieces(PieceInventoryDto inventory, PieceInventoryDto requirements)
    {
        var missing = new List<PieceInfo>();

        foreach (var piece in requirements.Pieces)
        {
            inventory.TryGetCount(piece.PieceId, piece.ColorId, out var ownedCount);
            if (piece.Count > ownedCount)
            {
                missing.Add(new PieceInfo(piece.PieceId, piece.ColorId, piece.Count - ownedCount));
            }
        }

        return PieceInventoryDto.Create(missing);
    }

    // Color-flex search that clones the user's inventory per design and backtracks to find a valid assignment.
    internal static bool TryCreateColorFlexibleAssignment(PieceInventoryDto flexibleRequirements, PieceInventoryDto flexibleInventory, out IReadOnlyList<ColorUsage> assignments, out bool hasSubstitution)
    {
        var result = new List<ColorUsage>();
        hasSubstitution = false;

        foreach (var pieceId in flexibleRequirements.DistinctPieceIds)
        {
            var availableColors = flexibleInventory.GetVariants(pieceId);
            if (availableColors.IsEmpty)
            {
                assignments = [];
                hasSubstitution = false;
                return false;
            }

            var availableOrderedColors = DeepClone(availableColors);

            //In place ordering, equals to OrderByDescending but better performance
            availableOrderedColors.Sort((left, right) => right.Count.CompareTo(left.Count));

            var colorRequirements = flexibleRequirements.GetVariants(pieceId);
            var requirementBuckets = DeepClone(colorRequirements);

            if (!TryAssignColorsForSet(requirementBuckets, availableOrderedColors, result, ref hasSubstitution))
            {
                assignments = [];
                hasSubstitution = false;
                return false;
            }
        }

        assignments = result;
        return result.Count > 0;
    }

    // Same backtracking core, but sourcing available colors from the precomputed inverted index.
    internal static bool TryCreateColorFlexibleAssignment(PieceInventoryDto flexibleRequirements, Dictionary<PieceKey, Dictionary<string, int>> flexibleInventory, out IReadOnlyList<ColorUsage> assignments, out bool hasSubstitution)
    {
        var result = new List<ColorUsage>();
        hasSubstitution = false;

        foreach (var pieceId in flexibleRequirements.DistinctPieceIds)
        {
            // Transform dictionary rows for this design into temporary PieceInfo instances so the downstream algorithm can reuse existing logic.
            ReadOnlySpan<PieceInfo> availableColors = flexibleInventory.Where(x => x.Key.PieceId == pieceId).Select(x => new PieceInfo(x.Key.PieceId, x.Key.ColorId, x.Value[x.Key.ColorId])).ToArray();
            if (availableColors.IsEmpty)
            {
                assignments = [];
                hasSubstitution = false;
                return false;
            }

            var orderedColors = DeepClone(availableColors);
            orderedColors.Sort((left, right) => right.Count.CompareTo(left.Count));

            var colorRequirements = flexibleRequirements.GetVariants(pieceId);
            var requirementBuckets = DeepClone(colorRequirements);

            if (!TryAssignColorsForSet(requirementBuckets, orderedColors, result, ref hasSubstitution))
            {
                assignments = [];
                hasSubstitution = false;
                return false;
            }
        }

        assignments = result;
        return result.Count > 0;
    }

    private async Task<FrozenDictionary<PieceKey, FrozenDictionary<string, int>>> BuildUserInventoryIndexColumnarAsync(IEnumerable<UserSummaryApiModel> userSummaries, CancellationToken cancellationToken)
    {
        var index = new Dictionary<PieceKey, Dictionary<string, int>>();

        foreach (var summary in userSummaries)
        {
            var detail = await userService.GetUserDetailAsync(summary.Username, cancellationToken);
            var inventory = userService.BuildUserInventory(detail);
            ref readonly var columns = ref inventory.Columns;
            var pieceIds = columns.PieceIds;
            var colorIds = columns.ColorIds;
            var counts = columns.Counts;

            for (var i = 0; i < pieceIds.Length; i++)
            {
                var key = new PieceKey(pieceIds[i], colorIds[i]);
                if (!index.TryGetValue(key, out var userCounts))
                {
                    userCounts = [];
                    index[key] = userCounts;
                }

                userCounts[summary.Username] = counts[i];
            }
        }

        return index.ToFrozenDictionary(kvp => kvp.Key, kvp => kvp.Value.ToFrozenDictionary());
    }

    // Backtracking wrapper: enforce that each required color gets assigned exactly once, recording substitutions along the way.
    private static bool TryAssignColorsForSet(List<PieceInfo> colorRequirements, List<PieceInfo> availableColors, List<ColorUsage> assignments, ref bool hasSubstitution)
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

    // Depth-first assignment: try every available color for the current requirement, rewinding counts as we backtrack.
    private static bool TryAssignColors(List<PieceInfo> colorRequirements, List<PieceInfo> availableColors, Dictionary<string, string> assignment, int index)
    {
        if (index >= colorRequirements.Count)
        {
            return true;
        }

        var requiredPiece = colorRequirements[index];
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

    // Utility to clone a span of PieceInfo into a mutable list for sorting and count adjustments.
    private static List<PieceInfo> DeepClone(ReadOnlySpan<PieceInfo> source)
    {
        var clones = new List<PieceInfo>(source.Length);
        foreach (var piece in source)
        {
            clones.Add(new PieceInfo(piece.PieceId, piece.ColorId, piece.Count));
        }

        return clones;
    }

    private static bool IsBuildable(PieceInventoryDto inventory, PieceInventoryDto requirements) => ComputeMissingPieces(inventory, requirements).IsEmpty;
}
