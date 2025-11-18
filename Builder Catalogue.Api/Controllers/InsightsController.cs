using BuilderCatalogue.Api.Models.Contracts;

namespace BuilderCatalogue.Api.Controllers;

[ApiController]
[Route("insights")]
public class InsightsController(LEGOSetService insightsService) : ControllerBase
{

    [HttpGet("buildable-sets/{username}", Name = "GetBuildableSets")]
    public async Task<ActionResult<BuildableLEGOSetsResponse>> GetBuildableSetsAsync(string username = "brickfan35")
    {
        var result = await insightsService.GetBuildableSetsAsync(username);
        return Ok(result);
    }

    [HttpGet("collaborators/{username}/{setName}", Name = "GetCollaborators")]
    public async Task<ActionResult<CollaborationResponse>> GetCollaboratorsAsync(string username = "landscape-artist", string setName = "tropical-island")
    {
        var result = await insightsService.FindCollaboratorsAsync(username, setName);
        return Ok(result);
    }

    [HttpGet("custom-build-size/{username}", Name = "GetBuildSizeRecommendation")]
    public async Task<ActionResult<BuildSizeRecommendationResponse>> GetBuildSizeRecommendationAsync(string username = "megabuilder99 ", [FromQuery] double percentile = 0.5)
    {
        var result = await insightsService.GetBuildSizeRecommendationAsync(username, percentile);
        return Ok(result);
    }

    [HttpGet("colour-flexibility/{username}", Name = "GetColourFlexibility")]
    public async Task<ActionResult<BuildableLEGOSetsResponse>> GetColourFlexibilityAsync(string username = "dr_crocodile")
    {
        var result = await insightsService.GetColourFlexibilityAsync(username);
        return Ok(result);
    }
}
