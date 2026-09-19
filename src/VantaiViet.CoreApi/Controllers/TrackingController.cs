using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Services.Interfaces;
namespace VantaiViet.CoreApi.Controllers;
[ApiController,Authorize,Route("api/tracking")]
public sealed class TrackingController(ITrackingService service) : ControllerBase
{
    [HttpGet("trips")]
    public async Task<IActionResult> ListAsync(CancellationToken ct,int page=1) => Respond(await service.ListAsync(page,ct));
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> SnapshotAsync(Guid id,CancellationToken ct) => Respond(await service.SnapshotAsync(id,ct));
    [HttpPost("{id:guid}/locations"),RequestSizeLimit(65536)]
    public async Task<IActionResult> SendAsync(Guid id,GpsBatchRequest r,CancellationToken ct) => Respond(await service.SendAsync(id,r,ct));
    [HttpGet("{id:guid}/locations")]
    public async Task<IActionResult> HistoryAsync(Guid id,CancellationToken ct,long afterId=0) => Respond(await service.HistoryAsync(id,afterId,ct));
    [HttpPut("{id:guid}/stops")]
    public async Task<IActionResult> StopsAsync(Guid id,SetTripStopsRequest r,CancellationToken ct) => Respond(await service.SetStopsAsync(id,r,ct));
    [HttpGet("{id:guid}/participants")]
    public async Task<IActionResult> ParticipantsAsync(Guid id,CancellationToken ct) => Respond(await service.ParticipantsAsync(id,ct));
    [HttpPut("{id:guid}/participants/{userId:guid}")]
    public async Task<IActionResult> GrantAsync(Guid id,Guid userId,CancellationToken ct) => Respond(await service.SetParticipantAsync(id,userId,true,ct));
    [HttpDelete("{id:guid}/participants/{userId:guid}")]
    public async Task<IActionResult> RevokeAsync(Guid id,Guid userId,CancellationToken ct) => Respond(await service.SetParticipantAsync(id,userId,false,ct));
    [HttpGet("{id:guid}/route")]
    public async Task<IActionResult> RouteAsync(Guid id,CancellationToken ct) => Respond(await service.RouteAsync(id,ct));
    private IActionResult Respond<T>(ShipmentResult<T> r)
    {
        if(r.ErrorCode is null) { return Ok(ApiResponse<T>.Ok(r.Data!,HttpContext.TraceIdentifier)); }
        var status=r.ErrorCode switch { "not_found"=>404,"forbidden"=>403,"tracking_stopped" or "conflict" or "point_conflict"=>409,_=>400 };
        var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext,statusCode:status,title:"Tracking request rejected.");
        problem.Extensions["errorCode"] = "tracking." + r.ErrorCode;
        return new ObjectResult(problem) { StatusCode=status };
    }
}
