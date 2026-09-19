using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Services.Interfaces;
namespace VantaiViet.CoreApi.Controllers;
[ApiController, Authorize, Route("api/shipments")]
public sealed class ShipmentsController(IShipmentService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateShipmentRequest request,CancellationToken ct) => Respond(await service.CreateAsync(request,ct));
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id,CancellationToken ct) => Respond(await service.GetAsync(id,ct));
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct,int page=1,int pageSize=20) => Respond(await service.ListAsync(false,page,pageSize,ct));
    [HttpGet("mine")]
    public async Task<IActionResult> Mine(CancellationToken ct,int page=1,int pageSize=20) => Respond(await service.ListAsync(true,page,pageSize,ct));
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id,UpdateShipmentRequest request,CancellationToken ct) => Respond(await service.UpdateAsync(id,request,ct));
    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id,ShipmentVersionRequest request,CancellationToken ct) => Respond(await service.TransitionAsync(id,"publish",request.Version,ct));
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id,ShipmentVersionRequest request,CancellationToken ct) => Respond(await service.TransitionAsync(id,"cancel",request.Version,ct));
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id,[FromQuery] long version,CancellationToken ct) => Respond(await service.TransitionAsync(id,"delete",version,ct));
    private IActionResult Respond<T>(ShipmentResult<T> result)
    {
        if(result.ErrorCode is null)
        {
            return Ok(ApiResponse<T>.Ok(result.Data!,HttpContext.TraceIdentifier));
        }

        var status=result.ErrorCode switch { "not_found"=>404,"forbidden"=>403,"conflict"=>409,_=>400 };
        var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext,statusCode:status,title:"Shipment request rejected.");
        problem.Extensions["errorCode"] = "shipment." + result.ErrorCode;
        return new ObjectResult(problem) { StatusCode=status };
    }
}
