using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using VantaiViet.CoreApi.Services.Interfaces;
namespace VantaiViet.CoreApi.Hubs;
[Authorize]
public sealed class TrackingHub(ITrackingService service) : Hub
{
    public async Task JoinTrip(Guid tripId)
    {
        if (!Guid.TryParse(Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,out var userId)
            || !await service.CanViewAsync(tripId,userId,Context.ConnectionAborted))
        { throw new HubException("Access denied."); }
        await Groups.AddToGroupAsync(Context.ConnectionId,tripId.ToString(),Context.ConnectionAborted);
    }
    public Task LeaveTrip(Guid tripId) => Groups.RemoveFromGroupAsync(Context.ConnectionId,tripId.ToString(),Context.ConnectionAborted);
}
