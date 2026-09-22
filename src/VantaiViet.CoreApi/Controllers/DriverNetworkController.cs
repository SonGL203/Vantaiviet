using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VantaiViet.CoreApi.DTOs;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Controllers;

/// <summary>A broker's own network: groups (feature 16), saved and favourite drivers (features 14-15),
/// and their invite code and who has joined through it (feature 17).</summary>
[ApiController, Authorize(Roles = "Broker"), Route("api/broker")]
public sealed class DriverNetworkController(IDriverNetworkService service) : ControllerBase
{
    [HttpPost("driver-groups")]
    public async Task<IActionResult> CreateGroupAsync(DriverGroupRequest request, CancellationToken cancellationToken)
    {
        return this.FromResult(await service.CreateGroupAsync(request, cancellationToken), "driver_group");
    }

    [HttpGet("driver-groups")]
    public async Task<IActionResult> ListGroupsAsync(CancellationToken cancellationToken)
    {
        return this.FromResult(await service.ListGroupsAsync(cancellationToken), "driver_group");
    }

    [HttpPut("driver-groups/{id:guid}")]
    public async Task<IActionResult> RenameGroupAsync(Guid id, DriverGroupRequest request, CancellationToken cancellationToken)
    {
        return this.FromResult(await service.RenameGroupAsync(id, request, cancellationToken), "driver_group");
    }

    [HttpDelete("driver-groups/{id:guid}")]
    public async Task<IActionResult> DeleteGroupAsync(Guid id, CancellationToken cancellationToken)
    {
        return this.FromResult(await service.DeleteGroupAsync(id, cancellationToken), "driver_group");
    }

    [HttpPost("drivers")]
    public async Task<IActionResult> SaveAsync(SaveDriverRequest request, CancellationToken cancellationToken)
    {
        return this.FromResult(await service.SaveAsync(request, cancellationToken), "saved_driver");
    }

    [HttpGet("drivers")]
    public async Task<IActionResult> ListAsync(CancellationToken cancellationToken, Guid? groupId = null, bool favoritesOnly = false)
    {
        return this.FromResult(await service.ListAsync(groupId, favoritesOnly, cancellationToken), "saved_driver");
    }

    [HttpPut("drivers/{id:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid id, UpdateSavedDriverRequest request, CancellationToken cancellationToken)
    {
        return this.FromResult(await service.UpdateAsync(id, request, cancellationToken), "saved_driver");
    }

    [HttpDelete("drivers/{id:guid}")]
    public async Task<IActionResult> RemoveAsync(Guid id, CancellationToken cancellationToken)
    {
        return this.FromResult(await service.RemoveAsync(id, cancellationToken), "saved_driver");
    }

    [HttpGet("invite")]
    public async Task<IActionResult> InviteCodeAsync(CancellationToken cancellationToken)
    {
        return this.FromResult(await service.InviteCodeAsync(cancellationToken), "invite");
    }

    [HttpGet("referrals")]
    public async Task<IActionResult> ReferralsAsync(CancellationToken cancellationToken)
    {
        return this.FromResult(await service.ReferralsAsync(cancellationToken), "invite");
    }

    [HttpPost("drivers/{driverUserId:guid}/reports")]
    public async Task<IActionResult> ReportDriverAsync(Guid driverUserId, DriverReportRequest request, CancellationToken cancellationToken)
    {
        return this.FromResult(await service.ReportDriverAsync(driverUserId, request, cancellationToken), "driver_report");
    }

    [HttpGet("drivers/reports/mine")]
    public async Task<IActionResult> MyReportsAsync(CancellationToken cancellationToken)
    {
        return this.FromResult(await service.MyReportsAsync(cancellationToken), "driver_report");
    }
}
