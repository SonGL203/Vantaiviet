using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Controllers;

[ApiController, Authorize, Route("api")]
public sealed class ShipmentBrokersController(IShipmentBrokerService service) : ControllerBase
{
    [HttpPost("shipments/{id:guid}/broker")]
    public async Task<IActionResult> AssignAsync(Guid id,AssignBrokerRequest request,CancellationToken ct) => Respond(await service.AssignAsync(id,request,ct));
    [HttpPost("shipments/{id:guid}/broker/accept")]
    public async Task<IActionResult> AcceptAsync(Guid id,BrokerDecisionRequest request,CancellationToken ct) => Respond(await service.DecideAsync(id,"accept",request,ct));
    [HttpPost("shipments/{id:guid}/broker/reject")]
    public async Task<IActionResult> RejectAsync(Guid id,BrokerDecisionRequest request,CancellationToken ct) => Respond(await service.DecideAsync(id,"reject",request,ct));
    [HttpPost("shipments/{id:guid}/broker/revoke")]
    public async Task<IActionResult> RevokeAsync(Guid id,BrokerDecisionRequest request,CancellationToken ct) => Respond(await service.DecideAsync(id,"revoke",request,ct));
    [HttpGet("broker-assignments/mine")]
    public async Task<IActionResult> ListAsync(CancellationToken ct,int page = 1) => Respond(await service.ListAsync(page,ct));

    private IActionResult Respond<T>(ShipmentResult<T> result)
    {
        if (result.ErrorCode is null) { return Ok(ApiResponse<T>.Ok(result.Data!,HttpContext.TraceIdentifier)); }
        var status = result.ErrorCode switch { "not_found" => 404,"forbidden" => 403,"conflict" => 409,_ => 400 };
        var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext,statusCode:status,title:"Broker assignment rejected.");
        problem.Extensions["errorCode"] = "broker." + result.ErrorCode;
        return new ObjectResult(problem) { StatusCode=status };
    }
}
