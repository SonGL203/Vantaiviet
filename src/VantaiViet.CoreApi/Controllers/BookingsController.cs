using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Services.Interfaces;
namespace VantaiViet.CoreApi.Controllers;
[ApiController,Authorize,Route("api")]
public sealed class BookingsController(IBookingService service) : ControllerBase
{
    [HttpPost("shipments/{shipmentId:guid}/requests")]
    public async Task<IActionResult> RequestAsync(Guid shipmentId,RequestTransportDto request,CancellationToken ct) => Respond(await service.RequestAsync(shipmentId,request,ct));
    [HttpGet("shipments/{shipmentId:guid}/requests")]
    public async Task<IActionResult> RequestsAsync(Guid shipmentId,CancellationToken ct,int page=1) => Respond(await service.ListRequestsAsync(shipmentId,page,ct));
    [HttpGet("driver/requests")]
    public async Task<IActionResult> MineAsync(CancellationToken ct,int page=1) => Respond(await service.ListRequestsAsync(null,page,ct));
    [HttpPost("transport-requests/{id:guid}/accept")]
    public async Task<IActionResult> AcceptAsync(Guid id,CancellationToken ct) => Respond(await service.AcceptAsync(id,ct));
    [HttpPost("transport-requests/{id:guid}/reject")]
    public async Task<IActionResult> RejectAsync(Guid id,CancellationToken ct) => Respond(await service.CloseRequestAsync(id,false,ct));
    [HttpPost("transport-requests/{id:guid}/withdraw")]
    public async Task<IActionResult> WithdrawAsync(Guid id,CancellationToken ct) => Respond(await service.CloseRequestAsync(id,true,ct));
    [HttpGet("bookings/mine")]
    public async Task<IActionResult> BookingsAsync(CancellationToken ct,int page=1) => Respond(await service.ListBookingsAsync(page,ct));
    private IActionResult Respond<T>(ShipmentResult<T> result)
    {
        if(result.ErrorCode is null) { return Ok(ApiResponse<T>.Ok(result.Data!,HttpContext.TraceIdentifier)); }
        var status=result.ErrorCode switch { "not_found"=>404,"forbidden"=>403,"driver_not_eligible"=>403,"conflict"=>409,"resource_unavailable"=>409,_=>400 };
        var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext,statusCode:status,title:"Transport request rejected.");
        problem.Extensions["errorCode"] = "booking." + result.ErrorCode;
        return new ObjectResult(problem) { StatusCode=status };
    }
}
