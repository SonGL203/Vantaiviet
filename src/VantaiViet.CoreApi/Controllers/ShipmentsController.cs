using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Services.Interfaces;
namespace VantaiViet.CoreApi.Controllers;
[ApiController, Authorize, Route("api/shipments")]
public sealed class ShipmentsController(IShipmentService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateShipmentRequest request,CancellationToken ct)
    {
        return Respond(await service.CreateAsync(request,ct));
    }
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id,CancellationToken ct)
    {
        return Respond(await service.GetAsync(id,ct));
    }
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct,int page=1,int pageSize=20)
    {
        return Respond(await service.ListAsync(false,page,pageSize,null,null,ct));
    }
    [HttpGet("mine")]
    public async Task<IActionResult> Mine(CancellationToken ct,int page=1,int pageSize=20,string? q=null,string? status=null)
    {
        return Respond(await service.ListAsync(true,page,pageSize,q,status,ct));
    }
    [HttpGet("mine/summary")]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        return Respond(await service.SummaryAsync(ct));
    }
    [HttpGet("mine/frequent-routes")]
    public async Task<IActionResult> FrequentRoutes(CancellationToken ct,int take=5)
    {
        return Respond(await service.FrequentRoutesAsync(take,ct));
    }
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id,UpdateShipmentRequest request,CancellationToken ct)
    {
        return Respond(await service.UpdateAsync(id,request,ct));
    }
    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id,ShipmentVersionRequest request,CancellationToken ct)
    {
        return Respond(await service.TransitionAsync(id,"publish",request.Version,ct));
    }
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id,ShipmentVersionRequest request,CancellationToken ct)
    {
        return Respond(await service.TransitionAsync(id,"cancel",request.Version,ct));
    }
    [HttpPost("{id:guid}/repost")]
    public async Task<IActionResult> Repost(Guid id,CloneShipmentRequest request,CancellationToken ct)
    {
        return Respond(await service.CloneAsync(id,request,true,ct));
    }
    [HttpPost("{id:guid}/copy")]
    public async Task<IActionResult> Copy(Guid id,CloneShipmentRequest request,CancellationToken ct)
    {
        return Respond(await service.CloneAsync(id,request,false,ct));
    }
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id,[FromQuery] long version,CancellationToken ct)
    {
        return Respond(await service.TransitionAsync(id,"delete",version,ct));
    }
    private IActionResult Respond<T>(ShipmentResult<T> result)
    {
        return this.FromResult(result,"shipment");
    }
}
