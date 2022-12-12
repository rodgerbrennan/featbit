using Application.Bases.Models;
using Application.EndUsers;
using Domain.EndUsers;

namespace Api.Controllers;

[Route("api/v{version:apiVersion}/envs/{envId:guid}/end-users")]
public class EndUserController : ApiControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ApiResponse<EndUser>> GetAsync(Guid id)
    {
        var request = new GetEndUser
        {
            Id = id
        };

        var user = await Mediator.Send(request);
        return Ok(user);
    }

    [HttpPost]
    public async Task<ApiResponse<PagedResult<EndUser>>> GetListAsync(Guid envId, SearchEndUser query)
    {
        var filter = new EndUserFilter(query);

        var request = new GetEndUserList
        {
            EnvId = envId,
            Filter = filter
        };

        var users = await Mediator.Send(request);
        return Ok(users);
    }

    [HttpPut]
    public async Task<ApiResponse<EndUser>> UpsertAsync(Guid envId, UpsertEndUser request)
    {
        request.EnvId = envId;

        var user = await Mediator.Send(request);
        return Ok(user);
    }

    [HttpPost("by-keyIds")]
    public async Task<ApiResponse<IEnumerable<EndUser>>> GetByKeyIdsAsync(Guid envId, [FromBody] string[] keyIds)
    {
        var request = new GetEndUserByKeyIds
        {
            EnvId = envId,
            KeyIds = keyIds
        };

        var users = await Mediator.Send(request);
        return Ok(users);
    }

    [HttpGet("{id:guid}/flags")]
    public async Task<ApiResponse<PagedResult<EndUserFlagVm>>> GetFlagsAsync(Guid envId, Guid id, string? searchText)
    {
        var request = new GetEndUserFlags
        {
            EnvId = envId,
            Id = id,
            SearchText = searchText
        };

        var flags = await Mediator.Send(request);
        return Ok(flags);
    }

    [HttpGet("{id:guid}/segments")]
    public async Task<ApiResponse<IEnumerable<EndUserSegmentVm>>> GetSegmentsAsync(Guid envId, Guid id)
    {
        var request = new GetEndUserSegments
        {
            EnvId = envId,
            Id = id
        };

        var segments = await Mediator.Send(request);
        return Ok(segments);
    }
    
    [HttpGet("get-by-featureflag")]
    public async Task<ApiResponse<PagedResult<FeatureFlagEndUserStatsVm>>> GetListByFeatureFlagAsync(Guid envId, [FromQuery] FeatureFlagEndUserFilter filter)
    {
        var request = new GetFeatureFlagEndUserList
        {
            EnvId = envId,
            Filter = filter
        };

        var users = await Mediator.Send(request);
        return Ok(users);
    }
}