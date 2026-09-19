using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Services.Interfaces;
namespace VantaiViet.CoreApi.Controllers;
[ApiController, Authorize, Route("api/trips")]
public sealed class TripsController(ITripService service) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAsync(Guid id,CancellationToken ct) => Respond(await service.GetAsync(id,ct));
    [HttpGet("{id:guid}/events")]
    public async Task<IActionResult> EventsAsync(Guid id,CancellationToken ct,int page=1) => Respond(await service.EventsAsync(id,page,ct));
    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> StartAsync(Guid id,TripVersionRequest r,CancellationToken ct) => Respond(await service.StartAsync(id,r,ct));
    [HttpPost("{id:guid}/pickup")]
    public async Task<IActionResult> PickupAsync(Guid id,TripHandoverRequest r,CancellationToken ct) => Respond(await service.PickupAsync(id,r,ct));
    [HttpPost("{id:guid}/deliver")]
    public async Task<IActionResult> DeliverAsync(Guid id,TripHandoverRequest r,CancellationToken ct) => Respond(await service.DeliverAsync(id,r,ct));
    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> CompleteAsync(Guid id,TripVersionRequest r,CancellationToken ct) => Respond(await service.CompleteAsync(id,r,ct));
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelAsync(Guid id,TripReasonRequest r,CancellationToken ct) => Respond(await service.CancelAsync(id,r,ct));
    [HttpPost("{id:guid}/incidents")]
    public async Task<IActionResult> IncidentAsync(Guid id,TripReasonRequest r,CancellationToken ct) => Respond(await service.ReportIncidentAsync(id,r,ct));
    [HttpPost("{id:guid}/proofs"), RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<IActionResult> UploadAsync(Guid id,UploadTripProofRequest r,CancellationToken ct) => Respond(await service.UploadProofAsync(id,r,ct));
    [HttpGet("{id:guid}/proofs/{proofId:guid}")]
    public async Task<IActionResult> DownloadAsync(Guid id,Guid proofId,CancellationToken ct)
    {
        var result=await service.DownloadProofAsync(id,proofId,ct);
        if (result.Data is null) { return Respond(result); }
        return File(result.Data.Content,result.Data.ContentType,proofId + (result.Data.ContentType=="image/png"?".png":".jpg"));
    }
    private IActionResult Respond<T>(ShipmentResult<T> result)
    {
        if (result.ErrorCode is null) { return Ok(ApiResponse<T>.Ok(result.Data!,HttpContext.TraceIdentifier)); }
        var status=result.ErrorCode switch { "not_found"=>404,"forbidden" or "driver_not_eligible"=>403,"conflict" or "invalid_transition"=>409,_=>400 };
        return Problem(statusCode:status,title:"Trip request rejected.",extensions:new Dictionary<string,object?> {["errorCode"]="trip."+result.ErrorCode,["traceId"]=HttpContext.TraceIdentifier});
    }
}
